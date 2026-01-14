using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Unity.WebRTC;

public class VideoRecorder : MonoBehaviour
{
    public bool printDebug;
    
    public enum State
    {
        Uninitialised = 0,
        Unsupported,
        Idle,
        Starting,
        Recording
    }
    public State state { get; private set; }
    
    private RTCPeerConnection pcLocal, pcRemote;
    private List<RTCRtpSender> pcLocalSenders = new();
    private DelegateOnTrack pcRemoteOntrack;

    private RTCRtpCodecCapability[] videoCodecs;
    private RTCRtpCodecCapability[] audioCodecs;
    
    private RecordingStream recordingStream;
    
    private ConcurrentQueue<Action> mainThreadActions = new ();
    private ConcurrentQueue<(byte[],uint)> h264Nalus = new ();
    private ConcurrentQueue<(byte[],uint)> opusPackets = new ();
    
    private Coroutine negotiationCoroutine;
    
    private const string VIDEO_MIME_TYPE = "video/h264";
    private const string AUDIO_MIME_TYPE = "audio/opus";

    private void Awake()
    {
        SharpMp4.Log.SinkDebug = (s, _) => mainThreadActions.Enqueue(() => Debug.Log(s)); 
        SharpMp4.Log.SinkError = (s, _) => mainThreadActions.Enqueue(() => Debug.LogError(s)); 
        SharpMp4.Log.SinkInfo = (s, _) => mainThreadActions.Enqueue(() => Debug.Log(s)); 
        SharpMp4.Log.SinkTrace = (s, _) => mainThreadActions.Enqueue(() => Debug.Log(s)); 
        SharpMp4.Log.SinkWarn = (s, _) => mainThreadActions.Enqueue(() => Debug.LogWarning(s));
        SharpMp4.Log.DebugEnabled = printDebug;
        SharpMp4.Log.ErrorEnabled = printDebug;
        SharpMp4.Log.InfoEnabled = printDebug;
        SharpMp4.Log.TraceEnabled = printDebug;
        SharpMp4.Log.WarnEnabled = printDebug;
    }

    private void Start()
    {
        var capabilities = RTCRtpSender.GetCapabilities(TrackKind.Video).codecs;
        var videoCodec = null as RTCRtpCodecCapability;
        for (var i = 0; i < capabilities.Length; i++)
        {
            if (string.Compare(capabilities[i].mimeType,VIDEO_MIME_TYPE,
                CultureInfo.InvariantCulture,CompareOptions.IgnoreCase) == 0)
            {
                videoCodec = capabilities[i];
            }
        }
        
        capabilities = RTCRtpSender.GetCapabilities(TrackKind.Audio).codecs;
        var audioCodec = null as RTCRtpCodecCapability;
        for (var i = 0; i < capabilities.Length; i++)
        {
            if (string.Compare(capabilities[i].mimeType,AUDIO_MIME_TYPE,
                    CultureInfo.InvariantCulture,CompareOptions.IgnoreCase) == 0)
            {
                audioCodec = capabilities[i];
            }
        }
        
        if (videoCodec == null || audioCodec == null)
        {
            var errStr = "Recorder startup failed as required codecs are" +
                            " not supported by this platform: ";
            errStr += videoCodec switch
            {
                null when audioCodec == null => $"{VIDEO_MIME_TYPE},{AUDIO_MIME_TYPE}",
                null => VIDEO_MIME_TYPE,
                _ => AUDIO_MIME_TYPE
            };

            Debug.LogWarning(errStr);
            state = State.Unsupported;
            return;
        }
        
        videoCodecs = new []{videoCodec};
        audioCodecs = new []{audioCodec};
        
        state = State.Idle;
    }
    
    private void Update()
    {
        while (mainThreadActions.TryDequeue(out var action))
        {
            action();
        }
        
        if (recordingStream != null)
        {
            while (h264Nalus.TryDequeue(out var nalu))
            {
                recordingStream.EnqueueH264Nalu(nalu.Item1,nalu.Item2);
            }
            
            while (opusPackets.TryDequeue(out var packet))
            {
                recordingStream.EnqueueOpusPacket(packet.Item1,packet.Item2);
            }
        }
    }

