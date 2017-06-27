using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using System.Windows.Input;

namespace SeniorMoment.Services
{

    /// <summary>
    /// Temporary class so we can transition to Windows.UI dialogs
    /// </summary>
    public class  MessageBox
    {
        public ICommand PrimaryButtonCommand { get; set; }
        public object PrimaryButtonCommandParameter { get; set; }
        public static DependencyProperty PrimaryButtonCommandParameterProperty { get; }
        public static DependencyProperty PrimaryButtonCommandProperty { get; }
        public Style PrimaryButtonStyle { get; set; }
        public string PrimaryButtonText { get; set; }

        ContentDialog dialog = new ContentDialog();

        public MessageBox(string message, string title = "Senior Moment", string buttonText = "OK", ICommand iCommand = null)
        {
            dialog.Title = title;
            dialog.Content = message;
            dialog.BorderBrush = new SolidColorBrush(Windows.UI.Colors.PaleGreen);
            dialog.BorderThickness = new Thickness(3);
            dialog.PrimaryButtonText = buttonText;
            dialog.IsPrimaryButtonEnabled=true;
            dialog.IsSecondaryButtonEnabled = false;
            if (iCommand == null)
                dialog.PrimaryButtonCommand = iCommand;
            Windows.Foundation.IAsyncOperation<ContentDialogResult> dr = dialog.ShowAsync();
        }
        public async Task<int> Show()
        { 
            var result = await dialog.ShowAsync();
            
            var toString = result.ToString();
            return 1;
        }
#pragma warning disable 67,169
        class MyClass : ICommand
        {
            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                throw new NotImplementedException();
            }

            public void Execute(object parameter)
            {
                throw new NotImplementedException();
            }
        }
    }
}
