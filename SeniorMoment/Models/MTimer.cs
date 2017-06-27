using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SeniorMoment.Services;
using SeniorMoment.ViewModels;

namespace SeniorMoment.Models
{
    /*    ++++++++++++++++ COMMENTS ABOUT MTimer ++++++++++++++++++++
     *
     * There are four separate events in terms of timing that cause MTimer to signal
     * VMTimer that there is a change that affects VMTimer.
     * 
     * When a VMTimeris created it creates a new MTimer, MSound and TimerStrip.
     * The MTimer registers itself with TickTock and passes a callback to
     * MTimer.Tick(). So when TickTock Ticks it propagates through to the 
     * VMTimer.Tick, MSound and the view AlarmStrip.
     * 
     * After that there are various NotifyPropertyChanged which cause the
     * TimerStrip Tile to show the time remaining / times past the alarm,
     * and the the TimerStrip Tiles to change color.
     * 
     * 1) Every second each MTimer registered with TickTock receives a Tick().
     *    This decrements MTimer.SecondsToZero, MTimer.SecondsToNextAlarm and
     *    MTimer.SecondsToNextPhase
     *    
     * 2) When the MTimer.SecondsToZero == 0, there is a callback to VMTimer.BlastOff()
     *    This plays the first alarm and then daisy chains further alarm calls.
     *    
     * 3) After the first alarm call MTimer Schedules a repeat alarm in SecondsBetweenAlarms
     *    after the this alarm finishes. This is repeated for up to 40 days or
     *    user intervention.
     *    
     * 4) SecondsToPhaseChange is also decremented. When it reaches zero the VMTimer 
     *    moves into the next stage of its life. Each stage is dictated by the next
     *    Phase = VMPhase.Phases.NextPhase. This new Phase resets SecondsBetweenAlarms 
     *    and SecondsToPhaseChange.
     *    
     * 5) After SecondsToZero has hit zero it keeps on getting more negative. It is never
     *    reset unless the user presses the Reload/Reset button when everything is
     *    restored to its initial values. Eventually the user will reset the alarm,
     *    delete it, or it will drop out after 40 days in the desert.
     *  
     * 6) This is the actual callbacks set up when VMTimer Created MTimer.
     * 
     *               callbackOnTick: new Action<int>(vmTimer.Tick),
     *               callbackOnNewPhase: new Action(vmTimer.PhaseNext),
     *               callbackOnZero: new Action(vmTimer.BlastOff),
     *               
     *               callbackOnHalfTick is not currently used but may be used in TaskFunnel
     * 
     */
    /// <summary>
    /// This class represents a Timer which is heading towards zero hour, or rather Zero Second. 
    /// See the explanation above and in the Constructor
    /// </summary>
    public class MTimer : IIntervalTimerOwner, IDisposable
    {
        #region    Properties

        #region    CallbackOnTick
        /// <summary>
        /// callback every time there is a TickTock.Tick()
        /// </summary>
        Action CallbackOnTick; // { get; private set; }
        #endregion CallbackOnTick

        #region    CallbackOnNewPhase
        /// <summary>
        /// When TimeToNewPhase reaches zero we call this
        /// </summary>
        public Action CallbackOnNewPhase { get; private set; }
        #endregion CallbackOnNewPhase

        #region    CallbackOnNextAlarm
        /// <summary>
        /// Call back when the next alarm finishes so we can reschedule the alarm according to Phase
        /// </summary>
        public Action CallbackOnNextAlarm { get; private set; }
        #endregion CallbackOnNextAlarm

        #region    CallbackOnZero
        /// <summary>
        /// One time callback when timer hits zero. From now on SecondsToZero will be negative
        /// </summary>
        public Action CallbackOnZero { get; private set; }
        #endregion CallbackOnZero

        //#region    intervalTimer
        ///// <summary>
        ///// The actual object that runs the system Timer and when Ticked makes
        ///// the appropriate callbacks.
        /////
        ///// </summary>
        //IntervalTimer intervalTimer;
        //#endregion intervalTimer

        #region    IsTicking
        /// <summary>
        /// 
        /// </summary>
        public bool IsTicking => MTimerStatus.HasFlag(MTimerStatus.Ticking);
        #endregion IsTicking

        #region    IsPaused
        /// <summary>
        /// examine MTimerStatus and return true if in any PausedAny state
        /// </summary>
        /// <returns></returns>
        public bool IsPaused => MTimerStatus.HasFlag(MTimerStatus.Paused);
        #endregion IsPaused

