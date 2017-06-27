using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SeniorMoment.Services;
using SeniorMoment.ViewModels;
using Windows.Storage;

namespace SeniorMoment.Models
{
    /// <summary>
    /// class that represents the voice or default sound recording. yyy lots of comments needed here
    /// it starts a microphone recording and if it is long enough it will be
    /// played on "time's up". If it's not long enough the default sound is used (or will be soon ???).
    /// The frequency of the alarm is dictated by the Phase.
    /// </summary>
    public class MSound : IEquatable<MSound>
    {
        #region    Properties -----------------------------------------------

        #region    CallbackOnStarted
        /// <summary>
        /// Callback when the sound starts playing
        /// </summary>
        public Action CallbackOnStarted { get; private set; }
        #endregion CallbackOnStarted

        #region    CallbackOnEnded
        /// <summary>
        /// Callback when the sound finishes playing
        /// </summary>
        public Action CallbackOnEnded { get; private set; }
        #endregion CallbackOnEnded

        #region    IsAlarming
        /// <summary>
        /// Set true when the alarm goes off. We can internally or user-driven (at some future time) silence this
        /// alarm by setting this false
        /// alarm.
        /// </summary>
        public bool IsAlarming;
        #endregion IsAlarming

        #region    localFolder (static)
        /// <summary>
        /// Where this app is allowed to persist data. This done for all
        /// users on this computer
        /// </summary>
        static StorageFolder LocalFolder { get => Statics.LocalFolder; }
        #endregion localFolder (static)

        #region    MAX_RECORDING_SECONDS (static)
        /// <summary>
        /// When we are recording we are going to max out at MAX_RECORDING_SECONDS seconds.
        /// This happens on Tick()
        /// </summary>
        const int MAX_RECORDING_SECONDS = 15;
        #endregion MAX_RECORDING_SECONDS (static)

        #region    MicrophoneStatus
        /// <summary>
        /// The appearance of the microphone is bound to the MicrophoneStatus.
        /// This changes from Idle to something else and eventually back to Idle
        /// </summary>
        MicrophoneStatus MicrophoneStatus => ViewModels.VMMicrophone.MicrophoneStatusStatic;
        #endregion microphoneStage

        #region    QueuedTask
        /// <summary>
        /// Passed through from PlayFiles => OnMSoundPlayed
        /// </summary>
        public QueuedTask QueuedTask { get; private set; }
        #endregion QueuedTask

        #region    Sound
        /// <summary>
        /// MSound controls the when and the where, Sound does the actual Recording and Playing
        /// </summary>
        public Sound Sound { get; private set; }
        #endregion Sound

        #region    SoundFilename
        /// <summary>
        /// The name of the StorageFile which is written to the recordings folder.
        /// It only has any meaning if the user has recorded on the Microphone.
        /// </summary>
        public string SoundFilename { get; private set; }
        #endregion SoundFilename

        #region    SoundStatus
        /// <summary>
        /// Return what state Sounds are in. There is only one static SoundStatus
        /// even though there are multiple Sounds. No Sound can be Played or Recorded at
        /// a time.
        /// </summary>
        public SoundStatus SoundStatus => Sound.SoundStatusStatic;
        #endregion SoundStatus

        #region    Name
        /// <summary>
        /// The name of the StorageFile which is written to the recordings folder.
        /// It only has any meaning if the user has recorded on the Microphone.
        /// </summary>
        public string Name => $"Recording_{UniqueId}";
        #endregion Name

        #region    UniqueId
        /// <summary>
        /// The 'unique' id is shared by the VMTimer, this MSound and the MTimer
        /// as there is a one-to-one relationship between them. It is used to generate
        /// the recording name.
        /// </summary>
        public int UniqueId { get => VMTimer.UniqueId; }
        #endregion UniqueId

        #region    VMTimer
        /// <summary>
        /// The VMTimer that created this MSound. The MSound can be passed on to a new
        /// VMTimer if the user presses Reload which (s)he thinks of as Reset
        /// </summary>
        VMTimer VMTimer;
        #endregion VMTimer

        #endregion Properties -----------------------------------------------

