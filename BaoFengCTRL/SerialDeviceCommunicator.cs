using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace BaoFengCTRL
{
    public class SerialDeviceCommunicator : IDisposable
    {
        private SerialPort _port;
        public byte BlockSize { get; set; }

        public SerialDeviceCommunicator(string portName, int baudRate = 115200)
        {
            _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            _port.Open();
            _port.DiscardInBuffer();
            BlockSize = 0x40;
        }

        public void Dispose()
        {
            if (_port != null)
            {
                if (_port.IsOpen)
                    _port.Close();
                _port.Dispose(); // <-- This is required to fully release resources
                _port = null;
            }
        }

        private byte[] SendReceive(byte[] dataToSend, int expectedResponseLength)
        {
            _port.Write(dataToSend, 0, dataToSend.Length);
            var buffer = new byte[expectedResponseLength];
            int offset = 0;

            while (offset < expectedResponseLength)
            {
                try
                {
                    int bytesRead = _port.Read(buffer, offset, expectedResponseLength - offset);
                    if (bytesRead == 0)
                    {
                        throw new Exception($"Short read [{expectedResponseLength} {offset}]");
                    }
                    offset += bytesRead;
                }
                catch (TimeoutException)
                {
                    throw new Exception("Read timed out");
                }
            }
            return buffer;
        }

        public void InitialCommunication()
        {
            Console.WriteLine("Starting initial communication...");

            Console.WriteLine($"Sending: PROGRAMBFNORMALU");
            Console.WriteLine($"Response: {ToHexString(SendReceive(Encoding.ASCII.GetBytes("PROGRAMBFNORMALU"), 1))}");

            Console.WriteLine("Sending: F");
            Console.WriteLine($"Response: {ToHexString(SendReceive(new byte[] { (byte)'F' }, 16))}");

            Console.WriteLine("Sending: M");
            Console.WriteLine($"Response: {Encoding.ASCII.GetString(SendReceive(new byte[] { (byte)'M' }, 15))}");

            byte[] sendCommand = new byte[] {
            (byte)'S', (byte)'E', (byte)'N', (byte)'D',
            0x21, 0x05, 0x0D, 0x01, 0x01, 0x01, 0x04, 0x11,
            0x08, 0x05, 0x0D, 0x0D, 0x01, 0x11, 0x0F, 0x09,
            0x12, 0x09, 0x10, 0x04, 0x00
        };
            Console.WriteLine("Sending: SEND command...");
            Console.WriteLine($"Response: {ToHexString(SendReceive(sendCommand, 1))}");
        }

        public byte[] ReadBlock(ushort address)
        {
            byte[] cmd = new byte[] { (byte)'R', (byte)(address >> 8), (byte)(address & 0xFF), BlockSize };
            byte[] response = SendReceive(cmd, 4 + BlockSize);

            if (response[0] != 0x52 || response[1] != (byte)(address >> 8) || response[2] != (byte)(address & 0xFF))
                throw new Exception("Wrong read response");

            return response.Skip(4).ToArray();
        }

        public void WriteBlock(ushort address, byte[] data)
        {
            byte[] cmd = new byte[4 + BlockSize];
            cmd[0] = (byte)'W';
            cmd[1] = (byte)(address >> 8);
            cmd[2] = (byte)(address & 0xFF);
            cmd[3] = BlockSize;
            Array.Copy(data, 0, cmd, 4, BlockSize);

            byte[] response = SendReceive(cmd, 1);
            if (response[0] != 0x06)
                throw new Exception("Wrong write response");
        }

        public byte[] ReadMemory(int keyIndex, ushort address, int count)
        {
            byte[] result = new byte[count];
            for (int i = 0; i < count; i += BlockSize)
            {
                var block = ReadBlock(address);
                byte[] data = keyIndex >= 0 ? Crypt(block, keyIndex) : block;
                Array.Copy(data, 0, result, i, BlockSize);
                address += BlockSize;
            }
            return result;
        }

        public void WriteMemory(int keyIndex, ushort address, byte[] data)
        {
            for (int i = 0; i < data.Length; i += BlockSize)
            {
                byte[] block = data.Skip(i).Take(BlockSize).ToArray();
                byte[] writeData = keyIndex >= 0 ? Crypt(block, keyIndex) : block;
                WriteBlock(address, writeData);
                address += BlockSize;
            }
        }

        public byte[] Crypt(byte[] data, int index)
        {
            byte[] keys = Encoding.ASCII.GetBytes("BHT CO 7A ES EIYM PQXN YRVB  HQPW RCMS N SATK DHZO RC SL6RB  JCGPN VJ PKEK LI LZ");
            byte[] result = new byte[data.Length];
            byte[] key = keys.Skip(index * 4).Take(4).ToArray();

            for (int i = 0, j = 0; i < data.Length; i++, j = (j + 1) & 0x3)
            {
                if (key[j] != 0x20 && data[i] != 0 && data[i] != 0xFF && key[j] != data[i] && (key[j] ^ data[i]) != 0xFF)
                    result[i] = (byte)(data[i] ^ key[j]);
                else
                    result[i] = data[i];
            }
            return result;
        }

        private string ToHexString(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", " ");
        }


    }
}