        #region    MTimerStatus
        /// <summary>
        /// enum: what is happening to the MTimer. The IntervalHandler has its own  status, which
        /// is purely concerned with 'timey' things.
        /// This MTimerStatus is concerned with SeniorMoment stuff
        /// 
        /// </summary>
        public MTimerStatus MTimerStatus { get; private set; } = MTimerStatus.PausedBeforeAlarm;
        #endregion MTimerStatus

        #region    NextUniqueId
        /// <summary>
        /// Return an incremental unique id, but it is only unique for the run of this program.
        /// then it starts again at 0.
        /// </summary>
        static int NextUniqueId
        {
            get
            {
                _NextUniqueId++;
                return _NextUniqueId;
            }
        }
        static int _NextUniqueId = -1;
        #endregion NextUniqueId

        #region    Phase
        /// <summary>
        /// The Phase that the VMTimer is in
        /// </summary>
        VMPhase Phase => VMTimer.Phase;
        #endregion Phase

        #region    Repeats
        /// <summary>
        /// The number of repeats for this sound.
        /// </summary>
        public int Repeats { get; private set; }
        #endregion Repeats

        #region    RepeatsOriginal
        /// <summary>
        /// "Repeats" is decremented, so we keep the original value here for a potential Reload
        /// </summary>
        public int RepeatsOriginal { get; private set; }
        #endregion RepeatsOriginal

        #region    SecondsToZero
        /// <summary>
        /// This starts at whatever the user entered on the TimerStrip. It then counts down passing through zero into
        /// the negative zone where it shows how long ago the alarm sounded. At certain values
        /// the VMTimer will affected by a change in Phase. At any time VMTimer can change the
        /// SecondsToNextPhase
        /// </summary>
        public int SecondsToZero;
        #endregion SecondsToZero

        #region    SecondsToZeroOriginal
        /// <summary>
        /// We remember this value because when the Timer has expired we
        /// may have to reset it to its original value
        /// </summary>
        public int SecondsToZeroOriginal;
        #endregion SecondsToZeroOriginal

        #region    SecondsToNewPhase
        /// <summary>
        /// How many seconds to the next Event. This is normally a VMTimer Phase change.
        /// However MTimer doesn't 'know' who or what owns him. Upon reaching that number
        /// MTimer calls the 'NewPhase' callback. For VMTimer this means move to Phase.NextPhase
        /// </summary>
        // public int SecondsToNextPhase = 0;
        int SecondsToNextPhase { get => SecondsToZero - Phase.SecondsToZero; }
        #endregion SecondsToNewPhase

        #region    SecondsToNextAlarm
        /// <summary>
        /// How many seconds to the next Alarm. Initially this is set to SecondsToZero which
        /// comes from the initial phase. It is decremented every second, so the first time
        /// it goes off is SecondsToZero == 0. An alarm is sounded and when it finishes
        /// there is a callback to RepeatSound. Subsequent to that the alarm will be repeated
        /// Phase.SecondsBetweenAlarms later and will continue until the repeat count is
        /// exhausted or the Timer is Paused or deleted.
        /// </summary>
        public int SecondsToNextAlarm = int.MaxValue;
        #endregion SecondsToNextAlarm

        #region    TimedCallbacks
        /// <summary>
        /// The MTimer receives parameters in a different format to IntervalTimer.
        /// This is a temp variable to arrange the callbacks we had at creation
        /// into TimerInterval format.
        /// </summary>
        List<(int SecondsToZero, Action Action)> TimedCallbacks = null;
        #endregion TimedCallbacks

        #region    UniqueId
        /// <summary>
        /// Index of MTimers. First MTimer is 0, second is 1 etc. If a MTimer is Disposed
        /// then the number is not re-used
        /// </summary>
        int UniqueId = -1;
        #endregion UniqueId

        #region    VMTimer
        /// <summary>
        /// The VMTimer that owns this MTimer. An MTimer in the future 
        /// could be owned by many types, but if it is owned by
        /// a VMTimer (and that's all that can be at the moment) then this is
        /// a shortcut to it
        /// </summary>
        VMTimer VMTimer;
        #endregion VMTimer

        #endregion Properties

        #region    Constructor

        private MTimer(
                    VMTimer vmTimer,
                    int secondsToZero,
                    MTickTock mTickTock,
                    MTimerStatus mTimerStatus = MTimerStatus.PausedBeforeAlarm,
                    int repeats = int.MaxValue,
                    Action callbackOnTick = null,
                    Action callbackOnNewPhase = null,
                    Action callbackOnZero = null,
                    Action callbackOnNextAlarm = null,
                    List<(int interval, Action action)> timedCallbacks = null
                    )

