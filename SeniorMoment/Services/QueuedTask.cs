using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SeniorMoment.Services
{
    /// <summary>
    /// This is an entry in Queue and PriorityQueue
    /// </summary>
    public class QueuedTask 
    {
        #region    Properties

        #region    ActionQT
        /// <summary>
        /// The Action added to the Queue which will eventually be rolled out as a Task
        /// surrounded with callbacks
        /// </summary>
        public Action<QueuedTask> ActionQT { get; set; }
        #endregion ActionQT

        #region    FuncQT xxx
        /// <summary>
        /// The Action added to the Queue which will eventually be rolled out as a Task
        /// surrounded with callbacks
        /// </summary>
        public Func<QueuedTask> FuncQT { get; private set; }
        #endregion FuncQT

        #region    Age
        /// <summary>
        /// length of time in millisecondsfrom Statics.BaseTime till now. TaskFunnel can set the 
        /// running order for Tasks using this information (plus Priority )
        /// </summary>
        public double Age { get; } = (DateTime.Now - Statics.BaseTime).TotalMilliseconds;
        #endregion Age

        #region    baseTime (static)
        /// <summary>
        /// Some arbitrary time that we can measure other times against to get intervals
        /// and then sort on them. The intervals must be positive
        /// </summary>
        static DateTime baseTime { get; } = DateTime.Now;
        #endregion baseTime (static)

        #region    CallbackOnStarted gps 
        /// <summary>
        /// What to callback when the Task starts       
        /// </summary>
        public Action CallbackOnStarted { get; private set; }
        #endregion CallbackOnStarted

        #region    CallbackOnEnded
        /// <summary>
        /// What to callback when the Task ends       
        /// </summary>
        public Action CallbackOnEnded { get; private set; }
        #endregion CallbackOnEnded

        #region    CancellationTokenSource gps
        /// <summary>
        /// Passed back to the caller To TaskFunnel.MeNext() as part of the 
        /// (Task,CancellationTokenSource) Tuple.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; private set; }
        #endregion CancellationTokenSource

        #region    Name
        /// <summary>
        /// Just so we can see in debug where this Action came from
        /// </summary>
        public string Name { get; private set; }
        #endregion Name

        #region    Parameters gps
        /// <summary>
        /// Parameters stored when TaskFunnel.Next is called so when the task is released
        /// the caller can 'remember' its parameters.
        /// </summary>
        public List<object> Parameters { get; private set; }
        #endregion Parameters

        #region    Priority gps=100
        /// <summary>
        /// The Priority of the action. The default is 100. Highest Priority = 1.
        /// </summary>
        public int Priority { get; /*private*/ set; } = 100;
        #endregion Priority

        #region    UniqueId
        /// <summary>
        /// A unique identifier that allows a caller to TaskFunnel to get access to it's variables. We do
        /// this because TaskFunnel only accepts and Action, not an Action<...,...>
        /// </summary>
        //public int UniqueId { get { return _UniqueId == -1 ? _UniqueId = Statics.NextUniqueId : _UniqueId; } }
        public int UniqueId => _UniqueId == -1 ? _UniqueId = Statics.NextUniqueId : _UniqueId; 
        private int _UniqueId = -1;
        #endregion UniqueId

        #region    UsedTaskFunnel
        /// <summary>
        /// Was this attempted directry by TrrPlayFiles or was it passed to the TaskFunnel
        /// </summary>
        public bool UsedTaskFunnel { get; private set; }
        #endregion UsedTaskFunnel

        #region    SortKey => by Priority and then Age 
        /// <summary>
        /// The key with which we sort the ActionQueue. Oldest within Priority. Used by
        /// TaskFunnel to re-order the queue everytime there is an addition/deletion
        /// to the queue
        /// </summary>
        public double SortKey => ((double)Priority * Int32.MaxValue) + Age;
        #endregion SortKey

        #region    Task
        /// <summary>
        /// The Task that was created by TaskFunnel from the passed Action. This is passed back to the 
        /// caller to TaskFunnel. It contains enough information to cancel the task
        /// </summary>
        public Task Task { get; private set; }
        #endregion Task

        #endregion Properties

        #region    Constructor
        /// <summary>
        /// All the information needed to reconstitute a task when it has been queued. It is
        /// possible to play with the Task in terms of CancellationToken.Source.Cancel()
        /// or to Task.ContinueWith
        /// </summary>
        /// <param name="task">The newly created task from newTask (Action)</param>
        /// <param name="action">The Action used to create this.Task</param>
        /// <param name="parameters">a List<object> of all the parameters that came with the Action</object></param>
        /// <param name="name">A name. This (with ToString) is primarily so it is easier
        /// to see in Intellisense. You need it when there are a bunch of tasks 
        /// just floating around</param>
        /// <param name="cts">CancellationTokenSource used by the caller if they want to 
        /// cancel the task at ant time, (running, completed , failed or not started)</param>
        /// <param name="priority"></param>
        public QueuedTask(
            Action<QueuedTask> actionQT,
            List<object> parameters,
            string name = null,
            Task task = null,
            Func<QueuedTask> funcQT = null,
            int priority = 100,
            Action callbackOnStarted = null,
            Action callbackOnEnded = null
            /*, bool usedTaskFunnel = false */
            /* , QueuedTask nextQueuedTask = null*/ )
        {
            CancellationTokenSource = new CancellationTokenSource();
            ActionQT = actionQT ?? throw new LogicException("actionQT is null"); // actionQT is mandatory
            Parameters = parameters ?? new List<object>(); // if no parameters supply an empty List to help prevent null references
            Name = name ?? ToString();
            Task = task ?? new Task(            // if no passed Task then generate one
                    (qt) =>  actionQT(this),
                    this, 
                    CancellationTokenSource.Token);
            Priority = priority;
            Age = (DateTime.Now - baseTime).TotalMilliseconds;
            CallbackOnEnded = callbackOnEnded;
            CallbackOnStarted = callbackOnStarted;
            //UsedTaskFunnel = usedTaskFunnel;
            //NextQueuedTask = nextQueuedTask;
        }
        #endregion Constructor

        #region    ToString
        /// <summary>
        /// Useful to the programmer for intellisense. This is the only
        /// reason Name exists.
        /// </summary>
        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString() => $"{Name} Priority: {Priority}  Age:{(int)Age} ms";
        #endregion ToString
    }
}
