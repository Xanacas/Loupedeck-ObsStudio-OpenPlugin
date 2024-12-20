﻿
namespace Loupedeck.ObsStudioPlugin
{
    using System.Collections.Generic;
    using System;
    using System.Dynamic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.CodeDom;
    using System.Text;


    /*
     Ini file state:
        Server enabled  (E)
        Server disabled (D)

    OBS state:
        Running (R)
        NOT running (N)

    OBS installation state:
        Installed (I)
        NOT installed OR portable (U)


    if R and E ->  Connect

    NOTE: During installation, if OBS is I, we try fixing INI FILE
    if R and D ->  Monitor ApplicationClose and fix the INI file when OBS is closed  . Set appropriate status ("Please restart OBS"). 
    if N and I ->  IF D, fix INI FILE

    if R and U and D -> Portable mode -> Detect INI File location and monitor AppClose. Set appropriate status. 
    if N and U -> EIther not there or portable mode -> Set appropriate status. ("Cannot detect OBS installation")
     */

    class ObsIniFile
    {

        private const String DEFAULT_PASSWORD = "NeverUseThis";
        private const String DEFAULT_SERVER = "localhost";
        private const Int32 DEFAULT_PORT = 4455;
        private const String ObsIniServerSection= "OBSWebSocket";
        public ObsStudioPlugin Plugin { get; private set; }
        public String ServerAddress { get; private set; } = DEFAULT_SERVER;
        public String ServerPassword { get; private set; } = DEFAULT_PASSWORD;
        public Int32 ServerPort { get; private set; } = DEFAULT_PORT;

        // True if iniFileExists
        public Boolean iniFileExists { get; private set; } = false;

        // True if we can get all settings from Ini file. 
        public Boolean iniFileGood { get; private set; } = false;

        private String __iniFilePath = null;
        private String _iniFilePath { 
            get => this.__iniFilePath;

            set
            {
                this.__iniFilePath = value; 
                this.ReadIniFile();
            }
        }

        public void SetPortableIniPath(String processPath)
        {
            //FIXME: Different for MAC
            // Get the directory of the file
            var directoryInfo = new DirectoryInfo(Path.GetDirectoryName(processPath));

            // Move up two levels
            if (directoryInfo.Parent != null && directoryInfo.Parent.Parent != null)
            {
                var twoLevelsUp = directoryInfo.Parent.Parent;
                this._iniFilePath  = $"{twoLevelsUp.FullName}\\config\\obs-studio\\global.ini";
                this.Plugin.Log.Info($"Found a path for portable installation, {this._iniFilePath}");
            }
            else
            {
                this.Plugin.Log.Error($"Cannot derive ini file path from portable process path {processPath}");
            }

        }

        public ObsIniFile(ObsStudioPlugin parent)
        {
            this.Plugin = parent;   
            // this.ServerAddress = server ?? DEFAULT_SERVER;
            // this.ServerPort = port ?? DEFAULT_PORT;
            // this.ServerPassword = password ?? DEFAULT_PASSWORD;
            
            this._iniFilePath = Helpers.IsWindows() 
                            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\obs-studio\\global.ini"
                            : Environment.GetEnvironmentVariable("HOME") + "/Library/Application Support/obs-studio/global.ini";
            this.Plugin.Log.Info($"Will try OBS Ini at {this._iniFilePath}");


        }

        private IniFile m_IniFile = null; 

        public void ReadIniFile()
        {
            this.iniFileGood = false;
            this.iniFileExists = File.Exists(this._iniFilePath);
            if (!this.iniFileExists)
            {
                return;
            }

            

            this.m_IniFile = new IniFile(this._iniFilePath);

            var password = this.m_IniFile.GetValue(ObsIniServerSection, "ServerPassword");
            var port = this.m_IniFile.GetValue(ObsIniServerSection, "ServerPort");
            this.ServerPassword = password ?? this.ServerPassword;
            this.ServerPort = Int32.TryParse(port, out var p) ? p : this.ServerPort;
            
            var serverEnabled = this.m_IniFile.GetValue(ObsIniServerSection, "ServerEnabled", "false");
            var authRequired = this.m_IniFile.GetValue(ObsIniServerSection, "AuthRequired", "false");
            this.iniFileGood = !password.IsNullOrEmpty() && !port.IsNullOrEmpty() && serverEnabled.EqualsNoCase("true") && authRequired.EqualsNoCase("true") /*Hypothetically we need to ensure password is non-zero*/;

            if (this.iniFileGood) {
                // File contains a complete WebSocket Section
                if(this.ServerPort != this.Plugin.OBSServerPort || this.ServerPassword != this.Plugin.OBSServerPassword)
                {
                    this.Plugin.Log.Info($"ReadIniFile(): File contains a complete WebSocket Section. Updating plugin settings from OBS Ini with {this.ServerAddress}, {this.ServerPort}, {this.ServerPassword.Substring(0,4)}******");
                    this.Plugin.UpdatePluginSettings(this.ServerAddress, this.ServerPort, this.ServerPassword);
                }
                
            } else if(this.Plugin.IsPluginConfigured) {
            
                this.Plugin.Log.Info($"ReadIniFile(): File does not contain a complete WebSocket Section. Existing PluginSettings remain. Plugin settings will not be updated from OBS Ini");
            }else {

            this.Plugin.Log.Warning($"ReadIniFile(): OBS Connection Details not configured, not in the PluginStore and not in OBS Ini. Plugin will not work until connection details are configured.");
            }
            

            // this.Plugin.Log.Info($"Read init file from '{this.__iniFilePath}': serverEnabled:{serverEnabled}, authRequired:{authRequired}");

            // //this.iniFileGood = serverEnabled.EqualsNoCase("true") && authRequired.EqualsNoCase("true") /*Hypothetically we need to ensure password is non-zero*/;
            // this.iniFileGood = (this.ServerPassword.Length > 10 && this.ServerPassword != DEFAULT_PASSWORD && this.ServerPort > 0) ? true : false ;
            // this.Plugin.Log.Info($"ReadIniFile: this.iniFileGood:{this.iniFileGood},this.ServerAddress:{this.ServerAddress} this.ServerPassword.Substring(0,5):{this.ServerPassword.Substring(0,5)}, this.ServerPort:{this.ServerPort}");
            /*FirstLoad = false
            ServerEnabled = true
            ServerPort = 4455
            AlertsEnabled = false
            AuthRequired = true
            ServerPassword = 111222333
            */
        }

