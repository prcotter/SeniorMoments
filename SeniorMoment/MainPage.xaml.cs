using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using SeniorMoment.Models;
using SeniorMoment.Services;
using SeniorMoment.ViewModels;
using SeniorMoment.Views;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

/// <summary>
/// The main namespace. Has children SeniorMoment.Services, SeniorMoment.Views,
/// SeniorMoment.ViewModels and SeniorMoment.Models. At some time ??? I think 
/// it might be good to have a SeniorMoment.Background which dispenses
/// with all the views to minimise the footprint whilst running in
/// the Background. Might also be good for Suspension as less data to
/// serialise
/// </summary>

namespace SeniorMoment
{
    #region    +++++++++COMMENTS READ THIS BEFORE MODIFYING THE PROGRAM +++++++++++++++++
    /*  
     *  Structure (ha!)
     * 
     * Model classes begin with M, ViewModels with VM and maybe someday Views with V
     * 
     * This program obviously depends on (System) timers. Strangely there is only one system timer
     * that runs and it runs permanently, apart from debugging routines for Pause and
     * Resume. It is called TickTock and has a granularity of one second so it uses
     * very little resource, especially in some (unwritten) background mode.
     * 
     * In these docs if you see A => B, it means that A calls, signals or callbacks B.
     * Sometimes there is =>...=> which means 'eventually'.
     * 
     * In the comments and summaries you will see unexpected capital letters. It means
     * they refer to Methods, Properties, enum values etc as in the following 
     * statements:
     *   
     *   "TimerControlButton.ActivityEdit => VMTimer.Edit"
     *   "User presses the Pause TimerControlButton => MTimer.Pause()"
     * 
     * At various times VMTimers are created (and destroyed) by the user. These in turn
     * CreatMTimers() / Dispose(). These MTimers will countdown for their VMTimer and will tell
     * the VMTimers that times-up. The MTimers register themselves with TickTock 
     * and are informed every second by TickTock.Tick() => MTimer(s).Tick()  
     * Everything happens on one second boundaries. This makes life easier to 
     * ensure only one recording / playback happens at a time.
     *
     * Assume the user adds a new timer. At that point a VMTimer is created. This in turn
     * has children: an MTimer, MSound=>new Sound() and a TimerStrip. The TimerStrip
     * is the View on one timer. It changes every second. VMTimer looks at the Phase
     * and uses Phase values to set various countdowns in MTimer. The most important
     * one is the (MTimer.SecondsToZero == 0)  =>...=> MSound.play(default alarm or user recording). 
     * 
     * MSound is responsible for all audio input (recording) and output (playback).
     * However it does not do the recording or playing. It constructs a Sound to do that.
     * It has two roles. 
     * 
     * ??? Next sentence needs total rewrite as it is a load of dingo's kidneys. (HHGTTG)
     * The static part of it maintains a list of recordings that have been scheduled but 
     * not yet played. It is also 'forbidden' to Record and Play at the same time.
     * Only one thing co-ordinates the recordings / playbacks so
     * they don't overlap ... the TaskFunnel. We will get to that later. It receives Actions
     * which it keeps in an orderly sequence and lets them run, but only one at a time.
     * 
     * MSound has instance variables for access by VMTimersuch as SecondsToAlarm
     * Pity there is no C++ friend option.
     * ??? (rewrite sentence) MSound is partially responsible for ensuring only one recording or playback is active.
     * While the user is recording a "voice alarm" no playbacks are allowed, but they may be scheduled.
     * If there are playbacks being played the microphone is disabled. When an MSound instance wants
     * to play something it adds it to schedule of things to be played and the next one in
     * line is played as soon as there is an opportunity. 
     * MSound.FinishedPlayback => VMTimer => MTimer to reschedule the alarm.
     * Rescheduling resets MTimer to replay the alarms in 'n' seconds (SecondsToAlarm where
     * 'n' is supplied by the Phase of VMTimer.
     * 
     * There is a table of Phases in Phase.Phases. This controls the life-cycle of VMTimers.
     * It controls how often alarms are played, optional callback on Tick(), color of the
     * TimerStrip numeric tiles and the volume. The only real code is when VMTimer requests
     * the next Phase with a GetPhaseFromSecondsToZero(n). VMTimer uses the new Phase
     * to tell MTimer what the new count parameters are. VMTimer makes no decisions
     * about how it operates. It's actions are defined by the Phase which in turn is
     * passed on to MTimer and MSound. There should be no contact between MTimer and
     * MSound as they have totally disparate functions. Wish I could enforce that in C#.
     * 
     * Each VMTimer also owns a TimerStrip, a View which the user sees as a countdown. The
     * appearance of the timer is set by VMTimer with help from MTimer and MSound
     * under the auspices of a Phase
     * .
     * The appearance of the TimerStrip is binded to values derived from the Phase (color)
     * the digits (from MTimer.SecondsToZero) and which TimerControlButtons
     * are on display and are en/disabled
     * 
     * A few little things.
     * 
     * - If you see in the comments something like List{VMTimers} it mean List<VMTimers>.
     *   It's a PITA trying to use angle brackets in XAML such as /// List<string> 
     *   
     *   There are certain codes I use in comments and these can be searched for. 
     *   The meanings are:
     *   
     *   xxx    I think this commented-out code can be deleted but I'm leaving it until I'm positive
     *   yyy    Code needed for NotImplementedException, expand stubs, todo, comeback and
     *          re-structure etc.
     *          Usually commented so I can remember what I'm meant to do
     *   ???    This section needs some more thought (usually there's a comment why it's needed)
     *   zzz    Some other reason. eg I use one at close-of-shop to say where I am and what I'm doing
     *        
     *   If there is a "var xxx = something" it is debug variable that should have been
     *   removed along with all the code that uses it. You can do that for me.
     *   
     *   This is my first attempt at Binding, MVVM and UWP, so I may have gone about it ass-backwards.
     *   (Arsy Versy). I was working on the basis that the Views would be not be 
     *   needed during a suspension or in the background, thus reducing the footprint. 
     *   I've tried to arrange things as follows.
     *   
     *   Services folder: This folder is stand-alone. In other words the complete folder
     *   could be copied to another project and compiled. Any M-V-VM can use any
     *   or all of these Services. Some classes in Services access other classes in Services so
     *   each class is not necessarily stand-alone. I have used them as a 'bunch' in projects.
     *   Some of them are not used in SeniorMoment. (eg TagControl and ProgrammedEvent)
     *   
     *   Views: Only has references to ViewModels, Views and Services. Access to Models is via a ViewModel
     *   
     *   ViewModels: well they are like firemen, they can go anywhere without a warrant
     *   
     *   Models: These can access other Models, but the only reference to a ViewModel
     *   should be callbacks and the ViewModel which owns it. 
     *   
     *   All ViewModels have an Owner, normally  with a specific type 
     *   (such as VMTimer which 'owns' an MTimer). MTimer is different as it's Owner
     *   property is Object so they can exist stand-alone. Currently they don't.
     *   
     *   I have tried to isolate the functions. For example the VMTimer tells the MTimer
     *   when to sound an alarm. Apart from that VMTimer has no sense of elapsed time.
     *   I guess VMTimer should have been called VMController.
     *   
     *   The MSound has no timing in it. MTimer informs VMTimer when time is up for
     *   three different types of 1-per-second countdown leading to three different callbacks:
     *   
     *   SecondsToZero => MSound.PlayXx_x(the alarm/recording)
     *   SecondsToNextPhase => This says when VMTimer should go to Phase.Next()
     *   SecondsToNextAlarm => MSound plays the next alarm/recording after STNA seconds.
     *   
     *   The SecondsToNextAlarm changes with the Phase. As the TimerStrip ages the 
     *   frequency of playback decreases and volume gets lower.
     *   
     *   So the Task/Method sequences follows this path:
     *   
     *   (MTimer.SecondsToNextAlarm == 0) => VMTimer => MSound => MSound.Play()
     *      => TaskFunnel.MeNext(playback) => MSound.playalarm => callback when alarm finishes
     *      => MTimer re-Scheduling using Phase.SecondsBetweenAlarms
     *    
     *   This will continue till the user Pauses , Reloads or Deletes the VMTimer / TimerStrip
     *   plus all the other hangers-on
     *   
     *    VMPhase (although tiny) is the master orchestrator. When a Phase is complete
     *   (at say +300 or -3600 seconds) a new set of values are loaded which amongst other things
     *   could change the color of the TimerStrip, or schedule a message that an alarm
     *   will finish in 5 minutes or have a special callback
     *   
     *   So let's examine the life-cycle of a timer.
     *   
     *   1) User presses the 'Add a timer' button, the one like an alarm clock
     *   
     *   2) MainPage creates a new VMTimer. static VMTimers.Add(new VMTimer)
     *   
     *   3) VMTimer creates the view TimerStrip and inserts it into a ListView in
     *          MainPage. So a new timer appears on the page.
     *          
     *   4) VMTimer looks up the Phase and grabs certain values which include:
     *           
     *         SecondsToZero: when the timer reaches this number it jumps to the next phase
     *         Color for countdown tiles whilst in this phase  ??? rename TimerStrip=>Timer, and TimerSegment to TimerDigit
     *         An optional callback when that number is reached
     *         Number of seconds between alarms -1 means no alarm (ie we haven't reached zero yet)
     *         Volume in range 0.0 to 1.0 for any alarms or advance warnings of alarms
     *         A phrase we can see in debug to identify the Phase
     *         
     *   5) VMTimer creates an MTimer and all it knows is when it reaches a certain SecondsToZero
     *      it should signal VMTimer and reschedule itself as per instructions as per
     *      Phase.SecondsBetweenAlarm => VMTimer =>MTimer
     *      
     *   6) VMTimer Creates a MSound which loads the default alarm "Default.wav"
     *   
     *   7) If the user presses and releases the Microphone...
     *       * MainPage => VMTimer => MSound.Play(Recording.m4a) => StartRecordBeep.wav =>callback
     *       * Recording User' voice
     *       * User presses and releases Microphone
     *       * MainPage = > VMTimer (in focus) => MSound.StopRecording => Sound.StopRecording
     *       This is the most complicated code in Senior moment so have a good
     *       think before playing around with it. You will probably need to activate the
     *       Trace because there are potentially events going left, right and centre.
     *      
     *   7.5) The user presses Start and the countdown to zero and beyond begins.
     *      
     *   8) VMTimer uses TaskTunnel to Schedule Record and Play. It is a FIFO
     *      stack for Sounds. All playbacks and recordings must play sequentially. When a recording is due then ... 
     *      
     *      TaskFunnel is empty:   MSound adds its playback file to the static playback Schedule.
     *                             On Tick() MSound plays the alarm (singing in the TaskFunnel)
     *                             When the alarming is over MSound vacates the TaskFunnel.
     *                             Tasks have priorities. Whenever a Sound is added to the
     *                             Schedule it is sorted on Age within Priority
     *                             
     *      TaskFunnel is Busy:    Schedule.Add(Sound)
     *      
     *   9) On TickTock.Tick() => MSound checks the Schedule and if there is something
     *      playing or recording, it just returns and waits for the next Tick().
     *      Eventually the Sound will be played, and if it is an alarm it is rescheduled
     *      
     *      Other recordings are played asynchronously on the UI thread. The UI thread continues to
     *      run while the sound plays.                 
     *      
     *  10) When the recording is finished MSound is signalled (by a Task.ContinueWith)
     *      that Elvis has left the TaskFunnel. MSound is now free to play the next (if any)
     *      Sound.
     *      
     *      TaskFunnel is a serializer. Various requests come in from various timers. Think of 
     *      a radio station receiving song requests. They can only be played one at a time. Maybe
     *      I should rename TaskFunnel to Broadcast. 
     *      TaskFunnel knows nothing about its tasks so it fits in Services.
     *      
     * Tracing:
     * 
     *      There is a built in Trace system using Tracer.TracerMain. If you want to use it then
     *      read the info in Tracer.TracerMain. Note that 'behaviour control' is dictated by variables
     *      in SeniorMoment.Service.Statics and are set in MainPage.Initialise(). 
     *      something to do find a better way to set these variables from Resources / Assets / Xaml.
     *      The Tracer class is useful for discovering the sequence of calls in a 
     *      multi-threaded application. The 'documentation' is in Services.Tracer. It is fairly
     *      simple. Create a Tracer, then add Trace("whatever you want") wherever you want.
     *      The Trace($"Entry") and Trace($"Exit") are very powerful when you are powerless to
     *      dictate what threads run when
     *      
     * LogicExceptions
     * 
     *      I throw these when something impossible happens. It's called a bug.  We
     *      then have a chance to make it impossible. A word of advice, don't remove them.
     *      It's much easier to debug a LogicException than another unexpected Exception
     *      thrown (possibly much) later. All exceptions thrown in the SeniorMoment are LogicExceptions
     *      (ie 'expected bugs'). All other Exceptions are in .Net code and are 'unexpected'.
     *      (Still bugs though)
     *      
     * Statics
     * 
     *      Many classes have List<themselves> in them. These are static plus any routines that
     *      are playing around with them.
     *      Any universal statics used by Services are in Service.Statics.
     *      Any statics that are application wide and used in the M-V-VM are in MStatics
     *      
     * #region
     * 
     *      What I have done with # regions is liked by some but is an anathema to some. You
     *      can get rid of them with a simple Find-Replace (Ctrl-H) and a regex.
     *      Just as a reminder you may need to get familiar with these shortcuts
     *      
     *          Ctrl-M Ctrl-M  Toggle minimum expansion/contraction of whatever you are in/on
     *          Ctrl-M Ctrl-L  Toggles between everything expanded and everything collapsed...
     *                         Done twice is effectively collapse everything
     *          
     *      If you get #region / #endregion out of synch you can do a regex search on #(end)?region. For
     *      that reason you will find they line up in the search with extra spaces...
     *              #region    Constructor
     *              #endregion Constructor
     *      It's a little extra effort but occasionally it makes it easier to sort out a region problem.
     *      Obviously this is all done by snippets. ??? Move a copy of snippets into the application
     *     
     * _Properties
     * 
     *      _Xyz normally means a backing variable for the Property Xyz. Casing tends to be respected.
     *      
     *      It is also used when a method requires an instance variable that should
     *      not be referenced elsewhere. In that case the variable is usually
     *      embedded in #region  method-name
     *      
     * Alphabetic Order
     * 
     *      You'll notice that Methods and Properties are in alphabetic order, except when they aren't.
     *      
     * Hot
     *      TimerStrip and VMTimer have the concept of one Hot version. There is always (??) one of each.
     *      It's the currently active one. So if the user clicks on any part of a TimerStrip then that
     *      TimerStrip is now Hot and so is the corresponding VMTimer. (Hot is not propagated through to 
     *      MTimer or MSound) If the user clicks on a TimerControlButton or a TimerSegement then the
     *      Hot objects stay Hot. Only when the focus changes to a new TimerStrip does Hot change.
     *      
     * timer
     *      You will see the word timer (little 't') used in comments. This refers to the
     *      VMTimer, TimerStrip, TimerControlButtons, TimerSegments as a whole. If the comment says
     *      that a timer is deleted it means the deletion of all the  aforementioned classes.
     *      
     *  Initialise
     *      The author is English and this is how we spell it in the old country. I use it not (just)
     *      because I'm obtuse, but because it allows to coder to know which bits were written by MS
     *      and which were written by me. Most of the time I use American spelling (Color etc)
     */
    #endregion +++++++++COMMENTS READ THIS BEFORE MODIFYING THE PROGRAM +++++++++++++++++

