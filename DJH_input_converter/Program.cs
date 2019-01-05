using System;
using System.Threading;
using HIDInterface;
using System.Reflection;
using vJoyInterfaceWrap;
using System.Diagnostics.Contracts;

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
            /* ID */
            public byte PAD;
            public byte DPAD;
            /* PAD */
            public byte Square;
            public byte Cross;
            public byte Circle;
            public byte Triangle;

            /* TT */
            public byte G => Cross;
            public byte B => Square;
            public byte R => Circle;
            /* Special */
            public byte Euphoria => Triangle;
            public byte PS;                     // <-- PS, SELECT, START
            public byte TT_normal;              // <-- 0x80 - idle
            public byte Filter_normal;
            public byte Filter_step;
            public byte Slider_normal;
            public byte Slider_step;
        }

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
                Console.Write("Select invalid value! (out of range)");
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
                Map.PAD = DJHBytes[1];
                Map.DPAD = DJHBytes[3];

                Map.Square = DJHBytes[8];       // + G
                Map.Cross = DJHBytes[10];       // + B
                Map.Circle = DJHBytes[13];      // + R
                Map.Triangle = DJHBytes[12];    // + Euphoria

                Map.PS = DJHBytes[2];

                Map.TT_normal = DJHBytes[7];

                Map.Filter_normal = DJHBytes[20];
                Map.Filter_step = DJHBytes[21];

                Map.Slider_normal = DJHBytes[22];
                Map.Slider_step = DJHBytes[23];

                // Print 
               Console.Write("\rPS: {0}\tSquare: {1}\tCross: {2}\tCircle: {3}\tTriangle: {4}\tFilter: {5}|{6}\tSlider: {7}|{8}\tTT: {9}\t", Map.PS,
                  Map.Square, Map.Cross, Map.Circle, Map.Triangle, Map.Filter_normal, Map.Filter_step, Map.Slider_normal, Map.Slider_step, Map.TT_normal);

                // Report vJoy 
                UpdateGamepad(Map);
            }

        }

        static void UpdateGamepad(DJHControllerMap Map)
        {
            iReport.bDevice = (byte)1;
            if (Map.TT_normal != 0) { iReport.AxisY = Map.TT_normal * Map.TT_normal / 128; }
             iReport.Dial = (Map.Filter_step * 0x2AAA) + (Map.Filter_normal / 3);
             iReport.Slider = (Map.Slider_step * 0x2AAA) + (Map.Slider_normal / 3); 
             iReport.Buttons = (uint) Map.PAD;

            if (Map.DPAD == 0x0F)
            {
                iReport.bHats = (uint)0xFFFFFF;
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
