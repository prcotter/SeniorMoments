using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using SeniorMoment; 

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SeniorMoment.Views
{
    public sealed partial class NumberStrip : UserControl
    {
        #region    Index
        /// <summary>
        /// zero based unique index of the passed alarm strip into the array held by VMPage
        /// </summary>
        public int Index { get; set; }
        #endregion Index
        
        // public string Label = "";
        public NumberStrip() : base()
        {
            this.InitializeComponent();
            DataContext = this;
        }

        private void NumberStripCanvas_GotFocus(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonLessLess_Loading(FrameworkElement sender, object args)
        {

        }
    }
}
