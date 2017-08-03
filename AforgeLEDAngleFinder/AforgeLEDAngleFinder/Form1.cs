using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video.DirectShow;
using AForge.Math;
namespace AforgeLEDAngleFinder
{
    public partial class Form1 : Form
    {
        AForge.Video.DirectShow.VideoCaptureDevice theCamera;
        System.Net.Sockets.TcpListener listener;
        System.Net.Sockets.TcpClient VRapp = null;
        System.Threading.Mutex vrconnectMutex = new System.Threading.Mutex();
        System.IO.Ports.SerialPort serial;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
           // VideoCaptureDeviceForm selector = new VideoCaptureDeviceForm();
           //var res = selector.ShowDialog();
           //if (res == System.Windows.Forms.DialogResult.OK)
           //{
           //    theCamera = selector.VideoDevice;
               startTheCamera();
           //}
           //else
           //    Application.Exit();
            serial = new System.IO.Ports.SerialPort("COM11");
            serial.DataReceived += serial_DataReceived;
            serial.Open();
            timer1.Start();
        }
        double angle = 0;
        void serial_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            
            string angleData = serial.ReadLine();
            System.Diagnostics.Trace.WriteLine(angleData);
           // double angle ;
            if(double.TryParse(angleData, out angle) == false) return ;
           // angle = angle;// / 100.0;
            //bool canTransmit = false;
            //vrconnectMutex.WaitOne();
            //if (VRapp != null)
            //    canTransmit = true;
            //vrconnectMutex.ReleaseMutex();
            updateTheFrame(null, angleData);
            //if (canTransmit)
            //{
            //    byte[] deltaAngleBytes = BitConverter.GetBytes(angle); //note: in degrees per frame
            //    try
            //    {
            //        if (VRapp.Connected)
            //            VRapp.GetStream().Write(deltaAngleBytes, 0, deltaAngleBytes.Length);
            //    }
            //    catch
            //    {
            //        VRapp = null;
            //        canTransmit = false;
            //    }
            //}
        }

       
        void startTheCamera()
        {
            //if (theCamera == null) return;
            //theCamera.NewFrame += theCamera_NewFrame;
           // theCamera.Start();
            listener = new System.Net.Sockets.TcpListener(31337);
            listener.Start();
            listener.BeginAcceptSocket(asyncAcceptVRApp, listener);
        }

        void asyncAcceptVRApp(IAsyncResult res)
        {
            try
            {
                vrconnectMutex.WaitOne();
                VRapp = listener.EndAcceptTcpClient(res);
              
                listener.BeginAcceptSocket(asyncAcceptVRApp, listener);
            }
            finally { vrconnectMutex.ReleaseMutex(); }
        }
        void stopTheCamera()
        {
            if (theCamera == null) return;
           
            theCamera.Stop();
            listener.Stop();
            if (VRapp != null)
                VRapp.Close();
        }
        AForge.Math.Vector3 getLEDLocCenter(Rectangle ledpos)
        {
            AForge.Math.Vector3 ret = new AForge.Math.Vector3();
            ret.X = (ledpos.X + ledpos.Width / 2.0f);
            ret.Y = (ledpos.Y + ledpos.Height / 2.0f);
            return ret;

        }

      //picks the first 3 points it detects, then returns the midpoint between the two closest, and the farthest point
        AForge.Math.Vector4 ledPoints(Rectangle [] leds)
        {
            if (leds.Length < 3)
                return new Vector4(0, 0, 0, 0);
            //first, find the two LEDs closest to each other
            Vector3 A = getLEDLocCenter(leds[0]);
            Vector3 B = getLEDLocCenter(leds[1]);
            Vector3 C = getLEDLocCenter(leds[2]);

            Vector3 AB = B - A;
            Vector3 BC = C - B;
            Vector3 AC = C - A;

            Vector3 CenterVector = AB;
            Vector3 TipPoint = new Vector3(0,0,0);
            Vector3 CenterPoint = new Vector3(0, 0, 0);
            if (BC.Norm < CenterVector.Norm)
                CenterVector = BC;
            if (AC.Norm < CenterVector.Norm)
                CenterVector = AC;
            if(CenterVector == AB)
            {
                CenterPoint = (A + B) / 2;
                TipPoint = C;
            }
            if (CenterVector == AC)
            {
                CenterPoint = (A + C) / 2;
                TipPoint = B;
            }
            if (CenterVector == BC)
            {
                CenterPoint = (C + B) / 2;
                TipPoint = A;
            }
            return new Vector4(CenterPoint.X, CenterPoint.Y, TipPoint.X, TipPoint.Y);
        }



        bool ledAngleInit = false;
        double lastLEDAngle = 0;
     
        void theCamera_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            Bitmap theFrame = eventArgs.Frame;
            AForge.Imaging.Filters.ColorFiltering colorFilter = new AForge.Imaging.Filters.ColorFiltering(new AForge.IntRange(200, 255),
                new AForge.IntRange(200, 255),
                new AForge.IntRange(200, 255));
            colorFilter.ApplyInPlace(theFrame);
            AForge.Imaging.BlobCounter blobCounter = new AForge.Imaging.BlobCounter(theFrame);
            var ledLOCs = blobCounter.GetObjectsRectangles();
            string ledpos = "not there";
            if (ledLOCs.Length > 0)
            {
                //should probably find out which one is actually the LED... later though.
                var pos = getLEDLocCenter(ledLOCs[0]);
                var centerPos = new AForge.Math.Vector3(theFrame.Width / 2, theFrame.Height / 2, 0);
                var ledVector = pos - centerPos;
                ledVector.Normalize();
                var ledAngle = Math.Acos(ledVector.X);
                if (ledVector.Y < 0)
                    ledAngle = -ledAngle;
                double deltaLEDAngle = 0;
                if(ledAngleInit)
                {
                    deltaLEDAngle = ledAngle - lastLEDAngle;
                    if (deltaLEDAngle > Math.PI * 2)
                        deltaLEDAngle -= Math.PI * 2;
                    if (deltaLEDAngle < -Math.PI * 2)
                        deltaLEDAngle += Math.PI * 2;
                    lastLEDAngle = ledAngle;  
                }
                else
                {
                    ledAngleInit = true;
                    lastLEDAngle = ledAngle;
                }
                deltaLEDAngle = ledAngle * 180 / Math.PI;
                bool canTransmit = false;
                vrconnectMutex.WaitOne();
                if (VRapp != null)
                    canTransmit = true;
                vrconnectMutex.ReleaseMutex();
                if(canTransmit)
                {
                    byte [] deltaAngleBytes = BitConverter.GetBytes(deltaLEDAngle); //note: in degrees per frame
                    try
                    {
                        if (VRapp.Connected)
                            VRapp.GetStream().Write(deltaAngleBytes, 0, deltaAngleBytes.Length);
                    }
                    catch
                    {
                        VRapp = null;
                        canTransmit = false;
                    }
                }
                double displayLEDAngle =  deltaLEDAngle;

                ledpos = displayLEDAngle.ToString();

                var triVec = ledPoints(ledLOCs);
                if (triVec != new Vector4(0, 0, 0, 0))
                {
                    ledpos = triVec.ToString();
                    Graphics g = Graphics.FromImage(theFrame);
                    g.DrawLine(Pens.Red, triVec.X, triVec.Y, triVec.Z, triVec.W);
                    g.Flush();
                    g.Dispose();
                }

                //if we're connected to the VR app, this is where we send the delta angle.
            }
            updateTheFrame(theFrame, ledpos);
        }
        delegate void updateTheFrameDelegate(Bitmap img, string label);
        void updateTheFrame(Bitmap theFrame, string label)

        {
            if(this.InvokeRequired)
            {
                updateTheFrameDelegate del = new updateTheFrameDelegate(updateTheFrame);
                if(theFrame != null)
                    this.BeginInvoke(del, (Bitmap)theFrame.Clone(), label );
                else
                    this.BeginInvoke(del, null, label);
                return;

            }
            pbCameraView.Image = theFrame;
            lblAngle.Text = label;
        }
        double lastAngle = 0;
        bool init = false;
        private void timer1_Tick(object sender, EventArgs e)
        {
            double speed = 0;
            if(!init)
            {
                lastAngle = angle;
                speed = 0;
                init = true;
            }
            else
            {
                speed = angle - lastAngle;
                if (speed < 0) speed = 0;  //when tiicking over, should be negative speed.  we wont register that here, so nothing to do
                lastAngle = angle;
                System.Diagnostics.Trace.WriteLine(speed);
            }
            bool canTransmit = false;
            vrconnectMutex.WaitOne();
            if (VRapp != null)
                canTransmit = true;
            vrconnectMutex.ReleaseMutex();
           // updateTheFrame(null, angleData);
            if (canTransmit)
            {
                byte[] deltaAngleBytes = BitConverter.GetBytes(speed); //note: in degrees per frame
                try
                {
                    if (VRapp.Connected)
                        VRapp.GetStream().Write(deltaAngleBytes, 0, deltaAngleBytes.Length);
                }
                catch
                {
                    VRapp = null;
                    canTransmit = false;
                }
            }
        }
    }
}
