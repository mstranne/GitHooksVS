using LibGit2Sharp;
using System;
using System.IO;
using Path = System.IO.Path;
using Directory = System.IO.Directory;
using System.Collections.Generic;
using Microsoft.VisualStudio.PlatformUI;
using System.Linq;

namespace GitHooksVS
{
    internal class GitHookFolderManager
    {
        /// <summary>
        ///  implementation ensures that the singleton instance is created in a thread-safe manner 
        ///  without requiring explicit locks or synchronization. This is particularly important in 
        ///  multithreaded applications where multiple threads might try to access the singleton instance simultaneously.
        ///  
        /// Lazy<T> ensures that the singleton instance is only created when it is first accessed.
        /// </summary>
        private static readonly Lazy<GitHookFolderManager> instance = new Lazy<GitHookFolderManager>(() => new GitHookFolderManager());

        private bool initialized = false;
        private string gitRootFolder;
        private string gitVSHookFolderPath;     // Folder .githooks
        private string gitHookFolderPath;       // Folder .git/hooks
        private FileSystemWatcher fileWatcher;

        // Private constructor to prevent instantiation from outside
        private GitHookFolderManager()
        {

        }

        /// <summary>
        /// Inizialized the GitHookFolderManager, by finding Root Folder of Git
        /// 
        /// </summary>
        /// <param name="solutionDir"></param>
        public void Initialize(string solutionDir)
        {
            try
            {
                if (initialized)
                    return;

                // Überprüfen der Initialisierung von LibGit2Sharp
                GlobalSettings.Version.ToString();

                string repoPath = Repository.Discover(solutionDir);
                if (string.IsNullOrEmpty(repoPath))
                    return;

                gitRootFolder = repoPath.TrimEnd(Path.DirectorySeparatorChar);
                // If the discovered path ends with ".git", get the parent directory
                if (gitRootFolder.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                {
                    gitRootFolder = Directory.GetParent(gitRootFolder).FullName;
                    gitHookFolderPath = Path.Combine(gitRootFolder, ".git", "hooks");
                }

                if (!CheckForGitHookFolder())
                    return;

                ConfigManager.Instance.Initialize(Path.Combine(gitVSHookFolderPath, "config.json"));

                ProcessGitHookFolder(gitVSHookFolderPath);

                initialized = true;
            }
            catch (TypeInitializationException ex)
            {
                Logger.Instance.WriteLine($"TypeInitializationException: {ex.Message}", LogLevel.ALWAYS_OUTPUT);
                if (ex.InnerException != null)
                {
                    Logger.Instance.WriteLine($"InnerException: {ex.InnerException.Message}", LogLevel.ALWAYS_OUTPUT);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.WriteLine($"Exception: {ex.Message}", LogLevel.ALWAYS_OUTPUT);
            }
        }

        public static GitHookFolderManager Instance
        {
            get
            {
                return instance.Value;
            }
        }

        /// <summary>
        /// Gets or sets the Git repository path.
        /// </summary>
        public string GitRootFolder
        {
            get { return gitRootFolder; }
            private set { gitRootFolder = value; }
        }

        private bool CheckForGitHookFolder()
        {
            gitVSHookFolderPath = Path.Combine(gitRootFolder, ".githooks");
            if (Directory.Exists(gitVSHookFolderPath))
            {
                fileWatcher = new FileSystemWatcher
                {
                    Path = gitVSHookFolderPath,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    Filter = "*.*",
                    IncludeSubdirectories = true
                };

                fileWatcher.Changed += OnChanged;
                fileWatcher.Created += OnChanged;
                fileWatcher.Deleted += OnChanged;

                fileWatcher.EnableRaisingEvents = true;

                Logger.Instance.WriteLine($"File Watcher Initialized", LogLevel.DEBUG_MESSAGE);
                return true;
            }
            return false;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            string hookFolder = GetSubfolderAndFile(e.FullPath);
            if (hookFolder != null && GitHookManager.TryParseHookFolder(hookFolder, out HookType hookType))
            {
                Logger.Instance.WriteLine($"Hook Folder: {hookFolder}", LogLevel.DEBUG_MESSAGE);
            }
            else
            {
                Logger.Instance.WriteLine($"File ({e.FullPath}) not inside a valid Hook Folder", LogLevel.DEBUG_MESSAGE);
                return;
            }

            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                Logger.Instance.WriteLine($"File {e.ChangeType}: {e.FullPath}, do nothing", LogLevel.DEBUG_MESSAGE);
            }
            else if (e.ChangeType == WatcherChangeTypes.Created)
            {
                Logger.Instance.WriteLine($"File {e.ChangeType}: {e.FullPath}", LogLevel.DEBUG_MESSAGE);
                List<String> scripts = GitHookManager.GetValidShScripts(Path.Combine(gitVSHookFolderPath, hookFolder));
                GitHookManager.CreateGitHookScript(scripts, gitHookFolderPath, hookType);
                ConfigManager.Instance.AddScriptEntry(hookType, e.FullPath, true);

            }
            else if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                Logger.Instance.WriteLine($"File {e.ChangeType}: {e.FullPath}", LogLevel.DEBUG_MESSAGE);

                List<String> scripts = GitHookManager.GetValidShScripts(Path.Combine(gitVSHookFolderPath, hookFolder));
                GitHookManager.CreateGitHookScript(scripts, gitHookFolderPath, hookType);
                ConfigManager.Instance.DeleteScriptEntry(hookType, e.FullPath);
            }
        }

