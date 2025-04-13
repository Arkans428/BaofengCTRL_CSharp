// SerialDeviceCommunicator.cs
using System;
using System.IO.Ports;
using System.Text;
using System.Linq;

public class SerialDeviceCommunicator : IDisposable
{
    private SerialPort _port; // Represents the serial port connection to the hardware device
    public byte BlockSize { get; set; } // Size of memory block to read/write, default is 64 bytes (0x40)

    // Constructor: Initializes and opens the serial port
    public SerialDeviceCommunicator(string portName, int baudRate = 115200)
    {
        _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 1000, // Set read timeout to 1 second
            WriteTimeout = 1000 // Set write timeout to 1 second
        };
        _port.Open(); // Open the serial connection
        _port.DiscardInBuffer(); // Clear any existing data in the input buffer
        BlockSize = 0x40; // Set default block size to 64 bytes
    }

    // Properly dispose of the serial port to release system resources
    public void Dispose()
    {
        if (_port != null)
        {
            if (_port.IsOpen)
                _port.Close(); // Close the serial port if it's open
            _port.Dispose(); // Dispose the SerialPort object
            _port = null; // Nullify the reference for safety
        }
    }

    // Send data to the device and read an expected number of response bytes
    private byte[] SendReceive(byte[] dataToSend, int expectedResponseLength)
    {
        _port.Write(dataToSend, 0, dataToSend.Length); // Send data to device
        var buffer = new byte[expectedResponseLength]; // Prepare buffer for response
        int offset = 0;

        // Keep reading until we've received the expected number of bytes
        while (offset < expectedResponseLength)
        {
            try
            {
                int bytesRead = _port.Read(buffer, offset, expectedResponseLength - offset); // Read bytes
                if (bytesRead == 0)
                    throw new Exception($"Short read [{expectedResponseLength} {offset}]");

                offset += bytesRead; // Track number of bytes received
            }
            catch (TimeoutException)
            {
                throw new Exception("Read timed out");
            }
        }
        return buffer; // Return full response
    }

    // Begin communication sequence with the device
    public void InitialCommunication()
    {
        Console.WriteLine("Starting initial communication...");

        // Step 1: Handshake
        Console.WriteLine("Sending: PROGRAMBFNORMALU");
        Console.WriteLine($"Response: {ToHexString(SendReceive(Encoding.ASCII.GetBytes("PROGRAMBFNORMALU"), 1))}");

        // Step 2: Send 'F' command
        Console.WriteLine("Sending: F");
        Console.WriteLine($"Response: {ToHexString(SendReceive(new byte[] { (byte)'F' }, 16))}");

        // Step 3: Send 'M' command
        Console.WriteLine("Sending: M");
        Console.WriteLine($"Response: {Encoding.ASCII.GetString(SendReceive(new byte[] { (byte)'M' }, 15))}");

        // Step 4: SEND command with custom payload
        byte[] sendCommand = new byte[] {
            (byte)'S', (byte)'E', (byte)'N', (byte)'D',
            0x21, 0x05, 0x0D, 0x01, 0x01, 0x01, 0x04, 0x11,
            0x08, 0x05, 0x0D, 0x0D, 0x01, 0x11, 0x0F, 0x09,
            0x12, 0x09, 0x10, 0x04, 0x00
        };
        Console.WriteLine("Sending: SEND command...");
        Console.WriteLine($"Response: {ToHexString(SendReceive(sendCommand, 1))}");
    }

    // Reads a memory block from the device
    public byte[] ReadBlock(ushort address)
    {
        byte[] cmd = new byte[] { (byte)'R', (byte)(address >> 8), (byte)(address & 0xFF), BlockSize }; // Build read command
        byte[] response = SendReceive(cmd, 4 + BlockSize); // Expect header + data

        // Validate the header
        if (response[0] != 0x52 || response[1] != (byte)(address >> 8) || response[2] != (byte)(address & 0xFF))
            throw new Exception("Wrong read response");

        return response.Skip(4).ToArray(); // Return the data only
    }

    // Writes a block of memory to the device
    public void WriteBlock(ushort address, byte[] data)
    {
        byte[] cmd = new byte[4 + BlockSize];
        cmd[0] = (byte)'W'; // Write command
        cmd[1] = (byte)(address >> 8);
        cmd[2] = (byte)(address & 0xFF);
        cmd[3] = BlockSize;
        Array.Copy(data, 0, cmd, 4, BlockSize);

        byte[] response = SendReceive(cmd, 1); // Expect ACK
        if (response[0] != 0x06)
            throw new Exception("Wrong write response");
    }

    // Reads memory in blocks, optionally decrypting it
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

    // Writes memory in blocks, optionally encrypting it
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

    // XOR-based encryption/decryption routine using 4-byte keys
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

    // Convert byte array to hexadecimal string with spaces
    private string ToHexString(byte[] data)
    {
        return BitConverter.ToString(data).Replace("-", " ");
    }
}