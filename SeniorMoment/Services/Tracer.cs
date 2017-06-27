using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Windows.Storage.Streams;
using Windows.Storage;

namespace SeniorMoment.Services
{
    /// <summary>
    /// Used to trace entry and exit (plus any other logging) of methods. It is
    /// particularly useful in a multi-threaded application. Each routine (of any importance)
    /// contains at or near the start and at or near the end
    /// <para/>Trace($"Entry") or Trace($"Entry any other info")
    /// <para/>Trace($"Exit") or Trace($"Exit any other info")
    /// <para/>It also supports the concept of Trace($"Entry&Exit any other info") although there is
    /// no special code needed for this
    /// <para/>Additionally other Trace("any other stuff that is important when debugging") can
    /// be added - so long as they do not begin with Entry of Exit. If you have an Entry
    /// you must have a corresponding Exit in all possible 'returns' or a mismatched 
    /// Entry-Exit exception will be thrown as Tracer logs the calls. Each tracer has a name
    /// which is defined by the caller. The caller can also demand the name is unique.
    /// If so and two tracers are created with the same name then an exception is thrown.
    /// Additionally, in order to write something it must be from the same thread unless
    /// the thread has AllowTraceCrossTask allowed.
    /// <para/>
    /// <!--Thought about having a list of Tracer 'friend' threads but 
    /// that seemed like over-egging the pudding) -->
    /// </summary>
    public class Tracer : IDisposable
    {
        #region    TraceElement class
        /// <summary>
        /// Every time we get Trace($"Entry ...") we add one of these to the trace stack.
        /// When we get Trace($"Exit ...") we make sure it matches the last "Entry"
        /// unless AllowCrossTaskTracing is set ... then the rules are different. 
        /// See later on for a description of what happens then - it's more complicated
        /// </summary>
        /// <remarks>I enjoy finding class names that have other meanings - like trace element</remarks>
        public class TraceElement : IEquatable<TraceElement>
        {
            #region    Assembly
            /// <summary>
            /// The assembly (probably) that holds the class (probably) that holds the member;
            /// </summary>
            public string Assembly { get; }
            #endregion Assembly

            #region    CompleteTraceStatment
            /// <summary>
            /// The complete entry from a Trace entry/exit.
            /// Trace ("Entry status={Status}") would store "Entry status=OK" here
            /// </summary>
            public string CompleteTraceStatment;
            #endregion CompleteTraceStatment

            #region    Filename
            ///<summary>
            /// The Class the Member was called from
            ///</summary> 
            public string Filename;
            #endregion Filename

            #region    Line
            /// <summary>
            /// Which line in which in the source code the Trace($"Entry") was called from 
            /// </summary>
            public int Line { get; } // Defined in the constructor
            #endregion Line

            #region    Member
            /// <summary>
            /// Where Trace($"Entry ...") was called from
            /// </summary>
            public string Member { get; }
            #endregion Member

            #region    Path
            /// <summary>
            /// Full path to .cs file (or whatever) that contains Member and Line
            /// </summary>
            public string Path { get; private set; }
            #endregion Path

            #region    regexToExtractClass
            /// <summary>
            /// The [CallerFilePath] attribute returns the path to the MyClass.cs all the way from the root. We are
            /// interested in the last two bits. In the case of Trace this would be "\Utility\Tracer". Hence this Regex
            /// </summary>
            static protected Regex regexToExtractClass = new Regex("\\\\(?<assembly>[^\\\\]*)\\\\(?<class>([^\\\\]*?))(\\.cs)?$", RegexOptions.Compiled);
            #endregion regexToExtractClass

            #region    TraceElement Constructor
            /// <summary>
            /// Every Trace($"Entry") creates a TraceElement and adds it to the stack so as
            /// we Trace($"Exit") we can ensure a 1 to 1 match
            /// </summary>
            /// <param name="member">The method Trace("...") was called from was called from</param>
            /// <param name="line">The line number Trace("...") was called from was called from</param>
            /// <param name="path">The full path to the file containing the Trace("...")</param>
            public TraceElement(string member, int line, string path, string completeTraceStatment, int depth)
            {
                Member = member;
                Line = line;
                Path = path;
                CompleteTraceStatment = completeTraceStatment;
                /*
                * We have the path to the class  (or rather the file, since there may be more than one class) We get the
                * last two portions which are the filename (eg MyClass.cs) and the assembly name (probably). If you have
                * gone round changing filenames etc then this information is iffy.
                */
                Match match = regexToExtractClass.Match(path);
                Filename = match.Groups["class"].Value;
                Assembly = match.Groups["assembly"].Value; // well let's assume it is the assembly name - doesn't really matter
            }
            #endregion TraceElement Constructor

            #region    ToString()
            /// <summary>
            /// Displays @line in @Member
            /// <para>eg: @311 in Execute</para>
            /// </summary>
            /// <returns>string showing what line in what member this element represents</returns>
            [System.Diagnostics.DebuggerStepThrough()]
            public override string ToString()
            {
                return $"{System.IO.Path.GetFileNameWithoutExtension(Path)}.{Member}@{Line} )";
            }
            #endregion ToString()

            #region    Equals => IEquatable<TracerElement>
            /// <summary>
            /// Equality of two TraceElements is not whether they are them object, but where they 
            /// both refer the same TraceElement.Path and TraceElement.Member. This is used to
            /// compare entryStack and exitStack items when using Statics.CrossTaskTrace
            /// </summary>
            /// <param name="other">the other TraceElement</param>
            /// <returns></returns>
            public bool Equals(TraceElement other) => other != null && this.Member == other.Member && this.Path == other.Path;
            #endregion Equals => IEquatable<TracerElement>

        }
        #endregion TraceElement class