    /// <summary>
    /// A Timer for old farts like me
    /// </summary>
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        #region    Properties

        #region    CalculatedHeight xxx
        /// <summary>
        /// How high the window should be with a varying number of TimerStrips on display
        /// </summary>
        double CalculatedHeight
        {
            get
            {
                double height = 0;
                height = VMTimer.VMTimers.Count * 65 + 95;
                // xxx MainPage.This.UpdateTimersOnPage();
                return height;
            }
        }
        #endregion CalculatedHeight

        #region    MicrophoneTimer
        /// <summary>
        /// Used to set upper limit to how long a recording may go on for
        /// </summary>
        public IntervalTimer MicrophoneTimer = null;
        #endregion MicrophoneTimer

        #region    MTickTock
        /// <summary>
        /// The once-a-second heartbeat of this program. Think 'Dark Side of the Moon"
        /// </summary>
        public MTickTock MTickTock { get; } = new MTickTock(1000);
        #endregion MTickTock

        #region    TimerStripsGridlength
        /// <summary>
        /// The height of the area occupied by the timers. I will need max and min somehow. The height
        /// is calculated as so-much-per timer. This is bound to the Xaml Height of the ListBox.
        /// Unfortunately Height is not an integer, but a GridHeight so we manufacture it every
        /// time. You have ignore ant
        /// </summary>
        public GridLength TimerStripsGridlength
        {
#pragma warning disable IDE0025 
            get { return new GridLength(VMTimer.VMTimers.Count * 68, GridUnitType.Pixel); }
#pragma warning restore 
        }
        #endregion TimerStripsGridlength