        {
            VMTimer = vmTimer;   // do this now as required Owner must exist before a Trace

            IntervalTimerStatus intervalTimerStatus =
                       mTimerStatus == MTimerStatus.Paused
                           ? IntervalTimerStatus.Paused
                           : IntervalTimerStatus.Counting;
            UniqueId = NextUniqueId;
            Trace($"Entry Id:{UniqueId} =>0{secondsToZero}");

            if (vmTimer == null)
                throw new LogicException("null Owner");

            MTickTock.This.AddMTimer(this);
            if (secondsToZero < 1)
                Statics.InternalProblem("secondsToZero=" + secondsToZero);
            SecondsToZero = secondsToZero;

            /* we remember the original time so if the user resets this VMTimer we recreate
             * the MTimer (in the code it is called Reload) with the original values
             */
            SecondsToZeroOriginal = secondsToZero;

            /* How long before the alarm goes off. For phases with SecondsToZero >= 0
             * we set the SecondsToNextAlarm To SecondsToZero. The alarm gets played
             * via BlastOff when SecondsToNextAlarm is played. After it finishes
             * playing we reschedule it for 'n' seconds later where 'n' is the value
             * in Phase.SecondsBetweenAlarms
             */
            SecondsToNextAlarm = secondsToZero;

            ///* Made MTickTock non-static in case there are places where I need a finer
            // * granularity than one second. So far not needed.
            // */
            //TickTock = mTickTock;

            /* Basically we have two types of sounds. The first is played just once such
             * as LongBeep.wav. The others (the alarms) repeats indefinitely with a timing
             * based on the Phase. We remember the value for a reload 
             */
            Repeats = RepeatsOriginal = repeats;

            /* What to call when Tick() happens. 
             */
            CallbackOnTick = callbackOnTick; // => vmTimer.Tick

            /*
             * When the timer event is up we have a different callback
             */
            CallbackOnNewPhase = callbackOnNewPhase;  // => vmTimer.PhaseNext

            /* And finally the one-off when the SecondsToZero is Zero. (ie alarm time)
             */
            CallbackOnZero = callbackOnZero; // => vmTimer.BlastOff

            /*
             * This callback is made when the Alarm has finished and we wish to raise another
             */
            CallbackOnNextAlarm = callbackOnNextAlarm;
            /*
             * Now some specialised callbacks that are over and above the normal
             */

            TimedCallbacks = TimedCallbacks ?? new List<(int SecondsToZero, Action Action)>();

            TimedCallbacks = new List<(int SecondsToZero, Action Action)>
            {

            };
            /*  Status of the MTimer. It can be Paused and Resumed any number of times before the 
             *  alarm goes off. After SecondsToZero reaches 0 it is set to GoingOff.
             *  If it is Paused after the Alarm goes off the user will cease to receive
             *  messages. Maybe I should remove that facility and replace it with HushOn()
             *  and HushOff. I really don't like Hush(bool). ???
             */


            MTimerStatus = mTimerStatus;
            Trace($"Exit");
        }
        #endregion constructor

        #region    CreateMTimer
        /// <summary>
        /// Factory Create a new MTimer which is initially Paused. 
        /// </summary>
        /// <param name="callback">delegate to call when the Timer reaches 0</param>
        /// <param name="repeats">How may times this timer should be initiated</param>
        public static MTimer CreateMTimer(

            VMTimer owner,
            int secondsToZero,
            MTickTock mTickTock,
            MTimerStatus mTimerStatus,
            int repeats,
            Action callbackOnTick = null,
            Action callbackOnNewPhase = null,
            Action callbackOnZero = null,
            Action callbackOnTickHalf = null,
            Action callbackOnNextAlarm = null,
            List<(int secondsToZero, Action action)> extraCallbacks = null
            )

        {
            Tracer.TracerMain.TraceCrossTask("Creating MTimer =>Zero:{secondsToZero} status={status} repeats={repeats}");

            if (extraCallbacks == null)
                extraCallbacks = new List<(int secondsToZero, Action action)>();
            if (owner == null)
            {
                if (callbackOnTick != null || callbackOnZero != null || callbackOnNewPhase != null || callbackOnNextAlarm != null)
                    Statics.InternalProblem("owner==null with callbacks");
                if (extraCallbacks.Count == 0)
                    Statics.InternalProblem("owner==null, no ExtraCallBacks");
            }
            var mTimer = new MTimer(
                                owner,
                                secondsToZero,
                                mTickTock,
                                mTimerStatus: mTimerStatus,
                                repeats: repeats,
                                callbackOnTick: callbackOnTick,
                                callbackOnNewPhase: callbackOnNewPhase,
                                callbackOnZero: callbackOnZero
                                )
            {

            };
            owner.Trace($"Exit");
            return mTimer;
        }
        #endregion CreateMTimer

