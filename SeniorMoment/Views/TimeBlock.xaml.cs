using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SeniorMoment.Views

{
    // xxx the focus stuff here is trying things out
    public sealed partial class TimeBlock : UserControl
    {
        public bool HasFocus = false;
        public TimeBlock()
        {
            InitializeComponent();
            DataContext = this; // should not refer to Parent
        }

        private void TimeBlock_GotFocus(object sender, RoutedEventArgs e)
        {
            HasFocus = true;
        }

        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            HasFocus = false;
        }
    }
}