        #region    TimerStripsHeight
        /// <summary>
        /// The height of the area occupied by the timers. I will need max and min somehow. The height
        /// is calculated as so-much-per timer. This is bound to the Xaml Height of the ListBox.
        /// Unfortunately Height is not an integer, but a GridHeight so we manufacture it every
        /// time. You have ignore ant
        /// </summary>
        public Double TimerStripsHeight
        {
#pragma warning disable IDE0025 
            get { return 68.0 * VMTimer.VMTimers.Count; }
#pragma warning restore 
        }
        #endregion TimerStripsHeight

        #region    This
        /// <summary>
        /// To access the MainPage from anywhere
        /// </summary>
        public static MainPage This { get; private set; }
        #endregion This

        // VMicrophone Microphone is declared in the Xaml. (FYI) 

        #region    VMMainPage
        /// <summary>
        /// Contains useful data about what is on MainPage, such as lists of dependency objects
        /// </summary>
        public VMMainPage VMMainPage { get; private set; }
        #endregion VMMainPage

        #region    VMTimerStripsSorted
        /// <summary>
        /// List of timer strips which is used to display the Timers on the UI
        /// </summary>
        internal List<TimerStrip> VMTimerStripsSorted;
        #endregion VMTimerStripsSorted

        #endregion Properties

