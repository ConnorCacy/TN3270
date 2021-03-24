using System;

namespace TN3270
{
    public enum Telnet : byte
    {
        IAC = 255,
        DO = 253,
        WONT = 252,
        WILL = 251,
        SB = 250, //subnegotiation begin 
        SE = 240, //subnegotiation end 
        EOR = 239,
        TELOPT_EOR = 25,
        TELOPT_TTYPE = 24,
        TELOPT_ECHO = 1,
        TELOPT_BINARY = 0
    }
    public enum Command : byte
    {
        Write = 0x01,
        ReadBuffer = 0x02,
        EraseWrite = 0x05,
        EraseWriteAlternate = 0x0d
    }
    public enum Order : byte
    {
        ProgramTab = 0x05,
        SetBufferAddress = 0x11,
        InsertCursor = 0x13,
        StartField = 0x1D,
        RepeatToAddress = 0x3C,
        EraseUnprotectedToAddress = 0x12

    }
    public enum AidKey : byte
    {
        Enter = 0x7D,
        F1 = 0x01,
        F2 = 0x02,
        F3 = 0x03
    }
}