        #region    GetIntervalTimerId
        public Int32 GetIntervalTimerId() => throw new NotImplementedException();
        #endregion GetIntervalTimerId

        #region    GetIntervalTimerName
        public String IntervalTimerName() => throw new NotImplementedException();
        #endregion GetIntervalTimerName

        #region    Pause
        /// <summary>
        /// Pause or the MTimer. All this really means is that it ignores TickTock.Tick. I
        /// really should replace TickTock.MTimer.Tick() with a callback pointing at Tick()
        /// </summary>
        public void Pause()
        {

            Trace($"Entry");
            switch (MTimerStatus)
            {
                case MTimerStatus.CountingBeforeAlarm:
                    MTimerStatus = MTimerStatus.PausedBeforeAlarm;
                    break;

                case MTimerStatus.CountingAfterAlarm:
                    MTimerStatus = MTimerStatus.PausedAfterAlarm;
                    break;

                default:
                    Statics.InternalProblem("Pause() while status is " + MTimerStatus.ToString());
                    break; // never gets here
            }
            Trace($"Exit");
        }
        #endregion Pause

        #region    PhaseChange
        /// <summary>
        /// There has been a Phase change so we need to reset the seconds between Alarms
        /// </summary>
        /// <param name="phase"></param>
        public void PhaseChange(VMPhase phase)
        {
            Trace($"Entry&Exit sex={phase.ToString()}");

            if (phase.SecondsToZero >= 0)
                SecondsToNextAlarm = phase.SecondsToZero;

        }
        #endregion PhaseChange

        #region    Reload
        /// <summary>
        /// When the user presses reload we reset all the values in this MTimer. This
        /// is called from VMTimer.Reload. MSound.Reload() does not exist as it
        /// is distinct from any events. It only handles recording and playback.
        /// </summary>
        internal void Reload()
        {
            Trace($"Entry&Exit");
            MTimerStatus = MTimerStatus.PausedBeforeAlarm;
            Repeats = RepeatsOriginal;
            SecondsToZero = SecondsToZeroOriginal;
            SecondsToNextAlarm = SecondsToZero;
            //VMTimer.TimerStrip.Focus(Windows.UI.Xaml.FocusState.Programmatic);
            Trace($"Entry&Exit sex={SecondsToNextAlarm} repeats={Repeats}");
        }

        #endregion Reload

        #region    Resume
        /// <summary>
        /// Resume the MTimer. All this really means is that The Tick is not propagated
        /// through the Models and ViewModels. There is a second type of 'pause/resume'
        /// in TickTock in which you can Cache the Ticks (ie not propagate them) and at
        ///  a later date we can un-cache them and play them at a higher speed 
        /// </summary>
        public void Resume()
        {
            Trace($"Entry {MTimerStatus}");
            switch (MTimerStatus)
            {
                case MTimerStatus.PausedBeforeAlarm:
                    MTimerStatus = MTimerStatus.CountingBeforeAlarm;
                    break;

                case MTimerStatus.PausedAfterAlarm:
                    MTimerStatus = MTimerStatus.CountingAfterAlarm;
                    break;

                default:
                    Statics.InternalProblem("Resume() status: " + MTimerStatus.ToString());
                    break; // never gets here
            }
            Trace($"Exit {MTimerStatus}");
        }

        #endregion Resume

