using System;
using System.Runtime.CompilerServices;
using System.Text;
using Windows.Foundation;
using Windows.Storage;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace SeniorMoment.Services
{
    /// <summary>
    /// Nominal class to hold static variables for the application
    /// </summary>
    static public class Statics
    {
        #region    Constants (but not necessarily const)

        #region    CRLF
        /// <summary>
        /// Carriage Return Line Feed
        /// </summary>
        static public string CRLF = Environment.NewLine;
        #endregion CRLF

        #region    CRLF2
        /// <summary>
        /// Carriage Return Line Feed - only twice
        /// </summary>
        static public string CRLF2 = Environment.NewLine + Environment.NewLine;
        #endregion CRLF2

        #endregion Constants: including non-const variables

        #region    Variables

        #region    AssetsFolder
        /// <summary>
        /// The Assets folder where images and sounds are kept
        /// </summary>
        static public StorageFolder AssetsFolder;
        #endregion AssetsFolder

        #region    BaseTime
        /// <summary>
        /// We use a base date to calculate the age of objects, usually because we will need
        /// to sort on it
        /// </summary>
        public static DateTime BaseTime { get; } = new DateTime(2015, 1, 1);
        #endregion BaseTime

        #region    CoreDispatcher
        /// <summary>
        /// The UI thread Dispatcher
        /// </summary>
        //public static CoreDispatcher CoreDispatcher;
        public static CoreDispatcher CoreDispatcher; //  = CoreWindow.GetForCurrentThread().Dispatcher;
        #endregion CoreDispatcher

        #region    FriendlyName
        ///<summary>
        /// Get Nice name for the application 
        ///</summary> 
        static public String FriendlyName { get; private set; }
        #endregion FriendlyName

        #region    InstallationFolder
        /// <summary>
        /// Pointer to the InstalledLocation. This is UWP specific
        /// </summary>
        public static StorageFolder InstallationFolder { get; } = Windows.ApplicationModel.Package.Current.InstalledLocation;
        #endregion InstallationFolder

        #region    LocalFolder
        /// <summary>
        /// The sandboxed storage area that exists on this Windows 10+ device.
        /// </summary>
        static public StorageFolder LocalFolder { get; } = ApplicationData.Current.LocalFolder;
        #endregion LocalFolder

        #region    NextUniqueId
        /// <summary>
        /// Return an incremental unique id, but it is only unique for the run of this program.
        /// then it starts again at 0.
        /// </summary>
        static public int NextUniqueId
        {
            get
            {
                _NextUniqueId++;
                return _NextUniqueId;
            }
        }
        static int _NextUniqueId = -1;
        #endregion NextUniqueId

        #region    TaskFunnel
        /// <summary>
        /// It is here in Statics so we don't have to carry an instance variable round into the multi-layered Sound.Play
        /// routines
        /// </summary>
        public static TaskFunnel TaskFunnel { get; } = new TaskFunnel();
        #endregion TaskFunnel

        #endregion Variables

        #region    Initialize
        ///<summary>
        /// Initialize various variables that cannot be statically defined
        ///</summary>
        static public void Initialize()
        {
            FriendlyName = "Senior Moment";
            CoreDispatcher = Window.Current.Dispatcher;
        }
        #endregion Initilise

        #region    PopulateListBoxFromEnum
        ///<summary> 
        ///Populate a combobox with the values from an enum. ComboBox.Items will be of type passed.
        ///<para>Example: PopulateListBoxFromEnum(lbPrefixOrSuffix, typeof(PrefixOrSuffix) )</para>
        ///</summary>
        ///<param name="comboBox">ListBox Control to be populated</param>
        ///<param name="enumType">The type of the enum</param>
        static public void PopulateComboBoxFromEnum(ComboBox comboBox, Type enumType)
        {
            comboBox.Items.Clear();
            Enum.GetNames(enumType).ForEach(a => comboBox.Items.Add(a));
        }
        #endregion PopulateListBoxFromEnum

        #region    s (For pluralizing words)
        ///<summary> 
        /// Eye candy. Decide if we want an s at end of the word depending if it is plural or zero.
        /// Looks like a stupid method name, but it tells it all and is 'small' as a string.Format parameter
        ///</summary>
        ///<param name="occurrences">this is number of 'things'. In reality this is either 1 or something else</param>
        ///<returns>Returns string.Empty if occurrences==1 otherwise it returns 's'</returns>
        ///<example>String.Format("There are {0} version{1} of this model" , count, s(count)) </example>
        public static string s(int occurrences)
        {
            if (occurrences == 1)
                return string.Empty;
            return "s";
        }
        #endregion (For pluralising words)

        #region    HelpInformation
        /// <summary>
        /// Display an on-top help information
        /// </summary>
        /// <param name="text">text to be displayed in help window</param>
        /// <param name="e">Adds extra exception info to the message</param>
        /// <param name="freeze">If true then the new window is opened as a modal window and the application is
        /// 'frozen' until you close the information window</param>

        static public void HelpInformation(string text, Exception e = null, bool freeze = false) // yyy
        {
            if (e != null)
                text = text + CRLF2 + GetExceptionMessageChain(e);
            MessageBox MessageBox = new MessageBox(text);
        }
        #endregion HelpInformation

        #region    GetExceptionMessageChain
        /// <summary>
        /// Finds all the messages and StckTrace for an exception plus chaining through the InnerExceptions
        /// </summary>
        /// <param name="e">The exception to be reported on</param>
        /// <returns>Returns all the exception messages chaining through inner exceptions</returns>
        public static string GetExceptionMessageChain(Exception e)
        {
            StringBuilder sb = new StringBuilder();
            while (e != null)
            {
                sb.Append(e.Message + CRLF2 + e.StackTrace + CRLF2);
                e = e.InnerException;
                if (e != null)
                    sb.Append("INNER EXCEPTION" + CRLF2);
            }
            return sb.ToString();
        }
        #endregion GetExceptionMessageChain

        #region    InternalProblem
        //
        public static void InternalProblem(
                    string info,
                    [CallerMemberName] string member = "",
                    [CallerLineNumber] int line = 0,
                    [CallerFilePath] string path = "",
                    Exception exc = null
                    )

        {

            Tracer.StopAllTracing();
            throw new LogicException($"From {member}@{line}: {info}", exc: exc);
        }
        #endregion InternalProblem

        #region    RunOnUI 
        /// <summary>
        /// Perform the Action on the UI thread usually because it uses a UI dependency object
        /// </summary>
        /// <param name="action"></param>
        public static void RunOnUI(Action action)
        {
            var ignore = RunOnUIAsync(action);
        }
        #endregion RunOnUI

        #region    RunOnUIAsync 
        /// <summary>
        /// Perform the Action on the UI thread when called from a different thread
        /// </summary>
        /// <param name="action"></param>
        async public static Task<string> RunOnUIAsync(Action action)
        {
            await CoreDispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { action(); });
            return "";
        }
        #endregion RunOnUI
    }
}
