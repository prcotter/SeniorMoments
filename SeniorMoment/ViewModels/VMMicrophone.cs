using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SeniorMoment.Models;
using SeniorMoment.Services;
using SeniorMoment.Views;
using Windows.UI.Xaml.Input;

namespace SeniorMoment.ViewModels
{
    public class VMMicrophone : ITick, IIntervalTimerOwner // xxx ,IVBase<VMicrophone>
    {
        #region    Properties

        #region    busyStatusesStatic
        /// <summary>
        /// A list of MicrophoneStatuses  which indicated that the Microphone is
        /// busy so MSound => Sound cannot play anything
        /// </summary>
        static List<MicrophoneStatus> busyStatusesStatic = new List<MicrophoneStatus> 
        {
             MicrophoneStatus.Pressed,
             MicrophoneStatus.Released,
             MicrophoneStatus.Entered,
        };
        #endregion busyStatusesStatic

        #region    IntervalTimerName
        /// <summary>
        /// Required by IIntervalTimer. Used in ToString and useful in debug
        /// </summary>
        /// <returns></returns>
        public string IntervalTimerName() => $"VMMicrophone";
        #endregion IntervalTimerName

        #region    IsBusy =>
        /// <summary>
        /// Says whether the Microphone is active in which 
        /// case MSound & Sound must not initiate Play / Record / Say.
        /// However VMicrophone can initiate Play, Say and Record 
        /// </summary>
        public bool IsBusy => busyStatusesStatic.Contains(MicrophoneStatusStatic);
        #endregion IsBusy

        #region    MicrophoneStatusStatic
        /// <summary>
        /// The appearance of the microphone is bound to the MicrophoneStage.
        /// This changes from Idle to something else.
        /// </summary>
        public static MicrophoneStatus MicrophoneStatusStatic = MicrophoneStatus.Idle;
        #endregion MicrophoneStatusStatic)

        #region    MicrophoneTimer gps
        /// <summary>
        /// 
        /// </summary>
        public IntervalTimer MicrophoneTimer { get; private set; }
        #endregion MicrophoneTimer

        #region    recordingStatusStatic
        /// <summary>
        /// Status of a recording. These are:
        /// <para/>
        /// </summary>
        static RecordingStatus recordingStatusStatic = RecordingStatus.None;
        #endregion recordingStatusStatic

        #region    Sound
        /// <summary>
        /// Used to record
        /// </summary>
        Sound Sound = new Sound("VMMicrophone");
        #endregion Sound

        #region    This (static)
        /// <summary>
        /// Pointer to VMMicrophone as there is only one of these
        /// </summary>
        public static VMMicrophone This { get; private set; }
        #endregion This

        #region    VMicrophone g
        /// <summary>
        /// Handy-dandy reference pointer. Chain pointers between VMicrophone and VMMicrophone,
        /// Quick definition of their duties. VMicrophone is in charge of how Microphone looks
        /// and whether it is enabled etc
        /// </summary>
        public VMicrophone VMicrophone { get; private set; }
        #endregion VMicrophone

        #endregion Properties

        #region    Constructor()
        /// <summary>
        /// Default (and only) constructor. This is the View for Microphone in MainPageXaml. The
        /// Microphone will change Enabled, IsHitTestVisible, Opacity and Background via binding.
        /// </summary>
        public VMMicrophone(VMicrophone vMicrophone)
        {
            try
            {   // yyy trying to find a design time bug in VMicrophone
                if (_ctor)
                    throw new LogicException("Only 1 VMicrophone permitted");
                _ctor = true;
                VMicrophone = vMicrophone;
                This = this;
            }
            catch (Exception e)
            {
                throw new LogicException("find design time error", e);
            }
        }
        #region    _ctor (static)    
        /// <summary>
        /// Used to ensure only one VMicrophone exists as there is only one Microphone. 
        /// On advice via Google this is not a Singleton
        /// </summary>
        static bool _ctor = false;
        #endregion _ctor  (static) 

        #endregion Constructor()

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
        private void MicrophonePressed()
        {
            MicrophoneTimer = new IntervalTimer(
                    owner: this,
                    intervalsToZero: 12,
                    callbackOnTick: null,
                    tickTock: TickTock.This,
                    intervalTimerStatus: IntervalTimerStatus.Counting,
                    timedCallbacks: new List<(int secondsToZero, Action action)>
                    {
                            (02,RecordingStatusOK),
                            (10,TooLong),
                            (11,SayTooLong)
                    });

            VMTimer.HotVMTimer.RecordPressed();
        }
        #endregion MicrophoneBorder_PointerPressed

        #region    RecordingStatusOK
        /// <summary>
        /// Used purely to simplify the constructor so I don't have...
        /// <para/> (02,new Action(()=>recordingStatus = RecordingStatus.OK))
        /// </summary>
        void RecordingStatusOK()
        {
            VMMicrophone.recordingStatusStatic = RecordingStatus.OK;
        }
        #endregion RecordingStatusOK

        #region    TooLong
        /// <summary>
        /// User has warbled on till the Recording has timed-out. Stop the Recording and 
        /// tell the user the options.
        /// </summary>
        void TooLong()
        {
            Sound.StopRecording();
        }
        #endregion TooLong

