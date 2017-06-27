using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace SeniorMoment.Services
{
    /// <summary>
    /// Represents a stack of 'memories' of whether something is computer generated
    /// or occurred due to user intervention.
    /// This is used by event handlers and other code to check if they are being fired
    /// due to the user intervention (eg pressing a button, entering text etc)
    /// or program caused the event.
    /// In general, That's not the Mathematical 'in general')if the program caused it
    /// we do not want the event to do any processing. If there is something that needs
    /// doing then the code should do it after the event is fired.
    /// The code in many events will look like this...
    /// <para>private void Control_EventHappened(object sender, EventArgs e)</para>
    /// <para>Code to do whether event was user- or program-instigated such as Trace()</para>
    /// <para>if (ProgramGeneratedEvent.IsProgramGenerated) 
    /// </para>
    /// <para>  return</para>
    /// <para>ProgramGeneratedEvent.Push</para>
    /// <para>... program stuff that may cause events to fire such as putting text in 
    /// TextBoxes or changing Checkboxes, or ComboBox SelectedItem </para>
    /// <para>ProgramGeneratedEvent.Pop</para>
    /// <para>End of event</para>
    /// </summary>
    public class ProgrammedEvent
    {
        #region    NameAndLineClass (nested in ProgrammedEvent)
        /// <summary>
        /// Class to keep list of who called what and where. It is used by ProgramGeneratedEvent 
        /// for list of pushed 'things' in the stack
        /// </summary>
        class NameAndLine
        {
            #region    Name
            /// <summary>
            /// Name of the routine that called Push()
            /// </summary>
            public string Name; // { get; private set; }
            #endregion Name

            #region    Line
            /// <summary>
            /// line number of routine that called Push()
            /// </summary>
            public int Line; // { get; private set; }
            #endregion Line

            #region    CodeFile
            /// <summary>
            /// the source code file that originated the Push()
            /// </summary>
            public string CodeFile;
            #endregion CodeFile

            #region    Constructor(name,line)
            /// <summary>
            /// Name and Line Constructor
            /// </summary>
            /// <param name="name">method name that made the Push()</param>
            /// <param name="line">line number of the Push()</param>
            /// <param name="path">the path of the file that did the Push()</param>
            public NameAndLine(string name, int line, string path)
            {
                Name = name;
                Line = line;
                CodeFile = Path.GetFileNameWithoutExtension(path);
            }
            #endregion Constructor(name,line)

            #region    ToString()
            /// <summary>
            /// Show "Pushed from MyMethod @911"
            /// </summary>
            /// <returns>a string like: "Pushed from GetNextFile @386"</returns>
            [System.Diagnostics.DebuggerStepThrough()]
            public override string ToString()
            {
                return string.Format("Pushed from {0} @{1}",
                                                  Name, Line);
            }
            #endregion ToString()
        }
        #endregion NameAndLine class  (nested in ProgrammedEvent)

        #region    Variables

        public const string CRLF = "\r\n";
        // public const string CRLF2 = "\r\n\r\n";

        #region    PGEStack    
        ///<summary>
        ///Get or Set theStack of Push and Pops. A list of members and line number 
        ///when PGE.Push was called without a matching PGE.Pop - at least not yet.
        ///</summary>
        static Stack<NameAndLine> PGEStack = new Stack<NameAndLine>();
        #endregion PGEStack

        #region    MaximumDepth
        ///<summary>Get or Set the maximum depth before we abort. This is to stop accidental
        /// infinite recursion. (ie  programming bugs)
        ///</summary>
        static public int MaximumDepth
        {
            get => _MaximumDepth; set
            {
                if (value < 0)
                    throw new ArgumentException("Must be 0 or greater");
                _MaximumDepth = value;
            }
        }
        ///<summary>The maximum depth that we can push before we abort.
        ///</summary>
        static private int _MaximumDepth = 100;
        #endregion MaximumDepth

        #region    IsProgramGenerated
        /// <summary>
        /// Are we are running in code where the event was caused by the
        /// program rather than user interaction. So the event looks like this..
        /// <code>
        /// private void Control_Event(object sender, EventArgs e)
        /// if (ProgrammedEvent.IsProgramGenerated
        /// {
        ///     ProgrammedEvent.Push()
        ///     Do other stuff that may cause events to fire
        ///     ProgrammedEvent.Pop()
        /// }</code>
        /// </summary>
        static public bool IsProgramGenerated => PGEStack.Count > 0;
        
        #endregion IsProgramGenerated

        #endregion Variables

        #region    AssertMaxOutstandingPushes
        ///<summary> 
        ///When we get back to user intervention layer we can ensure the stack is empty
        ///</summary>
        ///<param name="maxStackedPushes">Maximum number of outstanding Push() requests that have not
        ///yet had a matching Pop()</param>
        static public void AssertMaxOutstandingPushes(int maxStackedPushes)
        {
            if (PGEStack.Count > maxStackedPushes)
                throw new LogicException("Outstanding Push(es):" + "\r\n\r\n" + GetOutstandingPushes());
        }
        #endregion AssertMaxOutstandingPushes

        #region    Clear
        /// <summary>
        /// Get rid of stack. Dunno why I wrote this
        /// </summary>
        static public void Clear()
        {
            PGEStack.Clear();
        }
        #endregion Clear

        #region    GetOutstandingPushes
        ///<summary> GetOutstandingPushes
        ///Get a programmer-readable summary of Pushes that have not had outstanding Pops
        ///</summary>
        ///<returns>a programmer-readable summary of Pushes that have not had outstanding Pops</returns>
        static public string GetOutstandingPushes()
        {
            string pushBits = "";
            foreach (var element in PGEStack)
                pushBits += string.Format("Missing Pop for {0} element @{1} in {2} {3}", element.Name, element.Line, element.CodeFile, CRLF);
            return pushBits;
        }
        #endregion GetOutstandingPushes

        #region    Pop
        /// <summary>
        /// Get the last thing to be pushed on the stack. We ensure the push and pop were called from the same place.
        /// </summary>
        /// <param name="name">The routine that called Pop()</param>
        /// <param name="line">The line number in <paramref name="name"/> from which Pop() was called</param>
        static public void Pop([CallerMemberName] string name = "", [CallerLineNumber] int line = 0)
        {
            if (PGEStack.Count < 1)
                throw new LogicException(String.Format("Too much Pop is never a good thing ... Pop called from {0} line {1}", name, line));
            NameAndLine pushedNameAndLine = PGEStack.Pop();
            if (pushedNameAndLine.Name != name)
                throw new LogicException(String.Format("Poppycock ... Push called from {0} line {1}, Pop called from {2} line {3}",
                    pushedNameAndLine.Name, pushedNameAndLine.Line, name, line));

        }
        #endregion Pop

        #region    Push
        /// <summary>
        /// Add a memory of this Push() storing the class name and line number
        /// </summary>
        /// <param name="name">The name of the routine that called Push(). This should
        /// normally be left to default.</param>
        /// <param name="line">The line number in the <paramref name="name"/> that called Push(). This should
        /// normally be left to default.</param>
        /// <param name="path">The file that called Push(). So if the Path is 
        /// "D:\users\...\Action.cs then "Action" will be stored in CodeFile
        /// normally be left to default.</param>
        static public void Push([CallerMemberName] string name = "", [CallerLineNumber] int line = 0, [CallerFilePath] string path = "")
        {
            if (PGEStack.Count > _MaximumDepth)
                throw new LogicException("Program is too pushy. Time to push off");
            PGEStack.Push(new NameAndLine(name, line, path));

        }
        #endregion Push
    }
}