    private void OnDestroy()
    {
        if (pcLocal != null)
        {
            pcLocal.Close();
            pcLocal.Dispose();
            pcLocal = null;
        }
        
        if (pcRemote != null)
        {
            pcRemote.Close();
            pcRemote.Dispose();
            pcRemote = null;
        }
        
        recordingStream?.Dispose();
        recordingStream = null;
    }
    
    private void OnIceCandidate_Local(RTCIceCandidate candidate)
    {
        pcRemote.AddIceCandidate(candidate);
    }
    
    private void OnIceCandidate_Remote(RTCIceCandidate candidate)
    {
        pcLocal.AddIceCandidate(candidate);
    }

    private void OnSenderTransformVideo(RTCRtpTransform t, RTCTransformEvent e)
    {
        var tokenizer = new H264Frame(e.Frame.GetData().AsReadOnlySpan());
        while (tokenizer.TryGetNextNalu(out var nalu))
        {
            h264Nalus.Enqueue((nalu.ToArray(),e.Frame.Timestamp));
        }
        
        t.Write(e.Frame);
    }
    
    private void OnSenderTransformAudio(RTCRtpTransform t, RTCTransformEvent e)
    {
        opusPackets.Enqueue((e.Frame.GetData().ToArray(),e.Frame.Timestamp));
        t.Write(e.Frame);
    }
    
    public void StartRecording(int streamWidth, int streamHeight,
        Camera videoSource, string path, int framesPerSecond = -1)
    {
        if (state != State.Idle)
        {
            return;
        }
        
        StartRecording(videoSource.CaptureStream(streamWidth,streamHeight), path);
    }
    
    
    public void StartRecording(int streamWidth, int streamHeight, 
        Camera videoSource, AudioSource audioSource, string path, 
        int framesPerSecond = -1)
    {
        if (state != State.Idle)
        {
            return;
        }
        
        var stream = videoSource.CaptureStream(streamWidth,streamHeight);
        stream.AddTrack(new AudioStreamTrack(audioSource));
        StartRecording(stream,path);
    }

    public void StartRecording(int streamWidth, int streamHeight, 
        Camera videoSource, AudioListener audioSource, string path,
        int framesPerSecond = -1)
    {
        if (state != State.Idle)
        {
            return;
        }
        
        var stream = videoSource.CaptureStream(streamWidth,streamHeight);
        stream.AddTrack(new AudioStreamTrack(audioSource));
        StartRecording(stream,path, framesPerSecond);
    }
    
    private void StartRecording(MediaStream mediaStream, string path,
        int framesPerSecond = -1)
    {
        recordingStream = new RecordingStream(path,e =>
        {
            mainThreadActions.Enqueue(() =>
            {
                if (e != null)
                { 
                    Debug.LogWarning(e.Message);
                    return;
                }

                try
                {
                    // NativeGallery.SaveVideoToGallery(path,"Recordings",Path.GetFileName(path));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(exception);
                }
            });
        });
        
        RTCConfiguration config = default;
        pcLocal = new RTCPeerConnection(ref config);
        pcLocal.OnIceCandidate = OnIceCandidate_Local;
        pcRemote = new RTCPeerConnection(ref config);
        pcRemote.OnIceCandidate = OnIceCandidate_Remote;

        foreach (var track in mediaStream.GetTracks())
        {
            pcLocalSenders.Add(pcLocal.AddTrack(track, mediaStream));
        }
        
        foreach (var transceiver in pcLocal.GetTransceivers())
        {
            if (pcLocalSenders.Contains(transceiver.Sender))
            {
                if (transceiver.Sender.Track.Kind == TrackKind.Video)
                {
                    transceiver.SetCodecPreferences(videoCodecs);
                    if (framesPerSecond > 0)
                    {
                        var param = transceiver.Sender.GetParameters();
                        param.encodings[0].maxFramerate = (uint)framesPerSecond;
                        Debug.Log(param.encodings.Length);
                        param.encodings[0].minBitrate = 10_000_000;
                        param.encodings[0].maxBitrate = 10_000_000;
                        param.encodings[0].scaleResolutionDownBy = 1.0;
                        transceiver.Sender.SetParameters(param);
                    }
                }
                else
                {
                    transceiver.SetCodecPreferences(audioCodecs);
                }
            }
        }

        foreach (var sender in pcLocalSenders)
        {
            if (sender.Track.Kind == TrackKind.Video)
            {
                sender.Transform = new RTCRtpScriptTransform(
                    TrackKind.Video, e => OnSenderTransformVideo(sender.Transform, e));
            }
            else
            {
                sender.Transform = new RTCRtpScriptTransform(
                    TrackKind.Audio, e => OnSenderTransformAudio(sender.Transform, e));
            }
        }
        
        state = State.Starting;
        negotiationCoroutine = StartCoroutine(NegotiatePeerConnections());
    }
    