        #region    ---------------------COMMENTARY ABOUT SCHEDULING SOUNDS-----------
        /*
         * Lets see how all this scheduling etc fits together. I decided TaskFunnel is male.
         * 
         * 0.   An Axiom: There is no Sound=>MSound methods or properties. (No using SeniorMoment.Models)
         *      MSound=>Sound is of course OK. MSound knows it is an alarm. To Sound it
         *      is just another sound to play / text to say or a record. As such he
         *      has no idea about Repeats. Repeats is an aspect of the scheduler, MTimer. So
         *      that's where it is kept.
         * 1.   The user presses 'Go' for a timer
         *      The initial value of two countdowns SecondsToZero and SecondsToAlarm
         *      are the same and are set by the user (say 5 minutes = 300 seconds)
         * 2.   The countdown is watched on Tick by MTimer and continues till it reaches zero.
         *      From this point the only important countdowns are SecondsToNextAlarm 
         *      and SecondsToPhaseChange. SecondsToZero continues into negative territory.
         *      It is now measuring how long the Alarm has been running. It is used as part
         *      of the SortKey for re-arranging the VMTimers/TimerStrips
         * 3.   There is a callback from MTimer to VMTimer
         *      VMTimer tells MSound to play his alarm signal (one MSound per MTimer per VMTimer)
         *      MSound Creates an Action to play the MSound alarm
         *      This Action is passed to TaskFunnel.
         *      It is TaskFunnels job to ensure only one Recording and/or playback
         *      happens at once.
         *      TaskFunnel adds the Action to his ConcurrentQueue and then forgets about it.
         *      TaskFunnel sorts the ConcurrentQueue by Age within
         *      TaskFunnel checks if he IsBusy() and if he is he exits.
         *      If !IsBusy() TaskFunnel pops the next Action off the stack.
         *      There are two possibilities....
         *          TaskFunnel has to wait for completion.
         *          TaskFunnel Fires and Forgets
         *      JIC The next Tick will reawaken TaskFunnel to check he is not busy and if there
         *      is anything to do on the Stack. 'Normally' there is nothing.
         *      When TaskTunnel picks up the next Action, which is usually PlayAlarm.
         *      TaskFunnel first calls the 'before the alarm' callback.
         *      
         *      TaskFunnel converts the Action into a Task.
         *      TaskFunnel bolts 1 or 2 Tasks onto the aforementioned task
         *      The first is an "after the alarm" callback
         *      The second callback is to notify the caller that everything is finished.
         *      If it is a wait-for-it task the Notify is not needed as we know
         *      in the 'after the alarm" callback that the tasks have all finished.
         *      ie. it has already finished.
         *      
         */
        #endregion ---------------------COMMENTARY ABOUT SCHEDULING SOUNDS-----------

        #region    Constructor (VMTimer vmTimer) 
        /// <summary>
        /// The object that records and plays back sound
        /// </summary>
        /// <param name="vmTimer"></param>
        public MSound(VMTimer vmTimer) : base()
        {
            VMTimer = vmTimer;
            Trace($"Entry {vmTimer.ToString()}");
            Sound = new Sound($"Recording_{UniqueId}");
            //Sound.PlayedEventHandler += OnPlayedMSound; // zzz
            //Sound.RecordedEventHandler += OnRecordedMSound;
            Sound.SaidEventHandler += OnSaidMSound;
            Trace($"Exit  {vmTimer.ToString()}");
            return;
        }
        #endregion Constructor (VMTimer vmTimer)

        #region    BlastOff
        /// <summary>
        /// MTimer.SecondsToZero has reached zero. This is called once so it is time
        /// </summary>
        public void BlastOff()
        {
            IsAlarming = true;
            PlayAlarm();
        }
        #endregion BlastOff

        #region    Equals(MSound)) 
        /// <summary>
        /// Override of Equals() which compares
        /// </summary>
        bool IEquatable<MSound>.Equals(MSound other)
        {
            if (other as MSound == null)
                return false;
            return base.Equals(other);
        }
        #endregion Equals(MSound)

        #region    GoodWhenever
        /// <summary>
        /// Say Good Morning, Evening or Afternoon
        /// </summary>
        static public void GoodWhenever()
        {
            Tracer.TracerMain.TraceCrossTask($"Entry");
            int hour = DateTime.Now.Hour;
            string timeWord;

            if (hour < 12)
                timeWord = "morning";
            else if (hour < 18)
                timeWord = "afternoon"; // When awful darkness and silence reign, over the great Gromboolean plain. When the angry breakers roar as they beat on the rocky shore";
            else
                timeWord = "evening";

            SoundAction action = new SoundAction("say", new List<Object> { $"Good {timeWord} Paul." });
            Sound.SoundStatic.RunNextSoundAction(action);
            Tracer.TracerMain.TraceCrossTask($"Exit  {timeWord}");
            return;
        }
        #endregion GoodWhenever