        #region    Constants

        #region    CRLF / CRLF
        /// <summary>
        /// Carriage Return Line Feed
        /// </summary>
        public static string CRLF = Environment.NewLine;
        /// <summary>
        /// Carriage Return Line Feed - only twice
        /// </summary>
        static public string CRLF2 = CRLF + CRLF;
        #endregion CRLF / CRLF

        #endregion Constants

        #region    static Properties

        #region    AnyTraceCrossTaskr
        /// <summary>
        /// Returns the first ( or null if none ) Tracer in the TracerDictionay that allows TraceCrossTask(msg)
        /// </summary>
        static public Tracer AnyTraceCrossTaskr = TracerDictionary?.Values.FirstOrAlternative(tracer => tracer.AllowCrossTaskTracing, null);
        #endregion AnyTraceCrossTaskr

        #region    isInitialised
        ///<summary>
        /// set to true on first create of a Tracer after doing some one-time Initialization
        ///</summary> 
        static bool IsInitialisedStatic = false;
        #endregion isInitialised

        #region    mutex
        /// <summary>
        /// Keeps things clean when multiple thread may be opened.
        /// </summary>
        static Mutex mutex = new Mutex();
        #endregion mutex

        #region    NameUseCounts
        /// <summary>
        /// We keep a list of all the names that have been used to create Trace files. If we re-use the
        /// Trace.Name then we must not overwrite the trace file we just created. So the trace filename 
        /// is of the format
        /// <para>  BaseFile.Trace.Tracer-Name[count].txt</para>
        /// Where BaseFile might be MyNewProgram. Tracer-name is the Name supplied in the constructor
        /// and count ids the number of times a Tracer with this Name has been created. 
        /// First parameter is the unique Name for a Tracer, int is the number of times this
        /// name has been used. This Dictionary is used to update the instance variable NameUseCount.
        /// </summary>
        static Dictionary<string, int> NameUseCounts => _NameUseCounts;
        /// <summary>
        /// Backing variable
        /// </summary>
        static Dictionary<string, int> _NameUseCounts = new Dictionary<string, int>();
        #endregion NameUseCounts

        #region    Incarnations
        /// <summary>
        /// Every time we create a new Tracer we increment this. Used to create unique filenames with the
        /// same Tracer.Name. So if a class that uses Trace is created and destroyed multiple times then
        /// we still get a unique filename
        /// </summary>
        public static int Incarnations { get; private set; } = 0;
        #endregion Incarnations

        #region    TracerDictionary
        ///<summary>
        /// Dictionary of tracers indexed 
        ///</summary> 
        public static Dictionary<string, Tracer> TracerDictionary { get; private set; } = new Dictionary<string, Tracer>();
        #endregion TracerDictionary

        #endregion static Properties

        #region    Properties

        #region    AllowCrossTaskTracing
        ///<summary>
        ///If set true then any thread can post to this tracer. If set false then
        ///if the posting thread is not the same as the Tracer constructor thread an
        ///exception is thrown. The latter is safer as it helps eliminate accidental
        ///cross thread practices. Additionally, Tracer creates a separate file for 
        ///for each Tracer you can see what eat individual Tracer did. If the console log
        ///is set on then you will get a separate account so you can see the sequence
        ///of Trace()s on all threads in time order.
        ///</summary>
        bool AllowCrossTaskTracing;
        #endregion AllowCrossTaskTracing

        #region    BaseTime
        ///<summary>
        /// Initialized as the DateTime.Now, We get the time to first Trace() and it is reset to Now.
        /// Each subsequent trace gets the interval since BaseTime was last set and then resets the BaseTime
        ///</summary> 
        public DateTime BaseTime;
        #endregion BaseTime

        #region    Callback
        /// <summary>
        /// This is currently used as a debugging tool but it could be used for something else.
        /// </summary>
        public Func<string> Callback = null;
        #endregion Callback

        #region    entryAndExitWords
        /// <summary>
        /// If you are straight in and out of an event you can use of three words
        /// </summary>
        List<String> entryAndExitWords = new List<string> { "entryandexit", "entryexit", "entry&exit" };
        #endregion entryAndExitWords

        #region    entryStack
        /// <summary>
        /// Used to collect entry and exits from calls to Trace()
        /// </summary>
        Stack<TraceElement> entryStack = new Stack<TraceElement>();
        #endregion entryStack

        #region    ExitExtra
        /// <summary>
        /// Inserts a character after exit so Entry and Exit line up
        /// </summary>
        string ExitExtra = string.Empty;
        #endregion ExitExtra

        #region    Header
        ///<summary>
        /// What goes at the front of a trace line. 
        ///</summary> 
        public string Header
        {
            get
            {
                string interval = IsShowInterval ? " " + Math.Round((DateTime.Now - BaseTime).TotalMilliseconds, 3) : "";
                return $"[{Name}{interval}] ";
            }
        }
        #endregion Header

        #region    Id
        /// <summary>
        /// Id assigned to this Tracer. It is supplied by the user. If it is empty an Id is generated.
        /// </summary>
        public string Id
        {
            get
            {
                if (_Id == string.Empty)
                    _Id = "Id_" + UniqueId;
                return _Id;
            }
            private set => _Id = value;
        }
        private string _Id = string.Empty;
        #endregion Id

