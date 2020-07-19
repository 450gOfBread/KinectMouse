using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace KinectMouse
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        

        private KinectSensor sensor;

        bool pause = false;
        bool pauseGesture = false;
        int pauseClock = 0;

        int elevationAngle = 5;

        Queue<double> xr = new Queue<double>();
        Queue<double> yr = new Queue<double>();
        Queue<double> zr = new Queue<double>();
        Queue<double> xdif = new Queue<double>();
        Queue<double> ydif = new Queue<double>();
        double prevx = 0;
        double prevy = 0;
        

        Queue<double> xl = new Queue<double>();
        Queue<double> yl = new Queue<double>();
        Queue<double> zl = new Queue<double>();

        int sensitivity = 1;
        double xDif = 0;
        double yDif = 0;

        double ma = 0;
        double mi = 50;

        int smoothingHigh = 10; //15
        int smoothingMed = 6; //11
        int smoothingLow = 2; //6

        

        int mDownFlag = 0;
        bool mDown = false;

        int tabFlag = 0;
        bool tabDown = false;

        public MainWindow()
        {
            InitializeComponent();

            int smoothing = smoothingMed;

            for (int i = 0; i < smoothingLow; i++)
            {
                xl.Enqueue(0);
                yl.Enqueue(0);
                zl.Enqueue(0);
                

                xr.Enqueue(0);
                yr.Enqueue(0);
                zr.Enqueue(0);
               
            }

            for (int j = 0; j < smoothingMed; j++)
            {
                xdif.Enqueue(0);
                ydif.Enqueue(0);
            }
            
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {


            var sensorStatus = new KinectSensorChooser();

            sensorStatus.KinectChanged += KinectSensorChooserKinectChanged;
            kinectChooser.KinectSensorChooser = sensorStatus; sensorStatus.Start();
        }

        private void KinectSensorChooserKinectChanged(object sender, KinectChangedEventArgs e)
        {
            if (sensor != null)
            {
                sensor.SkeletonFrameReady -= KinectSkeletonFrameReady;
                

            }

            sensor = e.NewSensor;

            if (sensor == null) return;

            sensor.ElevationAngle = elevationAngle;
            sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
            sensor.SkeletonStream.Enable();
            
            sensor.SkeletonFrameReady += KinectSkeletonFrameReady;
        }

        

        [DllImport("user32")]
        public static extern int SetCursorPos(int x, int y);

        

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        [DllImport("user32.dll", EntryPoint = "keybd_event", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern void keybd_event(byte vk, byte scan, int flags, int extrainfo);


        private void KinectSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            var skeletons = new Skeleton[0];

            using (var skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            if (skeletons.Length == 0) return;


            var skel = skeletons.FirstOrDefault(x => x.TrackingState == SkeletonTrackingState.Tracked);


            if (skel == null)
            {
                prevx = 0;
                prevy = 0;

                
                return;
            }
            else
            {
                
                var shoulder = skel.Joints[JointType.ShoulderCenter];
                var lshoulder = skel.Joints[JointType.ShoulderLeft];

                var rightHand = skel.Joints[JointType.HandRight];
                XValueRight.Text = rightHand.Position.X.ToString();
               // YValueRight.Text = rightHand.Position.Y.ToString();
               // ZValueRight.Text = rightHand.Position.Z.ToString();

                var leftHand = skel.Joints[JointType.HandLeft];
                XValueLeft.Text = leftHand.Position.X.ToString();
                YValueLeft.Text = prevx.ToString();//leftHand.Position.Y.ToString();
                ZValueLeft.Text = System.Windows.Forms.Cursor.Position.Y.ToString();//shoulder.Position.Z.ToString();



                xdif.Dequeue();
                ydif.Dequeue();

                double xi = xr.Average();
                double yi = yr.Average();

                xr.Dequeue();
                yr.Dequeue();
                zr.Dequeue();
                

                xr.Enqueue(Convert.ToDouble(rightHand.Position.X) * 960 / 0.75 );
                yr.Enqueue(Convert.ToDouble(rightHand.Position.Y) * 960 / 0.75 );
                zr.Enqueue(Convert.ToDouble(rightHand.Position.Z) * 175 );

                double xf = xr.Average();
                double yf = yr.Average();

                xdif.Enqueue((xf - xi) * sensitivity);
                ydif.Enqueue((yf - yi) * sensitivity);

                xl.Dequeue();
                yl.Dequeue();
                zl.Dequeue();

                xl.Enqueue(Convert.ToDouble(leftHand.Position.X) * 960/0.75);
                yl.Enqueue(Convert.ToDouble(leftHand.Position.Y) * 960/0.75);
                zl.Enqueue(Convert.ToDouble(leftHand.Position.Z) * 175);

                /*
                double zrAvg = 350 - zr.Average();
                double zlAvg = 350 - zl.Average();

                if (zrAvg < 0) zrAvg = 0;
                if (zlAvg < 0) zlAvg = 0; */

                


                /////// Smoothing
                double rangeDif = Math.Sqrt(Math.Pow(Math.Abs(xf - xi), 2) + Math.Pow(Math.Abs(yf - yi), 2));

                if ( rangeDif <= 3.0 /*ShoulderToHand <= 0.2*/)//
                {
                    if (xdif.Count() < smoothingHigh)
                    {
                        xdif.Enqueue(xdif.Peek());
                    }
                    if (ydif.Count() < smoothingHigh)
                    {
                        ydif.Enqueue(ydif.Peek());
                    }
                }else if(rangeDif > 3.0 && rangeDif <= 10.0/*ShoulderToHand > 0.2 && ShoulderToHand < 0.3*/)
                {
                    if (xdif.Count() > smoothingMed)//
                    {
                        if (xdif.Count() > smoothingMed)
                        {
                            xdif.Dequeue();
                        }
                    }else
                    {
                        if (xdif.Count() < smoothingMed)
                        {
                            xdif.Enqueue(xdif.Peek());
                        }
                    }

                    if (ydif.Count() > smoothingMed)//
                    {
                        if (ydif.Count() > smoothingMed)
                        {
                            ydif.Dequeue();
                        }
                    }
                    else
                    {
                        if (ydif.Count() < smoothingMed)
                        {
                            ydif.Enqueue(ydif.Peek());
                        }
                    }
                }else if (rangeDif > 10.0/*ShoulderToHand >= 0.3*/)//
                {
                    if (xdif.Count() < smoothingLow)
                    {
                        xdif.Enqueue(xdif.Peek());
                    }
                    if (ydif.Count() < smoothingLow)
                    {
                        ydif.Enqueue(ydif.Peek());
                    }
                }
                ////////
                    /*
                    pointerRight.Margin = new Thickness(prevx + xdif.Average(), 0, 0, prevy + ydif.Average());


                    pointerRight.Width = zrAvg;
                    pointerRight.Height = zrAvg;


                    pointerLeft.Margin = new Thickness(xl.Average(), 0, 0, yl.Average());

                    pointerLeft.Width = zlAvg;
                    pointerLeft.Height = zlAvg;*/

                xDif = xdif.Average();
                yDif = ydif.Average();


                /////// Sensitivity

                double ShoulderToHand = shoulder.Position.Z - 0.2 - (zr.Average() / 175);

                int sensConst = 5;

                sensitivity = Convert.ToInt32((((ShoulderToHand) / 0.5)) * sensConst); //// Distance Based

                //sensitivity = Convert.ToInt32(rangeDif/2); //// Speed Based

                /////
                if (sensitivity < 0) sensitivity = 0;

                const double ignore = 0.1;

                if (Math.Abs(xf - xi) <= ignore) xDif = 0;
                if (Math.Abs(yf - yi) <= ignore) yDif = 0;

                if (Math.Abs(leftHand.Position.X - rightHand.Position.X) < 0.15 && Math.Abs(leftHand.Position.Y - rightHand.Position.Y) < 0.15 && Math.Abs(leftHand.Position.Z - rightHand.Position.Z) < 0.15)
                {
                    if (pauseClock < 5)
                    {
                        pauseClock++;
                    }
                    else
                    {
                        pauseGesture = true;
                        
                        
                    }
                }
                else
                {
                    
                    pauseClock = 0;

                    if (pauseGesture)
                    {
                        if (pause)
                        {
                            pause = false;
                        }
                        else
                        {
                            pause = true;
                        }
                        pauseGesture = false;
                    }
                }

                if (!pause)
                {

                    //prevx = System.Windows.Forms.Cursor.Position.X; //prevx + xDif;
                    //prevy = System.Windows.Forms.Cursor.Position.Y; //prevy + yDif;
                    //SetCursorPos(Convert.ToInt32(prevx + xDif) + 960, 540 - Convert.ToInt32(prevy + yDif));
                    //System.Windows.Forms.Cursor.Position = new System.Drawing.Point(Convert.ToInt32(prevx + xDif) + 960, 540 - Convert.ToInt32(prevy + yDif));
                    System.Windows.Forms.Cursor.Position = new System.Drawing.Point(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X + xDif), Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y - yDif));

                    
                    if ((leftHand.Position.Y >= (shoulder.Position.Y + 0.2)) && !tabDown)
                    {
                        if (tabFlag < 15)
                        {
                            tabFlag++;
                        }
                        if (tabFlag >= 15)
                        {

                            mDownFlag = 0;
                            tabDown = true;
                            mDown = false;

                            keybd_event(0x12, 0x38, 0, 0);  //Alt Down
                            keybd_event(0x09, 0x0F, 0, 0);  // Tab Down
                            keybd_event(0x09, 0x0F, 2, 0);  // Tab up

                        } 

                        

                    }
                    else if ((leftHand.Position.Y - shoulder.Position.Y) < -0.1 && !(Math.Abs(leftHand.Position.X - shoulder.Position.X) > 0.35) && mDown)
                    {

                        //mouse_event(0x04, (uint)(Convert.ToInt32(prevx + xDif) + 960), (uint)(540 - Convert.ToInt32(prevy + yDif)), 0, 0);
                        mouse_event(0x04, (uint)(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X)), (uint)(540 - Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y)), 0, 0);

                        mDown = false;
                        tabFlag = 0;
                        mDownFlag = 0;

                    }
                    else if ((leftHand.Position.Y - shoulder.Position.Y) >= -0.1 && (Math.Abs(leftHand.Position.X - shoulder.Position.X) > 0.35) && mDown)
                    {

                        /* mouse_event(0x04, (uint)(Convert.ToInt32(prevx + xDif) + 960), (uint)(540 - Convert.ToInt32(prevy + yDif)), 0, 0);
                         mouse_event(0x02 | 0x04, (uint)(Convert.ToInt32(prevx + xDif) + 960), (uint)(540 - Convert.ToInt32(prevy + yDif)), 0, 0);
                         mouse_event(0x02 | 0x04, (uint)(Convert.ToInt32(prevx + xDif) + 960), (uint)(540 - Convert.ToInt32(prevy + yDif)), 0, 0); */

                        mouse_event(0x04, (uint)(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X)), (uint)(540 - Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y)), 0, 0);
                        mouse_event(0x02 | 0x04, (uint)(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X)), (uint)(540 - Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y)), 0, 0);
                        mouse_event(0x02 | 0x04, (uint)(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X)), (uint)(540 - Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y)), 0, 0);

                        mDown = false;
                        tabFlag = 0;
                        mDownFlag = 0;
                    }
                    else if ((leftHand.Position.Y - shoulder.Position.Y) < -0.1 && !(Math.Abs(leftHand.Position.X - shoulder.Position.X) > 0.35) && tabDown)
                    {

                        //mouse_event(0x02 | 0x04, (uint)(Convert.ToInt32(prevy + xDif) + 960), (uint)(540 - Convert.ToInt32(prevy + yDif)), 0, 0);
                        mouse_event(0x02 | 0x04, (uint)(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X)), (uint)(540 - Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y)), 0, 0);

                        tabDown = false;
                        tabFlag = 0;
                        mDownFlag = 0;
                        keybd_event(0x12, 0x38, 2, 0);  //Alt up

                    }
                    else if ((leftHand.Position.Y - shoulder.Position.Y) >= -0.1 && (leftHand.Position.Y - shoulder.Position.Y) < 0.2 && !mDown && !tabDown)
                    {
                           tabDown = false;
                           tabFlag = 0;

                        if (mDownFlag < 10)
                        {
                            mDownFlag++;
                        }
                        if (mDownFlag >= 10)
                        {
                            //mouse_event(0x02, (uint)(Convert.ToInt32(prevx + xDif) + 960), (uint)(540 - Convert.ToInt32(prevy + yDif)), 0, 0);
                            mouse_event(0x02, (uint)(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X)), (uint)(540 - Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y)), 0, 0);
                            mDown = true;
                        }
                    }


                    
                   prevx = System.Windows.Forms.Cursor.Position.X; //prevx + xDif;
                   prevy = System.Windows.Forms.Cursor.Position.Y; //prevy + yDif;
                }
            }
        }

        private void pauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (pause)
            {
                pause = false;
            }
            else
            {
                pause = true;
            }
        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            angleTextBox.Text = angleSlider.Value.ToString();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            sensor.ElevationAngle = Convert.ToInt32(angleSlider.Value);
        }

        
    }
}
