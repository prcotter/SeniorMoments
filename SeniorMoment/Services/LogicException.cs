using System;
using System.Runtime.CompilerServices;
using System.IO;

namespace SeniorMoment.Services
{

    /// <summary>
    /// Special class used when the program detects an inconsistency. This should mean a
    /// programming bug (ie mine) rather than a user error.
    /// </summary>
    public class LogicException : Exception
    {
        /// <summary>
        /// Constructor. Display a big error box
        /// </summary>
        /// <param name="message">specific message from caller</param>
        /// <param name="exc">Inner exception if any. Defaults to null</param>
        /// <param name="member">The method that caused the exception</param>
        /// <param name="line">The line in <paramref name="member"/> that caused the exception</param>
        /// <param name="path">file path to code file that caused the exception</param>
        public LogicException
            (
                string message,
                Exception exc = null,
                [CallerMemberName] string member = "",
                [CallerLineNumber] int line = 0,
                [CallerFilePath] string path = ""
            ) : base(message, exc)
        {
            Callback?.Invoke(); // if there is a callback then go do it
            if (exc is AggregateException)
            {
                // ??? need different coding to handle this which arises at Media.Play
            }

            string CRLF2 = Statics.CRLF2;
            _message = string.Format(
                $"Internal {Statics.FriendlyName} error (aka bug) at line {line} in {member} for file {Path.GetFileName(path)}{CRLF2}The following information may help you or the author(Paul Cotter) solve the problem {message}{CRLF2}");

            _message += Tracer.AbortAll();

            Statics.RunOnUI(msgBox);
        }
        void msgBox()
        {
            new MessageBox(_message);
        }
        static public Action Callback = null;
        string _message;
    }
}