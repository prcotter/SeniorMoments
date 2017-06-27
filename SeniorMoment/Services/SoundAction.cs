using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeniorMoment.Services
{

    #region    SoundAction (class)
    /// <summary>
    /// Some activities such as PlayAndThenRecord (or whatever) require Operation 1
    /// (PlayFiles) be followed by Operation 2 (RecordUser). These actions run asynchronously
    /// but we wish to ensure (1) has finished before (2) starts. Additionally (1) has to know
    /// what (2) is as (1) is responsible for initiating (2). 
    /// For example if we had  a RecordAndReplay then Operation (2) would be
    /// Replay(). (Replay() doesn't exist, but I hope you get my drift). So we need to
    /// pass information from PlayFiles() to the 'Ended/Finished/Failed' event of (1). 
    /// This requires an SoundAction to be passed from (1) to its 'Ended/Finished/Failed'
    ///  event. It contains an Operation name such as "RecordUser" which the tells the 
    /// 'Ended/Finished/Failed' event what to do.
    /// <![CDATA[Each SoundAction is created with three parameters...
    /// 
    /// 1) "soundaction" which is one of 
    ///             "RecordUser",    
    ///             "Say",          "SayStatic",    "SayList,"      "SayOnUI",   "SayPrivate",
    ///             "PlayFilename", "PlayFilenames","PlayFiles",    "PlaySound"
    ///             
    /// 2) A List of parameters of type List<Object>. Sometimes (as in "SayList") one of
    ///    the parameters is itself a List<something>.
    ///    
    /// 3) The SoundAction which is to follow this one. ie We have a one-way linked list.
    /// 
    /// SoundAction can be daisy-chained in the same way that Task.Event.ContinueWith().
    /// A complicated imaginary example might be...
    /// 
    ///         MSound.RecordPlayDoubleBeepSay()... Record => Play => Beep => Say
    /// 
    /// We now set up the daisy-chain (aka one-way linked list) in MSound. 
    /// 
    /// There are two ways to do this...
    /// 
    /// (1) Nested SoundActions
    /// 
    ///   Sound.RunSoundActionChain(
    ///         new SoundAction("RecordUser", null,
    ///         new SoundAction("PlayFilename",new List<object>{"RecordingOk.wav",0.8},
    ///         new SoundAction("PlayFiles", new List<object>{
    ///                                 List<StorageFile>{BeepFile,BeepFile},
    ///                                 0.6 },  // volume parameter
    ///         new SoundAction("Say",new List<string>{"What next?", 0.5},
    ///         null );
    ///         
    /// (2) ContinueWith(soundAction) ... which is almost syntactic sugar for (1) above...
    /// 
    ///         var action1 = new SoundAction("RecordUser, null);
    ///         var action2 = new SoundAction("PlayFilename",new List<object>{"RecordingOk.wav",0.8});
    ///         var action3 = new SoundAction("PlayFiles", new List<object>{
    ///                                 List<StorageFile>{BeepFile,BeepFile},
    ///                                 0.6 });  // volume parameter
    ///         var action4 = new SoundAction("Say",new List<object>{"Whatever next?", 0.5});
    ///         
    ///     Sound.RunSoundActionChain(action1)
    ///             .ContinueWith(action2)
    ///             .ContinueWith(action3)
    ///             .ContinueWith(action4);
    ///             
    /// More long-winded but easier to understand.
    /// ]]>
    /// 
    /// </summary>

    public class SoundAction
    {
        #region    Properties

        #region    Operation
        /// <summary>
        /// The operation to be performed. See comments at start of SoundObject
        /// </summary>
        public string Operation;
        #endregion Operation

        #region    Parameters
        /// <summary>
        /// As we keep on seeing a List{object} where one of the objects may be
        /// a List{Files} or a List{string filenames}
        /// </summary>
        public List<object> Parameters;
        #endregion Parameters

        #region    NextSoundAction
        public SoundAction NextSoundAction;
        #endregion NextSoundAction

        #endregion Properties

        #region    Constructor
        public SoundAction(string operation, List<object> parameters, SoundAction soundAction = null)
        {
            Operation = operation;
            Parameters = parameters;
            NextSoundAction = soundAction;
        }
        #endregion Constructor

        #region    ContinueWith
        public SoundAction ContinueWith(SoundAction nextParameter)
        {
            SoundAction soundAction = this;
            while (soundAction.NextSoundAction != null)
                soundAction = soundAction.NextSoundAction;
            soundAction.NextSoundAction = nextParameter;
            return nextParameter;
        }
        #endregion ContinueWith

        #region    ToString

        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString()
        {
            string name = string.Empty;
            if (Parameters != null && Parameters.Count > 0)
                name = Parameters[0] as string ?? string.Empty;

            return $"{Operation} {name}";
            #endregion ToString
        }
    }
    #endregion SoundAction (class)
}
