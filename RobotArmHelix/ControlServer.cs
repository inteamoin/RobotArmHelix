using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
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
                                        if(queryParam.Count == 0)
                                        {
                                            var res = incoming.Request.RawUrl.Trim('/');
                                            var response = incoming.Response;
                                            switch(res)
                                            {
                                                case "index.html":
                                                    byte[] buffer = File.ReadAllBytes(@"index.html");

                                                    response.ContentLength64 = buffer.Length;
                                                    Stream st = response.OutputStream;
                                                    st.Write(buffer, 0, buffer.Length);
                                                    break;
                                                case "feedbackbase":
                                                    SendResponse(response, GetRandomJsonFeedback().BaseAngle);
                                                    break;
                                                case "feedbackshoulder":
                                                    SendResponse(response, GetRandomJsonFeedback().ShoulderAngle);
                                                    break;
                                                case "feedbackelbow":
                                                    SendResponse(response, GetRandomJsonFeedback().ElbowAngle);
                                                    break;
                                                case "feedbackwrist":
                                                    SendResponse(response, GetRandomJsonFeedback().WristAngle);
                                                    break;
                                                case "feedbackwriyaw":
                                                    SendResponse(response, GetRandomJsonFeedback().WristYawAngle);
                                                    break;
                                                case "feedbackgrip":
                                                    SendResponse(response, GetRandomJsonFeedback().GripAngle);
                                                    break;
                                                case "weightread":
                                                    SendResponse(response, GetRandomJsonFeedback().Weight);
                                                    break;
                                                case "pressureread":
                                                    SendResponse(response, GetRandomJsonFeedback().Force);
                                                    break;
                                                case "tempread":
                                                    SendResponse(response, GetRandomJsonFeedback().Temperature);
                                                    break;
                                            }
                                        }
                                        else
                                        {
                                            //Thread.Sleep(3000);
                                            foreach(var param in queryParam.AllKeys)
                                            {
                                                if (param == null) continue;
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

        private void SendResponse(HttpListenerResponse response, double feedbackValue)
        {
            response.ContentType = "Application/json";
            byte[] jsonBuffer = Encoding.ASCII.GetBytes(Convert.ToString(feedbackValue));
            response.ContentLength64 = jsonBuffer.Length;
            response.OutputStream.Write(jsonBuffer, 0, jsonBuffer.Length);
        }

        private Random random = new Random(); 
        //private Random forceRandom = new Random(50);
        //private Random weightRandom = new Random(10);
        //private Random fumesRandom = new Random(0);
        //private Random temperatureRandom = new Random(30);
        //private Random batteryRandom = new Random(12);
        

        private Feedback GetRandomJsonFeedback()
        {
            var feedback = new Feedback(
                random.Next(50,100),
                random.Next(20),
                random.Next(0,1),
                random.Next(20,30),
                random.Next(8,12),
                random.Next(0,360),
                random.Next(0, 180),
                random.Next(0, 180),
                random.Next(0, 180),
                random.Next(0, 180),
                random.Next(0,50)
                );
            //return JsonConvert.SerializeObject(feedback);
            return feedback;
        }

        public ControlServer()
        {
            StartServer();
        }
    }

    public struct Feedback
    {
        public double Force { get; set; }
        public double Weight { get; set; }
        public double Fumes { get; set; }
        public double Temperature { get; set; }
        public double Battery { get; set; }
        public double BaseAngle { get; set; }
        public double ShoulderAngle { get; set; }
        public double ElbowAngle { get; set; }
        public double WristAngle { get; set; }
        public double WristYawAngle { get; set; }
        public double GripAngle { get; set; }

        public Feedback(double force,
            double weight,
            double fumes,
            double temp,
            double battery,
            double baseAngle,
            double shoulderAngle,
            double elbowAngle,
            double wristAngle,
            double wristYawAngle,
            double gripAngle)
        {
            Force = force;
            Weight = weight;
            Fumes = fumes;
            Temperature = temp;
            Battery = battery;
            BaseAngle = baseAngle;
            ShoulderAngle = shoulderAngle;
            ElbowAngle = elbowAngle;
            WristAngle = wristAngle;
            WristYawAngle = wristYawAngle;
            GripAngle = gripAngle;
        }
    }
}