        #region    PlayAlarm
        /// <summary>
        /// MTimer has decided it's time to play the alarm. He will keep on doing that until
        /// the VMTimer times out (from Phase.Phases), the user deletes the alarm, the user
        /// pauses the alarm or the user Reloads the alarm.
        /// We are in a 'loop', playing an alarm and then rescheduling it to 
        /// play again after a wait depending on the current Phase for this VMTimer.
        /// MTimer.SecondsToAlarm==0 => MSound.PlayAlarm => PlayAlarmCallback =>
        /// MSound.PlayAlarm => PlayAlarmCallback => MSound.PlayAlarm => etc
        /// </summary>
        /// <returns>false if it could not play the alarm</returns>
        internal void PlayAlarm(QueuedTask qt = null)
        {
            if (qt == null)
            {
                qt = new QueuedTask(
                    actionQT: new Action<QueuedTask>(PlayAlarm),
                    parameters: null,
                    name: Name,
                    task: null,
                    priority: 110,
                    callbackOnStarted: null,
                    callbackOnEnded: null
                    );
            }

            if (!IsAlarming)
            {
                Trace($"Entry&Exit alarm off");
                return;
            }

            if (qt.Priority > 30)
                qt.Priority -= 5;
            /*
             * If the microphone is not idle we cannot Record or Play
             */
            var vmMicrophone = VMMicrophone.This;
            if (vmMicrophone.IsBusy)
            {
                Trace($"Entry status:{Sound.SoundStatusStatic} => TaskFunnel");
                Statics.TaskFunnel.MeNext(qt);
                Trace($"Exit  status:{Sound.SoundStatusStatic} => TaskFunnel");
                return;
            }
            /* set up event handler so it has two callbacks. one for the 'played any sound'
            * and one for the 'played alarm'. I know the following two lines look likely to cause an
            * exception but the compiler effectively inserts a 'new EventHandler()' before the +=
            * if it doesn't already exist. A second "+=" doesn't recreate the EventHandler, just
            * adds the next delegate. (ie multicast) 
            */
            Trace($"Entry qt={qt.ToString()}");
            Sound.PlayedEventHandler = null; // clears the event wire-ups.
            Sound.PlayedEventHandler += OnPlayedMSound;
            Sound.PlayedEventHandler += OnPlayedAlarm;
            Sound.PlayFile(
                Sound.SoundFile,
                volume: VMTimer.Phase.Volume,
                priority: VMTimer.Phase.Priority);

            Trace($"Exit qt={qt.ToString()}");
            return;
        }
        #endregion PlayAlarm

        #region    RecordUser
        /// <summary>
        /// Finger off the microphone button. Start the recording
        /// 
        /// </summary>
        internal void RecordUser() => Sound.RecordUser();
        #endregion RecordUser RecordUser
        
        #region    RecordingTooShortSoUseDefault
        ///// <summary>
        ///// If the user's recording is too short then we use the default
        ///// </summary>
        //void RecordingTooShortSoUseDefault()
        //{
        //    Trace($"Entry");

        //    Sound.PlayFilename("RecordingTooShort.m4a", Statics.AssetsFolder, 0.5);
        //    verifyStatus(SoundStatus.Idle);
        //    DateTime before = DateTime.Now;
        //    Sound.PlayFilename("Default.wav", Statics.AssetsFolder, 0.5);
        //    verifyStatus(SoundStatus.Idle);
        //    SoundFile = DefaultSoundFile;
        //    Trace($"Exit");
        //}
        #endregion RecordingTooShortSoUseDefault

        #region    Reload
        /// <summary>
        /// When the user hits the reload button (he thinks of it as restart) it is promulgated
        /// TaskControlButton=>TimerStrip=>VMTimer=>Here
        /// </summary>
        public void Reload()
        {
            Trace($"Entry&Exit");
            IsAlarming = false;
        }
        #endregion Reload

        #region    StopRecording 
        /// <summary>
        /// Recording has ceased either because of a timeout on the length of a 
        /// recording or the user has taken his finger off the Microphone Button
        /// </summary>
        public string StopRecording()
        {
            TraceStatic($"Entry");
            Sound.StopRecording();
            
            TraceStatic($"Exit");
            return "";
        }
        #endregion stopRecording

        #region    Tick
        /// <summary>
        /// Clock has ticked. 
        /// </summary>
        public void Tick()
        {
            Trace($"Entry&Exit does nothing");
        }
        #endregion Tick

