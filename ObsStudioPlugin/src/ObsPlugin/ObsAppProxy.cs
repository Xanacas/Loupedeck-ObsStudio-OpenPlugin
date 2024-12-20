﻿namespace Loupedeck.ObsStudioPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    using OBSWebsocketDotNet.Communication;
    using OBSWebsocketDotNet.Types.Events;

    /// <summary>
    /// Proxy to OBS websocket server, for API reference see
    /// https://github.com/obsproject/obs-websocket/blob/4.x-compat/docs/generated/protocol.md
    /// </summary>
    internal partial class ObsAppProxy : OBSWebsocketDotNet.OBSWebsocket
    {
        // Our 'own' events
        public event EventHandler<EventArgs> AppConnected;
        public event EventHandler<EventArgs> AppDisconnected;

        public ObsStudioPlugin Plugin { get; private set; }

        // Properties
        public Boolean IsAppConnected => this.IsConnected;

        // Folders to select from when we try saving screenshots
        public static readonly Environment.SpecialFolder[] ScreenshotFolders =
            {                
                Environment.SpecialFolder.MyPictures,
                Environment.SpecialFolder.MyDocuments,
                Environment.SpecialFolder.Personal,
                Environment.SpecialFolder.CommonPictures
            };

        public ObsAppProxy(ObsStudioPlugin _plugin)
        {
            this.Plugin = _plugin;

            // Trying to set screenshot save-to path
            for(var i=0; (i< ScreenshotFolders.Length) && String.IsNullOrEmpty(ObsAppProxy.ScreenshotsSavingPath); i++)
            {
                var folder = Environment.GetFolderPath(ScreenshotFolders[i]);
                if (Directory.Exists(folder))
                {
                    ObsAppProxy.ScreenshotsSavingPath = folder;
                }
            }
        }
        public void RegisterAppEvents()
        {
            //Mapping OBS Websocket events to ours
            this.Connected += this.OnAppConnected;
            this.Disconnected += this.OnAppDisconnected;
        }

        public void UnregisterAppEvents()
        {
            //Unmapping OBS Websocket events 
            this.Connected -= this.OnAppConnected;
            this.Disconnected -= this.OnAppDisconnected;
        }

        private Boolean _scene_collection_events_subscribed = false;

        private void UnsubscribeFromSceneCollectionEvents()
        {
            if (!this._scene_collection_events_subscribed)
            {
                this.CurrentProgramSceneChanged -= this.OnObsSceneChanged;
                this.CurrentPreviewSceneChanged -= this.OnObsPreviewSceneChanged;
        
                this.SceneItemEnableStateChanged  -= this.OnObsSceneItemVisibilityChanged;
                this.SceneItemCreated -= this.OnObsSceneItemAdded;
                this.SceneItemRemoved -= this.OnObsSceneItemRemoved;

                this.InputCreated -= this.OnObsInputCreated;
                this.InputRemoved -= this.OnObsInputDestroyed;

                this.InputMuteStateChanged -= this.OnObsInputMuteStateChanged;
                this.InputVolumeChanged -= this.OnObsInputVolumeChanged;

                this.InputNameChanged -= this.OnObsSourceNameChanged;

                this.SourceFilterCreated -= this.OnObsSourceFilterCreated;
                this.SourceFilterRemoved -= this.OnObsSourceFilterRemoved;
                this.SourceFilterEnableStateChanged -= this.OnObsSourceFilterEnableStateChanged;
                this.SourceFilterNameChanged -= this.OnObsSourceFilterNameChanged;

#if false
                this.SourceAudioActivated -= this.OnObsSourceAudioActivated;
                this.SourceAudioDeactivated -= this.OnObsSourceAudioDeactivated;
#endif
                this._scene_collection_events_subscribed = true;
            }
        }

        private void SubscribeToSceneCollectionEvents()
        {
            if (this._scene_collection_events_subscribed)
            {
                this.CurrentProgramSceneChanged += this.OnObsSceneChanged;
                this.CurrentPreviewSceneChanged += this.OnObsPreviewSceneChanged;

                this.SceneItemEnableStateChanged += this.OnObsSceneItemVisibilityChanged;

                this.SceneItemCreated += this.OnObsSceneItemAdded;
                this.SceneItemRemoved += this.OnObsSceneItemRemoved;

                this.InputCreated += this.OnObsInputCreated;
                this.InputRemoved += this.OnObsInputDestroyed;

                this.InputMuteStateChanged += this.OnObsInputMuteStateChanged;
                this.InputVolumeChanged += this.OnObsInputVolumeChanged;

                this.InputNameChanged += this.OnObsSourceNameChanged;

                this.SourceFilterCreated += this.OnObsSourceFilterCreated;
                this.SourceFilterRemoved += this.OnObsSourceFilterRemoved;
                this.SourceFilterEnableStateChanged += this.OnObsSourceFilterEnableStateChanged;
                this.SourceFilterNameChanged += this.OnObsSourceFilterNameChanged;

/*
            this.InputSettingsChanged += this.OnObsInputSettingsChanged;
            this.InputActiveStateChanged += this.OnObsInputActiveStateChanged;
            this.InputShowStateChanged += this.OnObsInputShowStateChanged;

            this.InputAudioBalanceChanged += this.OnObsInputAudioBalanceChanged;
            this.InputAudioSyncOffsetChanged += this.OnObsinputAudioSyncOffsetChanged;
            this.InputAudioTracksChanged += this.OnObsInputAudioTracksChanged;
            this.InputAudioMonitorTypeChanged += this.OnObsInputAudioMonitorTypeChanged;
            this.InputVolumeMeters += this.OnObsInputVolumeMeters;
*/


#if false
                this.SourceAudioActivated += this.OnObsSourceAudioActivated;
                this.SourceAudioDeactivated += this.OnObsSourceAudioDeactivated;
#endif
                this._scene_collection_events_subscribed = false;
            }
        }

/*
 
        private void OnObsInputActiveStateChanged(Object sender, InputActiveStateChangedEventArgs e)
        {
            this.Plugin.Log.Info($"Entering {MethodBase.GetCurrentMethod().Name}");
        }

        private void OnObsInputShowStateChanged(Object sender, InputShowStateChangedEventArgs e)
        {
            this.Plugin.Log.Info($"Entering {MethodBase.GetCurrentMethod().Name}");
        }

        private void OnObsInputAudioBalanceChanged(Object sender, InputAudioBalanceChangedEventArgs e)
        {
            this.Plugin.Log.Info($"Entering {MethodBase.GetCurrentMethod().Name}");
        }

        private void OnObsinputAudioSyncOffsetChanged(Object sender, InputAudioSyncOffsetChangedEventArgs e)
        {
            this.Plugin.Log.Info($"Entering {MethodBase.GetCurrentMethod().Name}");
        }

        private void OnObsInputAudioTracksChanged(Object sender, InputAudioTracksChangedEventArgs e)
        {
            this.Plugin.Log.Info($"Entering {MethodBase.GetCurrentMethod().Name}");
        }

        private void OnObsInputAudioMonitorTypeChanged(Object sender, InputAudioMonitorTypeChangedEventArgs e)
        {

            this.Plugin.Log.Info($"Entering {MethodBase.GetCurrentMethod().Name}");
        }

        private void OnObsInputVolumeMeters(Object sender, InputVolumeMetersEventArgs e)
        {
            this.Plugin.Log.Info($"Entering {MethodBase.GetCurrentMethod().Name}");
        }
*/

        private const UInt32 MAX_OBS_FETCH_ATTEMPTS = 10;

        internal void InitializeObsData(Object sender, EventArgs e)
        {
            // NOTE: This can throw! Exception handling is done OUTSIDE of this method
            var studioModeStatus = false;

            this.Plugin.Log.Info("Init: GetStudioModeEnabled");

            //With GetStudioModeEnabled() we try to proble whether OBS is ready for calls.
            var attempt = 0;
            while (!Helpers.TryExecuteFunc(() => this.GetStudioModeEnabled(), out studioModeStatus))
            {
                this.Plugin.Log.Warning("GetStudioModeEnabled failed. Assuming OBS is starting");
                System.Threading.Thread.Sleep(1000);
                if (attempt ++ > MAX_OBS_FETCH_ATTEMPTS)
                {
                    throw new Exception("Cannot get Studio Mode status. Giving up.");
                }
            }

            if(!Helpers.TryExecuteFunc(() => this.GetSceneList(), out var scenes))
            {
                this.Plugin.Log.Warning("Cannot retreive scenes");
            }

            if (!Helpers.TryExecuteFunc(() => this.GetStreamStatus(), out var streamingStatus))
            {
                this.Plugin.Log.Warning("Cannot retreive streaming status");
            }

            //var recordStatus = this.GetRecordStatus();
            if(! Helpers.TryExecuteFunc(() => this.GetRecordStatus(), out var recordStatus))
            {
                this.Plugin.Log.Warning("Cannot retreive recording status");
            }   
            
            if( !Helpers.TryExecuteFunc(() => this.GetVirtualCamStatus(), out var vcamstatus))
            {
                this.Plugin.Log.Warning("Cannot retreive virtual camera status");
            }

            // Retreiving Audio types.
            // this.OnAppConnected_RetreiveSourceTypes();

            if (streamingStatus != null)
            {
                this._currentStreamingState = streamingStatus.IsActive
                    ? OBSWebsocketDotNet.Types.OutputState.OBS_WEBSOCKET_OUTPUT_STARTED
                    : OBSWebsocketDotNet.Types.OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED;

                this.OnObsRecordingStateChange(this, recordStatus.IsRecording 
                    ? OBSWebsocketDotNet.Types.OutputState.OBS_WEBSOCKET_OUTPUT_STARTED
                    : OBSWebsocketDotNet.Types.OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED);

                this.OnObsStreamingStateChange(this, streamingStatus.IsActive
                    ? OBSWebsocketDotNet.Types.OutputState.OBS_WEBSOCKET_OUTPUT_STARTED
                    : OBSWebsocketDotNet.Types.OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED);
            }

            if (vcamstatus != null)
            {
                var arg = new OBSWebsocketDotNet.Types.Events.VirtualcamStateChangedEventArgs(new OBSWebsocketDotNet.Types.OutputStateChanged());
                arg.OutputState.IsActive = vcamstatus.IsActive;
                this.OnObsVirtualCameraStateChanged(sender, arg);
            }

            this.OnObsStudioModeStateChanged(sender, studioModeStatus);

            this.Plugin.Log.Info("Init: OnObsSceneCollectionListChanged");

            var collections = new List<String>();

            
            if (Helpers.TryExecuteSafe(() => collections = this.GetSceneCollectionList()))
            {
                this.Plugin.Log.Info($"Retreieved { collections?.Count } scenes in SceneCollectionList");
            }
            else
            {
                this.Plugin.Log.Warning($"Cannot retreive Scene Collections");
            }

            this.OnObsSceneCollectionListChanged(sender, new SceneCollectionListChangedEventArgs(collections));


            this.Plugin.Log.Info("Init: OnObsSceneCollectionChanged");
            var currentCollection = String.Empty;
            if (Helpers.TryExecuteSafe(() => { currentCollection = this.GetCurrentSceneCollection(); }))
            {
                this.Plugin.Log.Info($"Retreieved current scene collection {currentCollection}");
            }
            else
            {
                this.Plugin.Log.Warning($"Cannot retreive current scene collection");
            }

            // This should initiate retreiving of all data
            // to indicate that we need to force rescan of all scenes and all first parameter is null 
            this.OnObsSceneCollectionChanged(null , new CurrentSceneCollectionChangedEventArgs(currentCollection));
        }

        private void OnAppConnected(Object sender, EventArgs e)
        {
            this.Plugin.Log.Info("Entering AppConnected");

            // Subscribing to App events
            // Notifying all subscribers on App Connected
            // Fetching initial states for controls
            this.RecordStateChanged += this.OnObsRecordingStateChange;
            this.StreamStateChanged += this.OnObsStreamingStateChange;
            this.VirtualcamStateChanged += this.OnObsVirtualCameraStateChanged;
            this.StudioModeStateChanged += this.OnObsStudioModeStateChanged;
            this.ReplayBufferStateChanged += this.OnObsReplayBufferStateChange;

            this.SceneCollectionListChanged += this.OnObsSceneCollectionListChanged;
            this.CurrentSceneCollectionChanged += this.OnObsSceneCollectionChanged;
            this.CurrentSceneCollectionChanging += this.OnObsSceneCollectionChanging;

            this.SceneCreated += this.OnObsSceneCreated;
            this.SceneRemoved += this.OnObsSceneRemoved;
            this.SceneNameChanged += this.OnObsSceneNameChanged;

            //this.SceneTransitionEnded += this.OnObsTransitionEnd;
            this.SceneListChanged += this.OnObsSceneListChanged;

            this.AppConnected?.Invoke(sender, e);

            this.Plugin.Log.Info("AppConnected: Initializing data");
            _ = Helpers.TryExecuteSafe(() =>
            {
                this.InitializeObsData(sender, e);
            });

            // Subscribing to all the events that are depenendent on Scene Collection change
            this._scene_collection_events_subscribed = true;
            this.SubscribeToSceneCollectionEvents();
        }

        private void OnAppDisconnected(Object sender, ObsDisconnectionInfo arg)
        {
            this.Plugin.Log.Info($"Entering AppDisconnected. Disconnect reason:\"{arg.DisconnectReason}\"");
            this.Plugin.OBSWebsocketServerLastDisconnectReason = arg.DisconnectReason;


            this.RecordStateChanged -= this.OnObsRecordingStateChange;
            this.StreamStateChanged -= this.OnObsStreamingStateChange;
            this.VirtualcamStateChanged -= this.OnObsVirtualCameraStateChanged;
            this.StudioModeStateChanged -= this.OnObsStudioModeStateChanged;
            this.ReplayBufferStateChanged -= this.OnObsReplayBufferStateChange;

            this.SceneCollectionListChanged -= this.OnObsSceneCollectionListChanged;
            this.CurrentSceneCollectionChanged -= this.OnObsSceneCollectionChanged;
            this.CurrentSceneCollectionChanging -= this.OnObsSceneCollectionChanging;

            this.SceneListChanged -= this.OnObsSceneListChanged;
            this.SceneCreated -= this.OnObsSceneCreated;
            this.SceneRemoved -= this.OnObsSceneRemoved;

            this.SceneNameChanged -= this.OnObsSceneNameChanged;

            //this.TransitionEnd -= this.OnObsTransitionEnd;

            // Unsubscribing from all the events that are depenendent on Scene Collection change
            this._scene_collection_events_subscribed = false;
            this.UnsubscribeFromSceneCollectionEvents();

            this.AppDisconnected?.Invoke(sender, new System.EventArgs() );
        }

        private void SafeRunConnected(Action action, String warning) 
        {
            if (this.IsAppConnected)
            {
                if (!Helpers.TryExecuteSafe(action))
                {
                    this.Plugin.Log.Warning(warning);
                }
            }
        }
    }
}
