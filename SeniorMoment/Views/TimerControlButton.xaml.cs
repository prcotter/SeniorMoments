using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SeniorMoment.Services;
using SeniorMoment.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SeniorMoment.Views
{
    /// <summary>
    /// Any of the buttons on the TimerStrip that are not TimerSegments
    /// </summary>
    public sealed partial class TimerControlButton : UserControl, INotifyPropertyChanged
    {
        #region    Properties

        #region    ActivityWhenPressed
        public Activity ActivityWhenPressed { get; set; }
        #endregion ActivityWhenPressed

        #region    BitmapImageUrisWhenPaused (static List) 
        /// <summary>
        /// List of Images that are to appear on the 4 TimerStrip.TimerControlButtons when the alarm is Paused
        /// </summary>
        static public List<string> BitmapImageUrisWhenPaused { get; } = new List<string>
        {
            @"ms-appx:///Assets/ButtonHelp.png",
            @"ms-appx:///Assets/ButtonClose.png",
            @"ms-appx:///Assets/ButtonReload.png",
            @"ms-appx:///Assets/ButtonPlay.png"
        };
        #endregion BitmapImageUrisWhenPaused (static List)

        #region    BitmapImageUrisWhenCounting (static List)
        /// <summary>
        /// List of Images that are to appear on the TimerStrip TimerControlButtons when the alarm is Paused
        /// </summary>
        static public List<string> BitmapImageUrisWhenCounting { get; } = new List<string>
        {
            @"ms-appx:///Assets/ButtonNext.png",
            @"ms-appx:///Assets/ButtonClose.png",
            @"ms-appx:///Assets/ButtonReload.png",
            @"ms-appx:///Assets/ButtonPause.png"
        };
        #endregion BitmapImageUrisWhenCounting (static List)

        #region    CalculatedVisibility 
        /// <summary>
        /// get set, Dictates whether this TimerControlButton should be Visible or Collapsed
        /// </summary>
        Visibility CalculatedVisibility
        {
            get
            {
                if (TimerStrip == null)
                    return Visibility.Collapsed;
                if (IsHot)
                    return Visibility.Visible;
                if (Index == 0 && VMTimer.VMTimers.Count == 1)
                    return Visibility.Visible;
                return Visibility.Collapsed;
            }
        }
        #endregion CalculatedVisibility

        #region    MakeMeHot
        /// <summary>
        /// MakeHot is used to say that a particular TimerControlButton, timer strip etc is hot
        /// </summary>
        public void MakeMeHot()
        {
            TimerStrip.MakeMeHot();
        }
        #endregion MakeMeHot

        #region    Index
        /// <summary>
        /// Index 0 to 3 of the TimerControlButton on this TimerStrip, 0 = leftmost
        /// </summary>
        public int Index { get; set; } = 0;
        #endregion Index

        #region    IsHot
        /// <summary>
        /// Has this part of the TimerStrip that has focus. Unless it can see all the way 
        /// to the 
        /// </summary>
        bool IsHot => VMTimer == null ? false : VMTimer.IsHot;
        #endregion IsHot

        #region    IsPaused
        /// <summary>
        /// Is this TimerStrip paused
        /// </summary>
        public bool IsPaused => VMTimer == null ? true : VMTimer.IsPaused;
        #endregion isPaused

        #region    PropertyChanged PropertyChangedEventHandler 
        /// <summary>
        /// Delegate for when one of the four TimerControlButtons changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion PropertyChanged PropertyChangedEventHandler 

        #region    TimerStrip
        /// <summary>
        /// The TimerStrip that owns this button
        /// </summary>
        TimerStrip TimerStrip;
        #endregion TimerStrip

        #region    UriSource
        /// <summary>
        /// Set the image that appears on each TimerControlButton for Delete, Reload, Pause/Resume, Pointer/Edit
        /// </summary>
        public string UriSource
        {
            get
            {
                if (_UriSource == null)
                {
                    if (IsPaused)
                        _UriSource = BitmapImageUrisWhenPaused[Index];
                    else
                        _UriSource = BitmapImageUrisWhenCounting[Index];
                }
                return _UriSource;
            }
            set // note that UriSource = null does NOT set it to null
            {
                string uri;
                if (IsPaused)
                    uri = BitmapImageUrisWhenPaused[Index];
                else
                    uri = BitmapImageUrisWhenCounting[Index];
                if (uri == _UriSource)
                    return;

                _UriSource = uri;
                NotifyPropertyChanged();
            }
        }
        string _UriSource = null;
        #endregion UriSource

        #region    VMTimer (Set in Initialize)
        /// <summary>
        /// The ViewModel in control of this strip
        /// </summary>
        VMTimer VMTimer
        {
            get
            {
                if (TimerStrip == null)
                    return null;
                else
                    return TimerStrip.VMTimer;
            }
        }

        #endregion VMTimer (Set in Initialize)

        #endregion    Properties

        #region    Default public constructor 
        /// <summary>
        /// Default public constructor
        /// </summary>
        public TimerControlButton()
        {
            /* Can't use trace via VMTimer as the link has not been set up */

            InitializeComponent();
        }
        #endregion Default public constructor

        #region    Initialise  
        /// <summary>
        /// allow access to the associated TimerStrip and VMTimer
        /// </summary>
        public void Initialise(TimerStrip timerStrip)
        {
            TimerStrip = timerStrip;
            VMTimer.Trace($"Entry&Exit");
        }
        #endregion Initialise

        #region    NotifyPropertyChanged
        /// <summary>
        /// Update the UI as x:Bind has changed
        /// </summary>
        /// <param name="propertyName"></param>
        public void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            VMTimer.Trace($"Entry&Exit {propertyName ?? null}");
            if (PropertyChanged != null)
            {
                var args = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, args);
            }
            else
            { }
        }
        #endregion NotifyPropertyChanged


        #region    Events

        #region    Button_Tapped
        /// <summary>
        /// One of the four timer control button has been clicked. Work
        /// out what it does and why.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        //private void TimerControlButton_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        private void Button_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            VMTimer.Trace($"Entry {ActivityWhenPressed}");
            TimerControlButton timerControl = sender as TimerControlButton;
            switch (ActivityWhenPressed) // yyy I think we need an Activity enum
            {
                case Activity.Edit:
                    TimerStrip.VMTimer.EditTimerStrip();
                    break;
                case Activity.Delete: // Delete

                    if (VMTimer.VMTimers.Count > 1)
                        VMTimer.Phase = VMPhase.PhaseDead;
                    else
                        Sound.SayStatic("Sorry, you cannot delete the last timer");
                    break;

                case Activity.Reload: // Reset the timer but don't actually start it
                    VMTimer.Reload();
                    break;

                case Activity.PauseResumeToggle: // Flip between the paused and ticking state
                    if (IsPaused)
                        TimerStrip.VMTimer.Resume();
                    else
                        TimerStrip.VMTimer.Pause();
                    break;

                default:
                    Statics.InternalProblem("Index = " + Index);
                    break; // never gets here
            }
            VMTimer.Trace($"Exit");

        }
        #endregion Button_Tapped


        private void Button_FocusEngaged(Control sender, FocusEngagedEventArgs args)
        {
            MakeMeHot();
        }

        private void Button_FocusDisengaged(Control sender, FocusDisengagedEventArgs args)
        {

        }
        #endregion Events
    }

    #region Activity enum
    ///<summary>
    /// These are what the control buttons can do. Currently Play is not on the TimerStrip,
    /// 
    ///</summary> 
    public enum Activity
    {
        Edit,
        Delete,
        Reload,
        PauseResumeToggle,
        Play
    }
    #endregion Activity enum
}


