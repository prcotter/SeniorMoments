using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SeniorMoment.Models;
using SeniorMoment.Services;
using SeniorMoment.Views;

namespace SeniorMoment.ViewModels
{
    /// <summary>
    /// ViewModel that ties a MTimer (Model) to an TimerStrip (View)
    /// </summary>
    public class VMTimer : BaseViewModel, IDisposable, IMTimerOwner
    {
        #region    Properties

        #region    CrossThreadTracer (static)
        /// <summary>
        /// Used as a Tracer by things that can't have a tracer of their own.
        /// We use the first one we can find in VMTimers
        /// </summary>
        static public Tracer CrossThreadTracer
        {
            get
            {
                if (VMTimers.Count == 0)
                    return null;
                return VMTimers[0].Tracer;
            }
        }
        #endregion CrossThreadTracer (static) 

        #region    TimerName
        /// <summary>
        /// As required by  IMTimerOwner
        /// </summary>
        /// <returns></returns>
        public string TimerName() => $"MTimer {UniqueId}";
        #endregion TimerName

        #region    HotTimer
        /// <summary>
        /// The associated TimeStrip got focus and is informing its parent VMTimer. We
        /// now consider this VMTime is spotlighted and the user is (optionally) 
        /// creating / deleting / modifying /changing the recording / pausing / starting / re-starting
        /// </summary>
        static internal VMTimer HotVMTimer = null;
        #endregion HotTimer

        #region    IsHot => this ==  VMTimer.HotVMTimer;
        /// <summary>
        /// Does this VMTimer contain the TimerStrip that was last in Focus
        /// </summary>
        public bool IsHot => this == VMTimer.HotVMTimer;
        #endregion IsHot =>  this == VMTimer.HotVMTimer;

        #region    IsPaused => MTimer.IsPaused;
        /// <summary>
        /// Returns true if the MTimer associated with this VMTimer is Paused
        /// </summary>
        public bool IsPaused => MTimer.IsPaused;
        #endregion IsPaused => MTimer.IsPaused;

        #region    IsTicking => MTimer.IsTicking
        /// <summary>
        /// Is The MTimer ticking away or has it been Pause()'d
        /// </summary>
        public bool IsTicking => MTimer.IsTicking;
        #endregion IsTicking

        #region    MSound   
        /// <summary>
        /// Each VMTimer has an associated MSound which is responsible for recording,
        /// maintaining and playing back either a voice input or a 'beep'
        /// </summary>
        public MSound MSound { get; private set; }
        #endregion MSound

        #region    MTimer
        /// <summary>
        /// The MTimer assigned to this VM
        /// </summary>
        public MTimer MTimer { get; private set; }
        #endregion MTimer
        
        #region    Name
        /// <summary>
        /// Name (for debug) which ties into VTimer's Name
        /// </summary>
        public new string Name
        {
            get
            {
                if (MTimer == null)
                    return "No MTimer";
                return "VM_" + MTimer.Name;
            }
        }
        #endregion Name

        #region    Phase
        /// <summary>
        /// The stage of the lifetime of a Timer
        /// </summary>
        public VMPhase Phase { get; set; }
        #endregion Phase

        #region    SortKey
        /// <summary>
        /// We keep an ordered list of sorted VMTimers
        /// </summary>
        public int SortKey
        {
            get
            {
                if (_SortKey == 0)
                    _SortKey = -MTimer.SecondsToZero;
                return _SortKey;
            }
        }
        private int _SortKey = 0;
        #endregion SortKey

        #region    TimerStrip
        /// <summary>
        /// The Alarm strip connected to this VM
        /// </summary>
        public TimerStrip TimerStrip { get; private set; }
        #endregion TimerStrip

        #region    Tracer
        /// <summary>
        /// A Tracer which may or may not be started depending on what is in Statics.
        /// This source of this data should come from 
        /// </summary>
        public Tracer Tracer;
        #endregion Tracer

        #region    VMTimers (static)
        /// <summary>
        /// List of active timers which is used to populated the UI list of timers
        /// </summary>
        static public List<VMTimer> VMTimers = new List<VMTimer>();
        #endregion VMTimers (static)

        #endregion Properties

        #region    Constructor and factory CreateVMTimer

        #region Constructor int secondsToZero
        /// <summary>
        /// Create the VM connecting the UI TimerStrip to a new non-UI MTimer
        /// </summary>
        /// <param name="secondsToZero"></param>
        private VMTimer() : base()
        {

        }
        #endregion Constructor int secondsToZero

