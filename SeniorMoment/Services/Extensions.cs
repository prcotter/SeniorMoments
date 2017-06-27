
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using System.Reflection;
using Windows.Media;

namespace SeniorMoment.Services
{
    /// <summary>
    /// A class to hold a list of extensions to various classes
    /// </summary>
    public static class Extensions
    {
        #region    Control.TagControl
        /// <summary>
        /// Replace Control.Tag (which is a string) with a TagControl that changes the way the
        /// Control is ...er... controlled.
        /// </summary>
        /// <param name="control"></param>
        /// <returns></returns>
        public static TagControl TagControl(this Control control)
        {
            if (control.Tag == null || !(control.Tag is TagControl))
                return null;
            return (TagControl)control.Tag;
        }
        #endregion Control.TagControl

        #region    DependencyObject { Children, Descendants, GetDescendants, NameOrType, Ancestor }

        #region    DependencyObject.Children
        /// <summary>
        /// Returns the children of the dependency object. If you need a specific selection then 
        /// consider DependencyObjects.Descendants
        /// </summary>
        /// <param name="DependencyObject"></param>
        /// <returns>a list of the children</returns>
        public static List<DependencyObject> Children(this DependencyObject dependencyObject, List<Type> types = null)
        {
            if (dependencyObject == null)
                throw new ArgumentNullException("dependencyObject");
            int kidCount = VisualTreeHelper.GetChildrenCount(dependencyObject);
            List<DependencyObject> list = new List<DependencyObject>();
            if (kidCount == 0)
                return list;
            for (int i = 0; i < kidCount; i++)
            {
                DependencyObject childDependencyObject = VisualTreeHelper.GetChild(dependencyObject, i);
                if (types == null || types.Contains(childDependencyObject.GetType()))
                {
                    list.Add(childDependencyObject);
                }
            }
            return list;
        }
        #endregion DependencyObject.Children

        #region    DependencyObject.Descendants 
        public static List<DependencyObject> Descendants(this DependencyObject dependencyObject, Func<DependencyObject, bool> predicate = null, int generations = 100)
        {
            if (dependencyObject == null)
                throw new ArgumentNullException("dependencyObject");

            List<DependencyObject> predicatedList = new List<DependencyObject>();
            GetDescendants(dependencyObject, predicate, predicatedList, generations);
            if (predicatedList == null)
                throw new Exception("The depth of descendants exceeds the parameter 'generations' or there is a cirular dependancy");

            return predicatedList;
        }
        #endregion DependencyObject.Descendants 

        #region    DependencyObject.GetDescendants
        /// <summary>
        /// Get a list of decendants down to the depth specified and satifying the predicate
        /// </summary>
        /// <param name="parent">The object from which we get the decendants. This excludes the object itself</param>
        /// <param name="predicate">The filter to select which DependencyObjects are needed</param>
        /// <param name="predicatedList">A list of the descendants that match the predicate</param>
        /// <param name="generations">The number of generations to recurse through. '1' means the children,
        /// '2' the children and grandchildren etc</param>
        static void GetDescendants(DependencyObject parent, Func<DependencyObject, bool> predicate, List<DependencyObject> predicatedList, int generations)
        {
            if (predicatedList == null)
                throw new ArgumentNullException("list");
            if (generations-- < 0)
            {
                predicatedList = null;
                return;
            }
            /*
             * There are two lists. The childList is all the children of this element. This is used to recurse through
             * all the descendants. The other is the predicated list which are those elements that have 
             * passed the predicate selection.
             */

            List<DependencyObject> childList = new List<DependencyObject>();
            int kidCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < kidCount; i++)
                childList.Add(VisualTreeHelper.GetChild(parent, i));
            if (predicate == null)
                predicatedList.AddRange(childList);
            else
                predicatedList.AddRange(childList.Where(predicate).ToList());
            if (generations > 0)
                childList.ForEach(child => GetDescendants(child, predicate, predicatedList, generations));
        }
        #endregion DependencyObject.GetDescendants

        #region    DependencyObject.NameOrType
        public static string NameOrType(this DependencyObject dependencyObject)
        {
            FrameworkElement element = dependencyObject as FrameworkElement;
            if (element == null || string.IsNullOrWhiteSpace(element.Name))
                return dependencyObject.GetType().ToString();
            return element.Name;
        }
        #endregion DependencyObject.NameOrType

        #region    DependencyObject.Ancestor
        /// <summary>
        /// Return the closest parent that matches the filter (if supplied) If none if found returns null
        /// </summary>
        /// <param name="dependencyObject">this dependencyObject</param>
        /// <param name="predicate">filter (usually by type or Name)</param>
        /// <returns></returns>
        public static DependencyObject Ancestor(this DependencyObject dependencyObject, Func<DependencyObject, bool> predicate )
        {
            if (predicate == null)
                throw new LogicException("no predicate. Use DependencyObject.Parent");
            if (dependencyObject == null)
                throw new ArgumentNullException("dependencyObject");
            var parent = VisualTreeHelper.GetParent(dependencyObject);

            if (parent == null)
                return null;
            
            if (predicate(dependencyObject))
                    return dependencyObject;
            
            return parent.Ancestor( predicate); // recurse through ancestors to root looking for it
        }