    public void StopRecording()
    {
        if (state != State.Starting && state != State.Recording)
        {
            return;
        }
        
        StopCoroutine(negotiationCoroutine);
        negotiationCoroutine = null;
        
        foreach (var sender in pcLocalSenders)
        {
            sender.Track?.Dispose();
            pcLocal.RemoveTrack(sender);
            sender.Dispose();
        }
        pcLocalSenders.Clear();

        pcLocal.Close();
        pcRemote.Close();
        pcLocal.Dispose();
        pcRemote.Dispose();
        pcLocal = null;
        pcRemote = null;

        recordingStream.Dispose();
        recordingStream = null;
        
        state = State.Idle;
    }

    IEnumerator NegotiatePeerConnections()
    {
        // Create local offer
        var opOffer = pcLocal.CreateOffer();
        yield return opOffer;
        
        if (opOffer.IsError)
        {
            Debug.LogError(opOffer.Error.message);
            StopRecording();
            yield break;
        }

        if (pcLocal.SignalingState != RTCSignalingState.Stable)
        {
            Debug.LogError("Local PC signaling state not stable.");
            StopRecording();
            yield break;
        }
        
        var desc = opOffer.Desc;
        
        // Set offer as local desc for local, and remote desc for remote
        var opLocalLocalDesc = pcLocal.SetLocalDescription(ref desc);
        yield return opLocalLocalDesc;
        if (opLocalLocalDesc.IsError)
        {
            Debug.LogError(opLocalLocalDesc.Error.message);
            StopRecording();
            yield break;
        }
        
        var opRemoteRemoteDesc = pcRemote.SetRemoteDescription(ref desc);
        yield return opRemoteRemoteDesc;
        if (opRemoteRemoteDesc.IsError)
        {
            Debug.LogError(opRemoteRemoteDesc.Error.message);
            StopRecording();
            yield break;
        }

        // Create remote answer
        var opAnswer = pcRemote.CreateAnswer();
        yield return opAnswer;
        if (opAnswer.IsError)
        {
            Debug.LogError(opAnswer.Error.message);
            StopRecording();
            yield break;
        }
        
        desc = opAnswer.Desc;

        // Set answer as local desc for remote, and remote desc for local
        var opRemoteLocalDesc = pcRemote.SetLocalDescription(ref desc);
        yield return opRemoteLocalDesc;
        if (opRemoteLocalDesc.IsError)
        {
            Debug.LogError(opRemoteLocalDesc.Error.message);
            StopRecording();
            yield break;
        }

        var opLocalRemoteDesc = pcLocal.SetRemoteDescription(ref desc);
        yield return opLocalRemoteDesc;
        if (opLocalRemoteDesc.IsError)
        {
            Debug.LogError(opLocalRemoteDesc.Error.message);
            StopRecording();
            yield break;
        }
        
        state = State.Recording;
    }
}