        #region    Incarnation
        /// <summary>
        /// The first Tracer created will have an Incarnation of 1. The tenth will have
        /// an incarnation of 10 even if any or all of 2 to 9 have been disposed
        /// </summary>
        public int Incarnation { get; private set; }
        #endregion Incarnation

        #region    IsShowInterval
        ///<summary>
        /// In Trace statements display the Interval which is milliseconds since the basetime. That's
        /// normally when we Start();
        ///</summary> 
        public bool IsShowInterval;
        #endregion IsShowInterval

        #region    IsTracing
        /// <summary>
        /// Has Start been called
        /// </summary>
        public bool IsTracing = false;
        #endregion IsTracing

        #region    IsUniqueTask
        ///<summary>
        /// If true no other Trace is permitted on this Task and this Tracer is not allowed
        /// to run on its own thread if there is already a Tracer on it with !IsUniqueTask
        ///</summary> 
        public bool IsUniqueTask { get; private set; }
        #endregion IsUniqueTask

        #region    localFolder
        /// <summary>
        /// The sand-boxed Local folder that is used by the app
        /// </summary>
        static StorageFolder localFolder = ApplicationData.Current.LocalFolder;
        #endregion localFolder

        #region    Name
        /// <summary>
        /// Unique name assigned to this Tracer. It is supplied by the user. If it is empty a Name is generated.
        /// </summary>
        public string Name
        {
            get
            {
                if (_Name == string.Empty)
                    _Name = "Tracer" + UniqueId;
                return _Name;
            }
            private set => _Name = value;
        }
        private string _Name = string.Empty;
        #endregion Name

        #region    NameUseCount
        ///<summary>
        /// The number of times that a particular Tracer Name has been used. While at any given
        /// time no two Tracers can have the same name, if one is disposed then the name can be re-used.
        /// This is most useful in a which repeatedly a thread is created, 'Traced' and the thread terminates.
        /// The Trace messages will show (say) [M] but the trace file will be called MyProgram.Trace.Name[2]
        /// for the second use. 
        ///</summary> 
        public int NameUseCount { get; private set; } = -1;

        #region    IsShowAssembly
        /// <summary>
        /// Should Trace Show the Assembly (eg Services for Tracer)
        /// </summary>
        public bool IsShowAssembly { get; private set; }
        #endregion IsShowAssembly

        #region    streamWriter
        /// <summary>
        /// The writer used to write Trace output files to the MRecordings folder
        /// </summary>
        StreamWriter streamWriter;
        #endregion streamWriter

        #region    TaskId
        /// <summary>
        /// The thread ID as supplied by the Task Manager. This will be unique even
        /// if threads are created and destroyed, even this one.
        /// </summary>
        public int? TaskId { get; private set; }
        #endregion TaskId

        #region    Timing
        /// <summary>
        /// If timing is True then Trace messages are prepended with [M 1234] where
        /// 1234 is the number of milliseconds between the last Trace() (or Constructor)
        /// and this Trace(). This can be switched on and off at any time.
        /// </summary>
        static public bool Timing = false;
        #endregion Timing

        #region    traceData
        /// <summary>
        /// Used to hold information from a task so its there when the task returns
        /// </summary>
        string traceData = null;
        #endregion traceData

        #region traceFile
        /// <summary>
        /// The name of the trace file for this thread
        /// </summary>
        public StorageFile traceFile;
        #endregion traceFile

        #region traceFolder
        /// <summary>
        ///
        /// </summary>
        StorageFolder traceFolder;
        #endregion traceFolder

        #region    TracerMain
        /// <summary>
        /// Default Tracer if the calling class has no Tracer. This used by MainPage, TickTock etc
        /// </summary>
        public static Tracer TracerMain = null /* changed cus want to create it later= new Tracer("Main", "M") */ ;
        #endregion TracerMain

        #region    TracingOptions
        /// <summary>
        /// This is a debugging control variable. If takes a combination of the following Enum TracingOption values
        /// <para/>None            No debugging 
        /// <para/>Console         Debug to console
        /// <para/>File            Log to a file
        /// <para/>
        /// Enable Console output with: Project...Properties...Application tab...Output type=Console Application
        /// <para/>
        /// This
        /// </summary>
        public static TraceOptions TraceOptions
        {
            get => _traceOptions;
            set
            {
                if (IsInitialisedStatic)
                    throw new InvalidOperationException("Cannot reset TraceOptions after creation of a Tracer");
                _traceOptions = value;
            }
        }
        /// <summary>
        /// Backing variable
        /// </summary>
        static TraceOptions _traceOptions = TraceOptions.None;
        #endregion TracingOptions

        #region    UniqueId
        /// <summary>
        /// A unique number that can be used to generate a Name or the Trace
        /// </summary>
        public int UniqueId { get => _UniqueId++; }
        private int _UniqueId = 0;
        #endregion UniqueId

        #region    GetTraceFolder
        /// <summary>
        /// Create the trace folder. If it already exists then delete it
        /// </summary>
        async void GetTraceFolder()
        {
            await Task.Run(async () =>
           {

               var folderQuery = localFolder.CreateFolderQuery();
               var subFolders = await folderQuery.GetFoldersAsync();
               int count = subFolders.Count(a => a.Name.ToLower() == "trace");
               if (count > 0)
               {
                   var folder = subFolders.First(a => Name.ToLower() == "trace");
                   string folderName = folder.Name;
                   folder = await localFolder.GetFolderAsync(folderName);

                   await folder.DeleteAsync();
               }
               traceFolder = await localFolder.CreateFolderAsync("Trace");
               //            var subFolders = await folderQuery.GetFoldersAsync();
           });
        }