        #region    ToString();
        /// <summary>
        /// Override of ToString() which returns
        /// </summary>
        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString()
        {
            return $"MSound: {Name}";
        }
        #endregion ToString()

        #region    Trace()
        /// <summary>
        /// Write to the trace file which is inside VMTimer (or the console)
        /// </summary>
        /// <param name="info">Entry Exit or 'whatever' data to be traced</param>
        /// <param name="member">[CallerMemberName]</param>
        /// <param name="line">[CallerLineNumber]</param>
        /// <param name="path">[CallerFilePath]</param>
        [System.Diagnostics.DebuggerStepThrough()]
        void Trace(string info, [CallerMemberName] string member = "", [CallerLineNumber] int line = 0, [CallerFilePath] string path = "")
        {
            VMTimer.Trace(info, member, line, path);
        }
        #endregion Trace()     

        #region    TraceStatic()
        /// <summary>
        /// Write to the first VMTimer trace files. Used for tracing static methods
        /// </summary>
        /// <param name="info">Information that is to be traced. If the first work is 
        /// "Entry", "Exit" or "Entry&Exit" then a note of this is made and
        /// Tracer tries to match up one "Exit" per "Entry", unless AllowCrossTaskTracing
        /// which allows mismatched Entries and Exits. Entry&Exit are already a matched pair</param>
        /// <param name="member">[CallerMemberName]</param>
        /// <param name="line">[CallerLineNumber]</param>
        /// <param name="path">[CallerFilePath]</param>
        [System.Diagnostics.DebuggerStepThrough()]
        static public void TraceStatic(string info, [CallerMemberName] string member = "", [CallerLineNumber] int line = 0, [CallerFilePath] string path = "")
        {
            var vmTimer = VMTimer.VMTimers.First();
            if (vmTimer.Tracer == null)
                return;
            if (Tracer.TraceOptions == TraceOptions.None)
                Statics.InternalProblem("TraceOptions is None");
            vmTimer.Tracer.TraceCrossTask(info, member, line, path);
        }
        #endregion TraceStatic()

        #region    Events

        #region    OnPlayedMSound 
        /// <summary>
        /// When Sound has finished a PlayPppp() this event is triggered from 
        /// MediaPlayer.OnMediaPlayed and MediaPlayer.OnMediaFinished
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPlayedMSound(object sound, QueuedTask queuedTask)
        {
            Trace($"Entry {queuedTask}");
            Statics.TaskFunnel.InformTaskFunnelEventCompleted();
            Trace($"Exit {queuedTask}");
        }
        #endregion OnPlayedMSound 

        #region    OnPlayedAlarm 
        /// <summary>
        /// When Sound has finished a PlayFile() for this an alarm then this event is triggered from 
        /// MediaPlayer.OnMediaPlayed and MediaPlayer.OnMediaFinished to reschedule the alarm
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPlayedAlarm(object sound, QueuedTask queuedTask)
        {
            Trace($"Entry ");

            VMTimer.RescheduleAlarm();
            Statics.TaskFunnel.InformTaskFunnelEventCompleted();

            Trace($"Exit ");
        }
        #endregion OnPlayedAlarm 

        #region    OnSaidMSound 
        /// <summary>
        /// When Sound has Finished a Play() this event is triggered from 
        /// MediaPlayer.OnMediaPlayed and OnMediaFinished
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSaidMSound(object sound, QueuedTask queuedTask)
        {
            Trace($"Entry {queuedTask}");
            Statics.TaskFunnel.InformTaskFunnelEventCompleted();
            Trace($"Exit {queuedTask}");

        }
        #endregion OnSaidMSound 

        #region    OnRecordedMSound 
        /// <summary>
        /// When Sound has Finished a Play() this event is triggered from 
        /// MediaPlayer.OnMediaPlayed and OnMediaFinished
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRecordedMSound(object sound, QueuedTask queuedTask)
        {
            Trace($"Entry {queuedTask}");
            Statics.TaskFunnel.InformTaskFunnelEventCompleted();
            Trace($"Exit {queuedTask}");
        }
        #endregion OnRecordedMSound 

        #endregion Events
    }

    #region    RecordingStatus ENUM
    /// <summary>
    /// Possible situations/outcomes when about to recording
    /// </summary>
    enum RecordingStatus
    {
        None,     // not recording yet or everything is finished
        TooShort, // There is a minimum length for recordings
        TooLong,  // and a maximum as well
        OK        // The Cinderella length 2=>10 seconds  
    }
    #endregion RecordingStatus ENUM

}
