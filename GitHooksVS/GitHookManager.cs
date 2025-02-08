using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GitHooksVS
{
    public enum HookType
    {
        PRE_COMMIT,
        POST_CHECKOUT,
        POST_MERGE
    }

    internal static class GitHookManager
    {
        private const string HookHeader = "# Generated by GitHooksVS";
        private const string ShellScriptIndicator = "#!/bin/sh";

        /// <summary>
        /// Lookup table for hook types and their respective script parameters.
        /// </summary>
        private static readonly Dictionary<HookType, string> HookParameterLookup = new Dictionary<HookType, string>
        {
            { HookType.PRE_COMMIT, "\"$@\"" },
            { HookType.POST_CHECKOUT, "\"$1\" \"$2\" \"$3\"" },
            { HookType.POST_MERGE, "\"$1\"" }
        };

        private static readonly Dictionary<HookType, string> HookScriptNameLookup = new Dictionary<HookType, string>
        {
            { HookType.PRE_COMMIT, "pre-commit" },
            { HookType.POST_CHECKOUT, "post-checkout" },
            { HookType.POST_MERGE, "post-merge" }
        };

        /// <summary>
        /// Creates a Git hook script that calls all scripts in the provided list.
        /// </summary>
        /// <param name="scripts">List of scripts to be called by the Git hook.</param>
        /// <param name="hookPath">Path where the Git hook script should be created.</param>
        public static void CreateGitHookScript(List<string> scripts, string hookPath, HookType type)
        {
            StringBuilder scriptContent = new StringBuilder();
            scriptContent.AppendLine("#!/bin/sh");
            scriptContent.AppendLine(HookHeader);

            scriptContent.AppendLine("");
            scriptContent.AppendLine("echo \"GitHookVS is starting your scripts now\" ");
            scriptContent.AppendLine("");

            string parameters = HookParameterLookup.ContainsKey(type) ? HookParameterLookup[type] : "\"$@\"";

            foreach (var script in scripts)
            {
                scriptContent.AppendLine($"sh \"{script}\" {parameters}");
            }

            scriptContent.AppendLine("");
            scriptContent.AppendLine("exit 0");

            hookPath = Path.Combine(hookPath, HookScriptNameLookup[type]);
            File.WriteAllText(hookPath, scriptContent.ToString());
            // Make the script executable
            var fileInfo = new FileInfo(hookPath);
            fileInfo.Attributes |= FileAttributes.Normal;
        }

        /// <summary>
        /// Extracts the list of scripts called by the Git hook script.
        /// </summary>
        /// <param name="hookPath">Path to the Git hook script.</param>
        /// <returns>List of scripts called by the Git hook script.</returns>
        public static List<string> ExtractScriptsFromGitHook(string hookPath)
        {
            List<string> scripts = new List<string>();

            if (!File.Exists(hookPath))
                return scripts;

            var lines = File.ReadAllLines(hookPath);
            if (lines.Length == 0 || !lines[1].StartsWith(HookHeader))
                return scripts;

            foreach (var line in lines)
            {
                if (line.StartsWith("sh \""))
                {
                    var script = line.Substring(4, line.IndexOf("\" \"$@\"") - 4);
                    scripts.Add(script);
                }
            }

            return scripts;
        }

        /// <summary>
        /// Tries to parse a string to determine the corresponding HookType enum value.
        /// </summary>
        /// <param name="hookTypeString">The string representation of the hook type.</param>
        /// <param name="hookType">The corresponding HookType enum value if the string matches a known hook type.</param>
        /// <returns>
        /// True if the string matches a known hook type; otherwise, false.
        /// </returns>
        public static bool TryParseHookFolder(string hookTypeString, out HookType hookType)
        {
            switch (hookTypeString.ToUpperInvariant())
            {
                case "PRE-COMMIT":
                    hookType = HookType.PRE_COMMIT;
                    return true;
                case "POST-CHECKOUT":
                    hookType = HookType.POST_CHECKOUT;
                    return true;
                case "POST-MERGE":
                    hookType = HookType.POST_MERGE;
                    return true;
                default:
                    hookType = default;
                    return false;
            }
        }

        /// <summary>
        /// Returns a list of all valid .sh scripts or scripts without an extension in the specified folder.
        /// </summary>
        /// <param name="folderPath">The path to the folder to search for scripts.</param>
        /// <returns>A list of valid scripts in the specified folder.</returns>
        public static List<string> GetValidShScripts(string folderPath)
        {
            List<string> validScripts = new List<string>();

            if (!Directory.Exists(folderPath))
                return validScripts;

            var files = Directory.GetFiles(folderPath, "*", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                if (IsValidShScript(file))
                {
                    validScripts.Add(file);
                }
            }

            return validScripts;
        }

        /// <summary>
        /// Checks if a file is a valid .sh script.
        /// </summary>
        /// <param name="filePath">The path to the file to check.</param>
        /// <returns>True if the file is a valid .sh script; otherwise, false.</returns>
        private static bool IsValidShScript(string filePath)
        {
            if (Path.GetExtension(filePath) != ".sh" && Path.GetExtension(filePath) != string.Empty)
                return false;

            var firstLine = File.ReadLines(filePath).FirstOrDefault();
            return firstLine != null && firstLine.StartsWith(ShellScriptIndicator);
        }
    }
}
