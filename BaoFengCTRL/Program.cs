// Program.cs
using System;
using System.IO.Ports;

class Program
{
    static void Main(string[] args)
    {
        // Get the device name from command line or auto-detect
        string devName = args.Length > 0 ? args[0] : DetectSerialDevice();

        int baudRate = 115200; // Default baud rate
        int radioType = -1;    // Default radio type is undefined

        // Use 'using' to ensure the communicator is disposed correctly
        using var device = new SerialDeviceCommunicator(devName, baudRate);

        device.InitialCommunication(); // Start the communication sequence

        // Read 0x1000 bytes (4KB) from address 0xF000 with no decryption
        var memoryData = device.ReadMemory(-1, 0xF000, 0x1000);
        Console.WriteLine($"block data at 0x255: {memoryData[0x255]}"); // Display value at offset 0x255

        // If the radioType is set and doesn't match, update it
        if (radioType >= 0 && radioType <= 9 && (char)memoryData[0x255] != ('0' + radioType))
        {
            memoryData[0x255] = (byte)('0' + radioType); // Change value to match radioType
            device.WriteMemory(-1, 0xF000, memoryData);  // Write updated memory
        }

        // Read specific block from address 0xF240 and display byte at offset 0x15
        var finalBlock = device.ReadBlock(0xF240);
        Console.WriteLine($"Block Data at 0x15: {finalBlock[0x15]}");
    }

    // Auto-detect a USB/COM port if one isn't specified
    static string DetectSerialDevice()
    {
        foreach (string port in SerialPort.GetPortNames())
        {
            if (port.Contains("USB") || port.Contains("COM"))
            {
                Console.WriteLine($"Auto-detected port: {port}");
                return port;
            }
        }
        throw new Exception("No suitable serial port found.");
    }
}