        #endregion GetTraceFolder

        #endregion Properties

        #endregion Properties

        #region    Constructor
        /// <summary>
        /// Constructor. There is no default constructor
        /// </summary>
        /// <param name="name">The unique name for this Tracer. If another Tracer with the same name is created
        /// an ArgumentException will be thrown. This name can only be composed of a=>Z and 0=>9 and underscore.</param>
        /// <param name="id">A short form of the name which will appear in trace statements. If Id = "M" then
        /// trace statements will begin [M] or [M 128] if timing is required</param>
        /// <param name="isUniqueTask">Allows more than one Tracer on this thread. The first and any subsequent
        /// Tracers on the same thread must have isUniqueTask=false. If not there is an exception</param>
        /// <param name="startTracing">if false the tracer is created but is not started. You must call Start().
        /// This option is useful when you wish to trace only based on some criteria </param>
        /// <param name="allowCrossTaskTracing">if true Tasks other than the creating Task can write to this Tracer. This
        /// option is useful when you want the Tracefiles' content by function rather than thread or you want
        /// see the order of events across multiple threads.
        /// <param name="showInterval">show the timing (I think in milliseconds since the tracing started
        /// <
        public Tracer(
                string name = "",
                string id = "",
                bool isUniqueTask = true,
                bool startTracing = false,
                bool allowCrossTaskTracing = false,
                bool showInterval = false,
                bool showAssembly = false
                )
        {
            #region    Parameter Validation
            /*
             * Ensure we have been passed valid parameters
             */
            #region    Name
            if (name == null) throw new ArgumentNullException("name");
            if (name == string.Empty) throw new ArgumentException("Cannot be empty string", "name");
            Regex regexValidName = new Regex(@"^[ _A-Za-z0-9\.]*$");

            Match match = regexValidName.Match(name);
            if (!match.Success) throw new ArgumentException("Must be able to form part of a filename", "name");

            if (TracerDictionary.ContainsKey(name))
                throw new ArgumentException(
                    name + " already exists. Consider using \"M\" + (myTracer.NameUseCount).ToString()");
            Name = name;
            #endregion Name

            if (string.IsNullOrEmpty(Id))
                Id = name.Left(1);
            this.IsUniqueTask = isUniqueTask;
            AllowCrossTaskTracing = allowCrossTaskTracing;
            IsShowInterval = showInterval;
            IsShowAssembly = showAssembly;

            /* Checkout the threading and Abort if...
             *  1) This Tracer must be the only one on this thread and one already exists on this thread
             *  2) This thread has no unique thread requirement, but there is already a Tracer running on this
             *  thread which has a unique thread requirement 
             */
            TaskId = Task.CurrentId;
            IEnumerable<int?> threadIds = TracerDictionary.Values.Select(a => a.TaskId);
            if (threadIds.Contains(TaskId))
            {
                if (isUniqueTask)  // name should be null if not found
                    throw new InvalidOperationException("Task already used and this thread must be unique");
                if (TracerDictionary.Values.First(a => a.TaskId == TaskId).IsUniqueTask)
                    throw new InvalidOperationException("A Tracer on the same thread with isUniqueTask=true already exists");
            }
            #endregion Parameter Validation

            mutex.WaitOne(); // vaguely possible two tracers could be constructed at same time with same name
            {
                #region    Set up internal Tracer properties and Start

                Incarnations++;
                BaseTime = DateTime.Now;
                /*
                 * we are passed an id such as "H". The first time we use it the Id becomes "H1".
                 * Following time "H2", then "H3" whenever the Tracer is destroyed and recreated.
                 */
                TracerDictionary.Add(Name, this);
                string tracerName = NameUseCounts.Keys.FirstOrDefault(a => a == Name);
                if (tracerName == null)
                {
                    NameUseCounts.Add(Name, 1);  // static dictionary of all Names used
                    NameUseCount = 1;       // Instance variable, number of times this name has been used to this point
                }
                else
                    NameUseCount = ++NameUseCounts[Name];
                /* 
                 * Open the stream writer and get going
                 */
                if (startTracing)
                    Start();
                /*
                 * isInitalised is a static variable. If any Tracer has been created then we cannot
                 * change things like TraceOptions, tracing folders etc.
                 */
                IsInitialisedStatic = true;
                #endregion Set up internal Tracer properties and Start
            }
            mutex.ReleaseMutex();
        }
        #endregion Constructor

        #region    AbortAll
        /// <summary>
        /// Kill all the tracers and return all/any outstanding Trace($"Entry")
        /// </summary>
        /// <returns></returns>
        public static string AbortAll()
        {
            string outstandingEntries = "";
            foreach (var pair in TracerDictionary)
            {
                Tracer tracer = pair.Value;
                outstandingEntries += $"{tracer.Name} {tracer.GetOutstandingTraceEntryCalls()}{CRLF2}";
            }
            StopAllTracing();
            return outstandingEntries;
        }
        #endregion AbortAll

