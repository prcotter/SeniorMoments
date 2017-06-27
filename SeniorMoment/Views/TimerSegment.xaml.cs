using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SeniorMoment.Services;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace SeniorMoment.Views
{
    /// <summary>
    /// A single digit on an TimerStrip
    /// </summary>
    public partial class TimerSegment : UserControl, INotifyPropertyChanged
    {
        #region    Properties

        #region    Brush
        /// <summary>
        /// The SolidColorBrush created from the x:Bind variable Color
        /// </summary>
        public SolidColorBrush Brush
        {
            get
            {
                if (TimerStrip == null)
                    return new SolidColorBrush(Colors.PaleGoldenrod);
                return TimerStrip.Brush;
            }
        }
        #endregion Brush

        #region    ButtonContent
        /// <summary>
        /// Return a suitable number (or other string) to be placed on an TimerSegment.
        /// Setting the ButtonContent also causes the binding to be updated.
        /// </summary>
        string ButtonContent
        {
            get => TimerStrip?.GetButtonContent(Index);
            set
            {
                _ButtonContent = DateTime.Now.Second.ToString();
                if (_ButtonContent != _PreviousButtonContent)
                {
                    NotifyPropertyChanged("ButtonContent");
                    _PreviousButtonContent = _ButtonContent;
                }
            }
        }
        string _ButtonContent = "";         // _ButtonContent & _PreviousButtonContent must be 
        string _PreviousButtonContent = null; // different to cause a first time loading
        #endregion ButtonContent

        #region    Current, CurrentAsString
        /// <summary>
        /// The current value as displayed on the TimerSegment (with x:Bind)
        /// </summary>
        public int Current { get; set; } = 59;

        /// <summary>
        /// String representation of Current
        /// </summary>
        public string CurrentAsString { get => Current.ToString(); }

        #endregion Current, CurrentAsString

        #region    Index
        /// <summary>
        /// zero-based index of left-to-right position of TimerSegment within TimerStrip
        /// </summary>
        public int Index { get; set; } = 0;
        #endregion Index

        #region    LeftSegment, RightSegment
        /// <summary>
        /// Segment to the immediate left of this one, null if it is the first (Index=0)
        /// </summary>
        public TimerSegment LeftSegment => (Index == 0 ? null : TimerStrip.TimerSegments[Index - 1]);         //public TimerSegment LeftSegment { get { return (Index == 0 ? null : ((Parent)TimerStrip).TimerSegments[Index - 1]); } }

        /// <summary>
        /// Segment to the immediate Right of this one, null if it is the last (Index=3)
        /// </summary>
        public TimerSegment RightSegment => (Index == 0 ? null : TimerStrip.TimerSegments[Index + 1]);

        #endregion LeftSegment, RightSegment

        #region    Increment
        /// <summary>
        /// What happens to SecondsToZero we press the inner addition/subtraction buttons
        /// </summary>
        public int Increment { get; set; } = 1;
        #endregion Increment

        #region    IncrementPlus
        /// <summary>
        /// What happens to SecondsToZero we press the outer addition/subtraction buttons
        /// </summary>
        public int IncrementPlus { get; set; } = 5;
        #endregion Increment

        #region    Minimum, Maximum
        /// <summary>
        /// Maximum value to be displayed on TimerSegment. So for Minutes it would be 59
        /// </summary>
        public int Minimum { get; set; } = 0;

        /// <summary>
        /// Maximum value to be displayed on TimerSegment. So for Minutes it would be 59
        /// </summary>
        public int Maximum { get; set; } = 59;
        #endregion Minimum, Maximum

        #region    PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// Delegate for when Color or Number of TimerSegment changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion PropertyChangedEventHandler PropertyChanged;

        #region    TimerStrip 
        /// <summary>
        /// The TimerStrip to which this TimerSegment belongs
        /// </summary>
        public TimerStrip TimerStrip { get; private set; }

        #endregion TimerStrip

        #endregion Properties

        #region    Constructor ()
        /// <summary>
        /// public default constructor needed for Xaml
        /// </summary>
        public TimerSegment()
        {
            this.InitializeComponent();
            DataContext = this;
        }
        #endregion Constructor ()

        #region    Initialize
        /// <summary>
        /// Get the TimerSegment ready for action
        /// </summary>
        internal void Initialize(TimerStrip timerStrip)
        {
            TimerStrip = timerStrip;
        }
        #endregion Initialize

        #region    NotifyPropertyChanged
        /// <summary>
        /// Update the UI as x:Bind has changed
        /// </summary>
        /// <param name="propertyName"></param>
        
        public void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                Statics.RunOnUI(() =>
              {
                  var args = new PropertyChangedEventArgs(propertyName);
                  PropertyChangedOnUI(this, args);
              });
            }
            else
            { }
        }
        #endregion NotifyPropertyChanged

        void PropertyChangedOnUI(object sender, PropertyChangedEventArgs args)
        {
            PropertyChanged(sender, args);
        }
        #region    Tick
        /// <summary>
        /// Every time the clock ticks we have to work out what should be on this button.
        /// We cause a PropertyChanged event by assigning anything to the ButtonContent.
        /// <para/>This causes a 'get' which does: TimerStrip.GetButtonContent(Index)
        /// <para/>The ButtonContent is binded to the digits
        /// </summary>
        public void Tick()
        {
            /*
             * if (for example) on the last Tick the Hour was 5 and it still is 5
             * then we have no need to refresh the digit. ButtonContent is initialized 
             * to string.Empty so there is always a first time setting of ButtonContent
             */
            ButtonContent = "??";
        }
        #endregion Tick
    }
}
