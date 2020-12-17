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

        
        // Pretty well all variables are global to allow for easier
        // development of the script.

        // The kinect sensor itself.
        private KinectSensor sensor;

        // Variables that handle pausing of hand tracking.
        bool pause = false;
        bool pauseGesture = false;
        int pauseClock = 0;


        int elevationAngle = 5; // Kinect sensor angle



        // Intialize queues to be used as buffers that hold samples of hand positions.
        // These samples later get used for calculating averages of the most recent samples
        // from the sensor.

        // The overall purpose of this is to eliminate false movements created by the kinect
        // failing to consistently track a person.
        Queue<double> xr = new Queue<double>();
        Queue<double> yr = new Queue<double>();
        Queue<double> zr = new Queue<double>();

        Queue<double> xdif = new Queue<double>();
        Queue<double> ydif = new Queue<double>();
        

        Queue<double> xl = new Queue<double>();
        Queue<double> yl = new Queue<double>();
        Queue<double> zl = new Queue<double>();



        double prevX = 0; // Holds previous final x-position
        double prevY = 0; // Holds previous final y-position

        int sensitivity = 1; // Default sensitivity of cursor movement.

        double xDifAvg = 0; // Average difference in x-position for right hand.
        double yDifAvg = 0; // Average difference in y-position for right hand.


        const int smoothingHigh = 10; //15    // Smoothing constants for three different amounts of smoothing
        const int smoothingMed = 6; //11      // 
        const int smoothingLow = 2; //6       //

        

        // Flags used to track when the person gestures for Left Mouse Down.
        // Used for things like clicking, dragging, etc.
        int mDownFlag = 0;
        bool mDown = false;


        // Flags used to track when the person gestures for Alt-Tab. 
        // This displays every opened window and allows the person to easliy switch between those windows.
        int tabFlag = 0;
        bool tabDown = false;

        public MainWindow()
        {
            InitializeComponent();

            int smoothing = smoothingMed;


            // Initializing buffers for smoothing

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

        // Sets up window
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {


            var sensorStatus = new KinectSensorChooser();

            sensorStatus.KinectChanged += KinectSensorChooserKinectChanged;
            kinectChooser.KinectSensorChooser = sensorStatus; sensorStatus.Start();
        }


        // Sets up kinect sensor
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

        

        // Importing DLLs that handle windows events.

        [DllImport("user32")]
        public static extern int SetCursorPos(int x, int y);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        [DllImport("user32.dll", EntryPoint = "keybd_event", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern void keybd_event(byte vk, byte scan, int flags, int extrainfo);



        // Functions that is called when a sample is ready from the kinect sensor.
        // It handles ALL of the processing of hand movements.
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
                prevX = 0;  // Sets the values for the "previous coordinates" of the hand to 0 during the time
                prevY = 0;  // that the kinect can't find a person.

                
                return;
            }
            else
            {
                
                
                var shoulder = skel.Joints[JointType.ShoulderCenter]; // Represents the location that is slightly above the chest at shoulder level.

                var lshoulder = skel.Joints[JointType.ShoulderLeft];  // Represents the left shoulder.

                var rightHand = skel.Joints[JointType.HandRight];   // Represents the right hand.

                // String values of the positions of the right hand.
                // Used for debugging.

                XValueRight.Text = rightHand.Position.X.ToString();
               // YValueRight.Text = rightHand.Position.Y.ToString();
               // ZValueRight.Text = rightHand.Position.Z.ToString();

                var leftHand = skel.Joints[JointType.HandLeft];     // Represents the left hand.

                // String values of the positions of the left hand.
                // Used for debugging.
                XValueLeft.Text = leftHand.Position.X.ToString();
                YValueLeft.Text = prevX.ToString(); // currently used to display previous x coord value. // DEBUG
                ZValueLeft.Text = System.Windows.Forms.Cursor.Position.Y.ToString();




                // Loading each queue individually with new samples for the right hand

                xdif.Dequeue();  // Queues for the differences between initial hand placement. 
                ydif.Dequeue();  // 

                double xi = xr.Average();   // Calculates average between all samples in initial hand-placement buffers.
                double yi = yr.Average();   //

                xr.Dequeue();   // Getting rid of oldest value in the queue to make room for new value.
                yr.Dequeue();   //
                zr.Dequeue();   //
                

                xr.Enqueue(Convert.ToDouble(rightHand.Position.X) * 960 / 0.75 );   // Normalizes new sample to my monitor and enqueues value.
                yr.Enqueue(Convert.ToDouble(rightHand.Position.Y) * 960 / 0.75 );   //
                zr.Enqueue(Convert.ToDouble(rightHand.Position.Z) * 175 );          //

                double xf = xr.Average();   // Calculates average between all samples in final hand-placement buffers.
                double yf = yr.Average();   //

                xdif.Enqueue((xf - xi) * sensitivity);  // Enqueues new calculate differences in positions multiplied by the current calculated mouse sensitivity
                ydif.Enqueue((yf - yi) * sensitivity);  //



                // Does the same operations as above, but doesn't need to calculate difference in position.
                // Only need to calculate absolute position of left hand.
                xl.Dequeue();   
                yl.Dequeue();
                zl.Dequeue();

                xl.Enqueue(Convert.ToDouble(leftHand.Position.X) * 960/0.75);
                yl.Enqueue(Convert.ToDouble(leftHand.Position.Y) * 960/0.75);
                zl.Enqueue(Convert.ToDouble(leftHand.Position.Z) * 175);




                // This block handles smoothing of the cursor.

                // Smoothing has to change dynamically, 
                // otherwise there may be to much delay between hand movement and cursor movement,
                // or potentially not be smoothed enough to allow for clicking small buttons on screen.

                // The farther the right hand is from the body, the less smoothing. This allows the person to sweep the cursor across the screen.

                // The closer the right hand is to the body, more smoothing is done. This allows for small, non-jittery movement of the cursor.
                double rangeDif = Math.Sqrt(Math.Pow(Math.Abs(xf - xi), 2) + Math.Pow(Math.Abs(yf - yi), 2));

                if ( rangeDif <= 3.0 )
                {
                    if (xdif.Count() < smoothingHigh)
                    {
                        xdif.Enqueue(xdif.Peek());
                    }
                    if (ydif.Count() < smoothingHigh)
                    {
                        ydif.Enqueue(ydif.Peek());
                    }
                }else if(rangeDif > 3.0 && rangeDif <= 10.0)
                {
                    if (xdif.Count() > smoothingMed)
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

                    if (ydif.Count() > smoothingMed)
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
                }else if (rangeDif > 10.0)
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
                

                xDifAvg = xdif.Average();
                yDifAvg = ydif.Average();


                /////// Sensitivity Calculation

                // Finds the distance between the right hand and the right shoulder going towards the physical sensor.
                double ShoulderToHand = shoulder.Position.Z - 0.2 - (zr.Average() / 175);

                int sensConst = 5; // Constant used for sensitivity calculation.

                // Calculates the sensitivity.
                sensitivity = Convert.ToInt32((((ShoulderToHand) / 0.5)) * sensConst); //// Distance Based

                // sensitivity = Convert.ToInt32(rangeDif/2); //// Speed Based sensitivity. Currently not in use.


                
                // If previous calculation for sensitivity goes below zero, set it back to zero.
                if (sensitivity < 0) sensitivity = 0;

                const double ignore = 0.1;  // Constant distance between hand and body for which movement should be ignored.

                if (Math.Abs(xf - xi) <= ignore) xDifAvg = 0;   // If the average change in distance is below or equal to the "ignore threshold"
                if (Math.Abs(yf - yi) <= ignore) yDifAvg = 0;   // then ignore the movement.




                // Highest priority given to the action that allows for the pausing \ unpausing of 
                // the movement tracking.
                // Gesture: Hands together
                if (Math.Abs(leftHand.Position.X - rightHand.Position.X) < 0.15 && Math.Abs(leftHand.Position.Y - rightHand.Position.Y) < 0.15 && Math.Abs(leftHand.Position.Z - rightHand.Position.Z) < 0.15)
                {
                    if (pauseClock < 5) // If the person is doing the gesture for 5 samples in a row, then accept the gesture.
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
                    
                    pauseClock = 0; // Resets the counter for consecutive samples of the person doing this gesture.


                    // This toggles the pause state.
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

                    // Sets the new position of the cursor based off of the current position, and the average difference in hand position.
                    System.Windows.Forms.Cursor.Position = new System.Drawing.Point(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X + xDifAvg), Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y - yDifAvg));

                    
                    // This controls the Alt-Tab gesture.
                    // Gesture: Left hand straight above left shoulder
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


                    // Handles left mouse button being held down.
                    // Gesture: Left hand falling back straight down to person's side.
                    else if ((leftHand.Position.Y - shoulder.Position.Y) < -0.1 && !(Math.Abs(leftHand.Position.X - shoulder.Position.X) > 0.35) && mDown)
                    {

                        // Clicks at current cursor position
                        mouse_event(0x04, (uint)(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X)), (uint)(540 - Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y)), 0, 0);

                        mDown = false;
                        tabFlag = 0;
                        mDownFlag = 0;

                    }

                    // Handles double click.
                    // Gesture: Left hand falling straight out to the left of the person.
                    else if ((leftHand.Position.Y - shoulder.Position.Y) >= -0.1 && (Math.Abs(leftHand.Position.X - shoulder.Position.X) > 0.35) && mDown)
                    {

                        // Clicks at current cursor position
                        mouse_event(0x04, (uint)(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X)), (uint)(540 - Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y)), 0, 0);
                        mouse_event(0x02 | 0x04, (uint)(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X)), (uint)(540 - Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y)), 0, 0);
                        mouse_event(0x02 | 0x04, (uint)(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X)), (uint)(540 - Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y)), 0, 0);

                        mDown = false;
                        tabFlag = 0;
                        mDownFlag = 0;
                    }

                    // Handles click event right after person uses Alt-Tab gesture. This is used to "unpress" the Alt button.
                    // Gesture: Hand falling back straight down to person's side.
                    else if ((leftHand.Position.Y - shoulder.Position.Y) < -0.1 && !(Math.Abs(leftHand.Position.X - shoulder.Position.X) > 0.35) && tabDown)
                    {

                        // Clicks at current cursor position
                        mouse_event(0x02 | 0x04, (uint)(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X)), (uint)(540 - Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y)), 0, 0);

                        tabDown = false;
                        tabFlag = 0;
                        mDownFlag = 0;
                        keybd_event(0x12, 0x38, 2, 0);  //Alt up

                    }

                    // Handles the left mouse button being held down.
                    // Gesture: Left hand straight in front of the person.
                    else if ((leftHand.Position.Y - shoulder.Position.Y) >= -0.1 && (leftHand.Position.Y - shoulder.Position.Y) < 0.2 && !mDown && !tabDown)
                    {
                           tabDown = false;
                           tabFlag = 0;

                        // Consecutive sample counter used to activate Mouse Down gesture
                        if (mDownFlag < 10)
                        {
                            mDownFlag++;
                        }
                        if (mDownFlag >= 10)
                        {

                            // "presses and holds" left mouse button
                            mouse_event(0x02, (uint)(Convert.ToInt32(System.Windows.Forms.Cursor.Position.X)), (uint)(540 - Convert.ToInt32(System.Windows.Forms.Cursor.Position.Y)), 0, 0);

                            // Mouse button down flag set.
                            mDown = true;
                        }
                    }


                    
                   prevX = System.Windows.Forms.Cursor.Position.X; // saves previous cursor positions.
                   prevY = System.Windows.Forms.Cursor.Position.Y; //
                }
            }
        }


        // Handles pause button click on window.
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

        // Slider for adjusting kinect elevation angle. (BUGGED)
        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            angleTextBox.Text = angleSlider.Value.ToString();
        }

        // Sets kinect angle to the value of the slider when the button is clicked. (BUGGED)
        private void button_Click(object sender, RoutedEventArgs e)
        {
            sensor.ElevationAngle = Convert.ToInt32(angleSlider.Value);
        }

        
    }
}