        #region    Constructor   +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

        public MainPage()
        {
            ApplicationView.PreferredLaunchViewSize = new Size(520, 600);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;

            /* We need this tracer early because it is used in parts of the following initialisation.
             * 
             * All the Tracing in SeniorMoment is CrossTaskTracing. We keep one in Tracer
             * so it can be shared by any object in the App. That TRacer is also the one used 
             * by the first VMTimer. The reason for this is as follows...
             * 
             * ==   If you want to see the events of a single VMTimer then create a second timer 
             *      All events of THAT VMTimer will be logged sequentially to a separate file
             * ==   If you want see sequence of events throughout the VMTimer and all other 
             *      objects then use the first VMTimer as your test bed. It shares the
             *      same Timer as all the Non-VMTimer related events
             * ==   If you want to see just the non-VMTimer events then create a second VMTimer
             *      and delete the first  VMTimer. From then on you have your heart's desire.
             *      
             * You will probably have to fiddle around with when and where to Start and Stop
             * tracing  */

            Tracer.TraceOptions = TraceOptions.File;

            Tracer.TracerMain = new Tracer
            (
                    name: "Main",
                    id: "M",
                    isUniqueTask: false,
                    allowCrossTaskTracing: true,
                    showInterval: true,
                    showAssembly: false
            );
            ///* We need to do this before InitialiseComponent as InitialiseComponent eventually
            // * calls Sound.GetDefaultSoundFile that uses AssetsFolder */

            //Statics.AssetsFolder = GetAssetsFolderAsync().Result;

            /* Get everything we can on the page */

            InitializeComponent();
            DataContext = this;
            This = this;

            /*
             * Get some data about the DependencyObject tree. VMMainPage holds
             * various lists of dependency objects, trees and so on.
             */
            VMMainPage = new VMMainPage(this);

            /* Tell each VMTimer when to stop and start Tracing and where to put the output.
             * We will be tracing to a file in. Leave this code in comments for debugging purposes.
             * Currently the same commented out code is in MainPage_Lay
             */
            MStatics.TraceBeginsWhenSecondsToZeroIs = 4;
            MStatics.TraceEndsWhenSecondsToZeroIs = -3;

            /*
             * I don't like this. From time to time the following CreateVMTimer is executed
             * before the setting of the DefaultSoundFile. Then we end up
             * with an exception when we try to play the alarm. The Delay seems
             * to stop that. 
             * 
             * Haven't had it for two months. I wonder what I did??
             */
            var ignore2 = Task.Delay(20);
            return;
        }
        #endregion    Constructor

