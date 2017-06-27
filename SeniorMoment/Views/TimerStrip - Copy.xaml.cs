using System;
using System.Collections.Generic;
using System.Linq;
using SeniorMoment.Services;
using SeniorMoment.ViewModels;
using System.Runtime.CompilerServices;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace SeniorMoment.Views
{
    public sealed partial class TimerStrip : UserControl, IEquatable<TimerStrip>
    {
        #region    Properties

        #region    Brush
        /// <summary>
        /// The SolidColorBrush created from the x:Bind variable Color
        /// </summary>
        public SolidColorBrush Brush
        {
            get => _Brush;
            set
            {
                _Brush = new SolidColorBrush(Color);
                TimerSegments.ForEach(segment => segment.NotifyPropertyChanged("Brush"));
            }
        }
        SolidColorBrush _Brush = new SolidColorBrush(Colors.PaleGoldenrod);
        #endregion Brush

        #region    Color
        /// <summary>
        /// The color of this TimerSegment (ie the Brush)
        /// </summary>
        public Color Color
        {
            get => _Color;
            set
            {
                _Color = value;
                Brush = new SolidColorBrush(_Color);
            }
        }
        Color _Color;

        #endregion Color

        #region    HotTimerStrip
        /// <summary>
        /// The TimerStrip (plus associated VMTimer) which is designated as Hot. 
        /// Hot means any TimerControlButton will be aimed at this TimerStrip
        /// </summary>
        public TimerStrip HotTimerStrip => VMTimer.HotVMTimer.TimerStrip;
        #endregion HotTimerStrip

        #region    IsPaused
        /// <summary>
        /// Is this TimerStrip paused
        /// </summary>
        public bool isPaused => VMTimer == null ? true : VMTimer.IsPaused;
        #endregion IsPaused

        #region    TimerControlButtons List<TimerControlButton> 
        /// <summary>
        /// List of TimerControlButtons on this TimerStrip. These are things like
        /// play,  pause, delete, restart
        /// </summary>
        public List<TimerControlButton> TimerControlButtons { get; private set; } = new List<TimerControlButton>();
        #endregion TimerControlButtons List<TimerControlButton> 

        #region    TimerSegments List<TimerSegment> 
        /// <summary>
        /// List of TimerSegments on this TimerStrip. There is one each for days, hours, minutes and seconds
        /// </summary>
        public List<TimerSegment> TimerSegments { get; private set; } = new List<TimerSegment>();
        #endregion TimerSegments List<TimerSegment> 

        #region    UniqueId
        /// <summary>
        /// Use the VMTimer's UniqueId as the UniqueId for this TimerStrip 
        /// </summary>
        public int UniqueId { get => VMTimer.UniqueId; }
        #endregion UniqueId

        #region    VMTimer
        /// <summary>
        /// The VMTimer that is counting down for this strip
        /// </summary>
        public ViewModels.VMTimer VMTimer { get; }
        #endregion VMTimer

        #endregion Properties

        #region    Constructor()
        public TimerStrip(SeniorMoment.ViewModels.VMTimer vmTimer)
        {
            VMTimer = vmTimer;  // needed by TimerControlButton constructor
            this.InitializeComponent();
        }
        #endregion Constructor()

        #region    GetButtonContent
        /// <summary>
        /// Get whatever appears on the Button. This used in the binding via the
        /// property ButtonContent
        /// </summary>
        /// <param name="index">0=>3 indicating day, hour, minute, second</param>
        /// <returns></returns>
        public string GetButtonContent(int index)
        {
            #region    switch index 
            switch (index)
            {
                default:
                    Statics.InternalProblem("index not 0 to 3");
                    return "ignore"; // never gets here cus InternalProblem throws

                case 0:
                    return Day;

                case 1:
                    return Hour;

                case 2:
                    return Minute;

                case 3:
                    return Second;

            }
            #endregion switch index
        }
        #endregion GetButtonContent

        #region    Initialize
        /// <summary>
        /// Keep Lists of TimerSegments and TimerControlButtons
        /// </summary>
        internal void Initialize()
        {

            DependencyObject cc = (DependencyObject)this;
            List<DependencyObject> segments = (this as UserControl).Descendants(segment => segment.GetType().Equals(typeof(TimerSegment)));
            segments.ForEach(a => TimerSegments.Add(a as TimerSegment));
            TimerSegments.ForEach(segment => segment.Initialize(this));
            var timerControlButtons = this.Descendants(control => control.GetType().Equals(typeof(TimerControlButton)))
                    .Cast<TimerControlButton>();

            timerControlButtons.ForEach(timerCB => TimerControlButtons.Add(timerCB));
            TimerControlButtons.ForEach(timerCB => timerCB.Initialize(this));
        }
        #endregion Initialize

        #region    MakeMeHot
        /// <summary>
        /// Some part of this TimerStrip gets focus and becomes Hot. 
        /// This is propagated to VMTimer which controls which VMTimer is Hot.
        /// </summary>
        public void MakeMeHot()
        {
            VMTimer.MakeMeHot();
        }
        #endregion MakeMeHot

        #region    ResetButtonPictures
        /// time to update the TimerControlButtons
        public void ResetControlButtonImages()
        {
            /*
             * This code looks a bit weird. This forces the UriSource to be updated to one of the 
             * values in the BitmapImageUri lists. It does NOT set _UriSource to ignored
             */
            TimerControlButtons.ForEach(button => button.UriSource = "ignored");
        }
        #endregion ResetButtonPictures

        #region    NotifyPropertyChangedForTimerControlButtons
        /// <summary>
        /// From time to time the TimerControlButton need to be updated. The Timer buttons
        /// are updated as part of the Tick process. Setting the BitmapImage has no effect on the
        /// image as the Get function calculates it.
        /// </summary>
        /// <param name="property"></param>
        public void NotifySegments(string property = "ButtonContent")
        {
            TimerSegments.ForEach(button => button.NotifyPropertyChanged(property));
        }
        #endregion NotifyPropertyChangedForTimerControlButtons

        #region    SecondsToZero
        /// <summary>
        /// This is just a work variable so we always have a positive number when
        /// displaying the Timer,
        /// </summary>
        int SecondsToZero;
        #endregion SecondsToZero

        #region    Tick
        /// <summary>
        /// Clock has ticked. Just change the value(s) on the TimerSegments.
        /// We have to do it on the UI thread
        /// </summary>
        /// <param name="secondsToZero"></param>
        public void TickAt(int secondsToZero)
        {
            SecondsToZero = secondsToZero;
            Statics.RunOnUI(new Action(TickOnUI));
        }
        #endregion Tick

        #region    TickOnUI
        /// <summary>
        /// Anything that changes the UI needs to run on the UI thread. We are updating the TimerStrip
        /// number and colors
        /// </summary>
        void TickOnUI()
        {
            // We don't want minus signs on the button even when the Timer has gone off
            int buttonSecondsToZero = SecondsToZero < 0 ? -SecondsToZero : SecondsToZero;
            TimeSpan timespan = TimeSpan.FromSeconds(buttonSecondsToZero);
            int days = timespan.Days;
            int hours = timespan.Hours;
            int minutes = timespan.Minutes;
            int seconds = timespan.Seconds;
            // Get appropriate number, but don't show days/hours/minutes if there are none left 
            // Also if the number to the left is > 0 and this number < 10 then we need a leading '0'
            // So the following are what we want to see where == indicates a blank tile.
            //    ==  ==  ==   7        7 seconds, no leading zero
            //    ==  ==   6  08        6 min 8 seconds. 8 has leading zero cus minutes is non-zero
            //    ==   4  17  52
            //    ==  13  05  09
            //     1  04  06  28        1 day, 4 hours 6 minutes and 28 seconds
            Day = days == 0 ? "" : days.ToString();

            Hour = ("0" + hours.ToString()).Right(2);

            if (days == 0)
                if (hours == 0)
                    Hour = "";
                else
                    Hour = hours.ToString();

            Minute = ("0" + minutes.ToString()).Right(2);

            if (hours == 0)
                if (Hour == "")
                    if (minutes == 0)
                        Minute = "";
                    else
                        Minute = minutes.ToString();

            Second = ("0" + seconds.ToString()).Right(2);
            if (minutes == 0)
                if (Minute == "")
                    Second = seconds.ToString();
            /*
             * Update the TimerSegment number
             */
            TimerSegments.ForEach(a => a.Tick());
            NotifySegments();
        }
        #endregion TickOnUI

        public void Trace(string info,
                            [CallerMemberName]string member = "",
                            [CallerLineNumber] int line = 0,
                            [CallerFilePath] string path = "")
        { }

        #region    Trace => VMTimer.Trace() yyy
        ///// <summary>
        ///// Write to a trace file to the same Tracer as the one the parent VMTimer uses
        ///// Trace() => VMTimer.Trace();
        ///// </summary>
        ///// <param name="info">Entry Exit or 'whatever' data to be traced</param>
        ///// <param name="member">[CallerMemberName]</param>
        ///// <param name="line">[CallerLineNumber]</param>
        ///// <param name="path">[CallerFilePath]</param>
        //[System.Diagnostics.DebuggerStepThrough()] // never want to go into Trace unless debugging Tracer
        //public void Trace(string info,
        //                    [CallerMemberName]string member = "",
        //                    [CallerLineNumber] int line = 0,
        //                    [CallerFilePath] string path = "")
        //{
        //    VMTimer.Trace(info, member, line, path);
        //}
        #endregion Trace() Trace => VMTimer.Trace()

        #region    Binding Variables Day, Hour, Minute, Second

        #region    Day
        /// <summary>
        /// Days left for the Timer to run
        /// </summary>
        public String Day { get; private set; } = "";
        #endregion Day

        #region    Hour
        /// <summary>
        /// Number of hours for the Timer to run once days is zero
        /// </summary>
        public string Hour { get; private set; } = "";
        #endregion Hour

        #region    Minute
        /// <summary>
        /// Number of hours for the Timer to run once days is zero
        /// and hours is zero
        /// </summary>
        public string Minute { get; private set; } = "";
        #endregion Minute

        #region    Second
        /// <summary>
        /// Number of hours for the Timer to run once day is zero,
        /// hour is zero and minutes is zero
        /// </summary>
        public string Second { get; private set; } = "";
        #endregion Second

        #endregion Binding Variables Days, Hours, Minutes, Seconds

        #region    Equals (IEquatable<TimerStrip>
        /// <summary>
        /// IEquatable between two TimerStrips occurs when they UniqueId are the same.
        /// If so it means 'this' is the same instance as 'other'.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(TimerStrip other) => this.UniqueId == other?.UniqueId;
        #region    Events
        #endregion Equals (IEquatable<TimerStrip>

        #region    TimerStrip_GotFocus
        /// <summary>
        /// The TimerStrip received focus So we now have a new Hot TimerStrip and a new Hot VMImer.
        /// This calls another routine which can be called if one of the buttons on the TimerStrip
        /// is pressed. Hot does NOT mean in focus. It means any control buttons will apply to the
        /// Hot VMTimer and TimerStrip
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimerStrip_GotFocus(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            SetHot();
        }
        #endregion TimerStrip_GotFocus

        /// <summary>
        /// Called from TimerStrip saying 
        /// </summary>
        public void SetHot()
        {
            VMTimer.HotVMTimer = VMTimer;
        }
        #region    TimerStrip_LostFocus
        /// <summary>
        /// The last TimerStrip remains Hot until a new one is made hot. This can happen as follows:
        /// <list type="*" >
        /// * User presses New Timer
        /// * 
        /// </list>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimerStrip_LostFocus(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // TimerStripInFocus = null;
        }
        #endregion TimerStrip_LostFocus

        #region    TimerStrip_Loaded
        /// <summary>
        /// At this point the MTimer is Paused. So we fake a Tick() which causes the
        /// TimerStrip to update 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimerStrip_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            TickAt(VMTimer.MTimer.SecondsToZero);
        }
        #endregion    TimerStrip_Loaded

        #endregion Events
    }
}
