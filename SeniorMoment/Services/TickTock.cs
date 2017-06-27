using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace SeniorMoment.Services
{
    public class TickTock
    {
        #region    Properties

        #region    AgeInMilliseconds
        /// <summary>
        /// How old TickTock is since it was created or, if started, since it was started
        /// </summary>
        public int AgeInMilliseconds => (int)(DateTime.Now - startTime).TotalMilliseconds;
        #endregion AgeInMilliseconds

        #region    cachedTicks
        /// <summary>
        /// used By Pause, Resume and IsPauseAvailable. See comments in Tick
        /// </summary>
        int cachedTicks = 0;
        #endregion cachedTicks

        #region    Interval
        /// <summary>
        /// The Interval for ticking. Must be 1000 for release (1000 ms = 1 sec)
        /// </summary>
        public int Interval { get; protected set; } = 1000; //  you can speed thing up by specifying 100, slow it down by using 10000
        #endregion interval

        #region    IntervalOriginal
        /// <summary>
        /// The Interval for ticking. Must be 1000 for release (1000 ms = 1 sec)
        /// </summary>
        public int IntervalOriginal { get; private set; } = 1000; //  you can speed thing up by specifying 100, slow it down by using 10000
        #endregion IntervalOriginal

        #region    Tickers
        /// <summary>
        /// List of objects which have subscribed to Tick() are not MTimers that also need good Ticking off
        /// </summary>
        public List<ITick> Tickers { get; } = new List<ITick>();
        #endregion Tickers

        #region    oncePerIntervalTimer
        /// <summary>
        /// Once a second timer that calls Tick()
        /// </summary>
        System.Threading.Timer oncePerIntervalTimer;
        #endregion oncePerIntervalTimer

        #region    coreDispatcher
        /// <summary>
        /// Used to dispatch on the UI thread when the timer goes off on a non-UI thread
        /// </summary>
        CoreDispatcher coreDispatcher => Statics.CoreDispatcher;
        #endregion coreDispatcher

        #region    timerDelegate
        /// <summary>
        /// The delegate for "Top-of-the-hour" once-per-second Tick()
        /// </summary>
        static TimerCallback timerDelegate = null;
        #endregion timerDelegate

        #region    startTime
        /// <summary>
        /// Time at which Start() was issued
        /// </summary>
        DateTime startTime = DateTime.Now; // initialized so no exceptions in Debug
        #endregion startTime

        #region    This
        /// <summary>
        /// A pointer to the TickTock. There can only be one. If two exist
        /// then we have a problem
        /// </summary>
        public static TickTock This = null;
        #endregion This

        #endregion Properties

        #region    Constructor TickTock
        /// <summary>
        /// This is the heart-beat of the program. Every 'interval' (which is one second)
        /// all the alarms have to update themselves, maybe change Phase and possible
        /// move from a CountDown stage to the Alarm stage
        /// </summary>
        /// <param name="intervalInMilliseconds"></param>
        public TickTock(int intervalInMilliseconds)
        {
            This = This ?? this; // always point at the first TickTock
            IntervalOriginal = Interval = intervalInMilliseconds;
        }
        #endregion Constructor MTickTock

        #region    AddTIcker
        /// <summary>
        /// Add a One-second ticker to List(Tickers), 
        /// </summary>
        /// <param name="ticker"></param>
        public void AddTicker(ITick ticker)
        {
            Tickers.Add(ticker);
        }
        #endregion AddTIcker

        #region    CacheTicks
        /// <summary>
        /// Sets the mode for responding to Ticks. If 'on' then the ticks are cached.
        /// This means that we do not propagate the ticks. Rather we cache the ticks and 
        /// later replay them a higher speed to catch up. This method is aimed at 
        /// debug when there are just too many Ticks screwing us. See comments in Tick
        /// </summary>
        public void CacheTicks(bool on)
        {
            if (on)
            {
                if (cachedTicks != 0)
                    cachedTicks = 1; // bit of a lie. The number of cached ticks is one less than this
            }
            /*
                Check we are Paused and then Resume at 5 times speed till we 
                catch up to real time. See comments in Tick
            */
            else
            {
                if (cachedTicks < 1)
                    Statics.InternalProblem($"on:{on} cachedTicks={cachedTicks}");
                cachedTicks = -cachedTicks;
                oncePerIntervalTimer.Change(0, IntervalOriginal / 5);
            }
        }
        #endregion CacheTicks

        #region    Pause / Resume / IsPaused

        #region  _Paused
        /// <summary>
        /// flag used by  Pause / Resume / IsPaused
        /// </summary>
        bool _Paused = false;
        #endregion _Paused

        #region    Pause
        /// <summary>
        /// Stop Ticks propagating to the Tickers
        /// </summary>
        public void Pause()
        {
            if (_Paused == true)
                throw new LogicException("Already paused");
            _Paused = true;
        }
        #endregion Pause

        #region    Resume
        /// <summary>
        /// Set the Paused TickTock going
        /// </summary>
        public void Resume()
        {
            if (_Paused == false)
                throw new LogicException("not paused");
            _Paused = true;
        }
        #endregion Resume

        #region    IsPaused
        /// <summary>
        /// flip-flop switch whether ticks are propagated to the registered Tickers
        /// </summary>
        public bool IsPaused => _Paused;
        #endregion IsPaused

        #endregion Pause / Resume

        #region    RemoveTicker
        /// <summary>
        /// Remove a Ticker from whichever lists it is in
        /// </summary>
        /// <param name="ticker"></param>
        public void RemoveTicker(ITick ticker)
        {
            Tickers.Remove(ticker);
        }
        #endregion RemoveTicker

        #region    Start
        /// <summary>
        /// Start the big ticker going
        /// </summary>
        public void Start()
        {
            if (timerDelegate != null)
                Statics.InternalProblem("TickTock already running");
            {
                startTime = DateTime.Now;
                timerDelegate = new TimerCallback(tickCallback);
                oncePerIntervalTimer = new System.Threading.Timer(timerDelegate, null, 0, Interval);
            }
        }
        #endregion Start

        #region    Tick
        /// <summary>
        /// When this service Ticks we propagate that to all registered IntervalTimers
        /// ...EXCEPT we may be Paused - which means we do not propagate the ticks - or we may be
        /// Cached. Cached means we do not propagate the Ticks but 'remember' them.
        /// When we stop the cacheing we then replay the Ticks at a higher speed.
        /// </summary>
        public void Tick()
        {
            if (_Paused)
                return;

            /*
             * cachedTicks is in one of four states:
             * 
             * 0:   This is the normal state and Ticks at once per second are propagated
             *      through MTimers, VMTimers and TimerStrips
             *      
             * >0:  We are in a Paused state. Ticks are NOT propagated. We maintain a count
             *      of non-propagated Ticks so we can replay them at higher speed. This is
             *      set by Pause
             *      
             * <-1: When TickTock.CacheTicks(on: false) is called it flips CachedTicks from 
             *      positive to  negative. cachedTicks is now used for a different purpose as it 
             *      cachedTicks++ until it reaches -1 and TicksPropagated == Ticks.
             *      In this 'high-speed' mode we propagate more Ticks per second through the 
             *      timers until...
             *      
             * -1:  We reset the interval back to a second and we are 'normal' one again.
             */
            if (cachedTicks < -1)
            {
                cachedTicks++;
                return;
            }
            if (cachedTicks == -1)
            {
                cachedTicks = 0;  // we lose one tick because we gained one at Pause;
                oncePerIntervalTimer.Change(0, IntervalOriginal);
                return;
            }

            /*
             * The MTimers Tick() if they are not Paused(). They then VMTimer.Tick() which
             * is the callback passed to CreateMTimer().
             */
            Tickers.ForEach(ticker => ticker.Tick());
        }
        #endregion Tick

        #region    tickCallback
        /// <summary>
        /// Every second we tell all the Timers that another second of their brief lives has passed
        /// </summary>
        /// <param name="state"></param>
        private void tickCallback(object stateInfo)
        {
            /*
             * Run the callback as another task
            */
            var temp = Task.Run(() => Tick());
        }
        #endregion    tickCallback

        #region    ToString
        /// <summary>
        /// Return the name -primarily used for debugging when first written
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.DebuggerStepThrough()]
        public virtual new string ToString() => "TickTock Interval=" + Interval;
        #endregion ToString
    }
}
