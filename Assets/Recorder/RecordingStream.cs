using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpMp4;

/// <summary>
/// Assemble a recording by queuing up elements. The stream manages a thread
/// to handle performance intensive work. The public interface is not thread
/// safe and should all be operated from a single thread.
/// </summary>
public class RecordingStream : IDisposable
{
    public string path { get; }

    private ConcurrentQueue<(byte[],uint)> h264Nalus = new();
    private ConcurrentQueue<byte[]> opusPackets = new();
    private SemaphoreSlim semaphore = new (0, int.MaxValue);
    private CancellationTokenSource cts = new ();
    private Task task;
    
    private ulong totalOpusTimeUnits;
    private ulong totalh264TimeUnits;
    
    private Queue<byte[]> h264NalusPreBuffer = new();
    private long h264Timestamp = -1;
    
    private const uint H264_TIMESCALE = 90000;
    
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="path">Full path to write to, including extension.</param>
    /// <param name="onComplete">Called when all buffers are flushed and file is complete. May not be called from the main thread.</param>
    public RecordingStream(string path, Action<AggregateException> onComplete)
    {
        this.path = path;
        
        task = Task.Run(Build).ContinueWith(t =>
        {
            onComplete(t.Exception);
        });
    }
    
    private async Task Build()
    {
        await using var fileStream = new FileStream(path, FileMode.Create, 
            FileAccess.ReadWrite, FileShare.Read, bufferSize:4096, 
            FileOptions.Asynchronous);
        await using var bufferedStream = new BufferedStream(fileStream);
        
        await Stream(bufferedStream);
        await PatchLength(bufferedStream);
    }
    
    private async Task Stream(Stream stream)
    {
        using var builder = new FragmentedMp4Builder(new SingleStreamOutput(stream));
        
        var videoTrack = new H264Track();
        videoTrack.TimescaleOverride = H264_TIMESCALE;
        builder.AddTrack(videoTrack);
        
        var audioTrack = new OpusTrack(2,0,0,0);
        // Extract Opus timestamps from the packets themselves rather than rtp
        // as this seems to work well.
        audioTrack.Timescale = OpusPacket.TIME_UNITS_PER_SECOND;
        builder.AddTrack(audioTrack);
        
        while (!cts.Token.IsCancellationRequested)
        {
            if (h264Nalus.TryDequeue(out var nal))
            {
                videoTrack.FrameTickOverride = nal.Item2;
                videoTrack.SampleDuration = nal.Item2;
                await videoTrack.ProcessSampleAsync(nal.Item1);
            }
            
            if (opusPackets.TryDequeue(out var packet))
            {
                var units = GetFrameDurationTimeUnits(packet);
                audioTrack.SampleDuration = units;
                totalOpusTimeUnits += units;
                await audioTrack.ProcessSampleAsync(packet);
            }
            
            await semaphore.WaitAsync(CancellationToken.None);
        }
        
        // Process everything in buffer, even if cancelled 
        while (h264Nalus.TryDequeue(out var nal))
        {
            videoTrack.SampleDuration = nal.Item2;
            await videoTrack.ProcessSampleAsync(nal.Item1);
        }
        
        while (opusPackets.TryDequeue(out var packet))
        {
            var units = GetFrameDurationTimeUnits(packet);
            audioTrack.SampleDuration = units;
            totalOpusTimeUnits += units;
            await audioTrack.ProcessSampleAsync(packet);
        }
        
        // Flush to file, or any incomplete fragment will not get written
        await builder.FlushAsync();
    }
    
    private async Task PatchLength(Stream stream)
    {
        stream.Position = 0;
        
        // First box is ftyp box - skip it
        await Mp4Parser.ReadBox(null,stream);
        
        // Parse moovbox
        var moovBoxPosition = stream.Position;
        var box = await Mp4Parser.ReadBox(null,stream);
        if (box.Type != MoovBox.TYPE)
        {
            throw new FormatException("Could not find moov box in MP4 output." +
                                      " File length will not be patched and " +
                                      "output file may be interpreted as a " +
                                      "stream.");
        }
        
        // Update length
        const int mvhdTimescale = 1000;
        const double opusToMvhdTimescale = mvhdTimescale 
                                           / (double)OpusPacket.TIME_UNITS_PER_SECOND;
        const double h264ToMvhdTimescale = mvhdTimescale 
                                           / (double)H264_TIMESCALE;
        var opusDuration = (ulong)Math.Ceiling(totalOpusTimeUnits * opusToMvhdTimescale);
        var h264Duration = (ulong)Math.Ceiling(totalh264TimeUnits * h264ToMvhdTimescale);
        var duration = Math.Max(opusDuration,h264Duration);
        ((MoovBox)box).GetMvex().GetMehd().FragmentDuration = duration;
        
        // Patch moovbox
        stream.Position = moovBoxPosition;
        await Mp4Parser.WriteBox(stream,box);
    }
    
    /// <summary>
    /// Queue a NALU (video unit) for muxing. Not thread safe.
    /// </summary>
    public void EnqueueH264Nalu (byte[] nalu, uint timestamp)
    {
        if (timestamp != h264Timestamp && h264Timestamp >= 0)
        {
            var delta = (uint)(timestamp - h264Timestamp);
            totalh264TimeUnits += delta;
            while (h264NalusPreBuffer.TryDequeue(out var preBufferedNalu))
            {
                h264Nalus.Enqueue((preBufferedNalu,delta));
            }
            semaphore.Release();
        }
        
        h264NalusPreBuffer.Enqueue(nalu);
        h264Timestamp = timestamp;
    }
    
    /// <summary>
    /// Queue a sample (audio unit) for muxing. Not thread safe.
    /// </summary>
    public void EnqueueOpusPacket (byte[] packet, uint timestamp)
    {
        opusPackets.Enqueue(packet);
        semaphore.Release();
    }
    
    /// <summary>
    /// Flush queued buffers to the file and release resources. Flushing etc
    /// will happen asynchronously. The file will only be ready to use after the
    /// callback supplied in the constructor.
    /// </summary>
    public void Dispose()
    {
        cts.Cancel();
        semaphore.Release();
        
        task.ContinueWith(_ =>
        {
            semaphore.Dispose();
            cts.Dispose();
        }, CancellationToken.None,
           TaskContinuationOptions.ExecuteSynchronously,
           TaskScheduler.Default);
    }
    
    private uint GetFrameDurationTimeUnits(byte[] opusPacket)
    {
        var p = new OpusPacket(opusPacket);
        return p.totalDurationTimeUnits;
    }
}