        /// <summary>
        /// Gets the subfolder and file from the provided file path relative to the gitVSHookFolderPath.
        /// </summary>
        /// <param name="filePath">The file path to be processed.</param>
        /// <returns>The subfolder as a string if exactly one folder and one file remain, otherwise null.</returns>
        public string GetSubfolderAndFile(string filePath)
        {
            if (string.IsNullOrEmpty(gitVSHookFolderPath) || string.IsNullOrEmpty(filePath))
                return null;

            // Ensure the file path starts with the gitVSHookFolderPath
            if (!filePath.StartsWith(gitVSHookFolderPath))
                return null;

            // Get the relative path
            string relativePath = filePath.Substring(gitVSHookFolderPath.Length).TrimStart(Path.DirectorySeparatorChar);

            // Split the relative path into parts
            string[] parts = relativePath.Split(Path.DirectorySeparatorChar);

            // Check if there is exactly one folder and one file
            if (parts.Length == 2 && Directory.Exists(Path.Combine(gitVSHookFolderPath, parts[0])) && File.Exists(filePath))
            {
                return parts[0];
            }

            return null;
        }

        /// <summary>
        /// Processes the Git hook folder and updates the configuration.
        /// </summary>
        /// <param name="gitVSHookFolderPath">The path to the Git hook folder.</param>
        public void ProcessGitHookFolder(string gitVSHookFolderPath)
        {
            if (!Directory.Exists(gitVSHookFolderPath))
                return;

            var subfolders = Directory.GetDirectories(gitVSHookFolderPath);
            foreach (var subfolder in subfolders)
            {
                var folderName = Path.GetFileName(subfolder);
                if (GitHookManager.TryParseHookFolder(folderName, out HookType hookType))
                {
                    var validScripts = GitHookManager.GetValidShScripts(subfolder);
                    var newScripts = new List<ScriptEntry>();
                    var deletedScripts = new List<ScriptEntry>();

                    foreach (var script in validScripts)
                    {
                        if (!ConfigManager.Instance.ScriptEntryExists(hookType, script))
                        {
                            newScripts.Add(new ScriptEntry { FilePath = script, Enabled = true });
                        }
                    }

                    var existingScripts = ConfigManager.Instance.GetScriptEntries(hookType);
                    foreach (var existingScript in existingScripts)
                    {
                        if (!validScripts.Contains(existingScript.FilePath))
                        {
                            deletedScripts.Add(existingScript);
                        }
                    }

                    if (newScripts.Any() || deletedScripts.Any())
                    {
                        foreach (var script in deletedScripts)
                        {
                            ConfigManager.Instance.DeleteScriptEntry(hookType, script.FilePath);
                        }

                        var enabledScripts = ConfigManager.Instance.GetEnabledScriptEntries(hookType);
                        var allScripts = enabledScripts.Concat(newScripts).ToList();
                        GitHookManager.CreateGitHookScript(allScripts.Select(s => s.FilePath).ToList(), gitHookFolderPath, hookType);

                        foreach (var script in newScripts)
                        {
                            ConfigManager.Instance.AddScriptEntry(hookType, script.FilePath, script.Enabled);
                        }
                    }
                }
            }
        }

    }
}
