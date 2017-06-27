using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SeniorMoment.Services;

namespace SeniorMoment.Models
{
    /// <summary>
    /// A class to hold static variables which are dependant on this program
    /// for their existence. Whereas the Services folder could be transferred to
    /// another UWP project and compiled. The Services.Statics holds stuff which
    /// makes references things in SeniorMoment MVVM classes
    /// </summary>
    static public class MStatics
    {
        #region    MTickTock
        /// <summary>
        /// The heartbeat of the program. The once per second update both
        /// visually and 'modelly'
        /// </summary>
        public static MTickTock MTickTock;
        #endregion MTickTock

        #region    MAXTIMERS
        /// <summary>
        /// Maximum number of timers
        /// </summary>
        public const int MAXTIMERS = 24;
        #endregion MAXTIMERS

        #region    MINRECORDINGMILLISECONDS
        /// <summary>
        /// The mininimum time that the user records hiser voice
        /// </summary>
        static public int MINRECORDINGMILLISECONDS = 1500;
        #endregion MINRECORDINGMILLISECONDS

        //#region    TickingStatuses xxx moved to MTimer
        ///// <summary>
        ///// The various statuses that indicate a paused MTimer
        ///// </summary>
        //static public List<TimerStatus> TickingStatuses { get; } = new List<TimerStatus>
        //{ TimerStatus.Counting, TimerStatus.GoingOff};
        //#endregion TickingStatuses

        #region    TraceBeginsWhenSecondsToZeroIs
        /// <summary>
        /// When the VMTimers should begin tracing. 
        /// </summary>
        static public int TraceBeginsWhenSecondsToZeroIs = int.MinValue;
        #endregion TraceBeginsWhenSecondsToZeroIs

        #region    TraceEndsWhenSecondsToZeroIs
        /// <summary>
        /// When the VMTimers should stop tracing
        /// </summary>
        static public int TraceEndsWhenSecondsToZeroIs = int.MaxValue;
        #endregion TraceEndsWhenSecondsToZeroIs

    }
}