        #region    CreateVMTimer (static) (int secondsToZero, int repeats, string name) ??? something else to do here ???
        /// <summary>
        /// Factory method to create a VMTimer. Must run on the UI as updated MainPage
        /// </summary>
        /// <param name="secondsToZero"></param>
        /// <param name="repeats"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        static public VMTimer CreateVMTimer(int secondsToZero, int repeats = int.MaxValue, bool useTracerMain = false)
        {
            var vmTimer = new VMTimer();
            vmTimer.TimerStrip = new TimerStrip(vmTimer); // VMTimer .MTimer needs this instantiated
            lock (VMTimers)
            {
                vmTimer.MTimer = MTimer.CreateMTimer(
                    owner: vmTimer,
                    secondsToZero: secondsToZero,
                    mTickTock: MainPage.This.MTickTock,
                    mTimerStatus: MTimerStatus.PausedBeforeAlarm,
                    repeats: repeats,
                    callbackOnTick: new Action(vmTimer.Tick),
                    callbackOnNewPhase: new Action(vmTimer.PhaseReset),
                    callbackOnZero: new Action(vmTimer.BlastOff),
                    callbackOnNextAlarm: new Action(vmTimer.PlayAlarm)
                    );

                vmTimer.MSound = new MSound(vmTimer);

                MainPage.This.UpdateTimersOnPage();
                VMTimers.Add(vmTimer);
                VMTimer.HotVMTimer = vmTimer;
            }
            /* Each VMTimer has its own Trace. However the first VMTimer created shares
             * the Tracer with every other thing that does not have a tracer.
             * Objects that a VMTimer 'owns' shares their VMTimer Tracer
             */
            if (useTracerMain)
                vmTimer.Tracer = Tracer.TracerMain;
            else
                vmTimer.Tracer = new Tracer(
                    name: $"VMTracer{vmTimer.UniqueId}",
                    id: $"V{vmTimer.UniqueId}:",
                    isUniqueTask: false,
                    allowCrossTaskTracing: true,
                    startTracing: false,
                    showInterval: false);
            vmTimer.ResetPhase(secondsToZero);
            MainPage.This.UpdateTimersOnPageOnUI();
            vmTimer.UpdateTimerStrip();
            return vmTimer;
        }

        #endregion CreateVMTimer (static) (int secondsToZero, int repeats, string name)

        #endregion Constructor and factory CreateVMTimer

        #region    BlastOff
        /// <summary>
        /// SecondsToZero has reached zero. I used to call this PlayAlarm but I could
        /// never remember if that was the name for only the first, or future ones as well.
        /// BlastOff I remember. We sound the first alarm - a user recording or b-e-e-e-p
        /// </summary>
        public void BlastOff()
        {
            MSound.BlastOff();
        }
        #endregion BlastOff

        #region    EditTimerStrip  yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy
        /// <summary>
        /// Yyyyet to be written
        /// </summary>
        public void EditTimerStrip() // yyy Arrange for the TimerBlock to be placed over TimerStrips
        {
            Trace($"Entry");
            Trace($"Exit");
        }
        #endregion EditTimerStrip Commands

        #region    GetPhaseFromSecondsToZero
        /// <summary>
        /// Given the number of SecondToAlarm work out which phase we are in.
        /// </summary>
        /// <param name="secondsToZero">Number of seconds left to get down to the first alarm.
        /// This can be negative signifying this TimerStrip/VMTimer are still running even
        /// though the alarm time has past</param>
        /// <returns></returns>
        public void ResetPhase(int secondsToZero)
        {
            Phase = VMPhase.Phases.First(fase => fase.SecondsToZero < secondsToZero);
            TimerStrip.Color = Phase.Color;
        }
        #endregion GetPhaseFromSecondsToZero

        #region    MakeMeHot
        /// <summary>
        /// When any TimerControlButtons are pressed they are applied to this VMTimer / TimerStrip
        /// that will be affected. There is no MakeMeCold as thee is always a Hot VMTimer. Only
        /// a different MakeMeHot will 'un-hot' this VMTimer
        /// </summary>
        internal void MakeMeHot()
        {
            HotVMTimer = this;
        }

        #endregion MakeMeHot

        #region    Pause 
        /// <summary>
        /// Pauses or resumes the MTimer. This called when the user presses the Pause Button
        /// </summary>
        public void Pause()
        {
            Trace($"Entry");
            MTimer.Pause();
            TimerStrip.ResetControlButtonImages();
            UpdateTimerStrip();
            Trace($"Exit");
        }
        #endregion Pause