        #region    Tick
        /// <summary>
        /// Another second of your life wasted, let's knock it off the eggtimer.
        /// See 'Death' in Terry Pratchett's Discworld series.
        /// <para/>When the big TickTock in the sky goes Tick, that tick is propagated 
        /// via callbacks to the Owner. In terms of the VMTimers this tick is
        /// propagated to the VMTimer and thence to the MSound, the 
        /// TimerStrip and then to the TimerSegments.
        /// (As of writing the only Owners are VMTimers, but I'm hoping to use this
        /// code elsewhere sometime. UPDATE. Using it to timeout recordings )
        /// </summary>
        public void Tick()
        {
            if (MTimerStatus == MTimerStatus.PausedAfterAlarm ||
                MTimerStatus == MTimerStatus.PausedBeforeAlarm)
            {
                Trace($"Entry&Exit not Ticking");
                return;
            }
            Trace($"Entry secs to zero={SecondsToZero} Alarm={SecondsToNextAlarm} Phase={SecondsToNextPhase}");

            /* A 'normal' Tick. We head closer to the next Phase and (if the SecondsToZero is positive)
             * closer to the BigEvent. We pass back the number of seconds left so VMTimer can do
             * appropriate things with the TimerStrip. (Decrement the time etc)
             */
            SecondsToZero--;
            SecondsToNextAlarm--;
            CallbackOnTick(); // => VMTimer.Tick()

            /*
             * Seconds time to NextPhase is calculated as "SecondsToZero - Phase.SecondsToZero"
             * So if the Phase has a value of -3600 and SecondsToZero = -3000 the next Phase change
             * will be in 600 seconds
             */
            CallbackOnNewPhase();  //  new Action<object>(vmTimer.PhaseNext ),

            /* These are (or at least were when I wrote this the callbacks in CreateMTimer(...)           
             *
             *       callbackOnTick: new Action<int>(vmTimer.Tick),
             *       callbackOnNewPhase: new Action<object>(vmTimer.PhaseNext ),
             *       callbackOnZero: new Action(vmTimer.BlastOff)
             *       callbackOnNextAlarm
             *
             *   There is always a Phase change at SecondsToZero so I could have eliminated
             *   CallBackOnZero. However the two callbacks are essentially different and being pedantic
             *   I treat them differently.
             */

            if (SecondsToZero == 0)
                CallbackOnZero();  // => new Action(vmTimer.BlastOff) => MSound.ScheduleAlarm

            if (SecondsToNextAlarm == 0)
                VMTimer.PlayAlarm();

            Trace($"Exit secs to zero={SecondsToZero} Alarm={SecondsToNextAlarm} Phase={SecondsToNextPhase}");
            return;
        }
        #endregion Tick

        #region    Name => MTimer_0 etc
        /// <summary>
        /// The name of the MTimer using the UniqueId as an index. 
        /// The name of the 5th MTimer is MTimer_5
        /// </summary>
        public string Name => $"MTimer_{UniqueId}";
        #endregion Name => MTimer_0 etc

        #region    ToString
        /// <summary>
        /// To uniquely identify this MTimer
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString() => $"{Name} @{SecondsToZero}";
        #endregion ToString

        #region    Trace()
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
            VMTimer.Trace(info, member, line, path);

        }
        #endregion Trace()

        //#region    Trace()
        ///// <summary>
        ///// Write to a trace file or the console
        ///// </summary>
        ///// <param name="info">Entry Exit or 'whatever' data to be traced</param>
        ///// <param name="member">[CallerMemberName]</param>
        ///// <param name="line">[CallerLineNumber]</param>
        ///// <param name="path">[CallerFilePath]</param>
        //[System.Diagnostics.DebuggerStepThrough()] // never want to go into Trace unless debugging Tracer
        //public override void Trace(string info,
        //                    [CallerMemberName]string member = "",
        //                    [CallerLineNumber] int line = 0,
        //                    [CallerFilePath] string path = "")
        //{
        //    Owner.Trace(info, member, line, path);
        //}
        //#endregion Trace()

        #region    IEquatable stuff

        #region    Equals
        /// <summary>
        /// All objects are different based on a unique key.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool Equals(MTimer obj)
        {
            if (obj == null)
                return false;
            return this.UniqueId == obj.UniqueId;
        }
        #endregion Equals

        #region    GetHashCode
        /// <summary>
        /// Use a simpler Equals. The objects are the same if they have the same unique key
        /// rather than basing the HashCode on the whole object
        /// </summary>
        /// <returns>true if they are the same object</returns>
        public override int GetHashCode() => UniqueId;
        #endregion GetHashCode

        #endregion IEquatable stuff

        #region    Dispose
        /// <summary>
        /// Kill the IntervalTimer and remove it from TickTock
        /// </summary>
        public void Dispose()=> MTickTock.This.RemoveMTimer (this);
        #endregion Dispose

    }
    #region    TimerStatus ENUM
    /// <summary>
    /// The status of the Alarm
    /// </summary> 
    [System.Flags]
    public enum MTimerStatus
    {
        Ticking = 1,
        CountingBeforeAlarm = 3,
        CountingAfterAlarm = 5,

        Paused = 8,
        PausedAfterAlarm = 24,
        PausedBeforeAlarm = 40
    }
    #endregion TimerStatus enum,

    #region    IMTimerOwner
    /// <summary>
    /// For any object that instantiates MTimer
    /// </summary>
    public interface IMTimerOwner
    {
        void Tick();
        void Trace(string info,
                    [CallerMemberName]string member = "",
                    [CallerLineNumber] int line = 0,
                    [CallerFilePath] string path = "");

        string TimerName();
        #endregion IMTimerOwner
    }
}


