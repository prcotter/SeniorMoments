using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace SeniorMoment.Services
{
    public class DependencyObjectTree : Branch<DependencyObject>
    {
        #region    Constructor (DependencyObject dependencyObject, DependencyObjectTree parent) : base()
        /// <summary>
        /// Create a new Branch{DependancyObject} which is a child of either Root{DependencyObject}
        /// or a Branch{DependencyObject}
        /// </summary>
        /// <param name="dependencyObject"></param>
        /// <param name="parent"></param>
        private DependencyObjectTree(DependencyObject dependencyObject, DependencyObjectTree parent) : base()
        {
            Class = dependencyObject.GetType().Name;
        }
        #endregion Constructor(DependencyObject dependencyObject, DependencyObjectTree parent) : base()

        #region    CreateDependencyObjectTree 
        /// <summary>
        /// This is 'user code' which is not enforced. How the tree is constructed in entirely
        /// up the user...
        /// <para/>Create a tree of dependants and their dependants etc and chain it
        /// </summary>
        /// <param name="parentDependencyObject"></param>
        /// <param name="parentTree"></param>
        /// <param name="types"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        public static DependencyObjectTree CreateDependencyObjectTree(
                        DependencyObject dependencyObject,
                        DependencyObjectTree parent = null,
                        List<Type> types = null)
        {

            DependencyObjectTree rootTree = new DependencyObjectTree(dependencyObject, parent: null);
            int kidCount = VisualTreeHelper.GetChildrenCount(dependencyObject);
            var children = dependencyObject.Children();
            if (children.Count == 0)
                return null;
            var parentTree = rootTree;
            foreach (var childDependencyObject in children)
            {
                if (types != null && !types.Contains(childDependencyObject.GetType()))
                    parentTree = (DependencyObjectTree)rootTree.Parent;

                var branch = CreateDependencyObjectTree(childDependencyObject, parentTree, types);
                if (branch != null)
                {
                    branch.Parent = parentTree;
                    string name = branch.Name;
                    parentTree.AddBranch(branch);
                }
            }
            return rootTree;
        }
        #endregion CreateDependencyObjectTree
    }
}
