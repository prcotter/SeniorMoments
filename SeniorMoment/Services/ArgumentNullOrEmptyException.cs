using System;

namespace SeniorMoment.Services
{
    /// <summary>
    /// Simple exception thrown when an argument is null OR empty
    /// </summary>
    public class ArgumentNullOrEmptyException : ArgumentNullException

    {
        /// <summary>
        /// throw when a string argument is null or empty ... and shouldn't be
        /// </summary>
        public object Argument;
        
        /// <summary>
        /// Set true if the param was null
        /// </summary>
        public bool IsNullParameter;
        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="paramName">the string representation of the parameter name</param>
        /// <param name="param">string argument that has the problem</param>
        /// <param name="message">optional additional message</param>
        /// <example>throw new ArgumentNullOrEmptyException("name",name,"name should begin with #"</example>
        public ArgumentNullOrEmptyException(string paramName , object param, string message=null)
        {
            if (message == null)
                if (param == null)
                    message = paramName + " is null";
                else
                    message = ParamName + " is empty";
        }
    }
}