        private readonly Random random = new Random();
        //Generated 20 characters long random string
        private String GenerateServerPassword()
        {
            const Int32 length = 20;
            var s = "";

            for (var i = 0; i < length; i++)
            {
                s += (Char)this.random.Next(32, 127);
            }

            return s;
        }

        public Boolean FixIniFile()
        {
            if (!this.iniFileExists)
            {
                this.Plugin.Log.Error($"Cannot fix OBS ini file: File not exist ");
                return false;
            }

            if (this.iniFileGood)
            {
                this.Plugin.Log.Info($"File is already good, no need to fix");
                return true;
            }

            this.Plugin.Log.Info($"Fixing ini file");

            var none_val = "NONE";

            if (!this.m_IniFile.SectionExists(ObsIniServerSection))
            {
                this.m_IniFile.Add(ObsIniServerSection, new Dictionary<String, String>(StringComparer.InvariantCultureIgnoreCase));
            }

            var value = this.m_IniFile.GetValue(ObsIniServerSection, "ServerPassword", none_val);

            if( value == none_val || value.Length < 10 )
            {
                this.m_IniFile[ObsIniServerSection]["ServerPassword"] = this.GenerateServerPassword();   
            }

            value = this.m_IniFile.GetValue(ObsIniServerSection, "ServerPort", none_val);
            if (value == none_val)
            {
                this.m_IniFile[ObsIniServerSection]["ServerPort"] = DEFAULT_PORT.ToString();    
            }

            this.m_IniFile[ObsIniServerSection]["AuthRequired"] = "true";
            this.m_IniFile[ObsIniServerSection]["ServerEnabled"] = "true";

            if (this.m_IniFile.WriteIniFile(this._iniFilePath))
            {
                this.Plugin.Log.Info($"Updated ini file written to {this._iniFilePath}");
                
                this.ReadIniFile();
                this.Plugin.Log.Info($"FixIniFile: Update plugin settings");
                this.Plugin.UpdatePluginSettings(this.ServerAddress, this.ServerPort, this.ServerPassword);
                return true;
            }
            else
            {
                this.Plugin.Log.Error($"Cannot write OBS init file to {this._iniFilePath}");
                return false;
            }
                

        }


        class IniFile: Dictionary<String, Dictionary<String, String>>
        {

            //private readonly Dictionary<String, Dictionary<String, String>> ini = ;

            public IniFile(String file): base(StringComparer.InvariantCultureIgnoreCase)
            {
            
                var txt = File.ReadAllText(file);

                var currentSection = new Dictionary<String, String>(StringComparer.InvariantCultureIgnoreCase);

                this[""] = currentSection;

                foreach (var l in txt.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var line = l.Trim();
                    if (line.StartsWith(";"))
                    {
                        continue;
                    }

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = new Dictionary<String, String>(StringComparer.InvariantCultureIgnoreCase);
                        this[line.Substring(1, line.LastIndexOf("]") - 1)] = currentSection;
                        continue;
                    }

                    var idx = line.IndexOf("=");
                    if (idx == -1)
                    {
                        continue;
                    }

                    currentSection[line.Substring(0, idx).Trim()] = line.Substring(idx + 1).Trim();
                }
            }
            
            public String GetValue(String section,  String key = "",  String defaultValue = "") => this.SectionExists(section) && this[section].ContainsKey(key) ? this[section][key] : defaultValue;
            public Boolean SectionExists(String section) => this.ContainsKey(section);

            public Boolean WriteIniFile(String filename)
            {
                try
                {
                    using (var sw = new StreamWriter(filename))
                    {
                        foreach (var section in this)
                        {
                            if (!String.IsNullOrEmpty(section.Key))
                            {
                                sw.WriteLine($"[{section.Key}]");
                            }

                            foreach (var pair in section.Value)
                            {
                                sw.WriteLine($"{pair.Key}={pair.Value}");
                            }

                            sw.WriteLine();
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    ObsStudioPlugin.Proxy.Plugin.Log.Error($"Exception {ex.Message} when writing ini file {filename}");
                    return false;
                }
            }
        }
    }


}
