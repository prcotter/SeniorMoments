using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;


namespace SeniorMoment.Services
{
    /* ---------------------------------HOW IT WORKS---------------------------------------------
     * The caller has a task that he wishes to run (or it is already running). 
     * He wishes to wait 
     * 
     * We have to create a TaskWaiter. There are a number of ways to do this 
     * depending on the parameters passed;
     * 
     * TaskWaiter = new TaskWaiter(TimeSpan? timeout = null)
     * In (1) we create an anonymous task from the Action. No external access
     * is allowed to this Task.
     * 
     * In (2) we use the Task provided the caller. The caller has access to this Task including the 
     * ability to add ContinueWith Tasks
     * 
     * In (3) A dummy task is created to which the caller has no access
     * 
     * The "wait" can stop waiting when any one of these three things happen
     * 
     *  ++ caller has specified a wait interval that has expired
     *  ++ caller calls Release from another thread
     *  ++ the item is Disposed
     *  
     * 
     */

    /// <summary>
    /// Allows  a Task to go Sleep and be woken up by another Task
    /// </summary>
    public class TaskFreezer : IDisposable, IEquatable<TaskFreezer>
    {
        #region    Properties

        #region    cancellationTokenSource
        /// <summary>
        /// Used to cancel the waiting Task
        /// </summary>
        CancellationTokenSource cancellationTokenSource;
        #endregion cancellationTokenSource

        #region    Freezers
        /// <summary>
        /// List of TaskWaiters that are waiting
        /// </summary>
        static Dictionary<int, TaskFreezer> Freezers;
        #endregion Freezers

        #region    Name
        /// <summary>
        /// This is for Trace Statements. Can contain anything the programmer want. Recommend you
        /// override ToString and use Name plus whatever else you need
        /// </summary>
        public string Name { get; private set; }
        #endregion Name

        #region    UniqueId
        /// <summary>
        /// Provides a unique integer value for each instance of the surrounding class
        /// </summary>
        public int UniqueId
        {
            get
            {
                if (_UniqueId < 0)
                    _UniqueId = _UniqueIdGenerator++;
                return _UniqueId;
            }
        }
        int _UniqueId = -1;
        static int _UniqueIdGenerator = 0;
        #endregion UniqueId

        #region    waitTask
        /// <summary>
        /// This is the Wait task that we wait on when Freeze(..?) is
        /// called.
        /// </summary>
        Task waitTask;
        #endregion waitTask

        #endregion Properties

        #region    Methods

        #region    Constructor (string name = null TimeSpan? = null) 
        /// <summary>
        /// This object allows a Task to go Sleep and be woken up by another Task
        /// </summary>
        /// <param name="string Name"></param>
        public TaskFreezer(string name = null, TimeSpan? timeout = null)
        {
            Name = name ?? $"TaskWaiter_{UniqueId}";
            Trace($"Entry constructor {Name}");
            if (Freezers == null)
                Freezers = new Dictionary<int, TaskFreezer>();
            lock (Freezers)
            {
                Freezers.Add(UniqueId, this);
            }
            cancellationTokenSource = new CancellationTokenSource();
            Trace($"Exit constructor {Name}");
        }
        #endregion Constructor (string name = null TimeSpan? = null) 

        #region    Freeze
        public void Freeze(TimeSpan? timeout = null)
        {
            Trace($"Entry");
            if (timeout == null)
            {
                //cancellationTokenSource = new CancellationTokenSource();
                //IAsyncAction action = Statics.CoreDispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { Task.Delay(-1, cancellationTokenSource.Token); });
                //var awaiter = action.AsTask().GetAwaiter();
                //awaiter.OnCompleted(new Action(OnCompleted));
                //action.AsTask().Wait();
                waitTask = Task.Run(new Action(Delay));
                Task.WaitAll(waitTask);

            }
            else
            {
                var span = Convert.ToInt32(timeout.Value.TotalMilliseconds);
                waitTask = Task.Run(new Action(Delay));
                Task.WaitAll(waitTask);
            }

            Trace($"Exit");
        }
        #endregion Freeze

        #region    Delay
        /// <summary>
        /// This is used to Freeze the current task. There is an onCompleted
        /// event that cancels the bycalling Release()
        /// </summary>
        void Delay() => Task.Delay(-1);
        #endregion Delay

        #region    Release
        /// <summary>
        /// Let loose the dogs of war. Alternatively let the task  that called Freeze()
        /// start running again.
        /// </summary>        
        public void Release()
        {
            Trace($"Entry {Name}");
            if (cancellationTokenSource == null)
                throw new LogicException("NoToken");
            cancellationTokenSource.Cancel();
            Trace("Exit  {Operation}");
            return;
        }
        #endregion Release

        //#region    Trace()
        ///// <summary>
        ///// Write to the trace file or the console
        ///// </summary>
        ///// <param name="info"></param>
        ///// <param name="member"></param>
        ///// <param name="line"></param>
        ///// <param name="path"></param>
        //public void Trace(string info,
        //            [CallerMemberName] string member = "", [CallerLineNumber] int line = 0, [CallerFilePath] string path = "")
        //{
        //    Trace(info, member, line, path);
        //}
        //#endregion Trace()

        #endregion Methods

        #region    Events
        #region    OnCompleted
        /// <summary>
        /// The event called when the task finishes
        /// </summary>
        private void OnCompleted() => Release();
        #endregion OnCompleted
        #endregion Events

        #region    Interfaces and overrides, ToString, IDisposable, IEquatable

        #region    IDisposable
        private bool _disposedValue = false; // To detect redundant calls
        /// <summary>
        /// Remove dying TaskWaiter from the static Dictionary(uniqueId,taskWaiter)
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Freezers.Remove(this.UniqueId);
                }
                _disposedValue = true;
            }
        }

        public void Dispose() => Dispose(true);
        bool IEquatable<TaskFreezer>.Equals(TaskFreezer other) => throw new NotImplementedException();
        #endregion IDisposable

        #region    ToString();
        /// <summary>
        /// Override of ToString() which returns
        /// </summary>
        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString()
        {
            return $"Operation={Name}  ";
        }
        #endregion ToString()

        #endregion Interfaces and overrides, ToString, IDisposable, IEquatable

    }
}