        #region    SayTooLong
        /// <summary>
        /// Speak to user saying the recording they made is too long / incomplete
        /// </summary>
        void SayTooLong()
        {
            QueuedTaskTooLong = new QueuedTask(
                    actionQT: new Action<QueuedTask>(TooLongQueued),
                    parameters: null,
                    name: "VMMicrophone",
                    task: null,
                    priority: 25,
                    callbackOnStarted: null,
                    callbackOnEnded: null
                    );

            Statics.TaskFunnel.MeNext(QueuedTaskTooLong);
        }
        #endregion SayTooLong

        #region    TooLongQueued
        /// <summary>
        /// When the user records a message and it goes over the max length then TooLong() is called
        /// which in turn calls this. This because the callbacks are Action and not Action{} or
        /// Func{}
        /// </summary>
        /// <param name="qt"></param>
        void TooLongQueued(QueuedTask qt)
        {
            MSound mSound = VMTimer.HotVMTimer.MSound;
            mSound.Sound.RecordedEventHandler += ValidateRecording;
            var soundAction1 = new SoundAction("PlayFilename", new List<Object> { "StartRecordingBeep.wav", 0.5 });
            var soundAction2 = new SoundAction("Say", new List<object> { "Sorry, maximum recording length is 10 seconds, otherwise the alarms may be congested; Playing back" });
            var soundAction3 = new SoundAction("PlayFile", new List<object> { mSound.Sound.SoundFile });
            soundAction1.ContinueWith(soundAction2);
            soundAction2.ContinueWith(soundAction3);
            mSound.Sound.RunNextSoundAction(soundAction1);
        }

        /// <summary>
        /// This is to check if the recording is too short or too long
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ValidateRecording(object sender, QueuedTask e) => throw new NotImplementedException();
        #endregion TooLongQueued

        #region    QueuedTaskTooLong
        /// <summary>
        /// When the user has waffled on for more than 10 seconds we have stopped the recording
        /// and Queue a Task in TaskFunnel to say so
        /// </summary>
        QueuedTask QueuedTaskTooLong = null;
        #endregion SayTooLong

        #region    RecordReleased
        /// Depending on when the pointer is released we will do the following
        /// <para/> Within two seconds (ie before the beep) use Default.Wav
        /// <para/> Within two seconds after beep, tell user recording was too short - using the beep
        /// <para/> Up to user releasing Microphone before timeout - use the recording
        /// <para/> After timeout use the truncated recording
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecordReleased(object sender, PointerRoutedEventArgs e)
        {
            Trace($"Entry");
            VMTimer.HotVMTimer.RecordReleased();
            Trace($"Exit");
        }
        #endregion MicrophoneBorder_PointerReleased

        #region    RecordEntered
        /// <summary>
        /// Pointer has entered the microphone, indicate there is a record option 
        /// by chaining the background
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecordEntered(object sender, PointerRoutedEventArgs e)
        {
            Trace($"Entry");
            VMTimer.HotVMTimer.RecordEntered();
            Trace($"Exit");
        }
        #endregion RececordEntered

        #region    RecordExited
        /// <summary>
        /// The user has vacated the microphone so return the image to normal
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecordExited(object sender, PointerRoutedEventArgs e)
        {
            Trace($"Entry");
            VMTimer.HotVMTimer.RecordExited();
            Trace($"Exit");
        }
        #endregion    RecordExited

        #region    Tick
        /// <summary>
        /// required as base.tick() is abstract.
        /// </summary>
        public void Tick() { }
        #endregion Tick

        #region    ToString
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override String ToString() => $"VMMicrophone {recordingStatusStatic}";
        #endregion ToString

        #region    Trace
        /// <summary>
        /// Overridden Trace() which uses the statics Tracer.TracerMain. It 'will' exist but
        /// rather than ensuring it exists, we will throw the Trace() away if it doesn't
        /// </summary>
        /// <param name="info">Entry Exit or 'whatever' data to be traced</param>
        /// <param name="member">[CallerMemberName]</param>
        /// <param name="line">[CallerLineNumber]</param>
        /// <param name="path">[CallerFilePath]</param>
        public void Trace(string info,
                 [CallerMemberName] string member = "", [CallerLineNumber] int line = 0, [CallerFilePath] string path = "")
        {
            Tracer.TracerMain.TraceCrossTask(info + $" status:{MicrophoneStatusStatic}", member, line, path);
        }
        #endregion Trace

        #region    UpdateMicrophone
        /// <summary>
        /// Something has changed in terms of recording so we update the microphone image
        /// </summary>
        public void UpdateMicrophone() => VMicrophone.UpdateMicrophone();
        //{
        //    NotifyPropertyChanged("MicrophoneBackgroundBrush");
        //    NotifyPropertyChanged("MicrophoneBackgroundOpacity");
        //    NotifyPropertyChanged("MicrophoneBorderIsHitTestVisible");
        //    // NotifyPropertyChanged("");
        //}

        #endregion UpdateMicrophone
    }

    #region    MicrophoneStatus
    /// <summary>
    /// The stages the microphone goes through when it is pressed and released to
    /// start a recording and when it is pressed and released to stop it. 
    /// There is no ReleasedToStopRecording as that is Microphone.Idle
    /// </summary>
    public enum MicrophoneStatus
    {
        Idle,
        Entered,
        Pressed,
        Released,
        PostRelease,
    }
    #endregion MicrophoneStatus
}



