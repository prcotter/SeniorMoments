//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Runtime.InteropServices.WindowsRuntime;
//using Windows.Foundation;
//using Windows.Foundation.Collections;
//using Windows.UI.Xaml;
using SeniorMoment.Services;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml.Controls;
//using Windows.UI.Xaml.Controls.Primitives;
//using Windows.UI.Xaml.Data;
//using Windows.UI.Xaml.Input;
//using Windows.UI.Xaml.Media;
//using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SeniorMoment.Views
{
    public abstract partial class VBase : UserControl //, IVBase<UserControl>
    {
        //public static abstract VBase Hot { get; private set; }

        #region    Constructor
        //
        public VBase()
        {
            Trace($"Entry ");
            InitializeComponent();
            Trace($"Exit  ");
        }
        #endregion Constructor

        #region    Hot
        /// <summary>
        /// The currently active view
        /// </summary>
        public bool Hot;
        #endregion Hot

        /// <summary>
        /// This is something like 'focus'. There is always a current TimerStrip, VMtimer even if the 
        /// focus is on Microphone - in fact especially if it is on the Microphone. So at any given
        /// time there is always one timer which is Hot. 
        /// </summary>
        public virtual void MakeMeHot(bool isHot = true) { }

        /// <summary>
        /// Each view must have the ability to Trace.
        /// </summary>
        /// <param name = "info" ></ param >
        /// < param name= "member" ></ param >
        /// < param name= "line" ></ param >
        /// < param name= "path" ></ param >
        [System.Diagnostics.DebuggerStepThrough()] // never want to go into Trace unless debugging Tracer
        public virtual void Trace(string info,
                            [CallerMemberName]string member = "",
                            [CallerLineNumber] int line = 0,
                            [CallerFilePath] string path = "")
        {
            Tracer.TracerMain.TraceCrossTask(info, member, line, path);
        }

    }
}
