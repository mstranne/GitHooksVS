using EnvDTE;
using System;
using System.IO;

namespace GitHooksVS
{
    public enum LogLevel
    {
        DEBUG_MESSAGE,
        ALWAYS_OUTPUT
    }

    internal class Logger
    {
        private static readonly Lazy<Logger> instance = new Lazy<Logger>(() => new Logger());
        private OutputWindowPane outputPane;
        private string outputFilePath;
        private bool debugMode = false;

        // Private constructor to prevent instantiation from outside
        private Logger()
        {
#if DEBUG
            debugMode = true;
#else
            debugMode = false;
#endif
        }

        public static Logger Instance
        {
            get
            {
                return instance.Value;
            }
        }

        public bool DebugMode
        {
            get { return debugMode; }
            set { debugMode = value; }
        }

        public void Initialize(OutputWindowPane pane)
        {
            outputPane = pane;
        }

        public void SetOutputFile(string filePath)
        {
            outputFilePath = filePath;
        }

        public void WriteLine(string message, LogLevel logLevel = LogLevel.ALWAYS_OUTPUT)
        {
            Write(message + Environment.NewLine, logLevel);
        }

        public void Write(string message, LogLevel logLevel = LogLevel.ALWAYS_OUTPUT)
        {
            if (!string.IsNullOrEmpty(outputFilePath))
            {
                File.AppendAllText(outputFilePath, message);
            }

            if (logLevel == LogLevel.DEBUG_MESSAGE && !debugMode)
                return;

            if (outputPane != null)
            {
                outputPane.OutputString(message);
            }
        }
    }
}

