using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SeniorMoment.Services;
using SeniorMoment.ViewModels;
using Windows.UI.Core;

namespace SeniorMoment.Models
{
    public class MTickTock : ITick
    {
        #region    Properties

        #region    This
        /// <summary>
        /// A pointer to the TickTock. There can only be one. If two exist
        /// then we have a problem
        /// </summary>
        public static MTickTock This = null; // TickTock has a TickTock.This
        #endregion This

        #region    MTimers
        /// <summary>
        /// MTickTock wraps TickTock. It keeps the List{MTimer}. All other Tickers
        /// belong to TickTock.
        /// </summary>
        List<MTimer> MTimers = new List<MTimer>();
        #endregion MTimers

        #region    TickTock
        /// <summary>
        /// MTickTock is only responsible for MTimer.Tick. TickTock does trhe real
        /// Ticking, and hands it on to here,
        /// </summary>
        TickTock TickTock = new TickTock(1000); // 1 second interval
        #endregion TickTock

        #endregion Properties

        #region    Constructor MTickTock
        /// <summary>
        /// This is the heart-beat of the program. Every 'interval' (which is one second)
        /// all the alarms have to update themselves, maybe change Phase and possible
        /// move from a CountDown stage to the Alarm stage. This does all the SeniorMoment stuff,
        /// but all the Timing stuff is done by IntervalTimer. MTickTock does not need to reference
        /// TickTock except to register itself as a MTickTock.Ticker.
        /// This code looks a bit odd. It is registering MTiCkTock with its own subclass
        /// TickTock. So TickTock.Tick => MTickTock.Tick => MTimers.Tick.
        /// That's why MTickTock implements ITick
        /// </summary>
        /// <param name="intervalInMilliseconds"></param>
        public MTickTock(int intervalInMilliseconds) 
        {
            This = This ?? this; // global access to the first MTickTock created
            TickTock.AddTicker(this);
        }
        #endregion Constructor MTickTock

        #region    AddMTimer
        /// <summary>
        /// If this is an MTimer then we look after it in MTimer as it is a SeniorMoment
        /// variable as opposed to the  SeniorMoment.Services IntervalTimer
        /// </summary>
        /// <param name="mTimer"></param>
        public void AddMTimer(MTimer mTimer) { lock (MTimers) { MTimers.Add(mTimer); } }
        #endregion AddTIcker

        #region    RemoveMTimer
        /// <summary>
        /// An MTimer is being Disposed so remove it from the list of Tick-able MTimers
        /// </summary>
        /// <param name="mtimer"></param>
        public void RemoveMTimer(MTimer mTimer)
        {
            MTimers.Remove(mTimer);
        }
        #endregion RemoveMTimer

        #region    Start
        /// <summary>
        /// Start TickTock which calls this.Tick
        /// </summary>
        public void Start() => TickTock.Start();
        #endregion Start

        #region    Tick
        /// <summary>
        /// When this service Ticks we propagate that to all active MTimers and
        /// they inform their VMTimer which in turn informs each VMTimer.TimerStrip
        /// </summary>
        public void Tick()
        {
            /* clean out any dead VMTimers. Has to be done in a static method as we are
             * updating a static List<VMTimers>
             */
            VMTimer.TickStatic();
            /* Propagate through VMTimers */

            MTimers.ForEach(timer => timer.Tick());
            /*
             * This is currently a NOP
             */
            MainPage.This.Tick();
        }
        #endregion Tick

        #region    ToString
        /// <summary>
        /// Return the name -primarily used for debugging when first written
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString() => $"MTickTock ";
        #endregion ToString

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
            Tracer.TracerMain.TraceCrossTask(info, member, line, path);
        }
        #endregion Trace()
    }
}