        #region    AssertTraceStackIsAlmostEmpty
        /// <summary>
        /// This is to check that when we are ready to go there aren't any left over 
        /// Trace("entry") left around. However there will be one for Main and one for
        /// the entry to this routine so we expect 2 in the stack
        /// </summary>
        /// <param name="tracer">The Tracer instance to check</param>
        /// <param name="optionalMessage">Extra message we can add.</param>
        /// <param name="member">Method Trace was called from</param>
        /// <param name="line">line number in source file this was called from</param>
        /// <exception cref="InvalidOperationException">Trace stack has more than 2 entries so it wasn't reset properly on exit</exception> 
        static public void AssertTraceStackIsAlmostEmpty(Tracer tracer, string optionalMessage = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (tracer == null)
                return;
            /*
             * There should only be 0 or 1 entries in the stack. It should be whatever called us or zero
             * if thee caller did nor have a Trace($"Entry")
             */
            lock (tracer.entryStack)
            {
                if (tracer.entryStack.Count > 1)
                {
                    string traces = tracer.GetOutstandingTraceEntryCalls();
                    throw new LogicException
                        ($"AssertTraceStackIsAlmostEmpty failed @{line} in {member}  {CRLF2}See trace file \"{tracer.traceFile.Path}\"{CRLF2}{traces}");
                }
                {
                    if (tracer.entryStack.Count == 1)
                    {
                        string reMember = tracer.entryStack.First().Member;
                        if (reMember != member)
                            Statics.InternalProblem($"Mismatched entry between {member} and {reMember}");
                    }
                }
            }
        }
        #endregion AssertTraceStackIsAlmostEmpty

        #region    GetOutstandingTraceEntryCalls()
        ///<summary> 
        /// Called at program close and error situations to collect information about outstanding Trace($"Enter")s.
        /// and that we have has a Trace($"Exit") for every Trace($"Entry")
        ///</summary>
        /// <param name="element">Add additional information about this element to the normal message</param>
        /// <returns>string.Empty if no outstanding entries. Otherwise returns a string showing the outstanding
        /// Trace($"Enter") statements</returns>
        public string GetOutstandingTraceEntryCalls(TraceElement element = null)
        {
            if (entryStack.Count == 0)
                return string.Empty;
            StringBuilder builder = new StringBuilder("Outstanding trace calls:" + CRLF2);
            if (element != null)
                builder.AppendLine(element.ToString());
            lock (entryStack)
            {
                foreach (var traceElement in entryStack)
                    builder.Append(traceElement.ToString() + CRLF);
            }
            return builder.ToString();
        }
        #endregion GetOutstandingTraceEntryCalls()

        #region    Pause
        /// <summary>
        /// Pause the tracing. It can be Resumed
        /// </summary>
        public void Pause()
        {
            if (!IsTracing)
                throw new InvalidOperationException("Pause called but not Tracing");
            IsTracing = false;
        }
        #endregion Pause

        #region    Resume
        /// <summary>
        /// Pause the tracing. It can be Resumed
        /// </summary>
        public void Resume()
        {
            if (IsTracing)
                throw new InvalidOperationException("Resume called already Tracing");
            IsTracing = true;
        }
        #endregion Resume

        #region    Start
        /// <summary>
        /// wrapper for StartAsync()
        /// </summary>
        public void Start()
        {
            /*
             * Start can be called at any time and as often as you want. No logic check
             * because we may wish to switch it on without knowing it is already on
             */
            if (streamWriter == null)
                streamWriter = new StreamWriter(File.Open($"{localFolder.Path}\\{Name}.txt", FileMode.Create));
            IsTracing = true;
        }
        #endregion Start

        #region    stop Debug variable inside #pragma. Used for conditional breakpoints 
#pragma warning disable CS0414
        /// <summary>
        /// this is used in conditional breakpoints when debugging. It cannot be a Statics class variable
        /// as they are not in context at a breakpoint
        /// </summary>
        static int stop = 1;  // use in debug breakpoint condition (usually stop if debug != 0) 
#pragma warning restore CS0414
        #endregion stop Debug variable inside #pragma. Used for conditional breakpoints 

        #region    StopAllTracing
        ///<summary> RemoveTracers
        /// Shut down all the Tracing. That's what we do when we force the application to close;
        ///</summary>
        static public void StopAllTracing()
        {
            mutex.WaitOne();
            {
                while (TracerDictionary.Count() > 0)
                {
                    var tracer = TracerDictionary.First().Value;
                    Tracer.StopStatic(tracer, ignoreOutstandingEnters: true);
                    tracer = null;
                }
            }
            IsInitialisedStatic = false;
            mutex.ReleaseMutex();
        }
        #endregion StopAllTracing

        #region    StopStatic(tracer,ignoreEntries) Static
        ///<summary> Close()
        /// Close down the Tracer ensuring it to be in a healthy state. The Dictionary
        ///</summary>
        ///<param name="tracer">The Trace to close</param>
        ///<param name="ignoreOutstandingEnters">Ignore the fact that there may be outstanding Trace($"Enter")
        ///entries in the stack. If there are outstanding Trace($"Enter") then this is a programming error unless
        ///we are in a forced close down. </param>
        ///<exception cref="InvalidOperationException">Failed to close down the Trace file</exception>
        static public void StopStatic(Tracer tracer, bool ignoreOutstandingEnters = false)
        {
            if (tracer == null)
                Statics.InternalProblem("tracer null");
            if (!TracerDictionary.ContainsKey(tracer.Name))
                Statics.InternalProblem("Stop() called for a Tracer not in the dictionary");

            lock (tracer.entryStack)
            {
                tracer.IsTracing = false;
                if (ignoreOutstandingEnters)
                    tracer.entryStack.Clear();
                tracer.Dispose();
            }
        }
        #endregion StopStatic(tracer,ignoreOutstandingEnters) Static

        #region    ToString
        ///<summary>
        /// Display something like "MainTrace Id=MT"
        ///</summary> 
        ///<returns>TracerName id=MT</returns>
        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString()
        { return Name + " id=" + Id; }
        #endregion ToString

