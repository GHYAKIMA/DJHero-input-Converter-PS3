using System;
using System.Threading;
using HIDInterface;
using System.Reflection;
using vJoyInterfaceWrap;

namespace DJHInput_Converter
{
    class Program
    {
        /* Program information */
        static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        static readonly string Name = Assembly.GetExecutingAssembly().GetName().Name.ToString();

        /* vJoy */
        static public vJoy Gamepad;
        static public vJoy.JoystickState iReport;

        /* DJHero Bytes map */
        public struct DJHControllerMap
        {

            public byte Buttons;
            public byte DPAD;
            public byte PS; 

            public byte TT_normal;  
            public byte Filter_normal;
            public byte Filter_step;
            public byte Slider_normal;
            public byte Slider_step;
        }
        static int a = 0;
        static void Main()
        {
            Console.Clear();

            
            Console.Title = Name + " [" + Version + "]";

            // Create a joystick object 
            Gamepad = new vJoy();
            iReport = new vJoy.JoystickState();

            // Get list of all connected HID devices 
            HIDDevice.interfaceDetails[] Devices = HIDDevice.getConnectedDevices();

            // Count HID Devices 
            int ConnectedDevices = 0;

            // Print devices list
            Console.Write("Select DJHero USB Dongle\n-----------------------");

            foreach (HIDDevice.interfaceDetails Dev in Devices)
            {
                ConnectedDevices++;
                Console.Write("\n{0}.{1} (VID:{2} PID:{3})", ConnectedDevices, Dev.product, Dev.VID.ToString("X"), Dev.PID.ToString("X"));
            }

            Console.Write("\n-----------------------\n");

            /* Wait for user selection  */
            Console.Write("Selection: ");
            
            // Parse user input 
            if (!int.TryParse(Console.ReadLine(), out int devnum))
            {
                Console.Clear();
                Console.Write("Invalid input data!");
                Thread.Sleep(1000);
                Program.Main();
            }
            else
            {
                devnum -= 1;
            }

            // Check if user selected listed device
            if (devnum > ConnectedDevices - 1 | devnum < 0)
            {
                Console.Clear();
                Console.Write("Selected invalid value! (out of range)");
                Thread.Sleep(1000);
                Program.Main();
            }
            else
            {
                Console.Clear();
                Console.Write("Selected: {0}", Devices[devnum].product);

                // Check vJoy status
                switch (Gamepad.GetVJDStatus(1))
                {
                    case VjdStat.VJD_STAT_MISS:

                        Console.Write("\n vJoy is not installed or enabled!");
                        Thread.Sleep(1000);
                        Program.Main();
                        break;

                    case VjdStat.VJD_STAT_BUSY:
                        Console.Write("\n vJoy device {0} is already owned by another feeder", 1);
                        Thread.Sleep(1000);
                        Program.Main();
                        break;

                    case VjdStat.VJD_STAT_FREE:
                        Console.Write("\n vJoy device 1 is free");

                        // Acquire device
                        Gamepad.AcquireVJD(1);
                        Gamepad.ResetVJD(1);

                        Thread.Sleep(1000);
                        Console.Clear();

                        // Start reading DJH Bytes
                        ReadDJHDevice(Devices[devnum].devicePath);
                        
                        break;
                }

            }
            Thread.Sleep(-1);
        }

        static void ReadDJHDevice(string DevicePath)
        {
            // Create handle to the device
            HIDDevice DJH = new HIDDevice(DevicePath, false);

            // Read bytes & map DJH
            DJHControllerMap Map = new DJHControllerMap();
            while (true)
            {
                byte[] DJHBytes = DJH.read();

                // Map
                Map.Buttons = DJHBytes[1];
                Map.DPAD = DJHBytes[3];
                Map.PS = DJHBytes[2];

                Map.TT_normal = DJHBytes[7];
                Map.Filter_normal = DJHBytes[20];
                Map.Filter_step = DJHBytes[21];
                Map.Slider_normal = DJHBytes[22];
                Map.Slider_step = DJHBytes[23];

                // Print 
                Console.Write("\rButton: {0}\t DPAD: {1}\t PS: {2}\t TT: {3}\t Filter: {4}|{5}\tSlider: {6}|{7}\t", 
                   Map.Buttons, Map.DPAD, Map.PS, Map.TT_normal, Map.Filter_normal, Map.Filter_step, Map.Slider_normal, Map.Slider_step);

                // Report vJoy 
                UpdateGamepad(Map);
            }

        }

        static void UpdateGamepad(DJHControllerMap Map)
        {
            iReport.bDevice = (byte)1;
            if (Map.TT_normal != 0) { iReport.AxisY = (Map.TT_normal - 0x80) * 0x1FF + 0x3FFF; }
            iReport.Dial = ((Map.Filter_step * 0x1FFF) + (Map.Filter_normal * 0x20) - 0x100) * 3;
            iReport.Slider = (Map.Slider_step * 0x1FFF) + (Map.Slider_normal * 0x20);

            if (Map.PS >= 0x10)
            {
                a = 0x20;
                Map.PS -= 0x10;
            }
            else
            {
                a = 0x0;
            }
            iReport.Buttons = (uint)Map.Buttons + (uint)(Map.PS << 0x04) + (uint)a;

            if (Map.DPAD == 0x0F)
            {
                iReport.bHats = 0xFFFFFF;
            }
            else
            {
                iReport.bHats = (uint)4487 * Map.DPAD;
            }

            // Feed the driver
            Gamepad.UpdateVJD(1, ref iReport);
        }
    }
}
