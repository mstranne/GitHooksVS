﻿using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel.Design;

namespace GitHooksVS
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [ProvideToolWindow(typeof(SettingsWindow))] // Registriert dein Tool Window
    [Guid(GitHooksVSPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class GitHooksVSPackage : AsyncPackage
    {
        /// <summary>
        /// GitHooksVSPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "7a56b71f-f95a-414e-81e3-4aadc5759e59";

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            // Zugriff auf das Output Window erhalten
            DTE2 dte = (DTE2)await GetServiceAsync(typeof(DTE));
            OutputWindowPane outputPane = GetOutputPane(dte, "Git Hook Extension");

            // Initialize Logger
            Logger.Instance.Initialize(outputPane);

            // "Hello World" ausgeben
            Logger.Instance.WriteLine("Hello World from Git Hook Extension!", LogLevel.DEBUG_MESSAGE);

            string solutionDir = Path.GetDirectoryName(dte.Solution.FullName);
            GitHookFolderManager.Instance.Initialize(solutionDir);
            string repoPath = GitHookFolderManager.Instance.GitRootFolder;
            if (!string.IsNullOrEmpty(repoPath))
            {
                string repoName = new DirectoryInfo(repoPath).Name;
                Logger.Instance.WriteLine($"Git Repository gefunden: {repoName}");
            }
            else
            {
                Logger.Instance.WriteLine("Kein Git Repository gefunden.");
            }

            await SettingsWindowCommand.InitializeAsync(this);
        }

        private OutputWindowPane GetOutputPane(DTE2 dte, string paneName)
        {
            OutputWindow outputWindow = dte.ToolWindows.OutputWindow;
            foreach (OutputWindowPane pane in outputWindow.OutputWindowPanes)
            {
                if (pane.Name == paneName)
                    return pane;
            }
            return outputWindow.OutputWindowPanes.Add(paneName);
        }

        #endregion
    }
}
