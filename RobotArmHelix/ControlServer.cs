using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RobotArmHelix
{
    public class ControlServer
    {

        object socketLock = new object();
        public int PortNo { get; set; }

        public bool IsServerRunning
        {
            get { lock (socketLock) { return listener != null && listener.IsListening; } }
        }

        void StopServer()
        {
            lock (socketLock)
            {
                if (listener != null)
                {
                    listener.Stop();
                    listener = null;
                }
                PortNo = 0;
            }
        }

        HttpListener listener;
        public event EventHandler<JoystickData> JoyStickDataReceived;
        void StartServer()
        {
            lock (socketLock)
            {
                if (listener == null)
                {
                    try
                    {
                        PortNo = 8090;
                        var http = new HttpListener();
                        var url = $"http://localhost:{PortNo}/";
                        Debug.WriteLine("Trying to start HTTP listener on " + url);
                        http.Prefixes.Add(url);
                        http.Start();
                        listener = http;
                        Debug.WriteLine("Started HTTP listener on port " + PortNo);
                        Task.Run(() =>
                        {
                            try
                            {
                                while (http.IsListening)
                                {
                                    var incoming = http.GetContext();
                                    try
                                    {
                                        JoystickData jData = new JoystickData();
                                        var queryParam = incoming.Request.QueryString;
                                        if(queryParam.Count == 0 && incoming.Request.RawUrl.Trim('/').Equals("index.html"))
                                        {
                                            var response = incoming.Response;
                                            byte[] buffer = File.ReadAllBytes(@"index.html");

                                            response.ContentLength64 = buffer.Length;
                                            Stream st = response.OutputStream;
                                            st.Write(buffer, 0, buffer.Length);
                                        }
                                        else
                                        {

                                            foreach(var param in queryParam.AllKeys)
                                            {
                                                short val = short.Parse(queryParam[param]);
                                                switch (param)
                                                {
                                                    case "grip":
                                                        jData.buttonGripPressed = true;
                                                        jData.channel1 = val;
                                                        break;
                                                    case "arm":
                                                        jData.hat = val;
                                                        break;
                                                    case "elbow":
                                                        jData.buttonElbowPressed = true;
                                                        jData.channel1 = val;
                                                        break;
                                                    case "shoulder":
                                                        jData.buttonSholderPressed = true;
                                                        jData.channel1 = val;
                                                        break;
                                                    case "base":
                                                        jData.channel2 = val;
                                                        break;
                                                }
                                                JoyStickDataReceived?.Invoke(this, jData);
                                            }
                                        }
                                        incoming.Response.StatusCode = 200;
                                        incoming.Response.Close();
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.WriteLine("Error processing request ", e);
                                        incoming.Response.StatusCode = 500;
                                        incoming.Response.Close();
                                    }
                                }
                            }
                            catch (Exception) { }
                            Debug.WriteLine("HttpListener task completed");
                        });

                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error starting Notebook Server ", ex);
                        StopServer();
                    }
                }
            }
        }

        public ControlServer()
        {
            StartServer();
        }
    }
}
