using BaoFengCTRL;
using System;
using System.IO.Ports;

class Program
{
    static void Main(string[] args)
    {
        string devName = args.Length > 0 ? args[0] : DetectSerialDevice();
        int baudRate = 115200;
        int radioType = -1;

        using var device = new SerialDeviceCommunicator(devName, baudRate);
        device.InitialCommunication();

        var memoryData = device.ReadMemory(-1, 0xF000, 0x1000);
        Console.WriteLine($"block data at 0x255: {memoryData[0x255]}");

        if (radioType >= 0 && radioType <= 9 && (char)memoryData[0x255] != ('0' + radioType))
        {
            memoryData[0x255] = (byte)('0' + radioType);
            device.WriteMemory(-1, 0xF000, memoryData);
        }

        var finalBlock = device.ReadBlock(0xF240);
        Console.WriteLine($"Block Data at 0x15: {finalBlock[0x15]}");
    }

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