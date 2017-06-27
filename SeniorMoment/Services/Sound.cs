using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace SeniorMoment.Services

{
    /// <summary>
    /// Represents a class which handles recording, playing back of recordings, playing
    /// sound files, speaking text (speech synthesis) and maybe sometime converting
    /// task to text.
    /// </summary>
    public class Sound : ITracer
    {
        /*----------------------ALL ABOUT SOUND --------------------
         * 
         *    Sound is responsible for playing sounds, speech and recording. It is a Service and as such knows nothing about
         *    the application. It doesn't know if the Sound it is playing is an alarm or not. It provides
         *    EventHandlers such as PlayedEventHandler<SoundAction>.
         *    
         *    Sound is closely connected with SoundAction. If you wish to Daisy-chain Plays/Records so one doesn't
         *    start till the previous one finished you use SoundAction. Something like this...
         *    
         *    var SayIntro = new SoundAction("say", new List<object>{"just talk into the microphone"});
         *    var beep = new SoundAction("PlayFilename", new List<object>{"beep.m4a", folder: SoundFolder, volume: 0.5, callback: ChangeUIAfterBeep});
         *    var userInput = new SoundAction( "RecordUser", callback: DisableMike)
         *    var SayAllDone = new SoundAction("Say", new List<object>{"all done"});
         *    
         *    sayIntro.ContinueWith(
         *      beep.ContinueWith(
         *          userInput.ContinueWith(
         *              SayAlldone)))
         */

        #region    Properties

        #region    DefaultPriority
        /// <summary>
        /// Initial standard priority for a Sound.Play()/>
        /// </summary>
        public const int DefaultPriority = 100;
        #endregion DefaultPriority

        #region    DefaultSoundFile
        /// <summary>
        /// Default sound for a Sound if the user has not Recorded a message
        /// </summary>
        static public StorageFile DefaultSoundFile;
        #endregion DefaultSoundFile

        #region    lockStatus
        /// <summary>
        /// Used as a lock() in sayOnUI
        /// </summary>
        static object lockStatus = new object();
        #endregion lockStatus

        #region    lowLagMediaRecording
        /// <summary>
        /// Used by MediaPlayer.Play. Needs to be an instance variable so it can be passed 
        /// to StopRecording after a timeout
        /// </summary>
        LowLagMediaRecording lowLagMediaRecording;
        #endregion lowLagMediaRecording

        #region    MediaPlayer
        /// <summary>
        /// The MediaPlayer user to play Sounds
        /// </summary>
        MediaPlayer MediaPlayer;
        #endregion MediaPlayer

        #region    Name
        /// <summary>
        /// The Name of this Sound which is also the name of the file (minus extension)
        /// </summary>
        public string Name { get; private set; }
        #endregion Name

        #region    nextAction
        /// <summary>
        /// If the caller wants a sequential synchronous series of Sound activities they are
        /// set up in chained SoundActions. A SoundAction is a single-linked list pointing
        /// at SoundAction2 which points to 3 etc till the 'next' pointer is null;
        /// <para/
        /// <para/> Sound1.nextAction => Sound2 => Sound2.nextAction => Sound3...etc 
        /// <para/>
        /// <para/>This can be done to any depth. To set it up as follows see commentary at start of class
        /// </summary>
        SoundAction nextAction = null;
        #endregion nextAction

        #region    QueuedTask yyy
        /// <summary>
        /// This is created when we signal an alarm so the OnMSoundPlayed has the
        /// data to work out what happened
        /// </summary>
        public QueuedTask QueuedTask { get; private set; } = null;
        #endregion QueuedTask

        #region    SoundFile
        /// <summary>
        /// The sound to be heard. This defaults to Default.wav and stays
        /// that way unless the user makes a recording.
        /// </summary>
        public StorageFile SoundFile { get; private set; } 
        #endregion SoundFile

        #region    SoundStatic
        /// <summary>
        /// Used so that there is an Instance available for the SayStatic
        /// </summary>
        public static Sound SoundStatic { get; private set; } = new Sound("StaticInstanceOfSound");
        #endregion SoundStatic

        #region    SoundStatusStatic
        /// <summary>
        /// property used so that we do not play two messages at once or record during
        /// playback or vice versa. Recordings or playbacks only occur when Status=Idle. It
        /// is public because MainPage uses it to define the Brush to paint the microphone.
        /// It is static because there can be multiple Sounds but none of them can play or record
        /// if another sound is playing or recording
        /// </summary>
        static public SoundStatus SoundStatusStatic { get; private set; } = SoundStatus.Idle;
        #endregion SoundStatusStatic

        #endregion Properties

        #region    Constructor
        /// <summary>
        /// Create  new Sound which is responsible for both recording and playback
        /// of a Sound. There is one of these associated with an MSound. However the
        /// Sound has no knowledge or access to any MVVM aspects, hence it resides 
        /// in the Services folder.
        /// </summary>
        /// <param name="soundName">The Name of the sound. If a recording is made it will be
        /// called 'Name'.m4a</param>
        public Sound(string soundName)
        {
            Name = soundName;
            Trace($"Entry&Exit {Name}");
            SoundFile = DefaultSoundFile;
        }
        #endregion Constructor

        #region    SetDefaultSoundFile
        /// <summary>
        /// We will need this file for every VMTimer we create, so it initialised as a static
        /// </summary>
        /// <returns></returns>
        static async Task<string> SetDefaultSoundFile(string file = "Default.wav")
        {
            
            DefaultSoundFile = await Statics.AssetsFolder.GetFileAsync(file);
            return "OK";
        }
        #endregion SetDefaultSoundFile

        #region    GetRecordingsFolderAsync  (static)
        /// <summary>
        /// Get the folder called MRecordings which will hold all the messages
        /// </summary>
        /// <returns></returns>
        async Task<StorageFolder> GetRecordingsFolderAsync()
        {
            Trace($"Entry");
            StorageFolderQueryResult queryResult = Statics.LocalFolder.CreateFolderQuery();
            var subFolders = await queryResult.GetFoldersAsync();

            foreach (var subFolder in subFolders)
            {
                if (subFolder.Name.ToLower() == "mrecordings")
                {
                    Trace($"Exit found MRecordings");
                    return subFolder;
                }
            }
            var recordingsFolder = await Statics.LocalFolder.CreateFolderAsync("MRecordings");
            Trace($"Exit  MRecordings created");
            return recordingsFolder;
        }
        #endregion getRecordingsFolderAsync (static)

        #region    getSoundFileFromFolder
        /// <summary>
        /// This routine is just so we can do an await from a non-async caller
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        async Task<StorageFile> getSoundFileFromFolder(string filename, StorageFolder folder)
        {
            Trace($"Entry {folder}.{filename}");
            StorageFile file;
            {
                IStorageItem iStorageItem = await folder.TryGetItemAsync(filename).AsTask().ConfigureAwait(false); ;
                if (iStorageItem == null)
                    Statics.InternalProblem("Not found: " + filename);
                if (iStorageItem.IsOfType(StorageItemTypes.Folder))
                    Statics.InternalProblem(filename + " is a folder");
                file = (StorageFile)iStorageItem;
            }
            Trace($"Exit  {folder}.{filename}");
            return file;
        }
        #endregion getSoundFileFromFolderAsync (static)

        #region    GetRecordingsFolder
        /// <summary>
        /// Get the folder called MRecordings which will hold all the messages
        /// </summary>
        /// <returns></returns>
        StorageFolder GetRecordingsFolder()
        {
            if (_RecordingsFolder == null)
                _RecordingsFolder = GetRecordingsFolderAsync().Result;
            Trace($"Entry&Exit {_RecordingsFolder}");
            return _RecordingsFolder;
        }
        StorageFolder _RecordingsFolder = null;
        #endregion GetRecordingsFolder (static)

        #region    InitialiseStatic
        //
        public static void InitialiseStatic( )
        {
            var ignore = SetDefaultSoundFile().Result;
        }
        #endregion InitialiseStatic

        #region    PlayFilename 
        /// <summary>
        /// Play just one recording. We make a List with just one filename in it and pass 
        /// it to PlayFilenames
        /// </summary>
        /// <param name="filename">the filename of the sound file to be played</param>
        /// <param name="folder">the folder in which to search. If omitted it defaults to AssetsFolder</param>
        /// <param name="volume">from 0.0 (silent) to 1.0 (max at whatever the user has his speaker volume set</param>
        /// <returns>A task that can be waited on for the sounds to finish</returns>
        public void PlayFilename(string filename,
                                StorageFolder folder = null,
                                Double volume = 1,
                                double priority = DefaultPriority,
                                Action callback = null)
        {
            if (string.IsNullOrEmpty(filename))
                throw new ArgumentNullOrEmptyException("filename", filename);
            folder = folder ?? Statics.AssetsFolder ?? throw new LogicException("AssetsFolder missing");
            Trace($"Entry {folder}.{filename}");
            List<string> filenames = new List<string> { filename };
            PlayFilenames(filenames, folder);
            Trace($"Exit  {folder}.{filename}");
            return;
        }
        #endregion PlayFilename 

        #region    PlayFilenames
        /// <summary>
        /// Play a list of filenames
        /// </summary>
        /// <param name="filenames"></param>
        /// <param name="folder"></param>
        /// <param name="volume"></param>
        /// <param name="priority"></param>
        public void PlayFilenames(List<string> filenames,
                                    StorageFolder folder = null,
                                    Double volume = 1,
                                    int priority = DefaultPriority)
        {
            Trace($"Entry =>PlayFileNamesPrivate(same parms)");
            if (folder == null)
                folder = Statics.AssetsFolder;
            PlayFilenamesPrivate(filenames,
                                    folder: folder,
                                    volume: volume,
                                    priority: DefaultPriority
                                    );
            Trace($"Exit ");
            return;
        }
        #endregion PlayFilenames

        #region    PlayFilenamesPrivate
        /// <summary>
        /// Play one or more sounds that are passed filenames in the same passed folder
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="folder">the folder in which to search. If omitted it defaults to AssetsFolder</param>
        /// <param name="volume"></param>
        /// <returns>A task that can be waited on for the sounds to finish</returns>

        public void PlayFilenamesPrivate(List<string> filenames,
                                            StorageFolder folder = null,
                                            Double volume = 1,
                                            int priority = DefaultPriority)
        {
            folder = folder ?? Statics.AssetsFolder;
            String filenameConcat = string.Join(", ", filenames);
            Trace($"Entry {folder.Name}({filenameConcat}) Vol={volume} Pri={priority}");
            List<StorageFile> files = new List<StorageFile>();
            filenames.ForEach(file => files.Add(getSoundFileFromFolder(file, folder).Result));
            PlayFiles(files, volume: volume, priority: priority);
            Trace($"Exit  {folder.Name}({filenameConcat}) Vol={volume} Pri={priority}");
            return;
        }
        #endregion PlayFilenamesPrivate

        #region    RunNextSoundAction
        /// <summary>
        /// Run the sequence of sound events define in the SoundAction
        /// </summary>
        /// <param name="soundActionp"></param>
        public void RunNextSoundAction(SoundAction soundAction = null)
        {
            Trace($"Entry");
            if (soundAction == null)
                Statics.InternalProblem("soundAction null");
            UnwindSoundAction(soundAction);
            Trace($"Exit");
        }
        #endregion RunNextSoundAction

        #region    PlayFile
        /// <summary>
        /// Play a single file. Just wrap up the StorageFile file in a  List(StorageFile)
        /// and then call PlayFiles
        /// </summary>
        /// <param name="file"></param>
        /// <param name="volume"></param>
        /// <param name="priority"></param>
        public void PlayFile(
               StorageFile file,
               double volume = 1,
               int priority = DefaultPriority)
            => PlayFiles(new List<StorageFile> { file }, volume, priority);
        #endregion PlayFile

        #region    PlayFiles
        /// <summary>
        /// Create an Action to play that can be passed to the TaskFunnel. The Action represents
        /// the playing 0 or more files of type ma4 or wav
        /// </summary>
        /// <param name="files">A List of files of type ma4 and/or wav. An empty list is not acceptable</param>
        /// <param name="volume">From 0 (silent) to 1.0 (Max)</param>
        /// <returns>true if it is started now, false if it is queued</returns>
        void PlayFiles
                (List<StorageFile> files,
                double volume = 1,
                int priority = DefaultPriority)
        {
            if (files == null || files.Count == 0)
                Statics.InternalProblem(files == null ? "null files" : "files empty");
            string soundFileNames = string.Join(", ", files.Select(sound => sound.Name));
            Trace($"Entry {SoundStatusStatic} {soundFileNames}");

            List<object> parameters = new List<object>() { files, volume, priority };
            QueuedTask = new QueuedTask(
                PlayFilesFunnelled,
                parameters,
                Name);
            /*
             * The TaskFunnel creates an ordered list of tasks to be run sequentially.
             * It takes on the responsibility of not starting a new task till the old one
             * has finished. 
             */
            Statics.TaskFunnel.MeNext(QueuedTask);
            Trace($"Exit  QT={QueuedTask.ToString()}");
            return;
        }
        #endregion PlayFiles

        #region    PlayFilesFunnelled
        void PlayFilesFunnelled(QueuedTask qt)
        {
            
            if (SoundStatusStatic != SoundStatus.Idle)
            {
                Trace($"Entry Busy status={SoundStatusStatic} ReQueueing QT={qt.ToString()}");
                var ignore = Task.Delay(100);
                if (qt.Priority>30)
                    qt.Priority -= 5;
                Statics.TaskFunnel.MeNext(qt);
                Trace($"Exit  Busy status={SoundStatusStatic} ReQueueing QT={qt.ToString()}");
                return;
            }
            Trace($"Entry QT={qt.ToString()}");
            MediaPlayer = new MediaPlayer();
            MediaPlayer.MediaFailed += OnPlayFailedInternal;
            MediaPlayer.MediaEnded += OnPlayedInternal;
            MediaPlayer.AudioCategory = Windows.Media.Playback.MediaPlayerAudioCategory.Media;
            /*
             * This Queued task is created here so OnMSoundPlayedknows how the event came about
             * and if it should be rescheduled
            */
            List<object> parameters = qt.Parameters;

            List<StorageFile> files = (List<StorageFile>)parameters[0];
            double volume = (double)parameters[1];
            int priority = (int)parameters[2];
            string name = string.Join(" ", files.Select(file => file.Name));

            MediaPlaybackList playbackList = null;
            playbackList = new MediaPlaybackList();

            /* We build up a MediaPlaybackList of all the files.Even if we 
             * are only playing one sound we create a list with a single entry 
             * Makes the code easier. */

            foreach (StorageFile file in files)
            {
                MediaSource source = MediaSource.CreateFromStorageFile(file);
                MediaPlaybackItem item = new MediaPlaybackItem(source);
                playbackList.Items.Add(item);
            }
            MediaPlayer.Source = playbackList;
            MediaPlayer.Volume = volume;
            /* Media is part of the UI so we cannot Task.Run("MediaPlayer.Play")*/
            try
            {
                SetStatus(SoundStatus.Playing);
                MediaPlayer.Play();
            }
            catch (OperationCanceledException)
            { /* OK - just means the playback was cancelled, maybe due to a timeout */}
            catch (Exception e)
            {
                Statics.InternalProblem("TryPlayFiles", exc:e);
            }
            Trace($"Exit  QT={qt.ToString()}");

            return;
        }
        #endregion PlayFilesFunnelled

        #region    TryReplayFiles
        /// <summary>
        /// We need this routine as Action does not take parameters and we need to call TryPlayFiles which does.
        /// Luckily we stored stuff in QueuedTask before we rescheduled the alarm. 
        /// </summary>
        public void TryReplayFiles()
        {
            Trace($"Entry");
            var parms = QueuedTask.Parameters ?? throw new LogicException("Parameters null");
            PlayFiles((List<StorageFile>)parms[0],
                            (double)parms[1],
                            (int)parms[2]);
            Trace($"Exit");
        }
        #endregion TryReplayFiles

        #region    RecordUser
        /// <summary>
        /// Record the user's voice until StopRecording() is called or there is a timeout'
        /// This routine is only so we can make a call to 
        /// </summary>
        /// <param name="recordingFilenameNoExtension"></param>
        public void RecordUser(int minimum = 1, int maximum = 60)
        {
            Trace($"Entry {Name}");
            var ignore = RecordUserAsync(Name,minimum,maximum).Result;
            Trace($"Exit  {Name}");
        }
        #endregion RecordUser

        #region    RecordUserAsync
        /// <summary>
        /// Make a recording of the user
        /// </summary>
        public async Task<StorageFile> RecordUserAsync(string soundFilenameNoExtension,int minimum,int maximum)
        {
            Trace($"Entry {soundFilenameNoExtension}.m4a");

            VerifyStatus(SoundStatus.Idle);
            SetStatus(SoundStatus.Recording);

            #region    MediaCaptureInitializationSettings
            MediaCaptureInitializationSettings captureInitializationSettings;
            captureInitializationSettings = new Windows.Media.Capture.MediaCaptureInitializationSettings()
            {
                MediaCategory = MediaCategory.Speech,
                SharingMode = MediaCaptureSharingMode.SharedReadOnly
            };
            #endregion MediaCaptureInitializationSettings
            MediaCapture mediaCapture = new MediaCapture();
            mediaCapture.Failed += OnCaptureFailedInternal;
            await mediaCapture.InitializeAsync(captureInitializationSettings);
            var folder = GetRecordingsFolder();
            SoundFile = await folder.CreateFileAsync
                (soundFilenameNoExtension + ".m4a", CreationCollisionOption.ReplaceExisting);
            MediaEncodingProfile mediaEncodingProfile;
            mediaEncodingProfile = MediaEncodingProfile.CreateM4a(AudioEncodingQuality.High);

            lowLagMediaRecording = await mediaCapture.PrepareLowLagRecordToStorageFileAsync(
                    mediaEncodingProfile, SoundFile);
            await lowLagMediaRecording.StartAsync();
            
            Trace($"Exit  {soundFilenameNoExtension}.m4a");
            return SoundFile;
        }
        #endregion RecordUserAsync

        #region    Say(message)
        /// <summary>
        /// Say a single message.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="volume">0.0=silent, 1.0 = max</param>
        public string Say(string text, double volume = 1.0)
        {
            Trace($"Entry {text} vol={volume}");
            Say(new List<string> { text }, volume: volume);
            Trace($"Exit  {text} vol={volume}");
            return "";
        }
        #endregion Say(message)

        #region    Say List<string>
        /// <summary>
        /// Using the passed text string say them out loud via the speech synthesizer
        /// </summary>
        /// <param name="texts">A list of string to be spoken by the SpeechSynthesizer</param>
        /// <param name="callbackUser"></param>
        public void Say(List<string> texts, double volume)
        {
            Trace($"Entry {texts?[0] ?? "No texts"}");
            SayPrivate(texts: texts, volume: volume);
            Trace($"Exit  {texts?[0] ?? "No texts"}");
        }
        #endregion Say List<string>

        #region    SayPrivate(List<string>) 
        /// <summary>
        /// Say a number of messages on the speaker. End/Start with period or comma for a pause
        /// </summary>
        /// <param name="texts">list of messages to be spoken one after the other</param>
        private string SayPrivate(List<string> texts, double volume = 1.0)
        {
            string message = String.Join(", ", texts);
            Trace($"Entry {message}");
            if (texts.Count == 0)
            {
                Trace("Entry&Exit Count=0");
                return "";
            }
            string traceMsg;
            int count = texts.Count;
            if (count == 1)
                traceMsg = texts[0];
            if (count == 2)
                traceMsg = $"{count} texts: {texts[0]} + {texts[1]}";
            else
                traceMsg = $"{count} texts: {texts[0]} => {count - 2} more => {texts.Last()}";

            Trace($"Entry {traceMsg}");
            lock (texts)
            {
                string spokenMessage = string.Join(" ", texts);
                var x = Statics.CoreDispatcher.RunAsync
                (CoreDispatcherPriority.Normal, () => { SayOnUi(spokenMessage, volume: volume); });
            }
            Trace($"Exit  message");
            return "";
        }
        #endregion    SayPrivate(List<string>) 

        #region    SayOnUi
        /// <summary>
        /// This is where the 'Saying' takes place. This is invoked on the UI thread
        /// because MediaElement is a Windows.UI.Xaml.Control. The MediaElement is
        /// declared in Xaml so it can be on the visual tree. If it is not then
        /// MediaElement does not have its OnMediaXx_ed events triggered.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="callback"></param>
        void SayOnUi(string text, double volume = 1)
        {
            Trace($"Entry {text}");
            lock (lockStatus)
            {
                VerifyStatus(SoundStatus.Idle);

                //MediaElement mediaElement = MainPage.This.Resources["SayElement"] as MediaElement;
                MediaElement mediaElement = new MediaElement();
                MainPage.This.Resources["SayElement"] = mediaElement;
                var synthesizer = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
                IAsyncOperation<SpeechSynthesisStream> iAsyncStream = synthesizer.SynthesizeTextToStreamAsync(text);
                TaskAwaiter<SpeechSynthesisStream> streamWaiter = iAsyncStream.AsTask().GetAwaiter();
                SpeechSynthesisStream stream = streamWaiter.GetResult();
                mediaElement.SetSource(stream, stream.ContentType);
                mediaElement.MediaEnded += OnSaidInternal;
                mediaElement.MediaFailed += OnSaidFailedInternal;
                mediaElement.Volume = volume;
                SetStatus(SoundStatus.Playing);
                mediaElement.Play();

                var WaitTask = Task.Delay(100);

            }
            Trace($"Exit  {text}");
        }
        #endregion SayOnUi

        #region    SayStatic message
        /// <summary>
        /// Static version of say. Used as: Sound.SoundStatic.Say()
        /// </summary>
        /// <param name="message"></param>
        public static void SayStatic(string message, double volume = 1.0)
        {
            Sound.SoundStatic.Trace($"Entry vol={volume} \"{message}\"");
            SoundStatic.Say(message, volume: volume);
            Sound.SoundStatic.Trace($"Exit vol={volume} \"{message}\"");
        }
        #endregion SayStatic message

        #region    SayStatic messages
        /// <summary>
        /// Static version of say. Used as: Sound.SoundStatic.Say()
        /// </summary>
        /// <param name="message"></param>
        public static void SayStaticList(List<string> messages, double volume = 1.0)
        {
            string message = String.Join(", ", messages);
            Sound.SoundStatic.Trace($"Entry vol={volume} \"{messages}\"");
            SoundStatic.Say(messages, volume: volume);
            Sound.SoundStatic.Trace($"Exit vol={volume} \"{messages}\"");
        }
        #endregion SayStatic messages

        #region    SetStatus & debug 
        /// <summary>
        /// The Recording / Idle / Playing status has changed. A good time to tell VMTimer to update
        /// the MAINPage - in particular the microphone. Also a bit of debug stuff here.
        /// </summary>
        /// <param name="soundStatus"></param>
        /// <param name="method"></param>
        /// <param name="line">[CallerLineNumber]</param>
        void SetStatus(SoundStatus soundStatus, [CallerMemberName] String member = "", [CallerLineNumber] int line = -1)
        {
            Trace($"Entry from {SoundStatusStatic}");
            _linePrevious = line;
            _memberPrevious = member;
            _statusPrevious = soundStatus;
            SoundStatusStatic = soundStatus;
#if DEBUG
            statuses.Add(new StatusItem { Member = member, Line = line, SoundStatus = soundStatus });
#endif
            /*
             * ??? have to get rid of the following as a Service should never know nor refer to
             * anything outside SeniorMoment.
             */

            Action action = new Action(ViewModels.VMMicrophone.This.UpdateMicrophone);
            Statics.RunOnUI(action);
            Trace($"Exit  to {SoundStatusStatic}");
        }

        /*
         * The _variables will show the previous value of SoundStatus when we fail a VerifyStatus()
         */
        int _linePrevious;
        string _memberPrevious;
        SoundStatus _statusPrevious;
        #endregion SetStatus

        #region    statuses #if DEBUG
#if DEBUG
        /// <summary>
        /// Debug variable that creates a list of each status we have been in (zzz)
        /// </summary>
        List<StatusItem> statuses = new List<StatusItem>();
#endif
        #endregion statuses  #if DEBUG

        #region    StopRecording 
        /// <summary>
        /// Recording has ceased either because of a timeout on the length of a 
        /// recording or the user has taken his finger off the Microphone Button
        /// </summary>
        public string StopRecording()
        {
            Trace($"Entry");
            VerifyStatus(SoundStatus.Recording);
            var ignore = lowLagMediaRecording.StopAsync();
            ignore = lowLagMediaRecording.FinishAsync();
            lowLagMediaRecording = null;
            SetStatus(SoundStatus.Idle);
            OnRecordFinishedInternal(this, nextAction);
            Trace($"Exit");
            return "";
        }
        #endregion StopRecording

        #region    Trace()
        /// <summary>
        /// Write to the trace file or the console
        /// </summary>
        /// <param name="info">The message to be put in the console/trace file</param>
        /// <param name="member">Method where the Trace was called from </param>
        /// <param name="line">line in method where Trace was called from</param>
        /// <param name="path">path (string) to the file that contains member</param>
        [DebuggerStepThrough()] 
        public void Trace(string info,
           [CallerMemberName] string member = "",
           [CallerLineNumber] int line = 0,
           [CallerFilePath] string path = "")
        {
             Tracer.TracerMain?.TraceCrossTask(info, member, line, path);
        }
        #endregion Trace()

        #region    UnwindSoundAction
        /// <summary>
        /// When we have daisy-chained Sound method the first Sound.Record/Play/Say
        /// is passed a one-way linked list of SoundActions soundAction. As each event 
        /// finishes we have to strip the first entry out of soundAction and execute 
        /// SoundAction.NextSoundAction(), Continue till we reach the end of the chain
        /// </summary>
        public void UnwindSoundAction(SoundAction soundAction)
        {
            if (soundAction == null)
            {
                Trace($"Entry&Exit");
                return;
            }
            var op = soundAction.Operation;
            Trace($"Entry {op}");
            List<Object> parms = soundAction.Parameters as List<object>;
            int? counter = parms?.Count ?? -1;
            StorageFolder p1File = Statics.AssetsFolder; // default folder for recordings
            Object p0 = counter > 0 ? parms[0] : null,
                   p1 = counter > 1 ? parms[1] : null,
                   p2 = counter > 2 ? parms[2] : null,
                   p3 = counter > 3 ? parms[3] : null;
            List<string> p0ListString = p0 as List<string>;
            List<StorageFile> p0ListFile = p0 as List<StorageFile>;
            StorageFile p0File = p0 as StorageFile;
            String p0String = p0 as string;
            String p1String = p1 as string;
            Double p1Double = (p1 as double?) ?? 1.0;
            Double p2Double = (p2 as double?) ?? 1.0;
            Int32 p3Int = (p2 as int?) ?? 100;
            Int32 p4Int = (p2 as int?) ?? 100;

            switch (op.ToLower()) // op can
            {
                case "recorduser":
                    RecordUser();
                    break;

                case "saystatic":
                    SayStatic(p0String, p1Double);
                    break;
                case "say":
                    Say(p0String, p1Double);
                    break;
                case "saylist":
                    Say(p0ListString, p1Double);
                    break;
                case "sayonui":
                    SayOnUi(p0String, p1Double);
                    break;
                case "sayprivate":
                    SayPrivate(p0ListString, p1Double);
                    break;
                case "playdefault":
                    PlayFiles(new List<StorageFile> { SoundFile });
                    break;
                case "playfilenames":
                    PlayFilenames(p0ListString, folder: p1File, volume: p2Double, priority: p3Int);
                    break;
                case "playfilename":
                    PlayFilename(p0String, folder: p1File, volume: p2Double, priority: p3Int);
                    break;
                case "playfiles":
                    PlayFiles(p0ListFile, volume: p2Double, priority: p3Int);
                    break;
                case "playfile":
                    PlayFiles(new List<StorageFile> { p0File }, volume: p2Double, priority: p3Int);
                    break;

                default:
                    Statics.InternalProblem($"Invalid Operation {op ?? "Null"}");
                    break; // never gets here
            }
            nextAction = soundAction.NextSoundAction;
            Trace($"Exit  {op}");
        }
        #endregion UnwindSoundAction

        #region    VerifyStatus
        // <summary>
        /// Sanity checks in various places to ensure only one 'thing' can be
        /// Recording and/or Playing at any given moment.
        /// </summary>
        /// <param name="status"></param>
        /// <param name="lineNumber"></param>
        public void VerifyStatus(SoundStatus status, [CallerLineNumber] int lineNumber = 0)
        {
            if (SoundStatusStatic != status)
            {
                string msg = string.Format(
                    "MSound Status line:{0} Expecting {1} and Found {2} {3} Last SetStatus: {4} at {5} with Status {6}",
                                        lineNumber, status, SoundStatusStatic, Statics.CRLF2, _memberPrevious, _linePrevious, _statusPrevious
                    );

                Statics.InternalProblem(msg);
            }
        }
        #endregion VerifyStatus

        #region    Events & Delegates

        #region    OnCaptureFailedInternal
        /// <summary>
        /// Sanity checks in various places to ensure only one 'thing' can be
        /// Recording and/or Playing at any given moment.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCaptureFailedInternal(object sender, MediaCaptureFailedEventArgs e)
        {
            Trace($"Entry");
            SetStatus(SoundStatus.Idle);
            Statics.TaskFunnel.InformTaskFunnelEventCompleted();
            PlayFilename("SorryIFailedToRecordThat.m4a", Statics.AssetsFolder, volume: 0.5);
            // RunNextSoundAction(nextAction);
            Trace($"Exit");
        }
        #endregion OnCaptureFailedInternal

        #region    OnPlayedInternal
        /// <summary>
        /// When the Media is finished AND the system had to wait till it completed
        /// we take the OnMediaXx_ed events and run the next Task using unWindSoundAction
        /// </summary>
        /// <param name="sender">This Sound</param>
        /// <param name="args"></param>
        void OnPlayedInternal(object sender, object args)
        {
            Trace($"Entry ");
            SetStatus(SoundStatus.Idle);
            Statics.TaskFunnel.InformTaskFunnelEventCompleted();
            PlayedEventHandler?.Invoke(this, QueuedTask);
            UnwindSoundAction(nextAction);
            MediaPlayer.Dispose();
            Trace($"Exit ");
        }
        #endregion OnPlayedInternal

        #region    OnPlayFailedInternal
        /// <summary>
        /// When the Media is finished AND the system had to wait till it completed
        /// we take the OnMediaXx_ed events and release the Task held by TaskFreezer
        /// </summary>
        /// <param name="sender">This Sound</param>
        /// <param name="args"></param>
        void OnPlayFailedInternal(object sender, object args)
        {
            Trace($"Entry => OnPlayedInternal");
            OnPlayedInternal(sender, args);
            Trace($"Exit ");
        }
        #endregion OnPlayFailedInternal

        #region    OnSaidInternal
        /// <summary>
        /// When the Media is finished AND the system had to wait till it completed
        /// we take the OnMediaXx_ed events and release the Task held by TaskFreezer
        /// </summary>
        /// <param name="sender">This Sound</param>
        /// <param name="args"></param>
        void OnSaidInternal(object sender, object args)
        {
            Trace($"Entry ");
            SetStatus(SoundStatus.Idle);
            Statics.TaskFunnel.InformTaskFunnelEventCompleted();
            SaidEventHandler?.Invoke(this, QueuedTask);
            UnwindSoundAction(nextAction);
            Trace($"Exit ");
        }
        #endregion OnSaidInternal

        #region    OnSaidFailedInternal
        /// <summary>
        /// When the Media is finished AND the system had to wait till it completed
        /// we take the OnMediaXx_ed events and release the Task held by TaskFreezer
        /// </summary>
        /// <param name="sender">This Sound</param>
        /// <param name="args"></param>
        void OnSaidFailedInternal(object sender, object args)
        {
            Trace($"Entry ");
            SetStatus(SoundStatus.Idle);
            PlayedEventHandler?.Invoke(this, null);
            UnwindSoundAction(nextAction);
            Trace($"Exit ");
        }

        #endregion OnSaidInternal

        #region    OnRecordFinishedInternal
        /// <summary>
        /// When the Media is finished AND the system had to wait till it completed
        /// we take the OnMediaXx_ed events and release the Task held by TaskFreezer
        /// </summary>
        /// <param name="sender">This Sound</param>
        /// <param name="args"></param>
        public void OnRecordFinishedInternal(object sender, SoundAction soundAction)
        {
            Trace($"Entry ");
            SetStatus(SoundStatus.Idle);
            /* if caller had a callback invoke it - psst it's => MSound.OnRecorded(soundActionChain) 
               But of course we don't know that. Sound is a service and independent of any MVVM things */
            RecordedEventHandler?.Invoke(this, null);
            UnwindSoundAction(nextAction);
            Trace($"Exit ");
        }
        #endregion OnRecordFinishedInternal

        #region    External EventHandlers 

        #region    PlayedEventHandler
        /// <summary>
        /// EventHandler for SoundPlayEnded. This is used by the caller
        /// </summary>
        public EventHandler<QueuedTask> PlayedEventHandler;
        #endregion PlayedEventHandler

        #region    RecordedEventHandler
        /// <summary>
        /// Event Handler for OnRecorded. This is used by the caller, hence public
        /// </summary>
        public EventHandler<QueuedTask> RecordedEventHandler;
        #endregion RecordedEventHandler

        #region    SaidEventHandler
        /// <summary>
        /// Event Handler for OnSaid. This is used by the caller
        /// </summary>
        public EventHandler<QueuedTask> SaidEventHandler;
        #endregion SaidEventHandler 

        #region    FunnelledTaskCompleted
        /// <summary>
        /// When the various Played/Recorded/Said/Failed events are invoked these are
        /// propagated to TaskFunnel via a call to TaskFunnel.Completed. This routine
        /// then triggers TaskFunnel.
        /// </summary>
        void FunnelledTaskCompleted()
        {
            Trace($"Entry&Exit Empty()");
        }
        #endregion FunnelledTaskCompleted

        #endregion External EventHandlers 

        #endregion Events & Delegates
    }

    #region    SoundStatus enum
    /// <summary>
    /// The state of the recording there can only be one active recording / playing
    /// </summary>
    public enum SoundStatus
    {
        Idle,       // nothing being recorded or played
        Reserved,   // no sound to be played or recorded although nothing is being played or reserved
        Playing,    // a sound is being played an we are waiting for the OnMediaPlaed/Failed event 
        Recording,  // user is being recorded and will continue till user completion or timeout.
    }
    #endregion SoundStatus enum

    #region    class
    /// <summary>
    /// Used in SetStatus to work out bugs in terms of the Status. There are multiple
    /// Tasks all of which could potentially be setting a Status. 
    /// (or more likely forgetting to set the Status). zzz You can remove
    /// this but I found it useful to keep it in.
    /// </summary>
    public class StatusItem
    {
        public string Member;
        public int Line;
        public SoundStatus SoundStatus;
        [System.Diagnostics.DebuggerStepThrough()]
        public override string ToString() => $"{Member} {@Line} {SoundStatus}";
    }
    #endregion class
}