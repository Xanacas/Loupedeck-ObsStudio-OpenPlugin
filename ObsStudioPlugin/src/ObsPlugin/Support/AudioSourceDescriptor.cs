﻿namespace Loupedeck.ObsStudioPlugin
{
    using System;
    using OBSWebsocketDotNet;

    internal class AudioSourceDescriptor
    {
        public Boolean SpecialSource;
        public Boolean Muted;
        public Single Volume;

        public AudioSourceDescriptor(String name, OBSWebsocket that, Boolean isSpecSource = false)
        {
            this.Muted = false;
            this.Volume = 0;
            this.SpecialSource = isSpecSource;

            try
            {
                var v = that.GetVolume(name);
                this.Muted = v.Muted;
                this.Volume = v.Volume;
            }
            catch (Exception ex)
            {
                ObsStudioPlugin.Trace($"Exception {ex.Message} getting volume information for source {name}");
            }
        }
    }


}
