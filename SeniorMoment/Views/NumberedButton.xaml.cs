using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using SeniorMoment.Services;
using SeniorMoment.Views;
using SeniorMoment;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SeniorMoment.Views
{
    public partial class NumberedButton : UserControl
    {
#pragma warning  disable IDE0027 
        // pragma needed as these next three properties are bound and need accessors
        #region    Number (as show on button )
        /// <summary>
        /// The number on the button such as -5 or +1. The middle button has a different meaning
        /// </summary>
        public string Number
        {
            get
            {
                if (int.TryParse((string)JumpButton.Content, out int num))
                    return (string)JumpButton.Content;
                else
                    return "0";
            }
            set { JumpButton.Content = value; }
        }

        #endregion Number (as show on button )

        #region    Index
        /// <summary>
        /// From 0 to 4 to indicate which button was pressed
        /// </summary>
        public int Index  { get; set; }
        #endregion Index

        #region    Something
        /// <summary>
        ///  
        /// </summary>
        public string Something { get; set ; } 
        #endregion Something

#pragma warning restore IDE0027

        #region    IsIncrementButton
        /// <summary>
        /// is this one of the plus or minus buttons as opposed to the middle display button
        /// </summary>
        public bool IsIncrementButton => Index != 2;
        #endregion IsIncrementButton

        #region    Constructor 
        public NumberedButton( )
        {
            this.InitializeComponent();
            //DataContext = this;
        }
        #endregion Constructor

        #region    Events

        #region    JumpButton_Click
        private async void JumpButton_Click(object sender,RoutedEventArgs e)
        {

            MessageDialog dialog = new Windows.UI.Popups.MessageDialog("Changing values to 99", "Senior Moments")
            {
                DefaultCommandIndex = 0
            };
            var options = new Windows.UI.Popups.MessageDialogOptions();

            options = Windows.UI.Popups.MessageDialogOptions.AcceptUserInputAfterDelay;
            dialog.Options = options;
            await dialog.ShowAsync();
        }
        #endregion JumpButton_Click

        #endregion Events
    }
}
