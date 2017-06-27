using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SeniorMoment.Views;
using SeniorMoment.Models;
using SeniorMoment.Services;

namespace SeniorMoment.ViewModels
{

    /// <summary>
    /// All the View Models are derived from this
    /// yyy only used by VMTimer. We either get rid of it or
    /// apply it to the other Views
    /// </summary>
    public abstract class BaseViewModel : ITick, IEquatable<BaseViewModel>
    {
        #region    Name
        /// <summary>
        /// The supplied name or a generated unique name
        /// </summary>
        public string Name
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_Name))
                    _Name = "UniqueId " + UniqueId;
                return _Name;
            }
            private set => _Name = value;
        }
        private string _Name;
        #endregion Name

        #region    UniqueId / NextUniqueId
        /// <summary>
        /// Each ViewModel gets a unique key
        /// </summary>
        public int UniqueId
        {
            get
            {
                if (_UniqueId == -1)
                    _UniqueId = NextUniqueId++;
                return _UniqueId;
            }
        }
        /// <summary>
        /// Ever incrementing unique key
        /// </summary>
        private static int NextUniqueId = 0;
        /// <summary>
        /// Backing variable for UniqueId
        /// </summary>
        private int _UniqueId = -1;
        #endregion UniqueId

        #region    Constructor(string name => Name)
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">Name to be put in the Name field</param>
        public BaseViewModel(string name = null)
        {
            var any = UniqueId;
            if (string.IsNullOrWhiteSpace(name))
                Name = "UniqueId " + UniqueId;
            else
                Name = name;
        }
        #endregion Constructor(string name => Name)

        #region    ToString (Name or Generated Name)
        /// <summary>
        /// Display name supplied or generate a default one based on the unique key
        /// that every ViewModel has
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(Name))
                Name = "UniqueId " + UniqueId;
            return Name;
        }
        #endregion ToString (Name or Generated Name)

        #region    Equals
        /// <summary>
        /// All objects are different based on a unique key.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override sealed bool Equals(object obj)
        {
            var baseObj = obj as BaseViewModel;
            if (obj == null)
                return false;
            return this.UniqueId == baseObj.UniqueId;
        }
        #endregion Equals

        #region    GetHashCode
        /// <summary>
        /// Use a simpler Equals. The objects are the same if they have the same unique key
        /// rather than basing the hashcode on the whole object
        /// </summary>
        /// <returns>true if they are the same object</returns>
        public override int GetHashCode() => UniqueId;
        #endregion GetHashCode

        #region    Tick
        /// <summary>
        /// When the Timer ticks it calls this delegate which must be passed to MTimer
        /// in the CreateAlarm
        /// </summary>
        /// <param name="secondsToZero">number of seconds left before the timer goes off.
        /// Can be negative as we are past the timer has gone off</param>
        /// <param name="arg"></param>
        public abstract void Tick();
        #endregion Tick

        #region    Trace
        /// <summary>
        /// Only the descendant object knows which Tracer to use to Trace. But for the moment
        /// we shall use the Tracer.TracerMain which is a static Tracer, which uses the flag
        /// AllowCrossThreadTrace
        /// </summary>
        /// <param name="info">Entry Exit or 'whatever' data to be traced</param>
        /// <param name="member">[CallerMemberName]</param>
        /// <param name="line">[CallerLineNumber]</param>
        /// <param name="path">[CallerFilePath]</param>
        public virtual void Trace(string info, string member, int line, string path)
        {
            Tracer.TracerMain.TraceCrossTask(info, member, line, path);
        }
        #endregion Trace

        #region    IEquatable<BaseViewModel>
        /// <summary>
        /// Two ViewModels are the same if they have the same UniqueId. ie they are the same instance
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        bool IEquatable<BaseViewModel>.Equals(BaseViewModel other) =>
            this.UniqueId == other?.UniqueId;
        #endregion IEquatable<BaseViewModel>
    }
}
