using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SeniorMoment.Services;
using Windows.UI;

namespace SeniorMoment.ViewModels
{
    /// <summary>
    /// Describes the Phase of an TimerStrip from birth to death
    /// </summary>
    public class VMPhase : ITracer
    {
        #region    Properties

        #region    Callback (delegate)
        /// <summary>
        /// Delegate that controls what to do when there is a phase chage.
        /// Stored in Phase. If this is null no call is made. NOTE the Callback
        /// will not be running on the UI thread.
        /// </summary>
        public PhaseAction<VMTimer> Callback { get; private set; }
        #endregion Callback (delegate)

        #region    Color
        /// <summary>
        /// New color of the TimerStrip buttons
        /// </summary>
        public Color Color { get; private set; }
        #endregion Color

        #region    delegate void PhaseAction();
        /// <summary>
        /// What to do at this new phase in the life of an Alarm
        /// </summary>
        /// <param name="phase"></param>
        public delegate void PhaseAction<VMTimer>();
        #endregion delegate void PhaseAction();

        #region    PhaseDead
        /// <summary>
        /// When a VMTimer is set to this phase the next VMTimer.TickStatic will dispose of
        /// this VMTimer and associated MTimer and MSound
        /// </summary>
        public static VMPhase PhaseDead => new VMPhase(-secsPerDay * 40, Colors.MediumPurple, null, 1800, 1.0, 100, "Dead");
        #endregion PhaseDead

        #region    Priority
        /// <summary>
        /// The priority of Alarms during playback
        /// </summary>
        public int Priority { get; private set; }
        #endregion Priority

        #region    Phases
        /// <summary>
        /// Each VMTimer lives a lifecycle passing from one Phase to Another until
        /// it dies at 40 days of the user Pauses or deletes it.
        /// </summary>
        public static List<VMPhase> Phases = new List<VMPhase>
            { /*  DisplayMessage is important due to VMTimers.Where(timer=>timer.DisplayMessage=="whatever")

               Parameters are:

               SecondsToZero:   This defines when a Phase is active. The active Phase will be the last Phases.Phase
                                where MTimer.TimeToZero >= Phases.Phase.TimeToZero and < tthe previous one.
               Color            for countdown tiles whilst in this phase
               CallBack         A callback when zero is reached. It's not needed now
               SecondsbetweenAlarms  This used to MTimer.SecondsTo -1 means no alarm (ie we haven't reached zero yet)
               Volume in range 0.0 to 1.0
               Priority of the alarm, lower number means higher priority
               A phrase we can see in debug to identify the phase
              */
                new VMPhase(int.MaxValue - 3, Colors.White, null, -1, 1.0, 100, "Counting down"),
                new VMPhase(secsPerDay, Colors.LightGray, null, -1, 1.0, 100, "+1Day"),
                new VMPhase(10800, Colors.PaleGoldenrod, null, -1, 1.0, 100, "+3Hours"),
                new VMPhase(3600, Colors.DarkKhaki, null, -1, 1.0, 100, "+1Hour"),
                new VMPhase(300, Colors.Olive, null, -1, 1.0, 100, "+5Mins"),
                new VMPhase(10, Colors.MediumSpringGreen, null, -1, 1.0, 100, "+1Minute"),
                new VMPhase(0, Colors.Lime, null, 0, 1.0, 100, "HeadingToZero"),
                new VMPhase(-20, Colors.Red, null, 2, 0.8, 30, "-10sex"),       // This and next two are testing values
                new VMPhase(-60, Colors.DeepPink, null, 4, 0.7, 40, "-30sex"),
                new VMPhase(-240, Colors.HotPink, null,8, 0.6, 50, "-120sex"),
                new VMPhase(-1800, Colors.Fuchsia, null, 16, 1.0, 60, "Dead"),
                new VMPhase(-secsPerDay, Colors.MediumPurple, null, 1800, 0.6, 70, "-1Day"),
                new VMPhase(-secsPerDay* 33 , Colors.LightGray, null, 1800, 1.0, 80, "-33Days"),
                new VMPhase(-secsPerDay* 39 , Colors.Gray, null, 1800, 1.0, 90, "-39Days"),
                PhaseDead
        };
        #endregion Phases

