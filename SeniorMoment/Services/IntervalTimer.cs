using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SeniorMoment.Services
{
    /*    ++++++++++++++++ COMMENTS ABOUT IntervalTimer ++++++++++++++++++++
     *
     * There are four separate events in terms of timing that cause IntervalTimer to signal
     * VIntervalTimer that there is a change that affects VIntervalTimer.
     * 
     * When a VIntervalTimeris created it creates a new IntervalTimer, MSound and TimerStrip.
     * The IntervalTimer registers itself with TickTock and passes a callback to
     * IntervalTimer.Tick(). So when TickTock Ticks it propagates through to the 
     * VIntervalTimer.Tick, MSound and the view AlarmStrip.
     * 
     * After that there are various NotifyPropertyChanged which cause the
     * TimerStrip Tile to show the time remaining / times past the alarm,
     * and the the TimerStrip Tiles to change color.
     * 
     * 1) Every second each IntervalTimer registered with TickTock receives a Tick().
     *    This decrements IntervalTimer.SecondsToZero, IntervalTimer.SecondsToNextAlarm and
     *    IntervalTimer.SecondsToNextPhase
     *    
     * 2) When the IntervalTimer.SecondsToZero == 0, there is a callback to VIntervalTimer.BlastOff()
     *    This plays the first alarm and then daisy chains further alarm calls.
     *    
     * 3) After the first alarm call IntervalTimer Schedules a repeat alarm in SecondsBetweenAlarms
     *    after the this alarm finishes. This is repeated for up to 40 days or
     *    user intervention.
     *    
     * 4) SecondsToPhaseChange is also decremented. When it reaches zero the VIntervalTimer 
     *    moves into the next stage of its life. Each stage is dictated by the next
     *    Phase = VMPhase.Phases.NextPhase. This new Phase resets SecondsBetweenAlarms 
     *    and SecondsToPhaseChange.
     *    
     * 5) After SecondsToZero has hit zero it keeps on getting more negative. It is never
     *    reset unless the user presses the Reload/Reset button when everything is
     *    restored to its initial values. Eventually the user will reset the alarm,
     *    delete it, or it will drop out after 40 days in the desert.
     *  
     * 6) This is the actual callbacks set up when VIntervalTimer Created IntervalTimer.
     * 
     *               callbackOnTick: new Action<int>(vintervalTimer.Tick),
     *               callbackOnNewPhase: new Action(vintervalTimer.PhaseNext),
     *               callbackOnZero: new Action(vintervalTimer.BlastOff),
     *               
     *               callbackOnHalfTick is not currently used but may be used in TaskFunnel
     * 
     */
    /// <summary>
    /// This class represents a Timer which is heading towards zero hour, or rather Zero Second. 
    /// It has no Timer built into it but depends on TickTock to give him a kick every
    /// seconds (or whatever Interval is specified in TickTock.Constructor()
    /// </summary>
    public class IntervalTimer : ITick, IDisposable, IEquatable<IntervalTimer>, ITracer
    {
        #region    Properties

        #region    CallbackOnTick
        /// <summary>
        /// callback every time there is a TickTock.Tick()
        /// </summary>
        public Action CallbackOnTick { get; protected set; }
        #endregion CallbackOnTick

        #region    IsPaused
        /// <summary>
        /// Indicate whether the user has Paused the Timer
        /// </summary>
        public bool IsPaused => IntervalTimerStatus == IntervalTimerStatus.Paused;
        #endregion IsPaused

        #region    IsTicking
        /// <summary>
        /// We are either ticking or we are paused / waiting to start / finished etc
        /// </summary>
        public bool IsTicking => IntervalTimerStatus == IntervalTimerStatus.Counting;
        #endregion IsTicking

        #region    TickTock
        /// <summary>
        /// The heartbeat object that Tick()s this IntervalTimer
        /// </summary>
        TickTock TickTock; // yyy code Services.TickTock split from TickTock
        #endregion TickTock

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

        #region    Name 
        /// <summary>
        /// Unique name assigned to this IntervalTimer
        /// </summary>
        //public string Name { get; protected set; }
        public string Name => Owner.IntervalTimerName();
        #endregion Name

        #region    IntervalsToZero
        /// <summary>
        /// This starts at whatever the user entered on the TimerStrip. It then counts down passing through zero into
        /// the negative zone where it shows how long ago the alarm sounded. At certain values
        /// the VTTimer will affected by a change in Phase.
        /// </summary>
        public int IntervalsToZero;
        #endregion IntervalsToZero

        #region    IntervalsToZeroOriginal
        /// <summary>
        /// We remember this value because when the Timer has expired we
        /// may have to reset it to its original value
        /// </summary>
        public int IntervalsToZeroOriginal;
        #endregion IntervalsToZeroOriginal

        #region    IntervalTimerStatus
        /// <summary>
        /// Can be either Paused or Counting
        /// </summary>
        public IntervalTimerStatus IntervalTimerStatus { get; protected set; } = IntervalTimerStatus.Paused;
        #endregion IntervalTimerStatus

        #region    Owner
        /// <summary>
        /// The object the IntervalTimer should Tick() every Interval
        /// </summary>
        public IIntervalTimerOwner Owner { get; private set; } = null;
        #endregion Owner

        #region    TimedCallbacks
        /// <summary>
        /// These are extra callbacks that can be added which have no regard for this application.
        /// I will split ??? IntervalTimer into an IntervalTimer Service an IntervalTimer application specific
        /// class inheriting from IntervalTimer
        /// </summary>
        protected List<(int SecondsToZero, Action Action)> TimedCallbacks = new List<(int intervalTimer, Action action)>();
        #endregion TimedCallbacks

        #region    UniqueId
        /// <summary>
        /// Each IntervalTimer is given a unique ascending integer key for some unknown reason. 
        /// Sometimes useful in debug
        /// </summary>
        public int UniqueId { get; protected set; } = -1;
        #endregion UniqueId

        #endregion Properties

        #region    Constructor
        /// <summary>
        /// Constructor for IntervalTimer. 
        /// be used
        /// </summary>
        /// <param name="intervals">Number of seconds before the Timer goes off</param>
        public IntervalTimer
        (
            IIntervalTimerOwner owner,
            int intervalsToZero,
            Action callbackOnTick,
            TickTock tickTock,
            IntervalTimerStatus intervalTimerStatus = IntervalTimerStatus.Paused,
            List<(int atInterval, Action action)> timedCallbacks = null,
            int id = -1
        )
        {
            /*This Timer operates in two manners. The first is a Callback every Interval.
             * The next is a List<Tuple(Interval, Callback)> which makes the Callback
             * when (if ever) IntervalsToZero reaches Interval. These can be reset at any time
             * as List<> is public get; 
             * First let's deal with the optional IntervalsToZero Callback.
            */

            IntervalsToZero = intervalsToZero;
            if (IntervalsToZero < 1 && callbackOnTick != null)
                throw new LogicException($"=>0{IntervalsToZero} with callback");
            if (IntervalsToZero > 0 && callbackOnTick == null)
                throw new LogicException($"=>0{IntervalsToZero} no callback");

            /* The Owner is the object that implements Tick() and is Ticked every Interval.
             * We need this first so we can use Owner.Trace()
             */
            Owner = owner ?? throw new LogicException("Owner is null");

            /* A unique id. This must be unique amongst all IntervalTimers belonging to its
             * TickTock. If it has been supplied by the caller (!= -1) it is the caller's
             * responsibility to ensure uniqueness.
             */
            UniqueId = id < 0 ? NextUniqueId : id;

            /* Let's tell the world we have started */

            Trace($"Entry Id:{UniqueId} ");

            /* we remember the original time so if the user resets this IntervalTimer we recreate
             * the IntervalTimer (in the code it is called Reload) with the original values
             */
            IntervalsToZeroOriginal = IntervalsToZero;

            /* Made TickTock non-static in case there are places where I need a finer
             * granularity than one second. So far not needed.
             */
            TickTock = tickTock;

            /* What to call when Tick() happens. 
             */
            CallbackOnTick = callbackOnTick; // => intervalTimer.Tick
            if (callbackOnTick == null)
                CallbackOnTick = Owner.Tick;

            /*
             * As well as being used for Sound 'events' IntervalTimer can be used for other purposes.
             * So a list of callbacks at various Intervals can be added. yyy not done yet
             */
            TimedCallbacks = timedCallbacks ?? new List<(int intervalTimer, Action action)>();

            Trace($"Exit");
        }
        #endregion Constructor()

        int cntr = 0;

        //#region    CreateIntervalTimer
        ///// <summary>
        ///// Factory Create a new IntervalTimer which is initially Paused. If you create more than one TickTock
        ///// the static variable TickTock will point to the TickTock used when the first IntervalTimer was
        ///// created
        ///// </summary>
        //public static IntervalTimer CreateIntervalTimer
        //(
        //    IIntervalTimerOwner owner,
        //    int intervalsToZero = 0,
        //    Action callbackOnTick = null,
        //    Models.TickTock mTickTock = null,  // yyy TickTock => TickTock + TickTock
        //    IntervalTimerStatus intervalTimerStatus = IntervalTimerStatus.Paused,
        //    List<(int intervalTimer, Action action)> timedCallbacks = null
        //)
        //{
        //    if (_CreateIntervalTimer)
        //        throw new LogicException("Only one TickTock allowed - coding deficiency and I don't want to use a singleton");
        //    _CreateIntervalTimer = true;
        //    Tracer.TraceStatic($"Entry owner:{owner} =>0{intervalsToZero}");
        //    /* Life is easier with an empty list rather than a nullable one  */

        //    timedCallbacks = timedCallbacks ?? new List<(int intervalTimer, Action action)>();
        //    mTickTock = mTickTock ?? Models.TickTock.This;

        //    var intervalTimer = new IntervalTimer(
        //                        owner: owner,
        //                        intervalsToZero: intervalsToZero = 0,
        //                        callbackOnTick: callbackOnTick = null,
        //                        mTickTock: Models.TickTock.This,
        //                        intervalTimerStatus: intervalTimerStatus,
        //                        timedCallbacks: timedCallbacks);

        //    MainPage.This.TickTock.AddTicker(intervalTimer);

        //    Tracer.TraceStatic($"Exit  owner:{owner} =>0{intervalsToZero}");
        //    return intervalTimer;
        //}

        //static bool _CreateIntervalTimer = false;
        //#endregion CreateIntervalTimer

        #region    Pause
        /// <summary>
        /// Pause or the IntervalTimer. All this really means is that it ignores MTickTock.Tick. I
        /// really should replace MTickTock.IntervalTimer.Tick() with a callback pointing at Tick()
        /// </summary>
        public virtual void Pause()
        {
            if (IntervalTimerStatus == IntervalTimerStatus.Paused)
                Statics.InternalProblem($"already Paused");
            IntervalTimerStatus = IntervalTimerStatus.Paused;
            Trace($"Entry&Exit {IntervalTimerStatus}");
        }
        #endregion Pause

        #region    Reload
        /// <summary>
        /// When the user presses reload we reset all the values in this IntervalTimer. 
        /// </summary>
        internal virtual void Reload()
        {
            Trace($"Entry Ints={IntervalsToZero}");
            IntervalTimerStatus = IntervalTimerStatus.Paused;
            IntervalsToZero = IntervalsToZeroOriginal;
            Trace($"Exit Ints={IntervalsToZero} ");
        }
        #endregion Reload

        #region    Resume
        /// <summary>
        /// Resume the IntervalTimer. All this really means is that it acts on TickTock.Tick(). I
        /// really should replace TickTock.IntervalTimer.Tick() with a callback pointing at Tick() ???
        /// </summary>
        public virtual void Resume()
        {
            if (IntervalTimerStatus == IntervalTimerStatus.Counting)
                Statics.InternalProblem($"already Counting");
            IntervalTimerStatus = IntervalTimerStatus.Counting;
            Trace($"Entry&Exit Paused=>Counting");
        }
        #endregion Resume

        #region    RemoveTimer
        /// <summary>
        /// Remover this Ticker 
        /// </summary>
        void RemoveTimer() => TickTock.RemoveTicker((ITick)this);
        #endregion RemoveTimer

        #region    Ticked
        /// <summary>
        /// A list of events (MultiCastDelegate) to be triggered on each Tick
        /// </summary>
        public EventHandler Ticked;
        #endregion Ticked

        #region    Tick
        /// <summary>
        /// Another second of your life wasted, let's knock it off the egg timer.
        /// See 'Death' in Terry Pratchett's Discworld series.
        /// <para/>When the big TickTock in the sky goes Tick, that tick is propagated 
        /// via callbacks to the Owner. In terms of the VIntervalTimers this tick is
        /// propagated to the VIntervalTimer and thence to the MSound, the 
        /// TimerStrip and then to the TimerSegments.
        /// (As of writing the only Owners are VIntervalTimers, but I'm hoping to use this
        /// code elsewhere sometime.)
        /// </summary>
        public void Tick()
        {
            if (IsPaused)
            {
                Trace($"Entry&Exit {IntervalTimerStatus} Ints={IntervalsToZero} ");
                return;
            }
            Trace($"Entry counter={cntr++} Ints={IntervalsToZero} ");
            cntr++;
            /* First off we execute the user's callbacks as these may change what is done in the other
             * 'official' designated callback (callbackOnTick) and/or in the EventHandler Ticked
             */
            TimedCallbacks.Where(cb => cb.SecondsToZero == IntervalsToZero).ForEach(tup => tup.Action());

            /* A 'normal' Tick. 
             */
            IntervalsToZero--;
            CallbackOnTick();

            //if (Intervals == 0 && CallbackOnZero != null) ??? decide if count goes up or down
            //    CallbackOnZero();  // => new Action(vintervalTimer.BlastOff) => MSound.ScheduleAlarm


            Trace($"Exit counter={--cntr} Ints={IntervalsToZero}");
            cntr--;
            return;
        }
        #endregion Tick

        #region    Trace()
        /// <summary>
        /// Write to a trace file or the console
        /// </summary>
        /// <param name="info">Entry Exit or 'whatever' data to be traced</param>
        /// <param name="member">[CallerMemberName]</param>
        /// <param name="line">[CallerLineNumber]</param>
        /// <param name="path">[CallerFilePath]</param>
        [System.Diagnostics.DebuggerStepThrough()] // never want to StepInto Trace unless debugging Tracer
        public void Trace(string info,
                            [CallerMemberName]string member = "",
                            [CallerLineNumber] int line = 0,
                            [CallerFilePath] string path = "")
        {
            Owner.Trace(info, member, line, path);
        }
        #endregion Trace()

        #region     IEquatable stuff, ToString

        #region    Equals
        /// <summary>
        /// All objects are different based on a unique key.
        /// </summary>
        /// <param name="mtimer"></param>
        /// <returns></returns>
        public bool Equals(IntervalTimer mtimer) => mtimer == null ? false : UniqueId == mtimer.UniqueId;

        #endregion Equals

        #region    GetHashCode
        /// <summary>
        /// Use a simpler Equals. The objects are the same if they have the same unique key
        /// rather than basing the HashCode on the whole object
        /// </summary>
        /// <returns>true if they are the same object</returns>
        public override int GetHashCode() => UniqueId;

        //void ITick.Tick() => throw new NotImplementedException();
        #endregion GetHashCode

        #region    ToString
        /// <summary>
        /// Show the Name of this Timer
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString() => Name + UniqueId + "@" + IntervalsToZero;
        public void Dispose() { TickTock.RemoveTicker(this); }
        #endregion ToString

        #endregion IEquatable stuff, ToString
    }

    #region    TimerStatus ENUM
    /// <summary>
    /// The status of the Alarm
    /// </summary> 
    public enum IntervalTimerStatus
    {
        None,
        Counting,
        Paused
    }
    #endregion TimerStatus enum,

    #region    IIntervalTimerOwner
    /// <summary>
    /// For any object that instantiates IntervalTimer
    /// </summary>
    public interface IIntervalTimerOwner
    {
        void Tick();
        void Trace(string info,
                    [CallerMemberName]string member = "",
                    [CallerLineNumber] int line = 0,
                    [CallerFilePath] string path = "");

        string IntervalTimerName();
    }
    #endregion IIntervalTimerOwner

    #region    ITick
    /// <summary>
    /// Ensures that object ticks on the Interval
    /// </summary>
    public interface ITick
    {
        void Tick();
        void Trace(string info,
                            [CallerMemberName]string member = "",
                            [CallerLineNumber] int line = 0,
                            [CallerFilePath] string path = "");
    }
    #endregion ITick
}


