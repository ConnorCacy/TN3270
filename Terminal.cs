using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TN3270
{
    class Terminal
    {
        private const int EBCDIC_ENCODING_PAGE = 37;
        public async Task<bool> WaitForTextAsync(string text, int maxWaitTimeSeconds = 10)
        {
            var startWait = DateTime.Now;

            while (AsciiText == null || !AsciiText.Contains(text))
            {
                if (DateTime.Now.Subtract(startWait).TotalSeconds > maxWaitTimeSeconds) return false;
                await Task.Delay(50);
            }
            return true;
        }


        public bool TrySetText(int index, string text)
        {
            var concernedField = StartFields.Where(w => w.Index <= index).OrderBy(w => w.Index).LastOrDefault();
            if (concernedField == null) return false;
            if (concernedField.Index + text.Length > (24 * 80)) return false;
            concernedField.ModifiedDataTag = true;
            Encoding.GetBytes(text).CopyTo(ScreenBuffer, index + 1);
            return true;

        }

        public bool TrySetTextByFieldIndex(int index, string text)
        {
            var editableFields = StartFields.Where(w => w.CanEdit);
            if (editableFields.Count() < index) return false;
            var screenIndex = editableFields.Skip(index).First().Index;
            return TrySetText(screenIndex, text);
        }
        public void Send(AidKey key = AidKey.Enter)
        {

            List<byte> byteData = new();
            byteData.Add((byte)key);
            var cursorAddress = EncodeAddress(CursorIndex);
            byteData.AddRange(cursorAddress);

            var mdtFields = StartFields.Where(m => m.ModifiedDataTag);
            foreach (var field in mdtFields)
            {
                byteData.Add((byte)Order.SetBufferAddress);
                var fieldAddress = EncodeAddress(field.Index + 1);
                byteData.AddRange(fieldAddress);
                int index = field.Index + 1;
                while (!StartFields.Any(m => m.Index == index))
                {
                    if (index > (24 * 80)) break;
                    if (ScreenBuffer[index] == 0x0)
                    {
                        index++;
                        continue;
                    }
                    byteData.Add(ScreenBuffer[index++]);
                }
            }
            byteData.AddRange(new byte[] { (byte)Telnet.IAC, (byte)Telnet.EOR });
            WriteBytes(byteData.ToArray());

        }
        public Terminal(string host, int port = 23)
        {

            Encoding = Encoding.GetEncoding(EBCDIC_ENCODING_PAGE);
            Decoder = Encoding.GetDecoder();
            Client = new TcpClient(host, port);
            Stream = Client.GetStream();
            Buffer = new byte[5000];
        }
        public async Task<int> BeginSession()
        {
            bool inBinaryMode = false;

            while (!inBinaryMode)
            {
                ResponseLength = Stream.Read(Buffer, 0, Buffer.Length);
                if (ResponseLength >= Buffer.Length) throw new OverflowException("Error, buffer overload, increase or debug this");

                for (int i = 0; i < ResponseLength; i++)
                {
                    byte currentByte = Buffer[i];

                    switch ((Telnet)currentByte)
                    {
                        case Telnet.DO:
                            switch ((Telnet)Buffer[++i])
                            {
                                case Telnet.TELOPT_TTYPE:
                                    WriteTelnet(Telnet.IAC, Telnet.WILL, Telnet.TELOPT_TTYPE);
                                    break;
                                case Telnet.TELOPT_EOR:
                                    WriteTelnet(Telnet.IAC, Telnet.WILL, Telnet.TELOPT_EOR);
                                    break;
                                case Telnet.TELOPT_BINARY:
                                    WriteTelnet(Telnet.IAC, Telnet.WILL, Telnet.TELOPT_BINARY);
                                    break;
                                default:
                                    WriteBytes((byte)Telnet.IAC, (byte)Telnet.WONT, Buffer[i]);
                                    break;
                            }
                            break;
                        case Telnet.SB:
                            currentByte = Buffer[++i];
                            if (currentByte == (byte)Telnet.TELOPT_TTYPE &&
                                Buffer[i + 1] == (byte)Telnet.TELOPT_ECHO &&
                                Buffer[i + 2] == (byte)Telnet.IAC &&
                                Buffer[i + 3] == (byte)Telnet.SE)
                            {
                                i += 3;
                                //terminal type = IBM-3278-2
                                WriteBytes((byte)Telnet.IAC, (byte)Telnet.SB, (byte)Telnet.TELOPT_TTYPE, (byte)Telnet.TELOPT_BINARY,
                                    0x49, 0x42, 0x4D, 0x2D, 0x33, 0x32, 0x37, 0x38, 0x2D, 0x32, (byte)Telnet.IAC, (byte)Telnet.SE);
                            }
                            else
                            {
                                throw new NotSupportedException("Error, not sure what the telnet host is trying to ask (sub negotiate)");
                            }
                            break;
                        case Telnet.WILL:
                            switch ((Telnet)Buffer[++i])
                            {
                                case Telnet.TELOPT_EOR:
                                    WriteTelnet(Telnet.IAC, Telnet.DO, Telnet.TELOPT_EOR);
                                    break;
                                case Telnet.TELOPT_BINARY:
                                    WriteTelnet(Telnet.IAC, Telnet.DO, (byte)Telnet.TELOPT_BINARY);
                                    inBinaryMode = true;
                                    break;
                            }
                            break;
                        default:
                            if (currentByte != (byte)Telnet.IAC) throw new NotSupportedException("Unknown protocol");
                            break;
                    }
                }


            }
            ScreenBuffer = new byte[24 * 80];
  
            while (inBinaryMode)
            {
                await ReadStream();
            }

            return 0;
        }


        private readonly TcpClient Client;
        private readonly NetworkStream Stream;
        private byte[] Buffer { get; set; }
        private byte[] ScreenBuffer { get; set; }
       
        public string AsciiText { get; set; }
        private int ResponseLength { get; set; }
        private int CursorIndex { get; set; }
        private List<StartField> StartFields { get; set; }
        private readonly byte[] TranslationTable = { 0x40, 0xC1, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, 0x4F, 0x50, 0xD1, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x61, 0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0x6A, 0x6B, 0x6C, 0x6D, 0x6E, 0x6F, 0xF0, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F };

        Decoder Decoder { get; set; }
        Encoding Encoding { get; set; }
        private int DecodeAddress(byte byte1, byte byte2)
        {
            var index1 = Array.FindIndex(TranslationTable, e => e == byte1);
            var index2 = Array.FindIndex(TranslationTable, e => e == byte2);
            var addressBinary = $"{Convert.ToString(index1, 2).PadLeft(6, '0')}{Convert.ToString(index2, 2).PadLeft(6, '0')}";
            return Convert.ToInt32(addressBinary, 2);
        }
        private byte[] EncodeAddress(int address)
        {
            string binaryAddress = Convert.ToString(address, 2).PadLeft(12, '0');
            string leftAddress = binaryAddress.Substring(0, 6);
            string rightAddress = binaryAddress[6..];
            byte[] encodedAddress = new byte[2];
            encodedAddress[0] = TranslationTable[Convert.ToInt32(leftAddress, 2)];
            encodedAddress[1] = TranslationTable[Convert.ToInt32(rightAddress, 2)];
            return encodedAddress;
        }
        private void WriteBytes(params byte[] data)
        {
            Stream.Write(data, 0, data.Length);
        }
        private void WriteTelnet(params Telnet[] telnets)
        {
            WriteBytes(telnets.Select(m => (byte)m).ToArray());
        }



        private async Task<int> ReadStream()
        {
            ResponseLength = await Stream.ReadAsync(Buffer.AsMemory(0, Buffer.Length));
            while (Buffer[ResponseLength - 1] != (byte)Telnet.EOR)
            {
                ResponseLength += await Stream.ReadAsync(Buffer.AsMemory(ResponseLength - 1, Buffer.Length));
            }
            
            switch ((Command)Buffer[0])
            {
                case Command.ReadBuffer:
                    throw new NotSupportedException("Cannot read buffer back (yet)");
                case Command.Write:
                    //not quite implemented yet
                    ScreenBuffer = new byte[24 * 80];
                    break;
                case Command.EraseWrite:
                case Command.EraseWriteAlternate:
                    ScreenBuffer = new byte[24 * 80];
                    break;
                default:
                    throw new NotSupportedException("Unknown protocol");
            }
            //byte writeControlChar = Buffer[1];
            int screenIndex = 0;
            StartFields = new List<StartField>();
            for (int i = 2; i < ResponseLength - 2; i++)
            {
                if (Enum.IsDefined(typeof(Order), Buffer[i]))
                {
                    switch ((Order)Buffer[i])
                    {
                        case Order.InsertCursor:
                            CursorIndex = screenIndex;
                            break;
                        case Order.ProgramTab:
                            break;
                        case Order.SetBufferAddress:
                            screenIndex = DecodeAddress(Buffer[i + 1], Buffer[i + 2]);
                            i += 2;
                            break;
                        case Order.StartField:
                            ScreenBuffer[screenIndex] = 0x40;
                            StartFields.Add(new StartField(screenIndex++, Buffer[++i]));

                            break;
                        case Order.RepeatToAddress:
                            byte repeatChar = Buffer[i + 3];
                            int endAddress = DecodeAddress(Buffer[i + 1], Buffer[i + 2]);
                            if (endAddress == 0) endAddress = ScreenBuffer.Length - 1;
                            while (screenIndex <= endAddress)
                            {
                                ScreenBuffer[screenIndex] = repeatChar;
                                screenIndex++;
                            }
                            i += 3;
                      
                            screenIndex = endAddress;
                            break;
                        case Order.EraseUnprotectedToAddress:
                            byte eraseChar = 0x40;
                            int eraseEndAddress = DecodeAddress(Buffer[i + 1], Buffer[i + 2]);
                            while (screenIndex <= eraseEndAddress)
                            {
                                ScreenBuffer[screenIndex] = eraseChar;
                                screenIndex++;
                            }
                            
                            break;
                        default:
                          
                            break;
                    }
                }
                else
                {

                    ScreenBuffer[screenIndex++] = Buffer[i];
                }

            }
            var asciiScreen = new char[24 * 80];
            Decoder.Convert(ScreenBuffer, 0, ScreenBuffer.Length, asciiScreen, 0, ScreenBuffer.Length, false, out _, out _, out _);
            AsciiText = new string(asciiScreen);
            return 0;
        }
    }
}
