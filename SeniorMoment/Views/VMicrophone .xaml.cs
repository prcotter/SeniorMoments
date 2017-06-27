using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SeniorMoment.Services;
using SeniorMoment.ViewModels;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace SeniorMoment.Views
{
    public sealed partial class VMicrophone : UserControl, INotifyPropertyChanged
    {
        #region    Properties

        #region    BusyStatuses
        /// <summary>
        /// A list of MicrophoneStatuses  which indicated that the Microphone is
        /// busy so MSound => Sound cannot play anything
        /// </summary>
        List<MicrophoneStatus> BusyStatuses = new List<MicrophoneStatus>
        {
             MicrophoneStatus.Pressed,
             MicrophoneStatus.Released,
             MicrophoneStatus.Entered,
        };
        #endregion BusyStatuses

        #region    IsBusy
        /// <summary>
        /// Says whether the Microphone is active in which 
        /// case MSound & Sound must not initiate Play / Record / Say.
        /// However VMicrophone can initiate Play, Say and Record 
        /// </summary>
        public bool IsBusy => BusyStatuses.Contains(MicrophoneStatus);
        #endregion IsBusy

        #region    MicrophoneBackgroundOpacity
        /// <summary>
        /// Brightness of the Microphone. It is dimmed if we are recording or playing back as a visual
        /// cue that it is disabled
        /// </summary>
        double MicrophoneBackgroundOpacity
        {
            get
            {
                switch (MicrophoneStatus)
                {
                    case MicrophoneStatus.Idle: return 0.7;
                    case MicrophoneStatus.Entered: return 1.0;
                    case MicrophoneStatus.Pressed: return 0.0;
                    case MicrophoneStatus.Released: return 0.7;
                    case MicrophoneStatus.PostRelease: return 1.0;
                    default: throw new LogicException($"{MicrophoneStatus}");
                };
            }
        }

        #endregion MicrophoneBackgroundOpacity

        #region    MicrophoneBackgroundBrush & static SolidColorBrushes for VMicrophone
        /// <summary>
        /// Microphone background LightGreen on entry, Green when pressed until recording starts.
        /// Then Red while recording. At all other times it is Transparent, unless it is disabled
        /// </summary>
        SolidColorBrush MicrophoneBackgroundBrush => BrushPressed;
        //{
        //    get
        //    {
        //        switch (MicrophoneStatus)
        //        {
        //            case MicrophoneStatus.Idle: return BrushTransparent;
        //            case MicrophoneStatus.Entered: return BrushReadyToRecord;
        //            case MicrophoneStatus.Pressed: return BrushRecording;
        //            case MicrophoneStatus.Released: return BrushRecording;
        //            case MicrophoneStatus.PostRelease: return BrushReadyToRecord;
        //            default: throw new LogicException($"{MicrophoneStatus}");
        //        }
        //    }
        //}
        static SolidColorBrush BrushTransparent = new SolidColorBrush(Colors.Transparent);
        static SolidColorBrush BrushReadyToRecord = new SolidColorBrush(Colors.LightGreen);
        static SolidColorBrush BrushPressed = new SolidColorBrush(Colors.Green);
        static SolidColorBrush BrushRecording = new SolidColorBrush(Colors.Red);
        static SolidColorBrush BrushDisabled = new SolidColorBrush(Colors.LightGray);
        #endregion MicrophoneBackgroundBrush & static SolidColorBrushes for VMicrophone

        #region    MicrophoneBorderIsHitTestVisible
        /// <summary>
        /// We enable IsHitTestVisible when we are playing back a recording. We do not permit a recording
        /// during a playback. We also delay playbacks during a recording
        /// </summary>
        bool MicrophoneBorderIsHitTestVisible
        {
            get
            {
                switch (MicrophoneStatus)
                {
                    case MicrophoneStatus.Idle:
                    case MicrophoneStatus.Entered:
                    case MicrophoneStatus.PostRelease:
                        {
                            switch (Sound.SoundStatusStatic)
                            {
                                case SoundStatus.Recording:
                                case SoundStatus.Idle:
                                    return true;

                                case SoundStatus.Reserved:
                                case SoundStatus.Playing:
                                    return false;

                                default: throw new LogicException($"{MicrophoneStatus}");
                            }
                        }
                    case MicrophoneStatus.Pressed:
                    case MicrophoneStatus.Released:
                        return true;
                    default: throw new LogicException($"{MicrophoneStatus}");
                }
            }
        }
        #endregion MicrophoneBorderIsHitTestVisible

        #region    MicrophoneOpacity
        /// <summary>
        /// Microphone background turns grey when Recording
        /// </summary>
        double MicrophoneOpacity
        {
            get
            {
                switch (MicrophoneStatus)
                {
                    case MicrophoneStatus.Idle: return .7;
                    case MicrophoneStatus.Entered: return 1.0;
                    case MicrophoneStatus.Pressed: return 0.0;
                    case MicrophoneStatus.Released: return 0.7;
                    case MicrophoneStatus.PostRelease: return 1.0;
                    default: throw new LogicException($"{MicrophoneStatus}");
                }
            }
        }
        #endregion MicrophoneOpacity

        #region    MicrophoneStatus
        /// <summary>
        /// The status of the Microphone. This affects the how it looks in terms of Background,
        /// Opacity, Enabled, IsHitTestEnabled
        /// </summary>
        // yyy public MicrophoneStatus MicrophoneStatus => VMMicrophone.MicrophoneStatusStatic;
        public MicrophoneStatus MicrophoneStatus => MicrophoneStatus.Idle;
        #endregion MicrophoneStatus

        #region    This
        /// <summary>
        /// Pointer to VMicrophone as there is only one of these
        /// </summary>
        public static VMicrophone This { get; private set; }
        #endregion This

        #region    VMMicrophone
        /// <summary>
        /// There is a double link between the on-to-one classes VMMicrophone and VMicrophone
        /// </summary>
        VMMicrophone VMMicrophone;
        #endregion VMMicrophone

        #endregion Properties

        #region    Constructor default
        /// <summary>
        /// Default (and only) constructor
        /// </summary>
        public VMicrophone()
        {
            try
            {
                if (_ctor)
                    throw new LogicException("Only 1 VMicrophone permitted");
                this.InitializeComponent();

                _ctor = true;
                This = this;
                //  xxx This.Initialise();
                /* Create the thing which will look after the Microphone */
            }
            catch (Exception e)
            {
                throw new LogicException("find design time error", e);
            }
        }
        #region    _ctor (static)
        /// <summary>
        /// Ensures only one VMicrophone Exists as there is only one Microphone
        /// </summary>
        static bool _ctor = false;
        #endregion _ctor (static)

        #endregion Constructor()

        #region    Initialise
        /// <summary>
        /// Since it is created in the Xaml and must have a default constructor
        /// I figured I would need an Initialise. So far I haven't
        /// </summary>
        public void Initialise()
        {
            Trace($"Entry");
            VMMicrophone = new VMMicrophone(this);
            Trace($"&Exit");
        }
        #endregion Initialise



        #region    Binding stuff: PropertyChangedEventHandler, NotifyPropertyChanged

        #region    PropertyChanged PropertyChangedEventHandler
        /// <summary>
        /// Part of the method of telling the UI to update itself via Bindings
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion PropertyChanged PropertyChangedEventHandler

        #region    NotifyPropertyChanged
        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        public void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                var args = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, args);
            }
            else
            { }
        }
        #endregion NotifyPropertyChanged 

        #endregion Binding stuff: PropertyChangedEventHandler, NotifyPropertyChanged

        #region    Events

        #region    MicrophoneBorder_PointerPressed
        /// <summary>
        /// User has Pressed the microphone. Then the following sequence happens
        /// <para/> The background turns Red
        /// <para/> A voice uncannily like mine says "Recording"
        /// <para/> There is a beep
        /// <para/> It starts recording
        /// <para/> If the user releases the button before the beep then DefaultBeep.wav is used as the alarm
        /// <para/> If the user releases after the beep but within two seconds of the beep the recording is cancelled.
        /// <para/> After 11 seconds there is a long beep, the recording is stopped and used  as the alarm
        /// <para/> If the user releases before 10 seconds then the recording is stopped and used as the alarm
        /// <para/> In all the above cases if the alarm is not cancelled it is replayed back to the user.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MicrophoneBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Trace($"Entry");

            VMMicrophone.MicrophoneStatusStatic = MicrophoneStatus.Pressed;
            VMTimer.HotVMTimer.RecordPressed();
            UpdateMicrophone();
            Trace($"Exit");

            //MicrophoneTimer = MTimer.CreateMTimer(
            //    secondsToZero: 12,
            //    mTickTock: TickTock,
            //    mTimerStatus: MTimerStatus.Counting,
            //    repeats: 0,
            //    callbackOnTick: null,
            //    callbackOnZero: null,
            //    callbackOnNewPhase: null,
            //    callbackOnNextAlarm: null,
            //    extraCallbacks: new List<(int secondsToZero, Action action)>

            //MicrophoneTimer = new IntervalTimer( //
            //        intervals: 12,
            //        owner: null,
            //        mTickTock: TickTock,
            //        intervalTimerStatus: IntervalTimerStatus.Counting,
            //        callbackOnTick: null,
            //        uniqueId: -1,
            //        extraCallbacks: new List<(int secondsToZero, Action action)>
            //        {
            //                (02,()=>recordingStatus=RecordingStatus.OK),
            //                (10,TooLong),
            //                (11,SayTooLong)
            //        });

        }
        #endregion MicrophoneBorder_PointerPressed

        #region    MicrophoneBorder_PointerReleased
        /// <summary>
        /// Depending on when the pointer is released we will do the following
        /// <para/> Within two seconds (ie before the beep) use Default.Wav
        /// <para/> Within two seconds after beep, tell user recording was too short - using the beep
        /// <para/> Up to user releasing Microphone before timeout - use the recording
        /// <para/> After timeout use the truncated recording
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MicrophoneBorder_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            Trace($"Entry");
            VMMicrophone.MicrophoneStatusStatic = MicrophoneStatus.Released;
            VMTimer.HotVMTimer.RecordReleased();
            UpdateMicrophone();
            Trace($"Exit");
        }
        #endregion MicrophoneBorder_PointerReleased

        #region    MicrophoneBorder_PointerEntered
        /// <summary>
        /// Pointer has entered the microphone, indicate there is a record option 
        /// by chaining the background
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MicrophoneBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Trace($"Entry");
            VMMicrophone.MicrophoneStatusStatic = MicrophoneStatus.Entered;
            VMTimer.HotVMTimer.RecordEntered();
            UpdateMicrophone();
            Trace($"Exit");

        }
        #endregion MicrophoneBorder_PointerEntered

        #region    MicrophoneBorder_PointerExited
        /// <summary>
        /// The user has vacated the microphone so return the image to normal
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MicrophoneBorder_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            Trace($"Entry");
            VMMicrophone.MicrophoneStatusStatic = MicrophoneStatus.PostRelease;
            VMTimer.HotVMTimer.RecordReleased();
            UpdateMicrophone();
            Trace($"Exit");

        }
        #endregion    MicrophoneBorder_PointerExited

        #endregion Events

        #region    Trace() xxx
        /// <summary>
        /// Write to a trace file or the console
        /// </summary>
        /// <param name="info">Entry Exit or 'whatever' data to be traced</param>
        /// <param name="member">[CallerMemberName]</param>
        /// <param name="line">[CallerLineNumber]</param>
        /// <param name="path">[CallerFilePath]</param>
        [System.Diagnostics.DebuggerStepThrough()] // never want to go into Trace unless debugging Tracer
        public void Trace(string info,
                            [CallerMemberName]string member = "",
                            [CallerLineNumber] int line = 0,
                            [CallerFilePath] string path = "")
        {
            Tracer.TracerMain.TraceCrossTask(info, member, line, path);
        }
        #endregion Trace()

        #region    UpdateMicrophone xxx
        /// <summary>
        /// Something has changed in terms of recording so we update the microphone image
        /// </summary>
        public void UpdateMicrophone()
        {
            NotifyPropertyChanged("MicrophoneBackgroundBrush");
            NotifyPropertyChanged("MicrophoneBackgroundOpacity");
            NotifyPropertyChanged("MicrophoneBorderIsHitTestVisible");
            // NotifyPropertyChanged("");
        }
        #endregion UpdateMicrophone
    }
}