        #region    PhaseReset
        /// <summary>
        /// MTimer has signalled us via a callback that it is time to change phase.
        /// Everything is done via callbacks to MTimer has no knowledge of VMTimer
        /// Not what he does with them
        /// </summary>
        public void PhaseReset()
        {
            Trace($"Entry {Phase.DisplayMessage}");
            Phase = VMPhase.GetPhaseFromSecondsToZeroStatic(MTimer.SecondsToZero);

            Statics.RunOnUI(() => { TimerStrip.Color = Phase.Color; });

            if (Phase.Callback != null)
                Phase.Callback.Invoke();
            Trace($"Exit {Phase.DisplayMessage}");
        }
        #endregion PhaseReset

        #region    PlayAlarm
        /// <summary>
        /// Play the main alarm whether the default sound or a recording.
        /// </summary>
        public void PlayAlarm()
        {
            Trace($"Entry");
            MSound.PlayAlarm();
            Trace($"Exit");
        }
        #endregion PlayAlarm

        #region    Record Entered, Pressed, Exited and Released

        #region    RecordPressed
        /// <summary>
        /// Interface routine between view MainPage and Model MSound. Purely to separate
        /// functionality
        /// </summary>
        internal void RecordPressed() => MSound.Sound.RecordUser();
        #endregion RecordPressed

        #region    RecordEntered
        /// <summary>
        /// Interface routine between view MainPage and Model MSound. Purely to separate
        /// functionality
        /// </summary>
        internal void RecordEntered()
        {
            VMMicrophone.MicrophoneStatusStatic = MicrophoneStatus.Entered;
            // yyy Give a visual and maybe aural indication so the user knows he can make a recording
            // yyy there is now a visual signal - maybe get rid of the event
        }
        #endregion RecordEntered

        #region    RecordExited
        /// <summary>
        /// Interface routine between view MainPage and Model MSound. Purely to separate
        /// functionality
        /// </summary>
        internal void RecordExited()
        {
            // Undo any visual indication yyy not sure how this is best handled
        }
        #endregion RecordExited

        #region    RecordReleased
        /// <summary>
        /// Interface routine between view MainPage and Model MSound. Purely to separate
        /// functionality
        /// </summary>
        internal void RecordReleased() => MSound.RecordUser();
        #endregion RecordReleased

        #endregion Record Entered, Pressed, Exited and Released

        #region    Reload
        /// <summary>
        /// User has requested a Reload. The only thing that changes is  VMTimer and associated hangers on
        /// such as the TimerStrip, the MSound and the MTimer
        /// </summary>
        public void Reload()
        {
            Trace($"Entry");
            /* Establish the Phase immediately cus various things look at what Phase we are in
             */

            MTimer.Reload();
            MSound.Reload();
            Phase = VMPhase.GetPhaseFromSecondsToZeroStatic(MTimer.SecondsToZero);
            Phase.Callback?.Invoke();

            MainPage.This.UpdateTimersOnPage();
            TimerStrip.ResetControlButtonImages();
            TimerStrip.Color = Phase.Color;
            TimerStrip.NotifySegments();
            Trace($"Exit");
        }
        #endregion Reload

        #region    RescheduleAlarm
        /// <summary>
        /// An alarm once played keep on being played until the Timer is removed, reloaded, silenced or paused
        /// <
        /// </summary>
        public void RescheduleAlarm()
        {
            Trace($"Entry =>NextAlarm={MTimer.SecondsToNextAlarm}");
            MTimer.SecondsToNextAlarm = Phase.SecondsBetweenAlarms;
            Trace($"Exit =>NextAlarm={MTimer.SecondsToNextAlarm}");
        }
        #endregion RescheduleAlarm

        #region    Resume
        /// <summary>
        /// Resumes the counting which is in a paused state. Resume and Start are different 
        /// nomenclatures for the same thing. The time is Paused due to one of these reasons
        /// <list>
        /// 1) The user presses the  toggle to Resume toggle after previously pressing the same toggle to Pause
        /// 2) There is a new timer and the user has not pressed start.
        /// 3) The timer 'dies' and is re-incarnated but not started
        /// </list> 
        /// </summary>
        public void Resume()
        {
            Trace($"Entry");
            MTimer.Resume();
            TimerStrip.ResetControlButtonImages();
            UpdateTimerStrip();
            Trace($"Exit");
        }
        #endregion Resume

