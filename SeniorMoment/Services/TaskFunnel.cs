using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SeniorMoment.Services
{
    /// <summary>
    /// Provides for executing only one action at a time in a sequence dictated by
    /// Age within Priority. Never been tested with two TaskFunnels at the same
    /// time.
    /// </summary>
    public class TaskFunnel : TaskScheduler, ITracer
    {
        /* -------------------------EXPLANATION OF QUEUEING  __________________________
        
            One of the most important things is that Play, Say and Record are mutually and self-exclusive.
            Only one task can run at a time. Hence the name Funnel. Let's suppose Sound.Say("Hello world")
            has been called. Say has the choice of running it then and there, or adding it to the 
            ActionQueue. Currently it inspects the variable SoundStatus and if it is Idle then it
            executes the task itself immediately. Theoretically this should be OK. I think at a later date
            all things should pass through here. So assume that SoundStatus is either Record or Play
            then we add it to the Task Queue. This is a little more complex than it seems. The TaskFunnel
            has to absorb not only the Action but also the Action's Parameters. So what get's the saved
            thread-safe ConcurrentQueued ActionQueue is a QueuedTask. This contains the newly created
            Task, the original Action, the Action's Parameters, a Priority and how 'old' it is. 
            Every time a task is run, deleted or added the ActionQueue is re-sorted  by Priority
            and Age within Priority.
            TickTock signals Tick() and TaskFunnel checks if it has anything more to do. 
        */


        /* This class maintains a queue of Actions to be executed as Tasks sequentially. 
         * Suppose two actions arrive, first is Action (A), second is Action(B). 
         * If (B) has a higher Priority than (A) then (B) will be executed before (A)
         * regardless of when they where queued. If (A) and (B) have the same Priority
         * then (A) is executed first as it arrived first.
         */

        #region    Properties

        #region    ActionQueue
        /// <summary>
        /// This is a list of Actions to be performed in order unless there is something in the Priority queue
        /// </summary>
        ConcurrentQueue<QueuedTask> ActionQueue = new ConcurrentQueue<QueuedTask>();
        #endregion ActionQueue

        #region    IsBusy
        /// <summary>
        /// Says if there is a Task running or something waiting. 
        /// </summary>
        public bool IsBusy => !(RunningTask == null || RunningTask.IsCompleted);
        #endregion IsBusy

        #region    Lock
        /// <summary>
        /// Just an object that we can place a lock(Lock){...} on.
        /// </summary>
        object Lock = new object();
        #endregion Lock

        #region    MaximumPriorityActions
        /// <summary>
        /// The maximum number of Priority Tasks than can exist simultaneously in the
        /// ConcurrentQueue
        /// </summary>
        public int MaximumPriorityActions { get; private set; }
        #endregion MaximumPriorityActions

        #region    RunningTask
        /// <summary>
        /// The task running at the moment else null
        /// </summary>
        public Task RunningTask { get; private set; }
        #endregion RunningTask

        public Tracer Tracer => Services.Tracer.TracerMain;

        #endregion Properties

        #region    Constructor
        /// <summary>
        /// TaskFunnel is a task serializer that allows one task at a time to run
        /// </summary>
        /// <param name="maximumPriorityTasks">Gives the maximum number of entries in PriorityQueue. 0 means none</param>
        public TaskFunnel(int maximumPriorityTasks = int.MaxValue)
        {
            Trace($"Entry");
            if (maximumPriorityTasks < 0)
                Statics.InternalProblem("maximumPriorityTask==0");
            MaximumPriorityActions = maximumPriorityTasks;
            Trace($"Exit");
        }
        #endregion Constructor

        #region    InformTaskFunnelEventCompleted
        /// <summary>
        /// Some Tasks have two type of 'finishing'. For example if the Task contains
        /// MediaPlayer.Play() the Play() is issued and the Task ends. TaskFunnel then fires
        /// the OnTaskEnded event. However there is
        /// another 'finish' which I call Completed when the MediaPlayer has finished playing 
        /// and fires an event like OnMediaEnded. We use that event to tell TaskFunnel that
        /// the Task (ie Play) has Completed.
        /// </summary>
        public void InformTaskFunnelEventCompleted ()
        {
            Trace($"Entry");
            /*
             * EventHandler FunnelledTaskCompleted will be null unless 'someone' has don
             * a FunnelledTaskCompleted+= OnFunnellCompleted (or whatever) */

            FunnelledTaskCompleted?.Invoke(this, RunningTask);
            Trace($"Exit");
        }
        #endregion InformTaskFunnelEventCompleted

        #region    MeNext(QueuedTask)
        /// <summary>
        /// This is how the outside world requests access to the TaskFunnel. It uses a Thread safe ConcurentQueue{Action}.
        /// Any given routine like MSound.Say() may decide to execute it straight away or to enter it into the queue.
        /// We treat a QueuedTask as immutable and so have to recreate it.
        /// <param name="action">The action that the caller wants scheduled to be run sequentially as a task</param>
        /// </summary>
        /// <example>
        /// </example>
        /// <param name="qt">The QueuedTask used to create a task and its self-reference</param>
        /// <returns>The QueuedTask which holds sufficient information to recreate the Task</returns>
        public void MeNext(QueuedTask qt)
        {
            Trace($"Entry {qt}");
            lock (ActionQueue)
            {
                ActionQueue.Enqueue(qt);
                /* 
                 * When we add a QueuedTask to ActionQueue and there is at least one task already
                 * there then we must sort because there may be different priorities. Just Age alone
                 * is insufficient
                */
                if (ActionQueue.Count > 1)
                    ActionQueue = new ConcurrentQueue<QueuedTask>(ActionQueue.OrderBy(qa => qa.SortKey));
                StartNextTask(); // sets RunningTask
            }
            Trace($"Exit   {qt}");
            return;
        }
        #endregion MeNext

        #region    Overriding virtual functions required by constructor: public class TaskFunnel:TaskScheduler

        #region    GetScheduledTasks
        /// <summary>
        /// As required by abstract TaskScheduler.GetScheduledTasks
        /// </summary>
        /// <returns>a list of scheduled tasks</returns>
        /// <summary>
        /// This is required because of a virtual method in class TaskScheduler
        /// </summary>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return ActionQueue.Select(qt => qt.Task);
        }
        #endregion GetScheduledTasks

        #region    QueueTask
        /// <summary>
        /// This is required because of a virtual method in class TaskScheduler
        /// </summary>
        /// <param name="task"></param>
        protected override void QueueTask(Task task)
        {
            /*
             * Took me a while here. I had the signature:
             
             * protected internal override void QueueTask (Task task)
             
               I was getting a message...
             
               "Cannot change access modifiers when overriding 'protected internal'
                     inherited member 'TaskScheduler.QueueTask(Task)"
              
               Explanation here: https://stackoverflow.com/questions/2375792/overriding-protected-internal-with-protected
            */
        }
        #endregion QueueTask

        #region    TryExecuteTaskInline
        /// <summary>
        /// Overriding abstract TaskScheduler.TryExecuteTaskInline(Task,bool)
        /// </summary>
        /// <param name="task"></param>
        /// <param name="taskWasPreviouslyQueued">
        /// A Boolean denoting whether or not task has 
        /// previously been queued. If this parameter is True, then the task MAY have been
        /// previously queued (scheduled); if False, then the task is known not to have been queued, 
        /// and this call is being made in order to execute the task inline without queuing it.
        /// We will always want this true I think. 
        /// https://msdn.microsoft.com/en-us/library/system.threading.tasks.taskscheduler.tryexecutetaskinline%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396
        /// </param>
        /// <returns></returns>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            //
            return true;
        }
        #endregion TryExecuteTaskInline

        #endregion Overriding virtual functions required by constructor:  public class TaskFunnel:TaskScheduler

        #region    StartNextTask
        /// <summary>
        /// Kick off the next action in the queue if any. The Actions are converted into
        /// tasks to be run. If no actions are available we return  and have another
        /// another go at finding and executing a Task
        /// </summary>
        /// <returns>The Task that has started which will have a chain of ContinueWiths, the last
        /// of which signals the task has finished</returns>
        public Task StartNextTask()
        {
            if (IsBusy)
            {
                Trace($"Entry&Exit Busy");
                return null;
            }
            if (ActionQueue.Count == 0)
            {
                Trace($"Entry&Exit empty ActionQueue");
                return null;
            }
            /*  ActionQueue is a ConcurrentQueue so I don't need to lock on it
             *  or worry about queue/dequeue except when I destroy and recreate it
             *  in the process of sorting. A chance to try out a C#7 in-lined variable.
             */
            if (!ActionQueue.TryDequeue(out QueuedTask queuedTask))
            {
                Trace($"Entry&Exit TryDequeue failed (OK)");
                return null;
            }
            Trace($"Entry {queuedTask.ToString()}");
            /*
             * There are three separate tasks/callbacks here..
             * startCallbackTask:   A MeNext argument. This is executed just before the task is about to start
             * mainTask:            What it is all about. This is the task we are MeNext'ing
             * endCallback:         Passed to MeNext and executed after mainTask finishes
             */
            /*
             * Tell caller we are about to start his action
             */
            queuedTask.CallbackOnStarted?.Invoke();
            /*
             * The last two tasks are daisy-chained together using 
             * ContinueWith. These run sequentially. 
             */
            Task endTaskSoFar = RunningTask = queuedTask.Task;

            //if (queuedTask.CallbackOnEnded != null)
            //    endTaskSoFar = RunningTask.ContinueWith( (callback) => { new Task(queuedTask.CallbackOnEnded); });
            //endTaskSoFar.ContinueWith(callback=>new Action(() => RunningTask = null));
            RunningTask.Start();
            Trace($"Exit  {queuedTask.ToString()}");
            return RunningTask;
        }
        #endregion StartNextTask

        void RunningTaskEnded()
        {
            Trace($"Entry ");
            FunnelledTaskEnded?.Invoke(this, RunningTask);
            Trace($"Exit");
        }

        void RunningTaskFinished()
        {
            Trace($"Entry&Exit NOP");
        }

        #region    Tick
        /// <summary>
        /// Every Second we check to see if there is something to do ... and then do it
        /// </summary>
        public void Tick()
        {
            Trace($"Entry");
            StartNextTask(); // if there are any left to do.
            Trace($"Exit");
        }

        #endregion Tick

        #region    Trace()
        /// <summary>
        /// Write to the trace file or the console
        /// </summary>
        /// <param name="info">Entry Exit or 'whatever' data to be traced</param>
        /// <param name="member">[CallerMemberName]</param>
        /// <param name="line">[CallerLineNumber]</param>
        /// <param name="path">[CallerFilePath]</param>
        [System.Diagnostics.DebuggerStepThrough()]
        public void Trace(string info,
                        [CallerMemberName] string member = "", [CallerLineNumber] int line = 0, [CallerFilePath] string path = "")
        {
            if (Tracer == null || !Tracer.IsTracing)
                return ;
            if (Tracer.TraceOptions == TraceOptions.None)
                Statics.InternalProblem("TraceOption is None");
            Tracer.TraceCrossTask(info, member, line, path);
        }
        #endregion Trace()

        #region    EventHandlers

        #region    EventHandlers

        #region    FunnelledTaskStarted
        /// <summary>
        /// This event will be fired as soon as TaskFunnel starts the task. If it has to be rescheduled
        /// the event is not fired. It is fired by this.StartNextTask().
        /// </summary>
        public EventHandler<Task> FunnelledTaskStarted;
        #endregion FunnelledTaskStarted

        #region    FunnelledTaskEnded
        /// <summary>
        /// This event is fired when the TASK has ENDED. We will take MediaPlayer.Play as an example.
        /// The Task that issued Play() ends almost immediately and this event is triggered. 
        /// However the Task is not COMPLETED. (Completed is my word, not a .Net word). In my 
        /// parlance the task is completed after the fat lady sings. The onPlayed..etc.. event is fired
        /// and this information is padded to TaskFunnel by a call to TaskFunnel.Complete which 
        /// in turn triggers whatever has subscribed to the TaskFunnel.Completed.
        /// </summary>
        public EventHandler<Task> FunnelledTaskEnded;
        #endregion FunnelledTaskEnded

        #region    FunnelledTaskCompleted
        /// <summary>
        /// This event is triggered when the task that went into TaskFunnel.MeNext(...) comes to an end.
        /// (ie Task.Status = TaskStatus.RanToCompletion). See comments in <see cref=">FunnelledTaskCompleted"/>FunnelledTaskCompleted.
        /// </summary>
        /// <s
        public EventHandler<Task> FunnelledTaskCompleted;
        #endregion FunnelledTaskCompleted

        #region    FunnelledTaskFinished
        /// <summary>
        /// This event occurs when an external object (currently Sound) signals an async Task has
        /// finished. For example if a MediaElement.Say() creates a task round this and executes
        /// it. Then the Task is Completed when MediaElement.Say executes. The task doesn't hang
        /// aroung waiting for the speech to stop. What we want to know is when the Media
        /// Element/Player/Capture has just finished. TaskFunnel cannot know how or when
        /// async operations finish. However the MediaPlayer et al know this in events like
        /// OnMediaPlayed. So we use this event to tell TaskFunnel who then informs the subscribers. 
        /// TaskFunnel itself subscribes to this event
        /// </summary>
        public EventHandler<Task> FunnelledTaskFinished;  // xxx choose completed or finished
        #endregion FunnelledTaskFinished

        #endregion EventHandlers


        #endregion    EventHandlers
    }
    public interface ITaskFunnelNotification
    {
        
        void NotifyOnStart(ITaskFunnelNotification subscriber);
        void NotifyOnEnd(ITaskFunnelNotification subscriber);
    }
}
