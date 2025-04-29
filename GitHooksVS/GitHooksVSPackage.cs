
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

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
    [Guid(GitHooksVSPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(HookManageForm))]
    public sealed class GitHooksVSPackage : AsyncPackage, IVsSolutionEvents
    {
        /// <summary>
        /// GitHooksVSPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "b857a08d-eb4f-4c9d-9814-9a39114e0d02";

        private uint _solutionEventsCookie;
        private IVsSolution _solution;

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Get the IVsSolution service
            _solution = await GetServiceAsync(typeof(SVsSolution)) as IVsSolution;
            Assumes.Present(_solution);

            // Register for solution events
            _solution.AdviseSolutionEvents(this, out _solutionEventsCookie);

            Logger.Instance.WriteLine("Hello World from Git Hook Extension!", LogLevel.DEBUG_MESSAGE);
            DTE2 dte = (DTE2)await GetServiceAsync(typeof(DTE));
            OutputWindowPane outputPane = GetOutputPane(dte, "Git Hook Extension");
            Logger.Instance.Initialize(outputPane);

            await HookManageFormCommand.InitializeAsync(this);

            // Check if a solution is already open
            if (IsSolutionOpen())
            {
                Logger.Instance.WriteLine("A solution is already open during initialization.", LogLevel.DEBUG_MESSAGE);
                InitHooksForCurrentFolder();
            }
        }

        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_solution != null && _solutionEventsCookie != 0)
            {
                _solution.UnadviseSolutionEvents(_solutionEventsCookie);
                _solutionEventsCookie = 0;
            }

            base.Dispose(disposing);
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

        private string GetSolutionPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread(); // Sicherstellen, dass wir auf dem UI-Thread sind
            DTE2 dte = (DTE2)GetService(typeof(DTE));
            if (dte?.Solution != null && !string.IsNullOrEmpty(dte.Solution.FullName))
            {
                return dte.Solution.FullName; // Gibt den vollständigen Pfad zur Solution-Datei zurück
            }
            return null;
        }

        /// <summary>
        /// Initializes Git hooks for the current solution folder.
        /// </summary>
        private void InitHooksForCurrentFolder()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string solutionPath = GetSolutionPath();
            if (!string.IsNullOrEmpty(solutionPath))
            {
                Logger.Instance.WriteLine($"Opened Solution: {solutionPath}", LogLevel.DEBUG_MESSAGE);
                GitHookFolderManager.Instance.Initialize(solutionPath);
                string repoPath = GitHookFolderManager.Instance.GitRootFolder;
                if (!string.IsNullOrEmpty(repoPath))
                {
                    string repoName = new DirectoryInfo(repoPath).Name;
                    Logger.Instance.WriteLine($"Git Repository found: {repoName}");
                }
                else
                {
                    Logger.Instance.WriteLine("No Git Repository found.");
                }
            }
        }

        /// <summary>
        /// Uninitializes Git hooks for the current solution folder.
        /// </summary>
        private void UninitializeHooks()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Logger.Instance.WriteLine("Uninitializing Git hooks for the current solution.", LogLevel.DEBUG_MESSAGE);
            GitHookFolderManager.Instance.Uninitialize();
        }


        // IVsSolutionEvents-Implementierung
        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            InitHooksForCurrentFolder();
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Logger.Instance.WriteLine("Solution geclosed!", LogLevel.DEBUG_MESSAGE);
            UninitializeHooks();
            return VSConstants.S_OK;
        }

        // Andere IVsSolutionEvents-Methoden (falls nicht benötigt, einfach leer lassen)
        public int OnBeforeCloseSolution(object pUnkReserved) => VSConstants.S_OK;
        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) => VSConstants.S_OK;
        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) => VSConstants.S_OK;
        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) => VSConstants.S_OK;
        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) => VSConstants.S_OK;
        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) => VSConstants.S_OK;
        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) => VSConstants.S_OK;
        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) => VSConstants.S_OK;

        private bool IsSolutionOpen()
        {
            ThreadHelper.ThrowIfNotOnUIThread(); // Ensure we are on the UI thread

            object value;
            int hr = _solution.GetProperty((int)__VSPROPID.VSPROPID_IsSolutionOpen, out value);
            if (ErrorHandler.Succeeded(hr) && value is bool isOpen)
            {
                return isOpen;
            }

            return false;
        }

    }
}