        #region    Binding stuff

        #region    PropertyChanged PropertyChangedEventHandler
        /// <summary>
        /// Part of the method of telling the UI to update itself via Bindings
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion PropertyChanged PropertyChangedEventHandler

        #region    NotifyPropertyChanged
        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        public void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                var args = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, args);
            }
            else
            { }
        }
        #endregion NotifyPropertyChanged 

        #endregion Binding stuff

        #region    GetAssetsFolderAsync
        /// <summary>
        /// An async Task<> method so we can a folder from non-async method
        /// </summary>
        /// <returns></returns>
        async Task<StorageFolder> GetAssetsFolderAsync()
        {
            var installation = Statics.InstallationFolder;
            IStorageItem item = await installation.TryGetItemAsync(@"Assets").AsTask().ConfigureAwait(continueOnCapturedContext: false);
            return (item as StorageFolder);
        }
        #endregion GetAssetsFolderAsync

        #region    Tick
        /// <summary>
        /// The MainPage gets ticked off. I have not decided why yet.
        /// Update - moved some stuff to VMMainPage - just the Tick()et
        /// </summary>
        [System.Diagnostics.DebuggerStepThrough()]
        internal void Tick()
        {
            /* Re-order the timer strips if necessary
            */
            VMMainPage.Tick();
        }
        #endregion Tick

        #region    UpdateTimersOnPage
        /// <summary>
        /// Something has changed in the VMTimers so 
        /// </summary>
        public void UpdateTimersOnPage()
        {
            Statics.RunOnUI(UpdateTimersOnPageOnUI);
        }
        #endregion UpdateTimersOnPage

        #region    UpdateTimersOnPageOnUI
        /// <summary>
        /// Something has changed on (say after a Tick) and we have to refresh the TimerStrip 
        /// </summary>
        public void UpdateTimersOnPageOnUI()
        {
            VMTimerStripsSorted = VMTimer.VMTimers.OrderByDescending(timer => timer.MTimer.SecondsToZero)
                                    .Select(timer => timer.TimerStrip).ToList();
            NotifyPropertyChanged("VMTimerStripsSorted");
            NotifyPropertyChanged("TimerStripsGridlength");
            NotifyPropertyChanged("TimerStripsHeight");
            VMTimer_ListView.SelectedItem = VMTimer.HotVMTimer;
        }
        #endregion UpdateTimersOnPageOnUI

        #region    Events

        #region    MainPage_Loaded
        /// <summary>
        /// Start creating objects
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {

            /* 
             * This tracer will be used for all "Singleton" classes as well as the first VMTimer
             * By "Singleton" I mean classes of which only one will exist but are not static. 
             * They have internal checks to stop a second instantiation. This includes TickTock,
             * MainPage, static CreateAbcd() methods, TaskFunnel
             */
            Tracer.TracerMain.Start();

            /* We need to do this before InitialiseComponent as InitialiseComponent eventually
             * calls Sound.GetDefaultSoundFile that uses AssetsFolder */

            Statics.AssetsFolder = GetAssetsFolderAsync().Result;

            Sound.InitialiseStatic();

            /* Get the microphone up and running
             */
            MainPage.This.Microphone.Initialise();

            /*
            * We must always have at least one VMTimer or things will probably crash and burn.
            * The VMTimers are created in a Paused condition when created  */

            if (VMTimer.VMTimers.Count == 0)
            {
                VMTimer vmTimer = VMTimer.CreateVMTimer(10, useTracerMain: true);

                vmTimer.Resume();
                
                UpdateTimersOnPageOnUI(); // we are on the UI thread already
                // This.VMTimer_ListView.SelectedIndex = 0;

                /* Start the pulse of the program, one beat per second so everything is
                 * synchronized.
                 */
                MTickTock.Start();


                ///* Say Good Morning,  yyy to be removed or replaced */

                //MSound.GoodWhenever(); // To wake me up while I wait for a deploy

                ///* Start the Ticking for the first VMTimer */
                //VMTimer.VMTimers[0].Resume();
            }
        }
        #endregion MainPage_Loaded

        #region    MainPage_Unloaded
        private void MainPage_Unloaded(Object sender, RoutedEventArgs e)
        {
            Tracer.StopAllTracing();
        }
        #endregion MainPage_Unloaded

        #region    MainPage_LayoutUpdated
        /// <summary>
        /// The MainPage is being repainted. We use a first-and-only-time 
        /// backing variable to do stuff late in the day;
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainPage_LayoutUpdated(object sender, object e)
        {
            if (_MainPage_LayoutUpdated_First)
                LayoutUpdatedForFirstTime();
            _MainPage_LayoutUpdated_First = false;
        }
        bool _MainPage_LayoutUpdated_First = true;
        #endregion MainPage_LayoutUpdated

        #region    LayoutUpdatedForFirstTime
        void LayoutUpdatedForFirstTime()
        {
            /*
             * Give the async routines time to finish. This is only needed 
             * because we have test code in MainPage.Loaded. Once that has 
             * gone we could remove this delay . (I believe) yyy
             */
            Task.Delay(10);
        }
        #endregion LayoutUpdatedForFirstTime

        #region    TimerStrip_Clicked      
        /// <summary>
        /// User has clicked on an TimerStrip. The ItemClick event returns the
        /// VMTimerTell the TimerStrip to get ready to be changed
        /// </summary>
        /// <param name="sender">Ignored</param>
        /// <param name="e">Holds pointer to VMTimer</param>
        private void TimerStrip_Clicked(object sender, ItemClickEventArgs e)
        {
            TimerStrip timerStrip = e.ClickedItem as TimerStrip;
            // timerStrip.VMTimer.VMTimerClicked();
        }
        #endregion TimerStrip_Clicked

        #region    TimerControlButton_Click
        /// <summary>
        /// Timer button clicked which means add a new timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimerControlButton_Click(object sender, RoutedEventArgs e)
        {
            VMTimer.CreateVMTimer(secondsToZero: 40);
            UpdateTimersOnPage();
            //ReorderTimerList();
        }
        #endregion TimerControlButton_Click

        #endregion Events
    }
}
