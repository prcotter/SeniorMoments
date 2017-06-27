using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using SeniorMoment.Services;
using SeniorMoment.Views;
using SeniorMoment.Models;

namespace SeniorMoment.ViewModels
{
    /// <summary>
    /// The controlling ViewModel for the main page
    /// </summary>
    public class VMMainPage
    {
        /* I used these at first. I left them here as an example of how to use DependencyObjectTree
         * and the DependencyObject.Descendants Extension in Services.Extensions */

        //#region    Dependants
        ///// <summary>
        ///// A list of all the dependency objects
        ///// </summary>
        //public List<DependencyObject> Dependants { get; private set; }
        //#endregion Dependants

        //#region    DependantControls
        ///// <summary>
        ///// A list of all the dependency objects
        ///// </summary>
        //public List<Control> DependantControls { get; private set; }
        //#endregion DependantControls

        //#region    DependantFrameworkElements
        ///// <summary>
        ///// A list of all the dependency objects
        ///// </summary>
        //public List<FrameworkElement> DependantFrameWorkElements { get; private set; }
        //#endregion DependantFrameworkElements

        //#region    DependantsWithName
        ///// <summary>
        ///// A list of all the dependency objects
        ///// </summary>
        //public List<FrameworkElement> DependantsWithName { get; private set; }
        //#endregion DependantsWithName

        //#region    DependantTree
        ///// <summary>
        ///// A list of all the dependency objects
        ///// </summary>
        //public Branch<DependencyObject> DependantTree { get; private set; }
        //#endregion DependantWithNames

        #region    MainPage
        /// <summary>
        /// The View MainPage has this VMMainPage as a helper rather than a conduit to a 
        /// MMainPage (which does not exist)
        /// </summary>
        MainPage MainPage;
        #endregion MainPage

        #region    Tick
        /// <summary>
        /// Once a second see if we have anything to do concerning the MainPage
        /// </summary>
        public void Tick()
        {
            SortTimerStrips();
        }
        #endregion Tick

        #region    This
        /// <summary>
        /// Pointer to VMMainPage as there is only one of these
        /// </summary>
        public static VMMainPage This { get; private set; }
        #endregion This

        #region    Constructor 
        /// <summary>
        /// Create the master ViewModel. Get a few List{x} thingies just-in-case
        /// </summary>
        /// <param name="mainPage"></param>
        public VMMainPage(MainPage page)
        {
            MainPage = page;
            This = this;
            //DependantTree = DependencyObjectTree.CreateDependencyObjectTree(MainPage.This);
            // Dependants = MainPage.This.Descendants();
            // DependantFrameWorkElements = Dependants.Where(fwe => (fwe as FrameworkElement) != null).Cast<FrameworkElement>().ToList();
            // xxx DependantControls = DependantFrameWorkElements.Where(control => (control as Control) != null).Cast<Control>().ToList();
            // xxx DependantsWithName = DependantFrameWorkElements.Where(fwe => !string.IsNullOrWhiteSpace(fwe.Name)).ToList();
        }
        #endregion Constructor 

        #region    SortTimerStrips
        /// <summary>
        /// They timer strips may have to be re-ordered due to one of the following..
        /// <para/> 
        /// <para/> A timer has been added
        /// <para/> A timer has been Reloaded
        /// <para/> A timer has been Deleted
        /// <para/> A Paused timer is suddenly 'younger' as another timer overtakes
        /// it in age (SecondsToZero)
        /// <para/> 
        /// We first decide if there is need to move them ... if so we do it.
        /// </summary>
        internal void SortTimerStrips()
        {
            /* Go through the TimerStrips and see if any are misplaced. If so they needSorting.
             * 
            */
            bool needSorting = false;

            var strips = MainPage.VMTimerStripsSorted;
            
            TimerStrip stripNow = null, stripNext = null;
           var stripSearch = strips.GetEnumerator();
            if (!stripSearch.MoveNext())
                Statics.InternalProblem("no TimerStrips");
            
            /* Used GetEnumerator as it conveniently has Current and (Move)Next TimerStrip at the same time. 
             * And I wanted to try it */

            while (true)
            {
                stripNow = stripSearch.Current;
                if (!stripSearch.MoveNext())
                    break;
                stripNext = stripSearch.Current;
                if (stripNow.VMTimer.MTimer.SecondsToZero < stripNext.VMTimer.MTimer.SecondsToZero)
                {
                    needSorting = true;
                    break;
                }
            }
            if (needSorting)
                MainPage.UpdateTimersOnPage();
        }
        #endregion SortTimerStrips
    }
}
