﻿namespace Loupedeck.ObsStudioPlugin
{
    using System;
    using System.IO;

    public partial class ObsStudioPlugin : Plugin
    {
        // Note that plugin data directory MUST be created in Install, not InstallAdmin,
        //      otherwise we can have wrong permissions there
        public override Boolean Install()
        {
            if (!IoHelpers.EnsureDirectoryExists(this.GetPluginDataDirectory()))
            {
                Tracer.Error("Cannot create data directory ");

                // Note, if this is not possible it is something really not good with LD installation
                return false;
            }

            this.Log.Info($"Install: OBS Installed: {this.ClientApplication.GetApplicationStatus() == ClientApplicationStatus.Installed}. Ini file exists/good:{this.IniFile.iniFileExists}/{this.IniFile.iniFileGood}");
            this.IniFile ??= new ObsIniFile(this);

            if (this.ClientApplication.GetApplicationStatus() == ClientApplicationStatus.Installed
                        && this.IniFile.iniFileExists
                        && !this.IniFile.iniFileGood
                        )
            {
                this.Log.Info("Install: OBS Installed but INI file is bad, fixing");
// TODO: REMOVE INIFILE ding und nutze OBSConfigFile inzukunft. 
// TODO: ON INSTALL: create template configfile oder lade bestehende datei.
                //Attempting to fix ini file if it is not good. Can only be done when app is not running (for Portable app we need to know its location first
                this.IniFile.FixIniFile();
            }

            if(!IoHelpers.FileExists(this.GetPluginDataDirectory() + "config.json")) {
                IoHelpers.CreateEmptyFile(this.GetPluginDataDirectory() + "config.json");
                IoHelpers.WriteTextFile(this.GetPluginDataDirectory() + "config.json", "{serverPort: 4455, server: \"localhost\", password: \"\"}");
            }

            this.Update_PluginStatus();



            return true;
        }

    }
}