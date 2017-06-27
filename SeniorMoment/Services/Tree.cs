using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SeniorMoment.Services
{
    /* COMMENTS
     * In Summaries etc you will see things like Root{T} which means Root<T> cus it's a PITA
     * putting angles brackets in.
     * The Root and Branch classes class supports a generic tree collection. There is one Root<T>
     * and zero or more Branch<T> hanging off it. 
     * Let us suppose you want to create a new class inheriting from Branch<T>. You also have to
     * create a Root<T> as well. 
     *  First a concrete example based on Windows.UI.Xaml.Controls.DependencyObject:
     * 
    public class DependencyObjectRoot : Root<DependencyObject> 
    {
    // The interface IRoot<T> enforces the CreateRoot method
        public static DependencyObjectRoot CreateRoot(DependencyObject content, string name, 
                    bool allowDuplicateNames = true, bool allowDuplicateContent = true)
        {
            return (DependencyObjectRoot) new Root<DependencyObject>(content, name,allowDuplicateNames: true, allowDuplicateContent true);
        }

        public DependencyObjectRoot(DependencyObject content, string name = null, bool allowDuplicateNames = true, bool allowDuplicateContent = true)
            : base(content, name, allowDuplicateNames, allowDuplicateContent)
        {
        }
    }
        
    */

    class RootExtras<T> where T : class
    {
        #region    AllowDuplicateNames
        /// <summary>
        /// if true then the Root and all descendant branches must have a different names
        /// </summary>
        public bool AllowDuplicateNames;
        #endregion AllowDuplicateNames

        #region    AllowDuplicateContent
        /// <summary>
        /// If true then the Root and all descendant branches must have a different content.
        /// The content equality is based on first.Equals(second)
        /// </summary>
        public bool AllowDuplicateContent;
        #endregion AllowDuplicateContent

        #region    BranchesByUniqueKey
        /// <summary>
        /// Whenever we add a new branch we keep a quick reference to it
        /// </summary>
        public Dictionary<int, Branch<T>> BranchesByUniqueKey { get; } = new Dictionary<int, Branch<T>>();
        #endregion BranchesByUniqueKey

        #region    BranchesByName
        /// <summary>
        /// Whenever we add a new branch we keep a quick reference to it. 
        /// Cannot use a dictionary as we may have duplicate names
        /// </summary>
        //public Dictionary<string, Branch<T>> NamedBranches = new Dictionary<string, Branch<T>>();
        public List<KeyValuePair<string, Branch<T>>> BranchesByName = new List<KeyValuePair<string, Branch<T>>>();
        #endregion BranchesByName

        #region    RootExtras constructor
        /// <summary>
        /// The root has to have information that other branches do not
        /// </summary>
        /// <param name="allowDuplicateNames"></param>
        /// <param name="allowDuplicateContent"></param>
        public RootExtras(bool allowDuplicateNames, bool allowDuplicateContent)
        {
            AllowDuplicateNames = allowDuplicateNames;
            AllowDuplicateContent = allowDuplicateContent;
        }
        #endregion RootExtras
    }

    #region    IContentEquals<TContent>
    interface IContentEquals<TContent>
    {
        bool ContentEquals(TContent Content);
    }
    #endregion IContentEquals<TContent>

    #region    class Branch<T> 
    /// <summary>
    /// A hierarchical tree structure, with one root object having 0 or more 
    /// branches each of which can have child branches to any depth. A branch
    /// is actually a Branch object but the naming convention of Root and Branch in the
    /// method names make it easier to read.
    /// In the method calls the &lt;T&gt; is called 'content'
    /// </summary>
    /// <typeparam name="T">any class</typeparam>
    // yyy public class Branch<T> : IContentEquals<T> where T : class
    public class Branch<T> where T : class
    {
        #region    Variables and Accessor variables

        public string Class;

        #region    AllowDuplicateContent
                /// <summary>
                /// if true then a branch can only exist in one place in the tree. If false
                /// then the branch can exist in multiple places, with the proviso that two 
                /// branches cannot have the same parent. ie. All child branches of a parent
                /// branch must be unique;
                /// </summary>
        public bool AllowDuplicateContent
        { get => Root.RootExtras.AllowDuplicateContent; }
        #endregion _AllowDuplicateContent

        #region    AllowDuplicateNames
        /// <summary>
        /// If false each member of the tree must have a unique name
        /// </summary>
        public bool AllowDuplicateNames
        { get => Root.RootExtras.AllowDuplicateNames; }
        //{
        //    get { return Root.RootExtras.AllowDuplicateNames; }
        //    protected set { AllowDuplicateNames = value; }
        //}
        #endregion AllowDuplicateNames

        #region    Branches
        /// <summary>
        /// A list of all the branches (which are themselves Branch) which
        /// have this tree as a parent
        /// </summary>
        protected List<Branch<T>> Branches = new List<Branch<T>>();
        #endregion Branches

        #region     BranchesByUniqueKey
        /// <summary>
        /// Each branch has a unique key and using the key we can locate the Branch
        /// </summary>
        public Dictionary<int, Branch<T>> BranchesByUniqueKey { get { return RootExtras.BranchesByUniqueKey; } }
        #endregion  BranchesByUniqueKey

        #region    BranchesByName
        /// <summary>
        /// A list of names which correspond to Branches. This cannot be a Dictionary as Names are not unique 
        /// </summary>
        public List<KeyValuePair<string, Branch<T>>> BranchesByName { get { return RootExtras.BranchesByName; } }
        #endregion BranchesByName

        #region    Content
        /// <summary>
        /// When a branch is created the Content is null unless conent is added with SetContent
        /// of AddBranchWithContent(content) is called;
        /// </summary>
        public T Content { get; protected set; } = null;
        #endregion Content

        #region    Count
        /// <summary>
        /// The number of subtrees attached to this tree
        /// </summary>
        public int Count { get { return Branches.Count; } }
        #endregion Count

        #region    CRLF
        public static string CRLF = Environment.NewLine;
        public static string CRLF2 = CRLF + CRLF;
        #endregion CRLF

        #region    Depth
        /// <summary> Depth
        /// The root has depth 0, its children are depth 1, grandchilren depth 2 etc
        /// </summary>
        public int Depth
        {
            get
            {
                if (Parent == null)
                    _Depth = 0;
                else
                    _Depth = Parent.Depth + 1; // recurses thru Depth till we find a real Depth
                return _Depth;
            }
        }

        protected int _Depth = -1;
        #endregion Depth 

        #region    Index
        /// <summary>
        ///
        /// </summary>
        public int Index
        {
            get
            {
                if (Parent == null)
                    return _Index = 0;
                _Index = Parent.Branches.IndexOfAny(branch => branch._Name == this._Name);
                return _Index;
            }
        }
        int _Index = 0;
        #endregion Index

        #region IsRoot
        /// <summary>
        /// return true is this tree is the root
        /// </summary>
        public bool IsRoot { get { return Parent == null; } }
        #endregion IsRoot

        #region    Name
        /// <summary>
        /// set or get the Name of this branch. There are a number
        /// of sources for Name. In priority they are:
        /// <para/>* The 'user' has specified Name="Some Name"
        /// <para/>* The 'Content' has a 'Name' or 'name' property. (eg FrameworkElement or Control)  
        /// <para/>* If it is the root (Depth=0) then it is called 'Root'
        /// <para/>* Finally the default name is 'Depth X Index Y'
        /// </summary>
        public string Name
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_Name))
                    return _Name;
                if (!userSuppliedName || _Name == null) // First choice - user supplied the name
                {
                    _Name = string.Empty;
                    if (Content != null)
                    {
                        System.Type thisType = Content.GetType();  // second choice - the Content has a Name property
                        var properties = thisType.GetFields(BindingFlags.Public);
                        var nameProperty = thisType.GetField("Operation");

                        if (nameProperty == null)
                        {
                            System.Type contentType = Content.GetType();  // second choice - the Content has a Name property
                            properties = contentType.GetFields(BindingFlags.Public);
                            nameProperty = contentType.GetField("Operation");
                        }
                        if (nameProperty != null)
                            _Name = nameProperty.GetValue(Content) as string;
                    }

                    if (string.IsNullOrEmpty(_Name))
                        if (Depth == 0)
                            _Name = "Root"; // third choice, it is the root
                        else
                            _Name = "Depth " + Depth + " Index " + Index; // Finally "Depth 2 Index 4" (or whatever)
                }
                return _Name;
            }
            set
            {
                _Name = value;
                if (string.IsNullOrEmpty(_Name))
                    _Name = null;
                else
                    userSuppliedName = true;
            }
        }
        string _Name = null;
        #endregion Name

        #region    NextUniqueKey
        /// <summary>
        /// Return an incremental unique key
        /// </summary>
        static public int NextUniqueKey
        {
            get
            {
                _NextUniqueKey++;
                return _NextUniqueKey;
            }
        }
        static int _NextUniqueKey = -1;
        #endregion NextUniqueKey

        #region Parent
        /// <summary>
        /// Branch to which this tree was added. If it is null then this Branch is
        /// the root;
        /// </summary>
        public Branch<T> Parent { get; protected set; } = null;
        #endregion Parent

        #region Root
        /// <summary>
        /// This returns the great grandaddy of the tree from which all the children
        /// are branches or branches of branches etc
        /// </summary>
        public Branch<T> Root;

        #endregion Root

        #region    RootExtras
        /// <summary>
        /// The root of a Tree keeps various flags and lists that can be accessed by its branches.
        /// For all descendants this field must be null
        /// </summary>
        RootExtras<T> RootExtras
        {
            get { return Root._RootExtras; }
            set
            {
                if (Parent != null)
                    throw new LogicException("setting RootExtras with Parent not null");
                _RootExtras = null;
            }
        }
        RootExtras<T> _RootExtras;
        #endregion RootExtras

        #region    UniqueKey
        /// <summary>
        /// Every branch (including the root) has a unique key obtained from the root,
        /// <para/> If you write a derived class you could  reset the value. DON'T
        /// </summary>
        public int UniqueKey { get; protected set; }
        #endregion UniqueKey

        #region    UserData
        /// <summary>
        /// This field is free for you to use as and how you wish. It is like the Tag field on a control.
        /// </summary>
        public object UserData { get; set; }
        #endregion UserData

        #region    userSuppliedName
        /// <summary>
        /// If true then Name was set by the user so we leave it alone and not put any default values
        /// in there.
        /// </summary>
        protected bool userSuppliedName;
        #endregion userSuppliedName

        #endregion Variables and Accessor variables

        #region    Constructor (string Name, T Content)
        /// <summary>
        /// Create the base.
        /// </summary>
        public Branch(T content = null, string name = null, bool allowDuplicateNames = true, bool allowDuplicateContent = true)
        {
            if (string.IsNullOrEmpty(name))
                Name = "Root";
            Root = this;
            Parent = null;
            _Depth = 0;
            _Index = 0;
            _RootExtras = new RootExtras<T>(allowDuplicateNames, allowDuplicateContent);
            //RootExtras.AllowDuplicateNames = allowDuplicateNames;
            // xxx RootExtras.AllowDuplicateContent = allowDuplicateContent;
            RootExtras.BranchesByName.Add(new KeyValuePair<string, Branch<T>>(name, this));
            UniqueKey = NextUniqueKey;
            RootExtras.BranchesByUniqueKey.Add(UniqueKey, this);
            Content = content;
            Parent = null;
            Name = name;
            UniqueKey = NextUniqueKey;
            Initialise();
        }
        #endregion Constructor (string Name, T Content)

        #region  Initialise
        ///<summary>
        /// Initialise after creation or relocation. This sets the depth, index. By default
        /// it also recurses through the descendant branches etc.
        ///</summary>
        private void Initialise(bool recurse = true)
        {
            if (Parent == null)
                return;  // Depth and Index set in Root<T> Constructor
            _Index = Parent.Branches.IndexOf(this);
            if (!userSuppliedName)
                _Name = null;
            string name = Name; // forces re-evalution of the name
            if (recurse)
                Branches.ForEach(branch => branch.Initialise());
        }
        #endregion Initialise

        #region    AddBranchWithContent
        /// <summary>
        /// Create a branch from the content add it as a child branch to the parent branch/tree
        /// </summary>
        /// <param name="content"
        public Branch<T> AddBranchWithContent(T content, string name = null)
        {
            /*
             * It is a requirement that all the sibling branches of the root or a branch are unique.
             * It can be enforced that all branches are unique
             */

            if (content == null)
                throw new ArgumentNullException("content");

            Branch<T> offshoot = new Branch<T>(content: content, name: name);
            offshoot.Content = content;
            offshoot.Name = name;
            offshoot.Parent = this;

            this.AddBranch(offshoot);
            return offshoot;
        }
        #endregion AddBranchWithContent

        #region SetContent
        /// <summary> SetContent
        /// Set (or replace) the content of a branch
        /// </summary>
        public void SetContent(T content)
        {
            Content = content;
            if (!userSuppliedName)
                _Name = null;
        }
        #endregion SetContent

        #region    AddBranch
        /// <summary> 
        /// Add a new branch to the root or a branch.
        /// </summary>
        public Branch<T> AddBranch(Branch<T> branch)
        {
            if (Root.RootExtras.BranchesByUniqueKey.ContainsKey(branch.UniqueKey))
                throw new ArgumentException("You cannot add the same branch twice: " + branch.Name);
            if (!Root.AllowDuplicateContent)
                if (branch.Content != null)
                {
                    var foundContentBranch = FindByContent(branch.Content);
                    if (foundContentBranch != null)
                        throw new ArgumentException(string.Format("Duplicate Content between {0} and {1} ", branch.Name, foundContentBranch));
                }
            if (!Root.AllowDuplicateNames)
                if (Root.RootExtras.BranchesByName.Any(ky => ky.Key == branch.Name))
                    throw new InvalidOperationException("Operation already exists: " + branch.Name);

            branch.Root = Root;
            branch.Parent = this;
            branch.UniqueKey = NextUniqueKey;
            Branches.Add(branch);
            RootExtras.BranchesByUniqueKey.Add(branch.UniqueKey, branch);
            RootExtras.BranchesByName.Add(new KeyValuePair<string, Branch<T>>(branch.Name, this));
            branch.Initialise();
            return branch;
        }
        #endregion AddBranch

        #region    Contains
        /// <summary>
        /// Does this node the content T. 
        ///</summary>
        ///<param name="content">The content we are searching for</param>
        ///<param name="recurse">if true recursively search all sub-trees</param>
        ///<returns>true if the content is found</returns>
        public bool Contains(T content)
        {
            if (Content == content)
                return true;
            return null != FindByContent(content);
        }
        #endregion Contains

        #region    ContentEquals(T content)
        /// <summary>
        /// Checks if two contents are equal. This can be over-ridden just like Equals()
        /// </summary>
        public bool ContentEquals(T content)
        { return Content.Equals(content); }
        public interface IContentEquals<TContent> { }
        #endregion ContentEquals(T content)

        #region    FindBranch(Tree<T> branch)
        /// <summary> Find(T content)
        /// Locate the content in this tree, and if recurse is true try to
        /// find the content in all descendant branches. Note that 
        /// <para/>    tree.content1.Equals(tree.content2)
        /// <para/> rather than content1 == content2
        /// used rather than
        /// </summary>
        public Branch<T> FindBranch(Branch<T> branch, bool recurse = true)
        {
            if (Root.UniqueKey == branch.UniqueKey)
                return this;
            return FindBranchFrom(branch, recurse);
        }
        #endregion FindByName(string name)

        #region    FindBranchFrom(Tree<T> branch, bool recurse = true)
        /// <summary>
        /// Locate the the branch within the child  or descendant branches.
        /// <para/>    tree.content1.Equals(tree.content2)
        /// <para/> rather than content1 == content2
        /// used rather than
        /// </summary>
        Branch<T> FindBranchFrom(Branch<T> branch, bool recurse = true)
        {
            if (branch == null)
                throw new ArgumentNullException("branch");
            if (this.UniqueKey == branch.UniqueKey)
                return this;
            foreach (var offshoot in Branches)
            {
                if (offshoot.Name == branch.Name)
                    return offshoot;
                if (recurse)
                    return offshoot.FindBranchFrom(branch, recurse);
            }
            return null;
        }
        #endregion FindBranchFrom(Tree<T> branch, bool recurse = true)

        #region    FindByContent
        /// <summary>
        /// 
        /// </summary>
        public Branch<T> FindByContent(T content)
        {
            if (Content != null || content != null)
                if (ContentEquals(content))
                    return this;
            return Root.FindByContentDescendantOf(content);
        }
        #endregion FindByContent

        #region    FindByContentDescendantOf( T content)
        /// <summary> 
        /// Locate the content in the child branches, and if recurse is true try to
        /// find the content in all descendant branches. Note that Find uses:
        /// <para/>    tree.content1.Equals(tree.content2)
        /// <para/> rather than content1 == content2
        /// used rather than
        /// </summary>
        public Branch<T> FindByContentDescendantOf(T content)
        {
            if (content == null)
                throw new ArgumentNullException("content");
            if (Content.Equals(content))
                return this;

            foreach (var offshoot in Branches)
            {
                return offshoot.FindByContentDescendantOf(content);
            }
            return null;
        }
        #endregion FindByContentDescendantOf(Tree<T> parent, T content, bool recurse)

        #region    FindByNameFrom(Tree<T> branch, string name, bool recurse = true)
        /// <summary>
        /// Starting at the current Branch look for the name, and when recurse is set go through 
        /// the whole family root and branch
        /// </summary>
        /// <param name="name"></param>
        /// <param name="recurse"></param>
        /// <returns></returns>
        public Branch<T> FindByNameFrom(string name, bool recurse = true)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullOrEmptyException("name", name);
            if (Name == name)
                return this;
            foreach (var offshoot in Branches)
            {
                if (offshoot.Name == name)
                    return offshoot;
                if (recurse)
                {
                    var foundBranch = offshoot.FindByNameFrom(name, recurse);
                    if (foundBranch != null)
                        return foundBranch;
                }
            }
            return null;
        }
        #endregion FindByNameFrom(Tree<T> branch, string name, bool recurse = true)

        #region    FindOffshootByName(string name)
        /// <summary> Find(T content)
        /// Locate the content in this tree, and if recurse is true try to
        /// find the content in all descendant branches. Note that 
        /// <para/>    tree.content1.Equals(tree.content2)
        /// <para/> rather than content1 == content2
        /// used rather than
        /// </summary>
        public Branch<T> FindOffshootByName(string name, bool recurse = true)
        {
            if (this.Name == name)
                return this;
            return FindByNameFrom(name, recurse);
        }
        #endregion FindOffshootByName(string name)

        #region    GetListOfAllDescendants & AddDescendantsToList 

        /// <summary>
        /// Get a List{Branch{T}} that starts with 'this' and includes all descendants
        /// recursed
        /// </summary>
        /// <returns>A list of all descendants</returns>
        public IEnumerable<Branch<T>> GetListOfAllDescendants()
        {
            List<Branch<T>> list = new List<Branch<T>>();
            this.AddDescendantsToList(list);
            return list;
        }
        
        /// <summary>
        /// private recursed routine adding 'this' and then calling itself
        /// for each of its children
        /// </summary>
        /// <param name="list"></param>
        private void AddDescendantsToList(List<Branch<T>> list)
        {
            list.Add(this);
            foreach (var branch in Branches)
                branch.AddDescendantsToList(list);
        }
        #endregion GetListOfAllDescendants & AddDescendantsToList 

        #region    Prune
        /// <summary>
        /// Remove a branch of the tree (which is also a tree) 
        /// </summary>
        /// <param name="branch"></param>
        /// <returns>The pruned section of the tree with the Names and Depths reset for all descendant branches</returns>
        public Branch<T> Prune()
        {
            return Root.Prune();
        }
        #endregion Prune

        #region    Prune
        /// <summary>
        /// Remove a branch of the tree (which is also a tree) 
        /// </summary>
        /// <param name="branch"></param>
        /// <returns>The pruned section of the tree with the Names and Depths reset for all descendant branches</returns>
        private Branch<T> Prune(bool asTree = false)
        {
            Branch<T> exParent = Parent;
            Root = this;
            Parent = null;
            if (exParent == null)
                throw new ArgumentException("Cannot prune Root / basal Branch, there'd be nothing left");
            exParent.Branches.Remove(this);
            RootExtras = new RootExtras<T>(exParent.AllowDuplicateNames, exParent.AllowDuplicateNames);
            
            exParent.Root.ResetBranchListsAfterPrune(this);
            this.Initialise();
            return this;
        }
        #endregion Prune

        #region    RemoveContent
        /// <summary> RemoveContent
        /// Remove the content on a branch (and sub-branches if recurse is specified) do the same for all descendant branches
        /// </summary>
        public void RemoveContent(T content, bool recurse = false)
        {
            this.Content = null;
            string name = Name; // reset the name
        }
        #endregion RemoveContent

        #region    ResetBranchListAfterPrune
        /// <summary>
        /// After a branch has been pruned from this tree we have to remove the entries
        /// in the Dictionary{UniqueKey,Branch}
        /// </summary>
        /// <param name="branch">the starting branch that we beed to remove the entry from BranchLookUp</param>
        internal void ResetBranchListsAfterPrune(Branch<T> branch)
        {
            if (!BranchesByUniqueKey.ContainsKey(branch.UniqueKey))
                throw new LogicException("Root does not contain Branch with Unique key " + branch.UniqueKey);

            BranchesByUniqueKey.Remove(branch.UniqueKey);
            BranchesByName.Remove(BranchesByName.Where(scion => scion.Value.UniqueKey == branch.UniqueKey).First());
            Branches.ForEach(limb => ResetBranchListsAfterPrune(limb));
        }
        #endregion ResetBranchListAfterPrune

        public string ShowType() // xxx
        {
            return GetType().Name;
        }

        #region    yyy  WE NEED TO HANG ON TO THIS ***************************TurnBranchIntoRoot
        ///// <summary>
        ///// Create a copy of a branch as a root
        ///// </summary>
        ///// <param name="branch"></param>
        ///// <param name="allowDuplicateNames">Probably best not to reset this until you learn the ramifications</param>
        ///// <param name="allowDuplicateContent">Probably best not to reset this until you learn the ramifications</param>
        ///// <returns></returns>
        //protected Root<T> TurnBranchIntoRoot(bool? allowDuplicateNames = null, bool? allowDuplicateContent = null)
        //{
        //    Root = (Root<T>) this;
        //    if (Parent != null)
        //        throw new LogicException("Branch is already a Root or has no Parent");
        //    //Prune(this);
        //    if (this is Root<T>)
        //    {
        //        var tapRoot = this as Root<T>;
        //        tapRoot.RootExtras = new RootExtras<T>();
        //        if (allowDuplicateNames != null)
        //            AllowDuplicateNames = (bool)allowDuplicateNames;

        //        if (allowDuplicateContent != null)
        //            AllowDuplicateContent = (bool)allowDuplicateContent;

        //        return tapRoot;
        //    }


        //    Root<T> root = new Root<T>
        //    {
        //        Parent = null,
        //        userSuppliedName = this.userSuppliedName,
        //        Name = this._Name,
        //        _Index = 0,
        //        _Depth = 0,
        //        Content = this.Content,
        //        Branches = this.Branches, // like enums, looks like we can leave a trailing comma
        //    };
        //    /*
        //     * If user really want this new tree to have different 'Allowances' then this is 
        //     * his last chance. I don't think this will ever be used
        //     */

        //    if (allowDuplicateNames != null)
        //        AllowDuplicateNames = (bool)allowDuplicateNames;
        //    if (allowDuplicateContent != null)
        //        AllowDuplicateContent = (bool)allowDuplicateContent;
        //    Root = root;
        //    root.Name = userSuppliedName ? this._Name : "Root";
        //    foreach (var branch in Branches)
        //    {
        //        branch.Parent = root;

        //    }
        //    root.Initialise(recurse: true);
        //    return root;
        //}
        #endregion TurnBranchIntoRoot

        #region    Verify (for help in debugging derived classes)
        
        #region    VerifyRoot
        /// <summary>
        /// Ensure that the root
        /// </summary>
        public void VerifyRoot()
        {
            string msg = string.Empty;
            var root = this.Root;
            Type type = root.GetType();
            if (root.Parent != null) msg += "Root is a Parent/r/n";
            if (root.Depth != 0) msg += "Root depth=" + root.Depth + "/r/n";
            var branchList = GetListOfAllDescendants();
            int count = branchList.Count();
            int countUniqueKey = root.RootExtras.BranchesByUniqueKey.Count;
            int countNames = root.RootExtras.BranchesByName.Count;
            if (count != countUniqueKey) msg += "mismatched UniqueKey counts " + count + " to " + countUniqueKey + "/r/n";
            if (count != countNames) msg += "mismatched Operation counts " + count + " to " + countNames + "/r/n";

            if (msg != string.Empty)
            {
                new MessageBox(msg);
                return;
            }
            StringBuilder branchMessages = new StringBuilder();
            branchList.Skip(1).ForEach(branch => branch.VerifyBranch(branchMessages));
        }
        #endregion VerifyRoot

        #region    VerifyBranch
        /// <summary>
        /// Verify a branch (called 'branch') but not its descendants. Assume 'parent' is 'branch'.Parent.
        /// It ensures that 'parent's children['index'] points at 'branch' where 'index' is 'branch'.Index. It also checks 
        /// all 'branch'.Children have 'branch' as a parent, and all 'branch's Children have an Index corresponding to their
        /// index in 'branch'.Children
        /// <para/>This is used for testing the integrity of Root and all its descendants.
        /// </summary>
        /// <param name="message"></param>
        void VerifyBranch(StringBuilder message)
        {
            int count;

            if (Parent == null)
                throw new LogicException("Parent is null"); // we skipped the root

            if (Parent.Branches[Index] != this) message.AppendFormat("{0}.Branches[{1}] at Depth {2} does not point at {3}",
                   Parent.Name, Index, Parent.Depth, Name, Depth);
            for (int i = 0; i < Branches.Count; i++)
            {
                if (Branches[i].Index != i)
                    message.AppendFormat("'{0}'.Branch[{1}] points to Branch {2}with Index {3} ",
                       Name, i, Branches[i].Name, Branches[i].Index);
            }
            if (!Root.RootExtras.BranchesByUniqueKey.TryGetValue(UniqueKey, out Branch<T> otherBranch))
                message.AppendFormat("BranchesByUniqueKey does not contain {0}{1}", Name, CRLF);
            if (Name == null)
                message.AppendFormat("null Operation at Depth {0} Index {1}{2}", Depth, Index, CRLF);
            else
            if ((count = Root.RootExtras.BranchesByName.Count(scion => scion.Key == Name)) == 0)
                message.AppendFormat("BranchesByName does not contain {0}{1}", Name, CRLF);
            else
                if (count != 1 && !Root.AllowDuplicateNames)
                message.AppendFormat("No Duplicates but there are {0} branches named {1} one of which is at Depth {2} Index{3}{} ",
                                                            count, this.Name, Depth, Index, CRLF);
            return;
        }
        #endregion VerifyBranch

        #endregion Verify

        #region    Debug stuff

        #region    ShowTree 
        public void ShowTree()
        {
            StringBuilder treeStructure = new StringBuilder();
            AddChildrenForShow(this, treeStructure);
            var i = new MessageBox(treeStructure.ToString()).Show();

            //MessageBox.Show(treeStructure.ToString());
            //IEnumerable<Branch<T>> list = GetListOfAllDescendants();
            VerifyRoot();
        }
        #endregion ShowTree

        #region    AddChildrenForShow
        void AddChildrenForShow(Branch<T> father, StringBuilder treeStructure)
        {
            string indent = new string(' ', 4 * father.Depth);
            treeStructure.AppendFormat("{0}{1} Depth={2} Index={3}{4}",
                                indent, father.Name, father.Depth, father.Index, Statics.CRLF);
            foreach (var branch in father.Branches)
                AddChildrenForShow(branch, treeStructure);
        }
        #endregion AddChildrenForShow

        #endregion    Debug stuff
    }
    #endregion class Branch<T>
}
