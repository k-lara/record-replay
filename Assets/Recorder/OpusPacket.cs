public ref struct OpusPacket
{
    public byte[] packet { get; private set; }
    public bool isValid { get; private set; }
    public bool isStereo { get; private set; }
    public uint frameCount { get; }
    public uint frameDurationTimeUnits { get; }
    public uint totalDurationTimeUnits => frameDurationTimeUnits * frameCount;

    public const uint TIME_UNITS_PER_SECOND = 2000; 
    
    public OpusPacket(byte[] packet)
    {
        this.packet = packet;
        
        isValid = false;
        isStereo = false;
        frameCount = 0;
        frameDurationTimeUnits = 0;
        
        if (packet.Length == 0)
        {
            return;
        }
        
        var toc = packet[0];

        // RFC 6716 Table 2
        // We use 'time units' (2 x ms) rather than ms because some TOC configs
        // use half-ms values and it's convenient to store them as uints 
        var config = toc >> 3;
        switch(config)
        {
            case 00: frameDurationTimeUnits = 20; break;
            case 01: frameDurationTimeUnits = 40; break;
            case 02: frameDurationTimeUnits = 80; break;
            case 03: frameDurationTimeUnits = 120; break;
            case 04: frameDurationTimeUnits = 20; break;
            case 05: frameDurationTimeUnits = 40; break;
            case 06: frameDurationTimeUnits = 80; break;
            case 07: frameDurationTimeUnits = 120; break;
            case 08: frameDurationTimeUnits = 20; break;
            case 09: frameDurationTimeUnits = 40; break;
            case 10: frameDurationTimeUnits = 80; break;
            case 11: frameDurationTimeUnits = 120; break;
            case 12: frameDurationTimeUnits = 20; break;
            case 13: frameDurationTimeUnits = 40; break;
            case 14: frameDurationTimeUnits = 20; break;
            case 15: frameDurationTimeUnits = 40; break;
            case 16: frameDurationTimeUnits = 5; break;
            case 17: frameDurationTimeUnits = 10; break;
            case 18: frameDurationTimeUnits = 20; break;
            case 19: frameDurationTimeUnits = 40; break;
            case 20: frameDurationTimeUnits = 5; break;
            case 21: frameDurationTimeUnits = 10; break;
            case 22: frameDurationTimeUnits = 20; break;
            case 23: frameDurationTimeUnits = 40; break;
            case 24: frameDurationTimeUnits = 5; break;
            case 25: frameDurationTimeUnits = 10; break;
            case 26: frameDurationTimeUnits = 20; break;
            case 27: frameDurationTimeUnits = 40; break;
            case 28: frameDurationTimeUnits = 5; break;
            case 29: frameDurationTimeUnits = 10; break;
            case 30: frameDurationTimeUnits = 20; break;
            case 31: frameDurationTimeUnits = 40; break;
        }
        
        isStereo = (toc & 0b100) != 0;
        
        var fcc = toc & 0b11;
        switch (fcc)
        {
            case 0: frameCount = 1; break;
            case 1: frameCount = 2; break;
            case 2: frameCount = 2; break;
            case 3:
            {
                if (packet.Length < 2)
                {
                    return;
                }
                
                frameCount = (uint)(packet[1] & 0b00111111);
                break;
            }
        }
        
        isValid = true;
    }
}