        #region    TraceCrossTask, Trace to the Tracer that may or may not be on the same thread
        /// <summary>
        /// Write information to the trace output
        /// </summary>
        /// <param name="info">user specified info useful for debugging </param>
        /// <param name="path">last part of the path from the calling class. Uses the [CallerFilePath] attribute so it is not
        /// needed unless the class has it's own Trace() method</param>
        /// <param name="member">The method that called this Trace(etc). Uses the [CallerMemberName] attribute so it is not
        /// needed unless the class has it's own Trace() method</param>
        /// <param name="line">The line number that called this Trace(etc). Uses the [CallerLineNumber] attribute so it is not
        /// needed unless the class has it's own Trace() method</param>
        /// <example>
        /// Tracer.Trace($"Entry Index=" + index.ToString());
        /// <para/>
        /// If you wish a class has its own Trace() method it would look like this:
        /// <para/>
        /// <code>
        /// void Trace(string info, [CallerMemberName] string member = "", [CallerLineNumber] int line = 0, [CallerFilePath] string path = "")
        /// {
        ///     Tracer.TraceCrossTask(info, member, line, path);
        /// }
        /// </code>
        /// Frequently you can use another classes Trace as in myParent.Trace(info). The member, line and path will be
        /// handed down the chain to Tracer
        /// </example>
        public void TraceCrossTask(
                    string info,
                    [CallerMemberName] string member = "",
                    [CallerLineNumber] int line = 0,
                    [CallerFilePath] string path = ""
                    )
        {
            if (TraceOptions == TraceOptions.None)
                return;
            if (!IsTracing)
                return;
            if (!AllowCrossTaskTracing)
            {
                var callerTaskId = Task.CurrentId;
                throw new InvalidOperationException(
                    $"TraceCrossTask called from {callerTaskId} to Tracer {TaskId}, which does not have AllowTraceCrossTask");
            }

            TracePrivate(info, member, line, path);
        }
        #endregion TraceCrossTask, Trace to the Tracer that may or may not be on the same thread

