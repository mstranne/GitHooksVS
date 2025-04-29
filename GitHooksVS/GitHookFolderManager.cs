using LibGit2Sharp;
using System;
using System.IO;
using Path = System.IO.Path;
using Directory = System.IO.Directory;
using System.Collections.Generic;
using Microsoft.VisualStudio.PlatformUI;
using System.Linq;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio;

namespace GitHooksVS
{
    /// <summary>
    /// Manages Git hook folders and their configurations.
    /// </summary>
    internal class GitHookFolderManager
    {
        /// <summary>
        /// Singleton instance of the <see cref="GitHookFolderManager"/> class.
        /// </summary>
        private static readonly Lazy<GitHookFolderManager> instance = new Lazy<GitHookFolderManager>(() => new GitHookFolderManager());

        /// <summary>
        /// Indicates whether the manager has been initialized.
        /// </summary>
        private bool initialized = false;

        /// <summary>
        /// The root folder of the Git repository.
        /// </summary>
        private string gitRootFolder;

        /// <summary>
        /// The path to the .githooks folder.
        /// </summary>
        private string gitVSHookFolderPath;

        /// <summary>
        /// The path to the .git/hooks folder.
        /// </summary>
        private string gitHookFolderPath;

        /// <summary>
        /// File system watcher for monitoring changes in the .githooks folder.
        /// </summary>
        private FileSystemWatcher fileWatcher;

        /// <summary>
        /// Private constructor to prevent instantiation from outside.
        /// </summary>
        private GitHookFolderManager()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="GitHookFolderManager"/>.
        /// </summary>
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

        /// <summary>
        /// Initializes the GitHookFolderManager by discovering the Git root folder and setting up file watchers.
        /// </summary>
        /// <param name="solutionDir">The directory of the current solution.</param>
        public void Initialize(string solutionDir)
        {
            try
            {
                if (initialized)
                    return;

                // Check the initialization of LibGit2Sharp
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

        /// <summary>
        /// Uninitializes the GitHookFolderManager by resetting its state and unregistering file watchers.
        /// </summary>
        public void Uninitialize()
        {
            if(!initialized)
                return;

            if (fileWatcher != null)
            {
                fileWatcher.EnableRaisingEvents = false;
                fileWatcher.Changed -= OnChanged;
                fileWatcher.Created -= OnChanged;
                fileWatcher.Deleted -= OnChanged;
                fileWatcher.Dispose();
                fileWatcher = null;
            }

            initialized = false;
            gitRootFolder = null;
            gitVSHookFolderPath = null;
            gitHookFolderPath = null;

            Logger.Instance.WriteLine("GitHookFolderManager has been uninitialized.", LogLevel.DEBUG_MESSAGE);
        }

        /// <summary>
        /// Finalizer to ensure cleanup when the object is garbage collected.
        /// </summary>
        ~GitHookFolderManager()
        {
            Uninitialize();
        }

        /// <summary>
        /// Checks if the .githooks folder exists and sets up a file watcher for it.
        /// </summary>
        /// <returns>True if the folder exists and the watcher is set up; otherwise, false.</returns>
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

        /// <summary>
        /// Handles file system changes in the .githooks folder.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
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

                bool enableScript = ShowEnableScriptDialog(Path.GetFileName(e.FullPath), hookType);
                ConfigManager.Instance.AddScriptEntry(hookType, e.FullPath, enableScript);

                if (enableScript)
                {
                    var enabledScipts = ConfigManager.Instance.GetEnabledScriptEntries(hookType);
                    GitHookManager.CreateGitHookScript(enabledScipts.Select(s => s.FilePath).ToList(), gitHookFolderPath, hookType);
                }
            }
            else if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                Logger.Instance.WriteLine($"File {e.ChangeType}: {e.FullPath}", LogLevel.DEBUG_MESSAGE);
                ConfigManager.Instance.DeleteScriptEntry(hookType, e.FullPath);
                var enabledScipts = ConfigManager.Instance.GetEnabledScriptEntries(hookType);
                GitHookManager.CreateGitHookScript(enabledScipts.Select(s => s.FilePath).ToList(), gitHookFolderPath, hookType);
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

        public String GetCurrentHookFolder()
        { 
            return gitHookFolderPath; 
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
                            bool enableScript = ShowEnableScriptDialog(Path.GetFileName(script), hookType);
                            newScripts.Add(new ScriptEntry { FilePath = script, Enabled = enableScript });
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
                        var allScripts = enabledScripts.Concat(newScripts.Where(s => s.Enabled)).ToList();
                        GitHookManager.CreateGitHookScript(allScripts.Select(s => s.FilePath).ToList(), gitHookFolderPath, hookType);

                        foreach (var script in newScripts)
                        {
                            ConfigManager.Instance.AddScriptEntry(hookType, script.FilePath, script.Enabled);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Shows a message box to the user to decide whether a new script should be enabled or disabled.
        /// </summary>
        /// <param name="scriptName">The name of the new script.</param>
        /// <param name="hookType">The type of the Git hook.</param>
        /// <returns>True if the script should be enabled; otherwise, false.</returns>
        private bool ShowEnableScriptDialog(string scriptName, HookType hookType)
        {
            string message = $"Do you want to enable the new {GitHookManager.HookScriptNameLookup[hookType]} script: {scriptName} ?\n";
            string title = "Enable New Script";
            var result = VsShellUtilities.ShowMessageBox(
                ServiceProvider.GlobalProvider,
                message,
                title,
                OLEMSGICON.OLEMSGICON_QUERY,
                OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);

            return result == (int)VSConstants.MessageBoxResult.IDYES;
        }
    }
}