        //#region    Phases  These are closer to real values than the testing ones above, except for priorities
        ///// <summary>
        ///// Each VMTimer lives a lifecycle passing from one Phase to Another until
        ///// it dies at 40 days of the user Pauses or deletes it.
        ///// </summary>
        //public static List<VMPhase> Phases = new List<VMPhase>
        //    { //  DisplayMessage is important due to VMTimers.Where(timer=>timer.DisplayMessage=="whatever")
        //      // Parameters are:
        //      // SecondsToZero: when a timer reaches it it jumps to the next phase
        //      // Color for countdown tiles whilst in this phase
        //      // A callback when that number is reached
        //      // number of seconds between alarms -1 means no alarm (ie we haven't reached zero yet)
        //      // Volume in range 0.0 to 1.0
        //      // A phrase we can see in debug to identify the phase
        //        new VMPhase(int.MaxValue - 3, Colors.White, null, -1, 1.0, 100, "Counting down"),
        //        new VMPhase(secsPerDay, Colors.LightGray, null, -1, 1.0, 100, "+1Day"),
        //        new VMPhase(10800, Colors.PaleGoldenrod, null, -1, 1.0, 100, "+3Hours"),
        //        new VMPhase(3600, Colors.DarkKhaki, null, -1, 1.0, 100, "+1Hour"),
        //        new VMPhase(300, Colors.Olive, null, -1, 1.0, 100, "+5Mins"),
        //        new VMPhase(120, Colors.MediumSpringGreen, null, -1, 1.0, 100, "+1Minute"),
        //        new VMPhase(0, Colors.Lime, null, 5, 1.0, 100, "HeadingToZero"),
        //        new VMPhase(-300, Colors.Red, null, 10, 0.8, 100, "-5Minutes"),
        //        new VMPhase(-1800, Colors.DeepPink, null, 60, 0.7, 100, "-30Minutes"),
        //        new VMPhase(-18000, Colors.HotPink, null, 120, 0.6, 100, "-5Hours"),
        //        new VMPhase(-secsPerDay, Colors.HotPink, null, 120, 0.6, 100, "-1Day"),
        //        new VMPhase(-secsPerDay* 33 , Colors.Fuchsia, null, 1800, 1.0, 100, "-33Days"),
        //        new VMPhase(-secsPerDay* 39 , Colors.MediumPurple, null, 1800, 1.0, 100, "-39Days"),
        //        PhaseDead,
        //        PhaseReload
        //};
        //#endregion Phases

        #region    secsPerDay 86400
        /// <summary>
        /// Constant seconds in a day = 86400
        /// </summary>
        const int secsPerDay = 86400;
        #endregion secsPerDay

        #region    SecondsToNextAlarm (end of one Timer sound and start of next)
        /// <summary>
        /// What time intervals between repeating the Timer signal
        /// </summary>
        public int SecondsBetweenAlarms { get; private set; }
        #endregion SecondsToNextAlarm (end of one Timer sound and start of next)

        #region    SecondsToZero
        /// <summary>
        /// Seconds at which we change to the next Phase]>
        /// </summary>
        public int SecondsToZero { get; private set; }
        #endregion SecondsToZero

        #region    Volume
        /// <summary>
        /// Volume for the signal
        /// </summary>
        public double Volume { get; private set; } = 1;
        #endregion Volume

        #endregion Properties

        #region    GetPhaseFromSecondsToZeroStatic
        /// <summary>
        /// Given the number of SecondToAlarm work out which phase we are in.
        /// </summary>
        /// <param name="secondsToZero">Number of seconds left to get down to the first alarm.
        /// This can be negative signifying this TimerStrip/VMTimer are still running even
        /// though the alarm time has past</param>
        /// <returns></returns>
        static public VMPhase GetPhaseFromSecondsToZeroStatic(int secondsToZero)
        {
            Tracer.TracerMain?.TraceCrossTask($"Entry =>0:{secondsToZero}");
            VMPhase phase = VMPhase.Phases.First(fase => fase.SecondsToZero < secondsToZero);
            Tracer.TracerMain?.TraceCrossTask($"Exit  Phase:{phase.ToString()}");
            return phase;
        }
        #endregion GetPhaseFromSecondsToZeroStatic

        #region    Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="secondsToZero"></param>
        /// <param name="color"></param>
        /// <param name="whatToDo"></param>
        /// <param name="secondsBetweenAlarms"></param>
        /// <param name="volume"></param>
        /// <param name="displayMessage"></param>
        public VMPhase(int secondsToZero, Color color, PhaseAction<VMTimer> callback, int secondsBetweenAlarms, double volume, int priority, string displayMessage)
        {
            Trace($"Entry&Exit =>0:{secondsToZero}");
            SecondsToZero = secondsToZero;
            Color = color;
            Callback = callback;
            SecondsBetweenAlarms = secondsBetweenAlarms;
            Volume = volume;
            DisplayMessage = displayMessage;
            Priority = priority;
        }
        #endregion Constructor

        #region    DisplayMessage
        /// <summary>
        /// ToString() value, helps with debugging
        /// </summary>
        public string DisplayMessage { get; private set; } = "";
        #endregion DisplayMessage

        #region    ToString()
        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString() => DisplayMessage;
        #endregion ToString()

        #region    Trace()
        /// <summary>
        /// Write to the trace file or the console
        /// </summary>
        /// <param name="info"></param>
        /// <param name="member"></param>
        /// <param name="line"></param>
        /// <param name="path"></param>
        [System.Diagnostics.DebuggerStepThrough()]
        public void Trace(
                string info,
                [CallerMemberName] string member = "",
                [CallerLineNumber] int line = 0,
                [CallerFilePath] string path = "")
                             => Tracer.TracerMain.TraceCrossTask(info, member, line, path);
        #endregion Trace()
    }
}