        #region    TracePrivate
        /// <summary>
        /// Write out stuff to the trace file
        /// </summary>
        /// <param name="info">user supplied data</param>
        /// <param name="member">method that trace originated in</param>
        /// <param name="line">Line that trace was called within 'method'</param>
        /// <param name="path">full rooted path to SomeClass.cs file that contains the method that called Trace()</param>
        bool TracePrivate(string info, string member, int line, string path, Tracer tracer = null)
        {
            #region    Validation

            if (info == null)
                throw new ArgumentNullException($"Trace {member}@{line}");
            if (info.Trim() == string.Empty)
                throw new ArgumentException($"Trace empty {0}@{line}");

            if (!AllowCrossTaskTracing && Task.CurrentId != TaskId)
                throw new InvalidOperationException($"{Name} Trace() called on Task in which it was not created");

            #endregion Validation

            #region    Initialization

            string[] words = info.Split(new char[] { }, 2, StringSplitOptions.RemoveEmptyEntries);
            string firstWord = words[0].ToLower();
            string theRest = words.Count() > 1 ? ": " + words[1] : string.Empty;
            TraceElement element = null;  // null because of compiler error - unassigned variable
            string leader = Name;

            #endregion Initialization

            #region    Show Interval data 

            if (IsShowInterval)
            {
                double interval = (DateTime.Now - BaseTime).TotalMilliseconds;
                if (interval >= 100_000_000.0)
                {
                    BaseTime = DateTime.Now;
                    TracePrivate($"*** Resetting Interval to zero at {DateTime.Now.ToString("HH:MM:SS")}", member, line, path, tracer);
                }
            }
            #endregion Show Interval data 

            #region    switch(entry/exit/other) - Ensure Pop() matches Push()

            bool eitherEntryOrExit = true; // set false if line does not begin with Entry or Exit. Used to format the trace output
            int indent = 0;  // How much space at start of line. The deeper nested in methods then the more space
            var count = entryStack.Count;
            ExitExtra = "";
            path = Path.GetFileNameWithoutExtension(path);
            lock (entryStack)
            {
                switch (firstWord)
                {
                    case string x when firstWord == "entry":
                        #region    Entry
                        // Just remember where we came from so we can check Pop() matches previous Push()
                        indent = 2 * count;
                        element = new TraceElement(member, line, path, info, count);
                        entryStack.Push(element);
                        break;
                    #endregion Entry

                    #region    ??? Commentary on an example when using AllowCrossTaskTracing
                    /* This comment is now completely wrong. Have to rewrite it
                    * Let's take the example below. The entryStack is incremented EVERY push
                    * so there is a + in the column. Exit stack remains unchanged. We have A->G
                    * on the entryStack. 
                    * Exit G works as expected. Removes top entry G
                    * Exit E does not match. Without AllowCrossTaskTracing we would LogicException
                    * Exits E, D, C do not match they are pushed onto the Exit stack.
                    * Exit F matches and Removes F
                    * However the top element of entryStack and exitStack now match
                    * so we generate an Exit E.
                    * We now have a stack match on D so we generate an Exit D
                    * And the same goes for Exit C
                    * The comes Exit A which is out of sequence so it goes on the exitStack
                    * Exit B matches so B is removed from the entryStack
                    * Now we have a stack match and Exit A is generated.
                    * 
                    *            Entry    Exit    
                    *            Stack    Stack
                    *  Enter  A    +       
                    *  Enter  B    +       
                    *  Enter  C    +       
                    *  Enter  D    +       
                    *  Enter  E    +       
                    *  Enter  F    +       
                    *  Enter  G    +  
                    *  Exit   G    -
                    *  Exit   E            +
                    *  Exit   D            +
                    *  Exit   C            +
                    *  Exit   F    -               
                    *  =>Pop  E    -       -
                    *  =>Pop  D    -       -
                    *  =>Pop  C    -       -
                    *  Exit   A            +
                    *  Exit   B    -
                    *  =>Pop  A    -       -
                    */
                    #endregion ??? Commentary on an example when using AllowCrossTaskTracing

                    case string x when firstWord == "exit" && AllowCrossTaskTracing:
                        #region    Trace($"Exit") with AllowCrossTaskTracing:

                        if (entryStack.Count == 0)
                            Statics.InternalProblem($"entryStack is empty for a Pop request: {member}@{line} in {Path.GetFileName(path)}{CRLF}");

                        ExitExtra = " ";
                        info.Insert(3, " ");

                        var peekIn = element = entryStack.Peek();

                        indent = 2 * entryStack.Count - 2;

                        /* if misaligned Pop, we remember it so we can unwind it later 
                         */
                        if (peekIn.Member == member || peekIn.Path == path)
                            element = entryStack.Pop();
                        else
                        {
                            /* drop the last TraceElement in the entryStack that matched this Pop.
                             * */
                            try
                            {
                                element = entryStack.Last(te => te.Path == path && te.Member == member);
                            }
                            catch (Exception e)
                            {
                                UnMatchedPopAbort(info, member, line, path, e); // doesn't come back from this
                            }
                            if (element == null)
                                UnMatchedPopAbort(info, member, line, path, null); // we don't come back from this

                            /* we can't remove from the middle of a stack so we have to regenerate the stack minus
                             * the Trace("Entry") that corresponds to this Trace("Exit")
                             */
                            entryStack = new Stack<TraceElement>(entryStack.Where(te => te != element));
                            var tempStack = new Stack<TraceElement>(entryStack.Where(te => te != element));
                            entryStack = tempStack;
                        }
                        break;
                    #endregion Trace($"Exit") with AllowCrossTaskTracing

                    case string x when firstWord == "exit":
                        #region    --Exit-- no cross threading

                        ExitExtra = " ";
                        info.Insert(3, " ");
                        indent = 2 * entryStack.Count + 1;

                        if (entryStack.Count == 0)
                            Statics.InternalProblem($"No TraceElement for Pop request: @{line} in {member} {Path.GetFileName(path)}{CRLF}");

                        #region    Mismatched Exit
                        /*
                         * Ensure we enter and leave the same number of times and from the same place unless
                         * we are Tracing multiple Tasks. With AllowCrossTaskTracing Entry and Exit can
                         * come in any order.
                         */
                        var peek = entryStack.Peek();
                        if (peek.Member != member || peek.Path != path)
                            UnMatchedPopAbort(info, member, line, path, null);  // we don't come back from this
                        #endregion Mismatched Exit

                        element = entryStack.Pop();
                        #endregion --Exit-- no cross threading
                        break;

                    case string x when entryAndExitWords.Contains(firstWord):
                        #region    Trace("Entry&Exit")
                        element = new TraceElement(member, line, path, info, 0);
                        eitherEntryOrExit = true;
                        indent = 2 * entryStack.Count + 2;
                        #endregion Trace("Entry&Exit")
                        break;

                    default:
                        #region    Trace("Anything that doesn't begin with Entry, Exit, Entry&Exit
                        indent = 2 * entryStack.Count + 2;
                        eitherEntryOrExit = false;
                        break;
                        #endregion Trace("Anything that doesn't begin with Entry, Exit, Entry&Exit
                }

            } // End of lock(entryStack)
            #endregion switch(enter/exit/other) - Ensure Pop() matches Push()

            #region    Format output and WriteTrace
            string offset = new string(' ', 2 * indent);
            traceData = eitherEntryOrExit
                ? $"{Header}{offset}{firstWord}{ExitExtra} {path}.{member} @ {line} {theRest}  "
                        + (
                             IsShowAssembly
                                 ? $" [{element.Assembly}]{CRLF}"
                                 : $"{CRLF}"
                           )
                : $"{Header}{offset}{info}{CRLF}";

            Write(traceData);
            #endregion Format output and WriteTrace

            #region    Callback
            /*
             * Let's see if there is any callback data to be added to the Trace
             */
            if (Callback != null)
            {
                string callbackMsg = Callback();
                if (!string.IsNullOrEmpty(callbackMsg))
                    Write(callbackMsg);
            }
            #endregion Callback

            return true;
        }
        #endregion TracePrivate