        #endregion DependencyObject.Ancestor 

        #endregion DependencyObject

        #region    IEnumerable ForEach, IndexOf, IndexOfAny, FirstOrAlternative

        #region    ForEach
        /// <summary>
        /// For each element of arbitrary type &lt;T&gt; in a IEnumerable perform a System.Action on it  
        /// 
        /// Iterate through an IEnumerable collection and perform an Action on each.
        /// An Action is like a Func except it does not return anything. Therefore
        /// it can only be the last 'thing' as in:
        /// <para/>
        /// MyIEnumerable.Where(a=>a.Real==true).Select(b=>b.Reason).ForEach(c=>{Debug.WriteLine(c)});
        /// </summary>
        /// <typeparam name="T">The type of object in the IEnumerable</typeparam>
        /// <param name="source">The IEnumerable collection</param>
        /// <param name="action">The code which does not return any value</param>
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (action == null) throw new ArgumentNullException("action");

            foreach (T item in source)
            {
                action(item);
            }
        }
        #endregion ForEach

        #region    IEnumerable<T>.IndexOf / IndexOfAny
        /// <summary>
        /// Find zero-based position in IEnumerable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list">an IEnumerable we can iterate through</param>
        /// <param name="element">The member of the IEnumerable we are looking for</param>
        /// <returns>'n'. The zero-based index of the element in the IEnumerable. It is the same number
        /// that MyIenumerables.ElementAt(n) would return <paramref name="element"/> </returns>
        public static int IndexOf<T>(this IEnumerable<T> list, T element)
        {
            return list.Select((x, index) => EqualityComparer<T>.Default.Equals(element, x)
                                             ? index
                                             : -1)
                       .FirstOrAlternative(x => x != -1, -1);
        }
        public static int IndexOfAny<T>(this IEnumerable<T> list, Func<T, bool> predicate)
        {
            IEnumerable<T> found = list.Where(predicate);
            if (found.Count() == 0)
                return -1;
            int index = -1;
            foreach (T item in list)
            {
                index++;
                if (predicate(item))
                    return index;
            }
            throw new LogicException("Should exist");
        }
        #endregion IndexOf


        #region    FirstOrAlternative<T>(T alternative)
        /// <summary>
        /// Returns the first element or if not available returns the passed default element
        /// </summary>
        /// <typeparam name="T">The type of the IeNumerable&lt;T></typeparam>
        /// <param name="source">The IEnumerable from which to select the first element</param>
        /// <param name="alternate">If there is no first element this is returned</param>
        /// <returns>The first element, unless it doesn't exist in which case it used the passed default (alternate)</returns>
        public static T FirstOrAlternative<T>(this IEnumerable<T> source, T alternate)
        {
            return source.DefaultIfEmpty(alternate)
                         .First();
        }
        #endregion FirstOr<T>

        #region    FirstOrAlternative<T>(this IEnumerable<T> source, Func<T, bool> predicate, T alternate)
        /// <summary>
        /// Find the first element that matches a condition, and if none exists provide a default version
        /// </summary>
        /// <typeparam name="T">The type of the IEnumerable&lt;T></typeparam>
        /// <param name="source">The IEnumerable from which to select the first element which matches the condition</param>
        /// <param name="predicate">The condition which is to be matched</param>
        /// <param name="alternate">If no element matches the condition this is returned as the default</param>
        /// <returns>The first element that matches the predicate or, if none matches, the alternate</returns>
        public static T FirstOrAlternative<T>(this IEnumerable<T> source, Func<T, bool> predicate, T alternate)
        {
            return source.Where(predicate)
                         .FirstOrAlternative(alternate);
        }
        #endregion FirstOrAlternative<T>(this IEnumerable<T> source, Func<T, bool> predicate, T alternate)

        #endregion IEnumerable

        #region    CheckBox.IsActive
        /// <summary>
        /// Returns true if the CheckBox is visible and checked
        /// </summary>
        /// <param name="checkBox">a CheckBox on the form</param>
        /// <returns>true if the CheckBox is visible and checked, else returns false</returns>
        static public bool IsActive(this CheckBox checkBox)
        {
            if (checkBox == null)
                throw new ArgumentNullException("checkBox");
            return checkBox.Visibility == Visibility.Visible && checkBox.IsChecked == true;
        }

        #endregion CheckBox.IsActive

        #region    Clone<T>
        /// <summary>
        /// Clones a control
        /// </summary>
        /// <typeparam name="T">Type of Control</typeparam>
        /// <param name="controlToClone">Control to be cloned</param>
        /// <returns></returns>
        public static T Clone<T>(this T controlToClone)
            where T : Control
        {
            PropertyInfo[] controlProperties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            T instance = Activator.CreateInstance<T>();

            foreach (PropertyInfo propInfo in controlProperties)
            {
                if (propInfo.CanWrite)
                {
                    if (propInfo.Name != "WindowTarget")
                        propInfo.SetValue(instance, propInfo.GetValue(controlToClone, null), null);
                }
            }
            return instance;
        }
        #endregion Clone<T>

        #region    string.Left

        /// <summary>
        /// Returns the left of the string for 'length' characters
        /// </summary>
        /// <param name="input">input string</param>
        /// <param name="length">number of characters to return</param>
        /// <returns>the leftmost <paramref name="length"/>characters</returns>
        /// <exception cref="ArgumentNullException">input is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">string is shorter than length</exception>
        static public string Left(this string input, int length)
        {
            if (input == null)
                throw new ArgumentNullException("input");
            if (input.Length < length)
                throw new ArgumentOutOfRangeException("length", length, "input is shorter than length");
            return (input.Substring(0, length));
        }
        #endregion string.Left

        #region    string.Right

        /// <summary>
        /// Returns the Right of the string for 'length' characters
        /// </summary>
        /// <param name="input">input string</param>
        /// <param name="length">number of characters to return. zero is not an error</param>
        /// <returns>the rightmost <paramref name="length"/>characters</returns>
        /// <exception cref="ArgumentNullException">input is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">string is shorter than length</exception>
        static public string Right(this string input, int length)
        {
            if (input == null)
                throw new ArgumentNullException("input");
            if (input.Length < length)
                throw new ArgumentOutOfRangeException("length", length, "input is shorter than length");
            return (input.Substring(input.Length - length, length));
        }
        #endregion string.Right

        #region    RaiseOnUIThread
        /////<summary>
        /////Extension method which marshals events back onto the main thread. We do to the delegate and 
        /////get a list of everything that invoked it. There should only be one and it came From RegexChecker.
        ///// We then invoke the delegate on the same thread that created RegexChecker.
        /////Borrowed from http://stackoverflow.com/questions/1698889/raise-events-in-net-on-the-main-ui-thread
        /////</summary>
        /////<param name="multicast">the delegate that will be raised as an event on some unknown thread.</param>
        /////<param name="sender">origin of the event</param>
        /////<param name="args">arguments passed to the event</param>
        //public static void RaiseOnUIThread(this MulticastDelegate multicast, object sender, EventArgs args)
        //{
        //    Tracer.Trace($"Entry", useFirstTracer: true);
        //    var invocationList = multicast.GetInvocationList();
        //    foreach (Delegate del in invocationList)
        //    {
        //        // Try for WPF first
        //        DispatcherObject dispatcherTarget = del.Target as DispatcherObject;
        //        if (dispatcherTarget != null && !dispatcherTarget.Dispatcher.CheckAccess())
        //        {
        //            // WPF target which requires marshaling
        //            dispatcherTarget.Dispatcher.BeginInvoke(del, sender, args);
        //        }
        //        else
        //        {
        //            // Maybe it's WinForms?
        //            ISynchronizeInvoke syncTarget = del.Target as ISynchronizeInvoke;
        //            if (syncTarget != null && syncTarget.InvokeRequired)
        //            {
        //                // WinForms target which requires marshaling
        //                syncTarget.BeginInvoke(del, new object[] { sender, args });
        //            }
        //            else
        //            {
        //                // Just do it.
        //                del.DynamicInvoke(sender, args);
        //            }
        //        }
        //        Tracer.Trace($"Exit", useFirstTracer: true);
        //    }
        //}
        #endregion RaiseOnUIThread

        #region    RaiseOnUIThread<T>
        ///// <summary>
        ///// Extension method which marshals actions back onto the main thread
        ///// Borrowed from http://stackoverflow.com/questions/1698889/raise-events-in-net-on-the-main-ui-thread
        ///// </summary>
        ///// <typeparam name="T">the type of the IEnumerable</typeparam>
        ///// <param name="action">the action to be marshaled back to the UI thread</param>
        ///// <param name="args">Arguments to be based to the action</param>
        ///// <example>None - because I haven't used it yet</example>
        //public static void RaiseOnUIThread<T>(this Action<T> action, T args)
        //{
        //    // Try for WPF first
        //    DispatcherObject dispatcherTarget = action.Target as DispatcherObject;
        //    if (dispatcherTarget != null && !dispatcherTarget.Dispatcher.CheckAccess())
        //    {
        //        // WPF target which requires marshaling
        //        dispatcherTarget.Dispatcher.BeginInvoke(action, args);
        //    }
        //    else
        //    {
        //        // Maybe its WinForms?
        //        ISynchronizeInvoke syncTarget = action.Target as ISynchronizeInvoke;
        //        if (syncTarget != null && syncTarget.InvokeRequired)
        //        {
        //            // WinForms target which requires marshaling
        //            syncTarget.BeginInvoke(action, new object[] { args });
        //        }
        //        else
        //        {
        //            // Just do it.
        //            action.DynamicInvoke(args);
        //        }
        //    }
        //}
        #endregion RaiseOnUIThread<T>
    }
}


