using System;
using System.Collections.Generic;
 
using System.Text;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace SeniorMoment.Services

{
    /// <summary>
    /// Used to set up a control with a fixed meaning for "Control.Tag". When the control
    /// is passed to the static method ControlTag(Control myControl) we assume that myControl
    /// has a string in the format
    /// 
    /// | keyword_1 [parms_1] | keyword_2 [parms_2] |  .....  keyword_n [parms_n]
    /// 
    /// We create a a dictionary of these and store them in an instance of ControlTag and
    /// finally store this instance back in Control.Tag. So if the original Tag held the
    /// | text  .....| tip Show this when hovering|....
    /// We could now access it with (control.Tag as TagControl)["tip"].
    /// If the first character is not vertical bar, then we leave it alone and assume this
    /// not a TagControl control and the TAG will be left untouched.
    /// </summary>
    public class TagControl : Dictionary<string, string>
    {
        #region    Comments
        /*
         * The Tag is something that is created for each control. We make use of this 
         * at design time by putting info in the Control.Tag. As we loop through the
         * controls during our analysis stage we call the static routine...
         *                Tag.Create(control)
         * ...for each control.
         * Alternatively we call...
         *                CreateFamilyOfTags(myForm_or_other_control)
         * ...which recurses through the controls, sub-controls, sub-sub-controls etc.
         * 
         * The create routine looks at control.Tag and if it is not a string then it ignores the control.
         * If it is a string it expects it to be in the form
         *        | entry | entry | entry |
         * Where 'entry' is of the form:
         *  Keyword  [Parameter data]
         * The meaning of parameter data depends on the keyword.
         * 
         */

        #endregion

        #region    Variables

        #region    Control      Control, null

        ///<summary>Get the control that this tag belongs to
        ///</summary>
        public Control Control { get; private set; }
        #endregion

        #region    UserData
        /// <summary>
        /// A general field where any data can be stored and attached to a control. It could be 
        /// </summary>
        public object UserData { get; set; }
        #endregion UserData

        #region    OriginalTag
        /// <summary>
        /// The original string before it was split into keyword-value pairs
        /// </summary>
        public string OriginalTag = "";
        #endregion OriginalTag

        #region TaggedControlList
        ///<summary> TaggedControlList
        ///This a list of controls that have a Tag Control
        ///</summary>
        static public List<Control> TaggedControlList = new List<Control>();
        #endregion TaggedControlList

        #region    TagControls
        /// <summary>
        /// A list of the tags of type TagControl which most visible controls on the form have.
        /// They contain details about where the control is anchored, what its name is in the XML 
        /// history, plus a pointer back to the control
        /// </summary>
        public static List<TagControl> TagControls = new List<TagControl>();
        #endregion TagControls

        #endregion Variables

        #region    Constructor

        /// <summary>
        /// Private constructor called from Create 
        /// </summary>
        private TagControl() { }
        #endregion Constructor

        #region    Create
        /// <summary>
        /// Alter the control to use the standard set-up for Control.Tag. It 
        /// creates a dictionary of 'tag items'. The tag has a string in the format
        /// 
        /// keyword_1 [parms_1] | keyword_2 [parms_2] |  .....  keyword_n [parms_n]
        /// 
        /// If the following snippet is embedded in the original Control.Tag...
        /// 
        /// .....| tip Show this when hovering|....
        /// 
        /// We could now access it with ((Tag)(control.Tag)).Tags ["tip"].
        /// 
        /// All keyword names are stored in lower case, although the content
        /// of the tag is as supplied.
        /// </summary>
        /// <param name="control">Control that lies somewhere on the control tree on the editor pane.</param>
        static public void Create(Control control)
        {
            char[] crud = new char[] { ' ', '\r', '\n', '\t', '|' };

            /*
             * If the Tag is null we create one that is an empty string so we can add info
             * to it later
             */
             
            if (control.Tag == null)
                control.Tag = "";

            /*
             * If the programmer has decided to use the tag for something else then let him.
             * We will just ignore it
             */

            if (!(control.Tag is string))
                return;

            string allTags = (string)control.Tag;

            if (allTags != "" &&
                    allTags[0] != '|')
                throw new LogicException("Control " + control.Name + " does not have tag info beginning with '|' in first character position");

            allTags = allTags.Trim(crud);
            /*
             * Create the dictionary of all the tags that this control has. If the string
             * is empty then the it is an empty dictionary.
             */

            TagControl tagControl = new TagControl();
            tagControl.OriginalTag = (string)control.Tag;
            TaggedControlList.Add(control);
            /*
             * Keep reference pointer back to the control that created this tag dictionary
             */
            tagControl.Control = control;
            /*
             * Update the control such that its tag is now a TagControl.
             */
            if (allTags != "")
            {
                string[] tagz = allTags.Split('|');

                foreach (string tag in tagz)
                {
                    string cleanTag = tag.Trim(crud);
                    if (cleanTag != "")
                    {
                        string[] subTagz = cleanTag.Split(new char[] { ' ' }, 2);
                        if (subTagz.Length > 1)
                            tagControl.Add(subTagz[0], subTagz[1].Trim(crud));
                        else
                            tagControl.Add(subTagz[0], null);
                    }
                }
            }
            control.Tag = tagControl;
            string info = tagControl.ToString();
        }
        #endregion Create

        #region    CreateFamilyOfTags
        /// <summary>
        /// CFreate An unordered list of tags drilling down from the control
        /// (normally a form) to all the sub-controls etc.
        /// </summary>
        /// <param name="control">Parent Control which is included as the first element
        /// in the list. All the child, grandchild etc controls are also added.</param>
        /// <returns>Unordered List of TagDictionaries, one for each control.</returns>
        static public List<TagControl> CreateFamilyOfTags(Control control)
        {
            //List<TagControl> tagList = new List<TagControl>();
            TagControl.Create(control);
            CreateFamilyOfTags(control, TagControls);
            return TagControls;
        }
        /// <summary>
        /// Add this control's TagControl to the list, and then get all this
        /// controls sub-controls to do the same.
        /// </summary>
        /// <param name="control">control to be added, plus all of its descendant controls</param>
        /// <param name="list">The List of controls to which this control plus its children should be added.</param>
        static void CreateFamilyOfTags(Control control, List<TagControl> list)
        {
            TagControl.Create(control);
            if (control.Tag is TagControl)
                list.Add(((TagControl)control.Tag));
            var familySize = VisualTreeHelper.GetChildrenCount(control);
            for (int i = 0; i < familySize; i++)
            {
                if (VisualTreeHelper.GetChild(control, i) is Control child)
                    CreateFamilyOfTags(child);
            }
        }
        #endregion CreateFamilyOfTags

        #region    ToString
        /// <summary>
        /// the data stored in key-value-pairs
        /// </summary>
        /// <returns>the number of items in the dictionary plus the KeyValuePairs</returns>
        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(OriginalTag + "\r\n");
            foreach(var kvp in this)
                sb.Append(kvp.Key + "=" + kvp.Value + "\r\n");
            return sb.ToString();
        }
        #endregion
    }
    
}
