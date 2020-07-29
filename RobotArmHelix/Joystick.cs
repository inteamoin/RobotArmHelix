using log4net;
using SharpDX.DirectInput;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace RobotArmHelix
{
    public class Joystick : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        SharpDX.DirectInput.Joystick joystick;
        MyJoystickState state;
        static DirectInput directInput = new DirectInput();
        public bool enabled = false;
        bool[] buttonpressed = new bool[128];
        public string name;
        public bool elevons = false;

        public bool manual_control = false;

        string joystickconfigbutton = "joystickbuttons.xml";
        string joystickconfigaxis = "joystickaxis.xml";

        // set to default midpoint
        int hat1 = 0;//= 65535 / 2;
        int hat2 = 65535 / 2;
        int custom0 = 65535 / 2;
        int custom1 = 65535 / 2;


        public struct JoyChannel
        {
            public int channel;
            public joystickaxis axis;
            public bool reverse;
            public int expo;
        }

        public struct JoyButton
        {
            /// <summary>
            /// System button number
            /// </summary>
            public int buttonno;

            /// <summary>
            /// Fucntion we are doing for this button press
            /// </summary>
            public buttonfunction function;

            /// <summary>
            /// Mode we are changing to on button press
            /// </summary>
            public string mode;

            /// <summary>
            /// param 1
            /// </summary>
            public float p1;

            /// <summary>
            /// param 2
            /// </summary>
            public float p2;

            /// <summary>
            /// param 3
            /// </summary>
            public float p3;

            /// <summary>
            /// param 4
            /// </summary>
            public float p4;

            /// <summary>
            /// Relay state
            /// </summary>
            public bool state;
        }

        public enum buttonfunction
        {
            ChangeMode,
            Do_Set_Relay,
            Do_Repeat_Relay,
            Do_Set_Servo,
            Do_Repeat_Servo,
            Arm,
            Disarm,
            Digicam_Control,
            TakeOff,
            Mount_Mode,
            Toggle_Pan_Stab,
            Gimbal_pnt_track,
            Mount_Control_0,
            Button_axis0,
            Button_axis1,
        }


        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Implement reccomended best practice dispose pattern
        /// http://msdn.microsoft.com/en-us/library/b1yfkh5e%28v=vs.110%29.aspx
        /// </summary>
        /// <param name="disposing"></param>
        virtual protected void Dispose(bool disposing)
        {
            try
            {
                //not sure if this is a problem from the finalizer?
                if (disposing && joystick != null && joystick.Properties != null)
                    joystick.Unacquire();
            }
            catch
            {
            }

            try
            {
                if (disposing && joystick != null)
                    joystick.Dispose();
            }
            catch
            {
            }

            //tell gc not to call finalize, this object will be GC'd quicker now.
            GC.SuppressFinalize(this);
        }

        //no need for finalizer...
        //~Joystick()
        //{
        //    Dispose(false);
        //}


        private SynchronizationContext _context;

        public Joystick()
        {

            this._context = SynchronizationContext.Current;
            if (_context == null)
            {
                _context = new SynchronizationContext();
            }

            for (int a = 0; a < JoyButtons.Length; a++)
                JoyButtons[a].buttonno = -1;

            //if (currentInterface() == null)
            //    return;

            //if (Interface.MAV.cs.firmware == Firmwares.ArduPlane)
            //{
            //    loadconfig("joystickbuttons" + Interface.MAV.cs.firmware + ".xml",
            //        "joystickaxis" + Interface.MAV.cs.firmware + ".xml");
            //}
            //else if (Interface.MAV.cs.firmware == Firmwares.ArduCopter2)
            //{
            //    loadconfig("joystickbuttons" + Interface.MAV.cs.firmware + ".xml",
            //        "joystickaxis" + Interface.MAV.cs.firmware + ".xml");
            //}
            //else if (Interface.MAV.cs.firmware == Firmwares.ArduRover)
            //{
            //    loadconfig("joystickbuttons" + Interface.MAV.cs.firmware + ".xml",
            //        "joystickaxis" + Interface.MAV.cs.firmware + ".xml");
            //}
            //else
            //{
                loadconfig();
            //}
        }

        public void loadconfig(string joystickconfigbuttonin = "joystickbuttons.xml",
            string joystickconfigaxisin = "joystickaxis.xml")
        {
            log.Info("Loading joystick config files " + joystickconfigbuttonin + " " + joystickconfigaxisin);

            // save for later
            if (File.Exists(joystickconfigaxisin))
            {
                this.joystickconfigbutton = joystickconfigbuttonin;
                this.joystickconfigaxis = joystickconfigaxisin;
            }
            else
            {
                //this.joystickconfigbutton = Settings.GetUserDataDirectory() + joystickconfigbuttonin;
                //this.joystickconfigaxis = Settings.GetUserDataDirectory() + joystickconfigaxisin;
            }

            // load config
            if (File.Exists(this.joystickconfigbutton) && File.Exists(this.joystickconfigaxis))
            {
                try
                {
                    System.Xml.Serialization.XmlSerializer reader =
                        new System.Xml.Serialization.XmlSerializer(typeof(JoyButton[]), new Type[] { typeof(JoyButton) });

                    using (StreamReader sr = new StreamReader(this.joystickconfigbutton))
                    {
                        JoyButtons = (JoyButton[])reader.Deserialize(sr);
                    }
                }
                catch
                {
                }

                try
                {
                    System.Xml.Serialization.XmlSerializer reader =
                        new System.Xml.Serialization.XmlSerializer(typeof(JoyChannel[]),
                            new Type[] { typeof(JoyChannel) });

                    using (StreamReader sr = new StreamReader(this.joystickconfigaxis))
                    {
                        JoyChannels = (JoyChannel[])reader.Deserialize(sr);
                    }

                    Array.Resize(ref JoyChannels, 20);
                }
                catch
                {
                }
            }

            for(int i=1; i < 20; i++)
            {
                JoyChannels[i] = new JoyChannel()
                {
                    channel = i,
                    axis = joystickaxis.None,
                    expo = 0,
                    reverse = false
                };
            }
            
            JoyChannels[1].axis = joystickaxis.Y;
            JoyChannels[1].reverse = true;

            JoyChannels[2].axis = joystickaxis.X;
            JoyChannels[2].reverse = true;

            JoyButtons[0].buttonno = 0;
            JoyButtons[2].buttonno = 2;
            JoyButtons[3].buttonno = 3;
            JoyButtons[4].buttonno = 4;
            JoyButtons[5].buttonno = 5;

        }

        public void saveconfig()
        {
            log.Info("Saving joystick config files " + joystickconfigbutton + " " + joystickconfigaxis);

            // save config
            System.Xml.Serialization.XmlSerializer writer =
                new System.Xml.Serialization.XmlSerializer(typeof(JoyButton[]), new Type[] { typeof(JoyButton) });

            using (StreamWriter sw = new StreamWriter(joystickconfigbutton))
            {
                writer.Serialize(sw, JoyButtons);
            }

            writer = new System.Xml.Serialization.XmlSerializer(typeof(JoyChannel[]), new Type[] { typeof(JoyChannel) });

            using (StreamWriter sw = new StreamWriter(joystickconfigaxis))
            {
                writer.Serialize(sw, JoyChannels);
            }
        }

        JoyChannel[] JoyChannels = new JoyChannel[20]; // we are base 1
        JoyButton[] JoyButtons = new JoyButton[128]; // base 0
        private JoystickData previous;

        public static IList<DeviceInstance> getDevices()
        {
            return directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);
        }

        public static SharpDX.DirectInput.Joystick getJoyStickByName(string name)
        {
            var joysticklist = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);

            foreach (DeviceInstance device in joysticklist)
            {
                if (device.ProductName.TrimUnPrintable() == name)
                {
                    return new SharpDX.DirectInput.Joystick(directInput, device.InstanceGuid);
                }
            }

            return null;
        }

        public SharpDX.DirectInput.Joystick AcquireJoystick(string name)
        {
            joystick = getJoyStickByName(name);

            if (joystick == null)
                return null;

            joystick.Acquire();

            joystick.Poll();

            return joystick;
        }

        public bool start(string name)
        {
            this.name = name;

            joystick = AcquireJoystick(name);

            if (joystick == null)
                return false;

            enabled = true;

            System.Threading.Thread t11 = new System.Threading.Thread(new System.Threading.ThreadStart(mainloop))
            {
                Name = "Joystick loop",
                Priority = System.Threading.ThreadPriority.AboveNormal,
                IsBackground = true
            };
            t11.Start();

            return true;
        }

        public static joystickaxis getMovingAxis(string name, int threshold)
        {
            var joystick = new Joystick().AcquireJoystick(name);

            if (joystick == null)
                return joystickaxis.ARx;

            joystick.Poll();

            System.Threading.Thread.Sleep(300);

            joystick.Poll();

            var obj = joystick.CurrentJoystickState();
            Hashtable values = new Hashtable();

            // get the state of the joystick before.
            Type type = obj.GetType();
            PropertyInfo[] properties = type.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                values[property.Name] = int.Parse(property.GetValue(obj, null).ToString());
            }
            values["Slider1"] = obj.GetSlider()[0];
            values["Slider2"] = obj.GetSlider()[1];
            values["Hatud1"] = obj.GetPointOfView()[0];
            values["Hatlr2"] = obj.GetPointOfView()[0];
            values["Custom1"] = 0;
            values["Custom2"] = 0;

            MessageBox.Show("Please move the joystick axis you want assigned to this function after clicking ok");

            DateTime start = DateTime.Now;

            while (start.AddSeconds(10) > DateTime.Now)
            {
                joystick.Poll();
                System.Threading.Thread.Sleep(50);
                var nextstate = joystick.CurrentJoystickState();

                int[] slider = nextstate.GetSlider();

                int[] hat1 = nextstate.GetPointOfView();

                type = nextstate.GetType();
                properties = type.GetProperties();
                foreach (PropertyInfo property in properties)
                {
                    //Console.WriteLine("Name: " + property.Name + ", Value: " + property.GetValue(obj, null));

                    log.InfoFormat("test name {0} old {1} new {2} ", property.Name, values[property.Name],
                        int.Parse(property.GetValue(nextstate, null).ToString()));
                    log.InfoFormat("{0}  {1} {2}", property.Name, (int)values[property.Name],
                        (int.Parse(property.GetValue(nextstate, null).ToString()) + threshold));
                    if ((int)values[property.Name] >
                        (int.Parse(property.GetValue(nextstate, null).ToString()) + threshold) ||
                        (int)values[property.Name] <
                        (int.Parse(property.GetValue(nextstate, null).ToString()) - threshold))
                    {
                        log.Info(property.Name);
                        joystick.Unacquire();
                        return (joystickaxis)Enum.Parse(typeof(joystickaxis), property.Name);
                    }
                }

                // slider1
                if ((int)values["Slider1"] > (slider[0] + threshold) ||
                    (int)values["Slider1"] < (slider[0] - threshold))
                {
                    joystick.Unacquire();
                    return joystickaxis.Slider1;
                }

                // slider2
                if ((int)values["Slider2"] > (slider[1] + threshold) ||
                    (int)values["Slider2"] < (slider[1] - threshold))
                {
                    joystick.Unacquire();
                    return joystickaxis.Slider2;
                }

                // Hatud1
                if ((int)values["Hatud1"] != (hat1[0]))
                {
                    joystick.Unacquire();
                    return joystickaxis.Hatud1;
                }

                // Hatlr2
                if ((int)values["Hatlr2"] != (hat1[0]))
                {
                    joystick.Unacquire();
                    return joystickaxis.Hatlr2;
                }
            }

            MessageBox.Show("No valid option was detected");

            return joystickaxis.None;
        }

        public static int getPressedButton(string name)
        {
            var joystick = getJoyStickByName(name);

            if (joystick == null)
                return -1;

            //joystick.SetDataFormat(DeviceDataFormat.Joystick);

            joystick.Acquire();

            System.Threading.Thread.Sleep(500);

            joystick.Poll();

            var obj = joystick.CurrentJoystickState();

            var buttonsbefore = obj.GetButtons();

            MessageBox.Show(
                "Please press the joystick button you want assigned to this function after clicking ok");

            DateTime start = DateTime.Now;

            while (start.AddSeconds(10) > DateTime.Now)
            {
                joystick.Poll();
                var nextstate = joystick.CurrentJoystickState();

                var buttons = nextstate.GetButtons();

                for (int a = 0; a < joystick.Capabilities.ButtonCount; a++)
                {
                    if (buttons[a] != buttonsbefore[a])
                        return a;
                }
            }

            MessageBox.Show("No valid option was detected");

            return -1;
        }

        public void setReverse(int channel, bool reverse)
        {
            JoyChannels[channel].reverse = reverse;
        }

        public void setAxis(int channel, joystickaxis axis)
        {
            JoyChannels[channel].axis = axis;
        }

        public void setChannel(int channel, joystickaxis axis, bool reverse, int expo)
        {
            JoyChannel joy = new JoyChannel();
            joy.axis = axis;
            joy.channel = channel;
            joy.expo = expo;
            joy.reverse = reverse;

            JoyChannels[channel] = joy;
        }

        public void setChannel(JoyChannel chan)
        {
            JoyChannels[chan.channel] = chan;
        }

        public JoyChannel getChannel(int channel)
        {
            return JoyChannels[channel];
        }

        public void setButton(int arrayoffset, JoyButton buttonconfig)
        {
            JoyButtons[arrayoffset] = buttonconfig;
        }

        public JoyButton getButton(int arrayoffset)
        {
            return JoyButtons[arrayoffset];
        }

        public void changeButton(int buttonid, int newid)
        {
            JoyButtons[buttonid].buttonno = newid;
        }

        public int getHatSwitchDirection()
        {
            return (state.GetPointOfView())[0];
        }

        public int getNumberPOV()
        {
            return joystick.Capabilities.PovCount;
        }

        int BOOL_TO_SIGN(bool input)
        {
            if (input == true)
            {
                return -1;
            }
            else
            {
                return 1;
            }
        }

        public event EventHandler<JoystickData> JoyStickDataReceived;
        /// <summary>
        /// Updates the rcoverride values and controls the mode changes
        /// </summary>
        void mainloop()
        {
            bool saveState = false;
            while (enabled && joystick != null && !joystick.IsDisposed)
            {
                JoystickData jdata = new JoystickData();
                short hat = 0;
                try
                {
                    System.Threading.Thread.Sleep(50);
                    if (joystick.IsDisposed)
                        return;
                    //joystick stuff
                    joystick.Poll();
                    state = joystick.CurrentJoystickState();
                    if (isButtonPressed(0))
                    {
                        saveState = !saveState;
                    }

                    //Console.WriteLine(state);
                    
                    if (getNumberPOV() > 0)
                    {
                        int pov = getHatSwitchDirection();

                        if (pov != -1)
                        {
                            int angle = pov / 100;

                            //0 = down = 18000
                            //0 = up = 0

                            // 0
                            if (angle > 270 || angle < 90)
                                hat = 180;
                            // 180
                            if (angle > 90 && angle < 270)
                                hat = -180;
                            //// 90
                            //if (angle > 0 && angle < 180)
                            //    hat2 += 500;
                            //// 270
                            //if (angle > 180 && angle < 360)
                            //    hat2 -= 500;
                        }
                    }

                    //if (elevons)
                    //{
                    //    //g.channel_roll.set_pwm(BOOL_TO_SIGN(g.reverse_elevons) * (BOOL_TO_SIGN(g.reverse_ch2_elevon) * int(ch2_temp - elevon2_trim) - BOOL_TO_SIGN(g.reverse_ch1_elevon) * int(ch1_temp - elevon1_trim)) / 2 + 1500);
                    //    //g.channel_pitch.set_pwm(                                 (BOOL_TO_SIGN(g.reverse_ch2_elevon) * int(ch2_temp - elevon2_trim) + BOOL_TO_SIGN(g.reverse_ch1_elevon) * int(ch1_temp - elevon1_trim)) / 2 + 1500);
                    //    short roll = pickchannel(1, JoyChannels[1].axis, false, JoyChannels[1].expo);
                    //    short pitch = pickchannel(2, JoyChannels[2].axis, false, JoyChannels[2].expo);

                    //    if (getJoystickAxis(1) != Joystick.joystickaxis.None)
                    //        jdata.channel1 =
                    //            (short)
                    //                (BOOL_TO_SIGN(JoyChannels[1].reverse) * ((int)(pitch - 1500) - (int)(roll - 1500)) / 2 +
                    //                 1500);
                    //    if (getJoystickAxis(2) != Joystick.joystickaxis.None)
                    //        jdata.channel2 =
                    //            (short)
                    //                (BOOL_TO_SIGN(JoyChannels[2].reverse) * ((int)(pitch - 1500) + (int)(roll - 1500)) / 2 +
                    //                 1500);
                    //}
                    //else
                    //{
                        if (getJoystickAxis(1) != Joystick.joystickaxis.None)
                            jdata.channel1 = pickchannel(1, JoyChannels[1].axis,
                                JoyChannels[1].reverse, JoyChannels[1].expo);
                        //(ushort)(((int)state.Rz / 65.535) + 1000);
                        if (getJoystickAxis(2) != Joystick.joystickaxis.None)
                            jdata.channel2 = pickchannel(2, JoyChannels[2].axis,
                                JoyChannels[2].reverse, JoyChannels[2].expo);
                        //(ushort)(((int)state.Y / 65.535) + 1000);
                    //}
                    if (getJoystickAxis(3) != Joystick.joystickaxis.None)
                        jdata.channel3 = pickchannel(3, JoyChannels[3].axis, JoyChannels[3].reverse,
                            JoyChannels[3].expo); //(ushort)(1000 - ((int)slider[0] / 65.535) + 1000);
                    if (getJoystickAxis(4) != Joystick.joystickaxis.None)
                        jdata.channel4 = pickchannel(4, JoyChannels[4].axis, JoyChannels[4].reverse,
                            JoyChannels[4].expo); //(ushort)(((int)state.X / 65.535) + 1000);

                    if (getJoystickAxis(5) != Joystick.joystickaxis.None)
                        jdata.channel5 = pickchannel(5, JoyChannels[5].axis, JoyChannels[5].reverse,
                            JoyChannels[5].expo);
                    if (getJoystickAxis(6) != Joystick.joystickaxis.None)
                        jdata.channel6 = pickchannel(6, JoyChannels[6].axis, JoyChannels[6].reverse,
                            JoyChannels[6].expo);
                    if (getJoystickAxis(7) != Joystick.joystickaxis.None)
                        jdata.channel7 = pickchannel(7, JoyChannels[7].axis, JoyChannels[7].reverse,
                            JoyChannels[7].expo);
                    if (getJoystickAxis(8) != Joystick.joystickaxis.None)
                        jdata.channel8 = pickchannel(8, JoyChannels[8].axis, JoyChannels[8].reverse,
                            JoyChannels[8].expo);

                    if (getJoystickAxis(9) != Joystick.joystickaxis.None) jdata.channel9 = pickchannel(9, JoyChannels[9].axis, JoyChannels[9].reverse, JoyChannels[9].expo);
                    if (getJoystickAxis(10) != Joystick.joystickaxis.None) jdata.channel10 = pickchannel(10, JoyChannels[10].axis, JoyChannels[10].reverse, JoyChannels[10].expo);
                    if (getJoystickAxis(11) != Joystick.joystickaxis.None) jdata.channel11 = pickchannel(11, JoyChannels[11].axis, JoyChannels[11].reverse, JoyChannels[11].expo);
                    if (getJoystickAxis(12) != Joystick.joystickaxis.None) jdata.channel12 = pickchannel(12, JoyChannels[12].axis, JoyChannels[12].reverse, JoyChannels[12].expo);
                    if (getJoystickAxis(13) != Joystick.joystickaxis.None) jdata.channel13 = pickchannel(13, JoyChannels[13].axis, JoyChannels[13].reverse, JoyChannels[13].expo);
                    if (getJoystickAxis(14) != Joystick.joystickaxis.None) jdata.channel14 = pickchannel(14, JoyChannels[14].axis, JoyChannels[14].reverse, JoyChannels[14].expo);
                    if (getJoystickAxis(15) != Joystick.joystickaxis.None) jdata.channel15 = pickchannel(15, JoyChannels[15].axis, JoyChannels[15].reverse, JoyChannels[15].expo);
                    if (getJoystickAxis(16) != Joystick.joystickaxis.None) jdata.channel16 = pickchannel(16, JoyChannels[16].axis, JoyChannels[16].reverse, JoyChannels[16].expo);
                    if (getJoystickAxis(17) != Joystick.joystickaxis.None) jdata.channel17 = pickchannel(17, JoyChannels[17].axis, JoyChannels[17].reverse, JoyChannels[17].expo);
                    if (getJoystickAxis(18) != Joystick.joystickaxis.None) jdata.channel18 = pickchannel(18, JoyChannels[18].axis, JoyChannels[18].reverse, JoyChannels[18].expo);

                    jdata.hat = hat;
                    jdata.buttonGripPressed = isButtonPressed(5);
                    jdata.buttonElbowPressed = isButtonPressed(3);
                    jdata.buttonSholderPressed = isButtonPressed(2);
                    jdata.buttonWristPressed = isButtonPressed(4);
                    if (
                        ((jdata.buttonElbowPressed && Math.Abs(jdata.channel1) > Math.Abs(previous?.channel1 ?? 0)) 
                        || (jdata.buttonElbowPressed && Math.Abs(jdata.channel1) > Math.Abs(previous?.channel1 ?? 0))
                        || (jdata.buttonSholderPressed && Math.Abs(jdata.channel1) > Math.Abs(previous?.channel1 ?? 0)) 
                        || (jdata.buttonWristPressed && Math.Abs(jdata.channel1) > Math.Abs(previous?.channel1 ?? 0)))
                        && saveState)
                        saveState = false;
                    jdata.saveState = saveState;
                    //Console.WriteLine(jdata.ToString());
                    //DoJoystickButtonFunction();
                    //if (!saveState)
                    //{
                        JoyStickDataReceived?.Invoke(this, jdata);
                        previous = jdata;
                    //}
                    //Console.WriteLine("{0} {1} {2} {3}", jdata.channel1, jdata.channel2, jdata.channel3, jdata.channel4);
                }
                catch (SharpDX.SharpDXException ex)
                {
                    log.Error(ex);
                    _context.Send(
                        delegate { MessageBox.Show("Lost Joystick", "Lost Joystick"); }, null);
                    return;
                }
                catch (Exception ex)
                {
                    log.Info("Joystick thread error " + ex.ToString());
                } // so we cant fall out
            }
        }

        public void DoJoystickButtonFunction()
        {
            foreach (JoyButton but in JoyButtons)
            {
                if (but.buttonno != -1)
                {
                    getButtonState(but, but.buttonno);
                }
            }
        }

        void ProcessButtonEvent(JoyButton but, bool buttondown)
        {
            if (but.buttonno != -1)
            {
                //// only do_set_relay and Button_axis0-1 uses the button up option
                //if (buttondown == false)
                //{
                //    if (but.function != buttonfunction.Do_Set_Relay &&
                //        but.function != buttonfunction.Button_axis0 &&
                //        but.function != buttonfunction.Button_axis1)
                //    {
                //        return;
                //    }
                //}

                //switch (but.function)
                //{
                //    case buttonfunction.ChangeMode:
                //        string mode = but.mode;
                //        if (mode != null)
                //        {
                //            _context.Send(delegate
                //            {
                //                try
                //                {
                //                    Interface.setMode(mode);
                //                }
                //                catch
                //                {
                //                    MessageBox.Show("Failed to change Modes");
                //                }
                //            }, null);
                //        }
                //        break;
                //    case buttonfunction.Mount_Mode:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                Interface.setParam("MNT_MODE", but.p1);
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to change mount mode");
                //            }
                //        }, null);

                //        break;

                //    case buttonfunction.Arm:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                Interface.doARM(true);
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to Arm");
                //            }
                //        }, null);
                //        break;
                //    case buttonfunction.TakeOff:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                Interface.setMode("Guided");
                //                if (Interface.MAV.cs.firmware == Firmwares.ArduCopter2)
                //                {
                //                    Interface.doCommand(MAVLink.MAV_CMD.TAKEOFF, 0, 0, 0, 0, 0, 0, 2);
                //                }
                //                else
                //                {
                //                    Interface.doCommand(MAVLink.MAV_CMD.TAKEOFF, 0, 0, 0, 0, 0, 0, 20);
                //                }
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to takeoff");
                //            }
                //        }, null);
                //        break;
                //    case buttonfunction.Disarm:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                Interface.doARM(false);
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to Disarm");
                //            }
                //        }, null);
                //        break;
                //    case buttonfunction.Do_Set_Relay:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                int number = (int)but.p1;
                //                int state = buttondown == true ? 1 : 0;
                //                Interface.doCommand(MAVLink.MAV_CMD.DO_SET_RELAY, number, state, 0, 0, 0, 0, 0);
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to DO_SET_RELAY");
                //            }
                //        }, null);
                //        break;
                //    case buttonfunction.Digicam_Control:
                //        Interface.setDigicamControl(true);
                //        break;
                //    case buttonfunction.Do_Repeat_Relay:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                int relaynumber = (int)but.p1;
                //                int repeat = (int)but.p2;
                //                int time = (int)but.p3;
                //                Interface.doCommand(MAVLink.MAV_CMD.DO_REPEAT_RELAY, relaynumber, repeat, time, 0,
                //                    0, 0, 0);
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to DO_REPEAT_RELAY");
                //            }
                //        }, null);
                //        break;
                //    case buttonfunction.Do_Set_Servo:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                int channel = (int)but.p1;
                //                int pwm = (int)but.p2;
                //                Interface.doCommand(MAVLink.MAV_CMD.DO_SET_SERVO, channel, pwm, 0, 0, 0, 0, 0);
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to DO_SET_SERVO");
                //            }
                //        }, null);
                //        break;
                //    case buttonfunction.Do_Repeat_Servo:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                int channelno = (int)but.p1;
                //                int pwmvalue = (int)but.p2;
                //                int repeattime = (int)but.p3;
                //                int delay_ms = (int)but.p4;
                //                Interface.doCommand(MAVLink.MAV_CMD.DO_REPEAT_SERVO, channelno, pwmvalue,
                //                    repeattime, delay_ms, 0, 0, 0);
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to DO_REPEAT_SERVO");
                //            }
                //        }, null);
                //        break;
                //    case buttonfunction.Toggle_Pan_Stab:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                float current = (float)Interface.MAV.param["MNT_STAB_PAN"];
                //                float newvalue = (current > 0) ? 0 : 1;
                //                Interface.setParam("MNT_STAB_PAN", newvalue);
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to Toggle_Pan_Stab");
                //            }
                //        }, null);
                //        break;
                //    case buttonfunction.Gimbal_pnt_track:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                Interface.doCommand(MAVLink.MAV_CMD.DO_SET_ROI, 0, 0, 0, 0,
                //                    Interface.MAV.cs.gimballat, Interface.MAV.cs.gimballng,
                //                    (float)Interface.MAV.cs.GimbalPoint.Alt);
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to Gimbal_pnt_track");
                //            }
                //        }, null);
                //        break;
                //    case buttonfunction.Mount_Control_0:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                Interface.setMountControl(0, 0, 0, false);
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to Mount_Control_0");
                //            }
                //        }, null);
                //        break;
                //    case buttonfunction.Button_axis0:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                int pwmmin = (int)but.p1;
                //                int pwmmax = (int)but.p2;

                //                if (buttondown)
                //                    custom0 = pwmmax;
                //                else
                //                    custom0 = pwmmin;
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to Button_axis0");
                //            }
                //        }, null);
                //        break;
                //    case buttonfunction.Button_axis1:
                //        _context.Send(delegate
                //        {
                //            try
                //            {
                //                int pwmmin = (int)but.p1;
                //                int pwmmax = (int)but.p2;

                //                if (buttondown)
                //                    custom1 = pwmmax;
                //                else
                //                    custom1 = pwmmin;
                //            }
                //            catch
                //            {
                //                MessageBox.Show("Failed to Button_axis1");
                //            }
                //        }, null);
                //        break;
                //}
            }
        }

        public enum joystickaxis
        {
            None,
            Pass,
            ARx,
            ARy,
            ARz,
            AX,
            AY,
            AZ,
            FRx,
            FRy,
            FRz,
            FX,
            FY,
            FZ,
            Rx,
            Ry,
            Rz,
            VRx,
            VRy,
            VRz,
            VX,
            VY,
            VZ,
            X,
            Y,
            Z,
            Slider1,
            Slider2,
            Hatud1,
            Hatlr2,
            Custom1,
            Custom2
        }

        const int RESXu = 1024;
        const int RESXul = 1024;
        const int RESXl = 1024;
        const int RESKul = 100;
        /*

        ushort expou(ushort x, ushort k)
        {
          // k*x*x*x + (1-k)*x
          return ((ulong)x*x*x/0x10000*k/(RESXul*RESXul/0x10000) + (RESKul-k)*x+RESKul/2)/RESKul;
        }
        // expo-funktion:
        // ---------------
        // kmplot
        // f(x,k)=exp(ln(x)*k/10) ;P[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20]
        // f(x,k)=x*x*x*k/10 + x*(1-k/10) ;P[0,1,2,3,4,5,6,7,8,9,10]
        // f(x,k)=x*x*k/10 + x*(1-k/10) ;P[0,1,2,3,4,5,6,7,8,9,10]
        // f(x,k)=1+(x-1)*(x-1)*(x-1)*k/10 + (x-1)*(1-k/10) ;P[0,1,2,3,4,5,6,7,8,9,10]

        short expo(short x, short k)
        {
            if (k == 0) return x;
            short y;
            bool neg = x < 0;
            if (neg) x = -x;
            if (k < 0)
            {
                y = RESXu - expou((ushort)(RESXu - x), (ushort)-k);
            }
            else
            {
                y = expou((ushort)x, (ushort)k);
            }
            return neg ? -y : y;
        }

        */



        public void UnAcquireJoyStick()
        {
            if (joystick == null)
                return;
            joystick.Unacquire();
        }

        /// <summary>
        /// Button press check with debounce
        /// </summary>
        /// <param name="buttonno"></param>
        /// <returns></returns>
        bool getButtonState(JoyButton but, int buttonno)
        {
            var buts = state.GetButtons();

            // button down
            bool ans = buts[buttonno] && !buttonpressed[buttonno]; // press check + debounce
            if (ans)
                ButtonDown(but);

            // button up
            ans = !buts[buttonno] && buttonpressed[buttonno];
            if (ans)
                ButtonUp(but);

            buttonpressed[buttonno] = buts[buttonno]; // set only this button

            return ans;
        }

        void ButtonDown(JoyButton but)
        {
            ProcessButtonEvent(but, true);
        }

        void ButtonUp(JoyButton but)
        {
            ProcessButtonEvent(but, false);
        }

        public int getNumButtons()
        {
            if (joystick == null)
                return 0;
            return joystick.Capabilities.ButtonCount;
        }

        public joystickaxis getJoystickAxis(int channel)
        {
            try
            {
                return JoyChannels[channel].axis;
            }
            catch
            {
                return joystickaxis.None;
            }
        }

        public bool isButtonPressed(int buttonno)
        {
            if (state == null)
                return false;

            var buts = state.GetButtons();

            if (buts == null || JoyButtons[buttonno].buttonno < 0)
                return false;

            return buts[JoyButtons[buttonno].buttonno];
        }

        public short getValueForChannel(int channel, string name)
        {
            if (joystick == null)
                return 0;

            joystick.Poll();

            state = joystick.CurrentJoystickState();

            short ans = pickchannel(channel, JoyChannels[channel].axis, JoyChannels[channel].reverse,
                JoyChannels[channel].expo);
            log.DebugFormat("{0} = {1} = {2}", channel, ans, state.X);
            return ans;
        }

        public short getRawValueForChannel(int channel)
        {
            if (joystick == null)
                return 0;

            joystick.Poll();

            state = joystick.CurrentJoystickState();

            short ans = pickchannel(channel, JoyChannels[channel].axis, false, 0);
            log.DebugFormat("{0} = {1} = {2}", channel, ans, state.X);
            return ans;
        }

        short pickchannel(int chan, joystickaxis axis, bool rev, int expo)
        {
            int min, max, trim = 0;

            //if (Interface.MAV.param.Count > 0)
            //{
            //    try
            //    {
            //        if (Interface.MAV.param.ContainsKey("RC" + chan + "_MIN"))
            //        {
            //            min = (int)(float)(Interface.MAV.param["RC" + chan + "_MIN"]);
            //            max = (int)(float)(Interface.MAV.param["RC" + chan + "_MAX"]);
            //            trim = (int)(float)(Interface.MAV.param["RC" + chan + "_TRIM"]);
            //        }
            //        else
            //        {
            //            min = 1000;
            //            max = 2000;
            //            trim = 1500;
            //        }
            //    }
            //    catch
            //    {
            //        min = 1000;
            //        max = 2000;
            //        trim = 1500;
            //    }
            //}
            //else
            //{
            //    min = 1000;
            //    max = 2000;
            //    trim = 1500;
            //}

            //if (manual_control)
            //{
            //    min = -1000;
            //    max = 1000;
            //    trim = 0;
            //}

            min = -180;
            max = 180;
            trim = 0;

            if (chan == 3)
            {
                trim = (min + max) / 2;
                //                trim = min; // throttle
            }

            int range = Math.Abs(max - min);

            int working = 0;

            switch (axis)
            {
                case joystickaxis.None:
                    working = ushort.MaxValue / 2;
                    break;
                case joystickaxis.Pass:
                    working = (int)(((float)(trim - min) / range) * ushort.MaxValue);
                    break;
                case joystickaxis.ARx:
                    working = state.ARx;
                    break;

                case joystickaxis.ARy:
                    working = state.ARy;
                    break;

                case joystickaxis.ARz:
                    working = state.ARz;
                    break;

                case joystickaxis.AX:
                    working = state.AX;
                    break;

                case joystickaxis.AY:
                    working = state.AY;
                    break;

                case joystickaxis.AZ:
                    working = state.AZ;
                    break;

                case joystickaxis.FRx:
                    working = state.FRx;
                    break;

                case joystickaxis.FRy:
                    working = state.FRy;
                    break;

                case joystickaxis.FRz:
                    working = state.FRz;
                    break;

                case joystickaxis.FX:
                    working = state.FX;
                    break;

                case joystickaxis.FY:
                    working = state.FY;
                    break;

                case joystickaxis.FZ:
                    working = state.FZ;
                    break;

                case joystickaxis.Rx:
                    working = state.Rx;
                    break;

                case joystickaxis.Ry:
                    working = state.Ry;
                    break;

                case joystickaxis.Rz:
                    working = state.Rz;
                    break;

                case joystickaxis.VRx:
                    working = state.VRx;
                    break;

                case joystickaxis.VRy:
                    working = state.VRy;
                    break;

                case joystickaxis.VRz:
                    working = state.VRz;
                    break;

                case joystickaxis.VX:
                    working = state.VX;
                    break;

                case joystickaxis.VY:
                    working = state.VY;
                    break;

                case joystickaxis.VZ:
                    working = state.VZ;
                    break;

                case joystickaxis.X:
                    working = state.X;
                    break;

                case joystickaxis.Y:
                    working = state.Y;
                    break;

                case joystickaxis.Z:
                    working = state.Z;
                    break;

                case joystickaxis.Slider1:
                    int[] slider = state.GetSlider();
                    working = slider[0];
                    break;

                case joystickaxis.Slider2:
                    int[] slider1 = state.GetSlider();
                    working = slider1[1];
                    break;

                case joystickaxis.Hatud1:
                    hat1 = (int)Constrain(hat1, 0, 65535);
                    working = hat1;
                    break;

                case joystickaxis.Hatlr2:
                    hat2 = (int)Constrain(hat2, 0, 65535);
                    working = hat2;
                    break;

                case joystickaxis.Custom1:
                    working = (int)(((float)(custom0 - min) / range) * ushort.MaxValue);
                    working = (int)Constrain(working, 0, 65535);
                    break;

                case joystickaxis.Custom2:
                    working = (int)(((float)(custom1 - min) / range) * ushort.MaxValue);
                    working = (int)Constrain(working, 0, 65535);
                    break;
            }
            // between 0 and 65535 - convert to int -500 to 500
            working = (int)map(working, 0, 65535, -500, 500);

            if (rev)
                working *= -1;

            // save for later
            int raw = working;

            working = (int)Expo(working, expo, min, max, trim);

            //add limits to movement
            working = Math.Max(min, working);
            working = Math.Min(max, working);

            return (short)working;
        }

        public static double Expo(double input, double expo, double min, double max, double mid)
        {
            // input range -500 to 500

            double expomult = expo / 100.0;

            if (input >= 0)
            {
                // linear scale
                double linearpwm = map(input, 0, 500, mid, max);

                double expomid = (max - mid) / 2;

                double factor = 0;

                // over half way though input
                if (input > 250)
                {
                    factor = 250 - (input - 250);
                }
                else
                {
                    factor = input;
                }

                return linearpwm - (factor * expomult);
            }
            else
            {
                double linearpwm = map(input, -500, 0, min, mid);

                double expomid = (mid - min) / 2;

                double factor = 0;

                // over half way though input
                if (input < -250)
                {
                    factor = -250 - (input + 250);
                }
                else
                {
                    factor = input;
                }

                return linearpwm - (factor * expomult);
            }
        }

        static double map(double x, double in_min, double in_max, double out_min, double out_max)
        {
            return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
        }

        double Constrain(double value, double min, double max)
        {
            if (value > max)
                return max;
            if (value < min)
                return min;
            return value;
        }
    }

    public class MyJoystickState
    {
        internal JoystickState baseJoystickState;

        public MyJoystickState(JoystickState state)
        {
            baseJoystickState = state;
        }

        public int[] GetSlider()
        {
            return baseJoystickState.Sliders;
        }

        public int[] GetPointOfView()
        {
            return baseJoystickState.PointOfViewControllers;
        }

        public bool[] GetButtons()
        {
            return baseJoystickState.Buttons;
        }

        public int AZ
        {
            get { return baseJoystickState.AccelerationZ; }
        }

        public int AY
        {
            get { return baseJoystickState.AccelerationY; }
        }

        public int AX
        {
            get { return baseJoystickState.AccelerationX; }
        }

        public int ARz
        {
            get { return baseJoystickState.AngularAccelerationZ; }
        }

        public int ARy
        {
            get { return baseJoystickState.AngularAccelerationY; }
        }

        public int ARx
        {
            get { return baseJoystickState.AngularAccelerationX; }
        }

        public int FRx
        {
            get { return baseJoystickState.TorqueX; }
        }

        public int FRy
        {
            get { return baseJoystickState.TorqueY; }
        }

        public int FRz
        {
            get { return baseJoystickState.TorqueZ; }
        }

        public int FX
        {
            get { return baseJoystickState.ForceX; }
        }

        public int FY
        {
            get { return baseJoystickState.ForceY; }
        }

        public int FZ
        {
            get { return baseJoystickState.ForceZ; }
        }

        public int Rx
        {
            get { return baseJoystickState.RotationX; }
        }

        public int Ry
        {
            get { return baseJoystickState.RotationY; }
        }

        public int Rz
        {
            get { return baseJoystickState.RotationZ; }
        }

        public int VRx
        {
            get { return baseJoystickState.AngularVelocityX; }
        }

        public int VRy
        {
            get { return baseJoystickState.AngularVelocityY; }
        }

        public int VRz
        {
            get { return baseJoystickState.AngularVelocityZ; }
        }

        public int VX
        {
            get { return baseJoystickState.VelocityX; }
        }

        public int VY
        {
            get { return baseJoystickState.VelocityY; }
        }

        public int VZ
        {
            get { return baseJoystickState.VelocityZ; }
        }

        public int X
        {
            get { return baseJoystickState.X; }
        }

        public int Y
        {
            get { return baseJoystickState.Y; }
        }

        public int Z
        {
            get { return baseJoystickState.Z; }
        }
    }

    public static class Extensions
    {
        public static string TrimUnPrintable(this string input)
        {
            return Regex.Replace(input, @"[^\u0020-\u007E]", String.Empty);
        }

        public static MyJoystickState CurrentJoystickState(this SharpDX.DirectInput.Joystick joystick)
        {
            return new MyJoystickState(joystick.GetCurrentState());
        }
    }

    public class JoystickData : EventArgs
    {
        internal short channel1;
        internal short channel2;
        internal short channel3;
        internal short channel4;
        internal short channel5;
        internal short channel6;
        internal short channel7;
        internal short channel8;
        internal short channel9;
        internal short channel10;
        internal short channel11;
        internal short channel12;
        internal short channel13;
        internal short channel14;
        internal short channel15;
        internal short channel16;
        internal short channel17;
        internal short channel18;
        internal bool buttonGripPressed;
        internal bool buttonElbowPressed;
        internal bool buttonSholderPressed;
        internal bool buttonWristPressed;
        internal bool saveState;
        internal short hat;

        public override string ToString()
        {
            return
            $"Channel1: {channel1} |" +
            $"Channel2: {channel2} |" +
            $"Channel3: {channel3} |" +
            $"Channel4: {channel4} |" +
            $"Channel5: {channel5} |" +
            $"Channel6: {channel6} |" +
            $"Channel7: {channel7} |" +
            $"Channel8: {channel8} |" +
            $"Channel9: {channel9} |" +
            $"Channel10: {channel10} |" +
            $"Channel11: {channel11} |" +
            $"Channel12: {channel12} |" +
            $"Channel13: {channel13} |" +
            $"Channel14: {channel14} |" +
            $"Channel15: {channel15} |" +
            $"Channel16: {channel16} |" +
            $"Channel17: {channel17} |" +
            $"Channel18: {channel18}"; 
        }
    }
}