        #region    Tick
        /// <summary>
        /// Non-UI. When the big clock in the sky goes tick, that tick is propagated  
        /// down to the MTimers which in turn propagates it to their respective VMTimers,
        /// MSound, TimerStrips and finally TimerSegments. 
        /// </summary>
        /// <param name="secondsToZero">This starts off as the value the user entered. 
        /// It is decremented every second. Eventually it reaches zero and the alarms
        /// begin. But it keeps counting down into the negative zone as various Phases
        /// occur after the alarm has started such as Tile color (which is really a brush),
        /// frequency of the alarm and volume</param>
        public override void Tick()
        {
            var secondsToZero = MTimer.SecondsToZero;
            Trace($"Entry {secondsToZero}>>Zero");
            MSound.Tick();
            TimerStrip.TickAt(secondsToZero);

            Trace($"Exit  {secondsToZero}>>Zero");

            return;
        }
        #endregion Tick

        #region    TickStatic
        /// <summary>
        /// We have a static Tick to deal with dead VMTimers. We have to remove them
        /// from the list of active VMTimers. This is called once per Tick by
        /// TickTock.Tick
        /// </summary>
        internal static void TickStatic()
        {
            if (VMTimers.Count == 0)
                Statics.InternalProblem("No VMTimers");
            Tracer.TracerMain.TraceCrossTask($"Entry");
            /* Let's see if we have any timers that have either been killed or
             * committed suicide
             */
            var deadVMTimers = VMTimers.Where(timer => timer.Phase.DisplayMessage.ToLower() == "dead");
            bool notify = deadVMTimers.Count() != 0;

            /* if we are about to delete the final timer then reload it rather than delete it 
             */

            if (deadVMTimers.Count() == VMTimers.Count)
            {
                var timer = deadVMTimers.First();
                Statics.RunOnUI(new Action(timer.Reload));
                deadVMTimers = deadVMTimers.Skip(1);
            }
            /* Kill off remaining dead timers 
             */

            for (int i = 0; i < deadVMTimers.Count(); i++)
                deadVMTimers.Last().Dispose();
            if (notify)
                MainPage.This.UpdateTimersOnPage();
            Tracer.TracerMain.TraceCrossTask($"Exit");
            return;
        }
        #endregion TickStatic

        #region    ToString();
        //
        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString()
        {
            return $"VMTimer{UniqueId}.txt";
        }
        #endregion ToString()

        #region    Trace()
        /// <summary>
        /// Write to the trace file or the console
        /// </summary>
        /// <param name="info">Entry Exit or 'whatever' data to be traced</param>
        /// <param name="member">[CallerMemberName]</param>
        /// <param name="line">[CallerLineNumber]</param>
        /// <param name="path">[CallerFilePath]</param>
        [System.Diagnostics.DebuggerStepThrough()]
        public override void Trace(string info,
                        [CallerMemberName] string member = "", [CallerLineNumber] int line = 0, [CallerFilePath] string path = "")
        {
            if (Tracer == null || !Tracer.IsTracing)
                return;
            if (Tracer.TraceOptions == TraceOptions.None)
                Statics.InternalProblem("TraceOption is None");
            Tracer.TraceCrossTask($"U-Id={UniqueId}{info}", member, line, path);
        }
        #endregion Trace()

        #region    UpdateTimerStrip
        /// Due to various thing we ask the timer to update it's colors and what 
        /// buttons are available, etc;
        public void UpdateTimerStrip()
        {
            Trace($"Entry");
            TimerStrip.NotifySegments();
            Trace($"Exit");
        }
        #endregion UpdateTimerStrip

        #region    IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    lock (VMTimers)
                    {
                        TimerStrip = null;
                        MTimer.Dispose();
                        VMTimers.Remove(this);
                    }
                    if (Tracer != null)
                        Tracer.StopStatic(Tracer);
                }
                disposedValue = true;
            }
        }
        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Trace($"Entry");
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            Trace($"Exit"); // yyy hope this one works as it may have disappeared
        }
        #endregion IDisposable Support

        #region    IntervalTimerName
        /// <summary>
        /// The parent (Owner), like in real life, gets to name the child IntervalTimerName 
        /// </summary>
        /// <returns></returns>
        public String IntervalTimerName() => $"IntTimer_{UniqueId}";
        #endregion IntervalTimerName
    }
}
