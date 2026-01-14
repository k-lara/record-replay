using System;

/// <summary>
/// Extract a sequence of NALUs from a byte array representing an encoded H264
/// frame.
/// </summary>
public ref struct H264Frame
{
    private ReadOnlySpan<byte> frame;
    private int head;
    
    public H264Frame(ReadOnlySpan<byte> frame)
    {
        this.frame = frame;
        this.head = 0;
    }
    
    /// <summary>
    /// Attempt to extract the next NALU in the frame. Will provide a new NALU
    /// each time it is called until no NALU remain. 
    /// </summary>
    /// <returns>True if a NALU was found, false otherwise.</returns>
    public bool TryGetNextNalu(out ReadOnlySpan<byte> nalu)
    {
        var zeroCounter = 0;
        var naluStart = head;
        for (int i = head; i < frame.Length; i++)
        {
            if (frame[i] == 0)
            {
                zeroCounter++;
                continue;
            }
            
            if (frame[i] == 1 && zeroCounter == 3)
            {
                // Magic NALU starter sequence
                var lenMid = (i - 3) - naluStart;
                if (lenMid > 0)
                {
                    head = i + 1;
                    nalu = frame.Slice(naluStart,lenMid);
                    return true;
                }
                naluStart = i + 1;
            }
            
            zeroCounter = 0;
        }

        // Set head to end of frame - scan is now complete
        head = frame.Length;

        // Return final nalu if there is one
        var lenFinal = frame.Length - naluStart;
        if (lenFinal > 0)
        {
            nalu = frame.Slice(naluStart,lenFinal);
            return true;
        }
        
        // No remaining NALU
        nalu = ReadOnlySpan<byte>.Empty;
        return false;
    }
}
