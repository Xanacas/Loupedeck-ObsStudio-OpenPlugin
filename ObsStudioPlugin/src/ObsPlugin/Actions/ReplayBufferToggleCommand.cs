﻿namespace Loupedeck.ObsStudioPlugin.Actions
{
    using System;

    internal class ReplayBufferToggleCommand : GenericOnOffSwitch
    {
        public ReplayBufferToggleCommand()
            : base(
                name: "ReplayBufferToggle",
                displayName: "Replay Buffer Toggle",
                description: "Starts/Stops the Replay Buffer in OBS Studio",
                groupName: "",
                offStateName: "Start recording into the Replay Buffer",
                onStateName: "Stop recording into the Replay Buffer",
                offStateImage: "STREAM_StartReplayBuffer.png",
                onStateImage: "STREAM_StopReplayBuffer.png")
        {
        }

        protected override void ConnectAppEvents(EventHandler<EventArgs> eventSwitchedOff, EventHandler<EventArgs> eventSwitchedOn)
        {
            ObsStudioPlugin.Proxy.AppEvtReplayBufferOff += eventSwitchedOff;
            ObsStudioPlugin.Proxy.AppEvtReplayBufferOn += eventSwitchedOn;
        }

        protected override void DisconnectAppEvents(EventHandler<EventArgs> eventSwitchedOff, EventHandler<EventArgs> eventSwitchedOn)
        {
            ObsStudioPlugin.Proxy.AppEvtReplayBufferOff -= eventSwitchedOff;
            ObsStudioPlugin.Proxy.AppEvtReplayBufferOn -= eventSwitchedOn;
        }

        protected override void RunToggle() => ObsStudioPlugin.Proxy.AppToggleReplayBuffer();
    }
}
