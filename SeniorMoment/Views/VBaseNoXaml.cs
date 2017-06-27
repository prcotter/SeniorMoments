using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SeniorMoment.Views
{

    public interface IVBase
    {

    }
    public partial class VBase<T> where T : class
    {
        //public static abstract VBase Hot { get; private set; }

        #region    Constructor
        //
        public VBase() 
        {
            
            //Trace($"Entry {}");
            //InitializeComponent();
            //Trace($"Exit  {}");
        }
        #endregion Constructor

        ///// <summary>
        ///// This is the View that we should be using. 
        ///// </summary>
        //public void Use()
        //{
        //    Hot = this;
        //}

        /// <summary>
        /// Each view must have the ability to Trace. 
        /// </summary>
        /// <param name="info"></param>
        /// <param name="member"></param>
        /// <param name="line"></param>
        /// <param name="path"></param>
        //public void Trace(string info,
        //                    [CallerMemberName]string member = "",
        //                    [CallerLineNumber] int line = 0,
        //                    [CallerFilePath] string path = "");

    }
}