        #region    Unmatched Pop
        /// <summary>
        /// There was a Trace($"Exit whatever") but there is no corresponding
        /// </summary>
        /// <param name="info">The "Exit whatever" in the Exit that failed</param>
        /// <param name="member">The class that issued The Trace($"Exit")</param>
        /// <param name="line">The line number in <paramref name="member"/></param>
        /// <param name="path">The path to the class that issued the Trace($"Exit")</param>
        /// <param name="e">Optional exception value</param>
        void UnMatchedPopAbort(string info, string member, int line, string path, Exception e)
        {
            var element = entryStack.Pop();
            string ensure = $"*** Ensure that \"{member}\" has executed Trace(\"Entry\") once{CRLF}and not executed Trace(\"Exit\") twice{CRLF2}";
            /*
             * eg...
             * Mismatched Trace
             * 
             * @472 in ExecuteHelper
             * Last stack entry @ 355 in Execute
             * Passed trace info: Exit index=0
             * Outstanding Trace calls
             * ...A list of Calls that got us here that we haven't travelled back down
             * 
             * ***Ensure etc
             */
            string yet = GetOutstandingTraceEntryCalls(element);
            string whereAreWe =
                $"Mismatched {member}.Trace(\"{info}\") failed @{line} in {Path.GetFileNameWithoutExtension(path)}{CRLF2}" +
                $"Top entry in stack: [{element.Member} @{element.Line} in {element.Filename} in Assembly {element.Assembly}] {CRLF2}" +
                $"{yet}{CRLF2}{ensure}";

            Write(whereAreWe);

            StopAllTracing();

            Statics.InternalProblem(whereAreWe, exc: e);
        }
        #endregion Unmatched Pop

        #region    TraceStatic Trace to the tracer associated with this thread
        /// <summary>
        /// Write information to the trace output
        /// </summary>
        /// <param name="info">user specified info useful for the </param>
        /// <param name="path">last part of the path from the calling class. Uses the [CallerFilePath] attribute so it is not
        /// needed unless the class has it's own Trace() method</param>
        /// <param name="member">The method that called this Trace(etc). Uses the [CallerMemberName] attribute so it is not
        /// needed unless the class has it's own Trace() method</param>
        /// <param name="line">The line number that called this Trace(etc). Uses the [CallerLineNumber] attribute so it is not
        /// needed unless the class has it's own Trace() method</param>
        /// <param name="useFirstTracer">This is set true if we wish to write to the main tracer
        /// from a thread which is not the main thread. This happens due to external events like
        /// FileSystemWatcher. It should not contain any Entries or Exits</param>
        /// <example>
        /// Tracer.Trace($"Entry Index=" + index.ToString());
        /// <para></para>
        /// if the class has its own Trace() method it would look like this:
        /// <para></para><code>
        /// void Trace(string info, [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        /// { Tracer.Trace(info,  member, line, [CallerFilePath] string path = "");}
        /// </code>
        /// </example>

        static public bool TraceStatic(string info, [CallerMemberName] string member = "",
                                              [CallerLineNumber] int line = 0,
                                              [CallerFilePath] string path = "",
                                              bool useFirstTracer = false)
        {
            if (TraceOptions == TraceOptions.None)
                return false;
            if (TracerDictionary.Count() == 0)
                throw new InvalidOperationException("Trace called with no active Tracers");

            /*
             * Decide what tracer is to be used. Use the one that was created on the same
             * thread as the current thread, unless it is specified to use the first tracer.
             * That option is used for other threads over which we have control. The only
             * example here are events caused by the FileSystemWatcher on the regex file://
             */

            int? taskId = Task.CurrentId;
            Tracer tracer;
            mutex.WaitOne();
            {
                if (useFirstTracer)
                    tracer = TracerDictionary.Values.First();
                else
                    tracer = TracerDictionary.Values.FirstOrDefault(a => a.TaskId == taskId);
            }
            mutex.ReleaseMutex();

            try
            {
                if (tracer == null)
                    throw new InvalidOperationException("No Tracer active for thread");
                if (String.IsNullOrEmpty(tracer.Name))
                    Statics.InternalProblem("Trace called from a thread that has no Tracer assigned to it");
                if (taskId != tracer.TaskId)
                    info += $"on UI thread {tracer.TaskId} from thread {taskId}";

                return tracer.TracePrivate(info, member, line, path);
            }

            catch (Exception e) // unused variable e, warning suppressed
            {
                Statics.HelpInformation("Trace exception", e);
                throw;
            }

        }
        #endregion TraceStatic Trace to the tracer associated with this thread

        #region    Write
        /// <summary>
        /// Wrapper to remove async stuff. Just Write("text")
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        void Write(string msg)
        {
            streamWriter.Write(msg);
        }
        #endregion Write

        #region    WriteAsync
        /// <summary>
        /// Desperately trying different things so the sequence of trace entries
        /// is correct
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="CRLF"></param>
        /// <returns></returns>
        async Task<string> WriteAsync()
        {
            await FileIO.AppendTextAsync(traceFile, traceData);
            return string.Empty;
        }
        #endregion WriteAsync

        #region    IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    AssertTraceStackIsAlmostEmpty(this);
                    TracerDictionary.Remove(Name);
                    if (streamWriter != null)
                    {
                        //streamWriter?.BaseStream.Flush();
                        //streamWriter?.BaseStream.Dispose();
                        streamWriter.Dispose();
                    }
                }

                disposedValue = true;
            }
        }
        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put clean-up code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion IDisposable Support
    }
    #region    TraceOption enum
    ///<summary>
    /// Defines where the trace goes to
    /// <para/>Console so is available to be seen in output
    ///</summary>
    [Flags]
    public enum TraceOptions
    {
        /// <summary>
        /// No tracing active
        /// </summary>
        None = 0,
        /// <summary>
        /// Tracing to console
        /// </summary>
        Console = 1,
        /// <summary>
        /// Tracing to a file
        /// </summary>
        File = 2,
    }
    #endregion TraceOption enum

    #region    ITracer
    /// <summary>
    /// Required for all things that use Tracer.Trace in any of its forms
    /// </summary>
    public interface ITracer
    {
        void Trace(string info,
                        [CallerMemberName]string member = "",
                        [CallerLineNumber] int line = 0,
                        [CallerFilePath] string path = "");
    }
    #endregion ITracer

}

