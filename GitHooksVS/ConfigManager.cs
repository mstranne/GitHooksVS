using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace GitHooksVS
{
    public class ScriptEntry
    {
        public string FilePath { get; set; }
        public bool Enabled { get; set; }
    }


    public class Config
    {
        public Dictionary<HookType, List<ScriptEntry>> HookScripts { get; set; } = new Dictionary<HookType, List<ScriptEntry>>();
    }

    internal class ConfigManager
    {
        private static readonly Lazy<ConfigManager> instance = new Lazy<ConfigManager>(() => new ConfigManager());
        private string configFilePath;
        private Config config;

        // Private constructor to prevent instantiation from outside
        private ConfigManager()
        {
            config = new Config();
        }

        public static ConfigManager Instance
        {
            get
            {
                return instance.Value;
            }
        }

        /// <summary>
        /// Initializes the ConfigManager with the specified configuration file path.
        /// </summary>
        /// <param name="configFilePath">The path to the configuration file.</param>
        public void Initialize(string configFilePath)
        {
            this.configFilePath = configFilePath;
            LoadConfig();
        }

        /// <summary>
        /// Adds a new script entry to the specified hook type.
        /// </summary>
        /// <param name="hookType">The hook type.</param>
        /// <param name="filePath">The file path.</param>
        /// <param name="enabled">The enabled status.</param>
        public void AddScriptEntry(HookType hookType, string filePath, bool enabled)
        {
            if (!config.HookScripts.ContainsKey(hookType))
            {
                config.HookScripts[hookType] = new List<ScriptEntry>();
            }

            var entry = new ScriptEntry
            {
                FilePath = filePath,
                Enabled = enabled
            };
            config.HookScripts[hookType].Add(entry);
            SaveConfig();
        }

        /// <summary>
        /// Deletes a script entry from the specified hook type.
        /// </summary>
        /// <param name="hookType">The hook type.</param>
        /// <param name="filePath">The file path.</param>
        public void DeleteScriptEntry(HookType hookType, string filePath)
        {
            if (config.HookScripts.ContainsKey(hookType))
            {
                config.HookScripts[hookType].RemoveAll(e => e.FilePath == filePath);
                SaveConfig();
            }
        }

        /// <summary>
        /// Gets the list of script entries for the specified hook type.
        /// </summary>
        /// <param name="hookType">The hook type.</param>
        /// <returns>The list of script entries for the specified hook type.</returns>
        public List<ScriptEntry> GetScriptEntries(HookType hookType)
        {
            if (config.HookScripts.ContainsKey(hookType))
            {
                return config.HookScripts[hookType];
            }
            return new List<ScriptEntry>();
        }

        /// <summary>
        /// Checks if a script entry with the specified hook type and file path exists.
        /// </summary>
        /// <param name="hookType">The hook type.</param>
        /// <param name="filePath">The file path.</param>
        /// <returns>True if the script entry exists; otherwise, false.</returns>
        public bool ScriptEntryExists(HookType hookType, string filePath)
        {
            if (config.HookScripts.ContainsKey(hookType))
            {
                return config.HookScripts[hookType].Any(e => e.FilePath == filePath);
            }
            return false;
        }

        /// <summary>
        /// Loads the configuration from the file.
        /// </summary>
        private void LoadConfig()
        {
            if (File.Exists(configFilePath))
            {
                var json = File.ReadAllText(configFilePath);
                config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }
        }

        /// <summary>
        /// Saves the configuration to the file.
        /// </summary>
        private void SaveConfig()
        {
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(configFilePath, json);
        }
    }
}
