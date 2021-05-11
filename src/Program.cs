using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace WebConfigConnectionStringTool
{
    public class Program
    {
        #region User32 Lib

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hWnd);

        #endregion

        /// <summary>
        /// Saved command-line arguments.
        /// </summary>
        private static string[] CmdArgs { get; set; }

        /// <summary>
        /// Init all the things..
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        private static void Main(string[] args)
        {
            // Attempt to hide the cursor.
            HideCursor();

            // Save command-line arguments, for later use.
            CmdArgs = args;

            // Show list of all connection strings in local web.config file?
            if (CheckCmdSwitch("list", "l"))
            {
                ListConnectionStrings();
            }

            // Open Microsoft SQL Server Management Studio?
            else if (CheckCmdSwitch("open", "o"))
            {
                LoginToSqlManagementStudio();
            }

            // Show help?
            else
            {
                ShowHelp();
            }
        }

        #region App helper methods

        /// <summary>
        /// Check if a cmd-args switch exists.
        /// </summary>
        /// <param name="names">Switch to check for.</param>
        /// <returns>Found.</returns>
        private static bool CheckCmdSwitch(params string[] names)
        {
            if (CmdArgs == null)
            {
                return false;
            }

            return names.Any(
                name => CmdArgs.Any(
                    n => n == $"-{name}" ||
                         n == $"--{name}"));
        }

        /// <summary>
        /// Get an option value from the list of arguments.
        /// </summary>
        /// <param name="names">Name of value to get.</param>
        /// <returns>Value.</returns>
        private static string GetCmdArgValue(params string[] names)
        {
            if (CmdArgs == null ||
                CmdArgs.Length < 2)
            {
                return null;
            }

            for (var i = 0; i < CmdArgs.Length - 1; i++)
            {
                if (names.Any(
                    name => CmdArgs[i] == $"-{name}" ||
                            CmdArgs[i] == $"--{name}"))
                {
                    return CmdArgs[i + 1];
                }
            }

            return null;
        }

        /// <summary>
        /// Show help screen with options and descriptions.
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine($"Usage: {Assembly.GetExecutingAssembly().GetName().Name} [options]");
            Console.WriteLine();

            Console.WriteLine(" --open       Read local web.config file, attempt to open Microsoft SQL Management Studio and login.");
            Console.WriteLine(" --test-cs    Use the connection string: TestConnectionString. Defaults to MainConnectionString.");
            Console.WriteLine(" --cs <name>  Use the named connection string. Defaults to MainConnectionString.");
            Console.WriteLine(" --list       List all found connection strings.");
        }

        #endregion

        #region App functionality methods

        /// <summary>
        /// Parse and list all found connections strings.
        /// </summary>
        private static void ListConnectionStrings()
        {
            var configFile = FindLocalWebConfigFile();

            if (configFile == null)
            {
                return;
            }

            var doc = new XmlDocument();

            try
            {
                doc.Load(configFile);
            }
            catch (Exception ex)
            {
                WriteError(ex);
                return;
            }

            var nodes = doc.GetElementsByTagName("connectionStrings");

            foreach (XmlNode node in nodes)
            {
                foreach (XmlNode csn in node.ChildNodes)
                {
                    var name = csn?.Attributes?["name"]?.Value;
                    var entries = csn?.Attributes?["connectionString"]?.Value.Split(';');

                    if (name == null ||
                        entries == null)
                    {
                        continue;
                    }

                    string hostname = null;
                    string database = null;
                    string username = null;

                    foreach (var entry in entries)
                    {
                        var sp = entry.IndexOf('=');

                        if (sp == -1)
                        {
                            continue;
                        }

                        var key = entry.Substring(0, sp).Trim().ToLower();
                        var value = entry.Substring(sp + 1).Trim();

                        switch (key)
                        {
                            case "data source":
                                hostname = value;
                                break;

                            case "initial catalog":
                                database = value;
                                break;

                            case "user id":
                                username = value;
                                break;
                        }
                    }

                    if (hostname == null ||
                        username == null)
                    {
                        continue;
                    }

                    Console.WriteLine($"{name} - {username}@{database}:{hostname}");
                }
            }
        }

        /// <summary>
        /// Attempt to open SQL Management Studio and log in.
        /// </summary>
        private static void LoginToSqlManagementStudio()
        {
            // Get web.config file.
            var configFile = FindLocalWebConfigFile();

            if (configFile == null)
            {
                return;
            }
            
            // Get connection string name.
            var connectionStringName = GetCmdArgValue("cs") ??
                                       (CheckCmdSwitch("test-cs")
                                           ? "TestConnectionString"
                                           : "MainConnectionString");

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("[CONNECTION STRING] ");

            Console.ResetColor();
            Console.WriteLine(connectionStringName);

            // Get connection string credentials.
            if (!GetCredentials(
                configFile,
                connectionStringName,
                out var hostname,
                out var username,
                out var password))
            {
                return;
            }

            // Locate a copy of Microsoft SQL Server Management Studio. 
            var execFile = FindLocalSqlStudio();

            if (execFile == null)
            {
                return;
            }

            // Launch SQL Studio, wait for it to starte, then attempt to login.
            LaunchAndLoginToSqlStudio(
                execFile,
                hostname,
                username,
                password);
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Get credentials for the given connection string.
        /// </summary>
        /// <param name="file">Web.config file to parse.</param>
        /// <param name="connectionStringName">Name of connection string to get credentials for.</param>
        /// <param name="hostname">Found hostname.</param>
        /// <param name="username">Found username.</param>
        /// <param name="password">Found password.</param>
        /// <returns></returns>
        private static bool GetCredentials(
            string file,
            string connectionStringName,
            out string hostname,
            out string username,
            out string password)
        {
            hostname = null;
            username = null;
            password = null;

            var doc = new XmlDocument();

            try
            {
                doc.Load(file);
            }
            catch (Exception ex)
            {
                WriteError(ex);
                return false;
            }

            var nodes = doc.GetElementsByTagName("connectionStrings");

            foreach (XmlNode node in nodes)
            {
                foreach (XmlNode csn in node.ChildNodes)
                {
                    var name = csn?.Attributes?["name"]?.Value;
                    var entries = csn?.Attributes?["connectionString"]?.Value.Split(';');

                    if (name != connectionStringName ||
                        entries == null)
                    {
                        continue;
                    }

                    foreach (var entry in entries)
                    {
                        var sp = entry.IndexOf('=');

                        if (sp == -1)
                        {
                            continue;
                        }

                        var key = entry.Substring(0, sp).Trim().ToLower();
                        var value = entry.Substring(sp + 1).Trim();

                        switch (key)
                        {
                            case "data source":
                                hostname = value;
                                break;

                            case "user id":
                                username = value;
                                break;

                            case "password":
                                password = value;
                                break;
                        }
                    }
                }
            }

            return hostname != null &&
                   username != null &&
                   password != null;
        }

        /// <summary>
        /// Attempt to hide the cursor.
        /// </summary>
        private static void HideCursor()
        {
            try
            {
                Console.CursorVisible = false;
            }
            catch
            {
                //
            }
        }

        /// <summary>
        /// Attempt to find a web.config file in the local space.
        /// </summary>
        /// <returns>Full path.</returns>
        private static string FindLocalWebConfigFile()
        {
            Console.WriteLine("Locating web.config files..");
            Console.CursorTop--;

            // Get all web.config files.
            string[] files;
            string path;

            try
            {
                path = Directory.GetCurrentDirectory();

                files = Directory.GetFiles(
                    path,
                    "web.config",
                    SearchOption.AllDirectories);

                if (files.Length == 0)
                {
                    throw new Exception($"Unable to find any web.config files to parse in {path}");
                }
            }
            catch (Exception ex)
            {
                WriteError(ex);
                return null;
            }

            // If we only found 1, success.
            if (files.Length == 1)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write("[WEB.CONFIG] ");

                Console.ResetColor();
                Console.WriteLine($"..{files[0].Substring(path.Length)}");

                return files[0];
            }

            // We found multiple files. Let the user select which one to use.
            var str = $"Found {files.Length} web.config files to select from. Select the correct one.";

            var ltc = files
                .Select(t => t.Length + 3)
                .Max() + str.Length;

            Console.WriteLine(str);

            var index = 0;
            var exitLoop = false;

            string file = null;

            while (true)
            {
                for (var i = 0; i < files.Length; i++)
                {
                    Console.ForegroundColor = i == index
                        ? ConsoleColor.DarkCyan
                        : ConsoleColor.White;

                    Console.WriteLine($" > ..{files[i].Substring(path.Length)}");
                }

                var key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        index--;

                        if (index == -1)
                        {
                            index = files.Length - 1;
                        }

                        break;

                    case ConsoleKey.DownArrow:
                        index++;

                        if (index == files.Length)
                        {
                            index = 0;
                        }

                        break;

                    case ConsoleKey.Escape:
                        exitLoop = true;
                        break;

                    case ConsoleKey.Enter:
                        file = files[index];
                        exitLoop = true;
                        break;
                }

                Console.CursorTop -= files.Length;

                if (exitLoop)
                {
                    break;
                }
            }

            // Clear all strings.
            Console.CursorTop--;

            str = "";

            for (var i = 0; i < Console.WindowWidth; i++)
            {
                str += " ";
            }

            for (var i = 0; i < files.Length + 1; i++)
            {
                Console.WriteLine(str);
            }

            // Check for data..
            if (file == null)
            {
                return null;
            }

            // Output
            Console.CursorTop -= files.Length + 1;

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("[WEB.CONFIG] ");

            Console.ResetColor();
            Console.WriteLine($"..{file.Substring(path.Length)}");

            return file;
        }

        /// <summary>
        /// Attempt to locate a local copy of Microsoft SQL Server Management Studio. 
        /// </summary>
        /// <returns>Full path.</returns>
        private static string FindLocalSqlStudio()
        {
            Console.WriteLine("Locating Microsoft SQL Server Management Studio..");
            Console.CursorTop--;

            string path = null;

            // Get from cache.
            var cacheFile = $"{Assembly.GetExecutingAssembly().Location}.cache";

            if (File.Exists(cacheFile))
            {
                try
                {
                    path = File.ReadAllText(cacheFile);
                }
                catch
                {
                    //
                }
            }

            if (File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write("[SSMS] ");

                Console.ResetColor();
                Console.WriteLine(path);

                return path;
            }

            // Search to find it.
            var folders = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            var index = -1;
            var files = new List<string>();

            while (true)
            {
                index++;

                if (index == folders.Count)
                {
                    break;
                }

                try
                {
                    files.AddRange(
                        Directory.GetFiles(
                            folders[index],
                            "ssms.exe",
                            SearchOption.TopDirectoryOnly));
                }
                catch
                {
                    //
                }

                try
                {
                    folders.AddRange(
                        Directory.GetDirectories(
                            folders[index],
                            "*",
                            SearchOption.TopDirectoryOnly));
                }
                catch
                {
                    //
                }
            }

            files = files
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();

            if (files.Count == 0)
            {
                WriteError(new Exception("Unable to locate Microsoft SQL Server Management Studio!"));
                return null;
            }

            if (files.Count == 1)
            {
                path = files[0];

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write("[SSMS] ");

                Console.ResetColor();
                Console.WriteLine(path);

                try
                {
                    File.WriteAllText(cacheFile, path);
                }
                catch
                {
                    //
                }

                return path;
            }

            // We found multiple files. Let the user select which one to use.
            var str = $"Found {files.Count} files to select from. Select the correct one.";

            var ltc = files
                .Select(t => t.Length + 3)
                .Max() + str.Length;

            Console.WriteLine(str);

            index = 0;

            var exitLoop = false;

            string file = null;

            while (true)
            {
                for (var i = 0; i < files.Count; i++)
                {
                    Console.ForegroundColor = i == index
                        ? ConsoleColor.DarkCyan
                        : ConsoleColor.White;

                    Console.WriteLine($" > {files[i]}");
                }

                var key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        index--;

                        if (index == -1)
                        {
                            index = files.Count - 1;
                        }

                        break;

                    case ConsoleKey.DownArrow:
                        index++;

                        if (index == files.Count)
                        {
                            index = 0;
                        }

                        break;

                    case ConsoleKey.Escape:
                        exitLoop = true;
                        break;

                    case ConsoleKey.Enter:
                        file = files[index];
                        exitLoop = true;
                        break;
                }

                Console.CursorTop -= files.Count;

                if (exitLoop)
                {
                    break;
                }
            }

            // Clear all strings.
            Console.CursorTop--;

            str = "";

            for (var i = 0; i < ltc; i++)
            {
                str += " ";
            }

            for (var i = 0; i < files.Count + 1; i++)
            {
                Console.WriteLine(str);
            }

            // Output
            Console.CursorTop -= files.Count + 1;

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write("[FOUND] ");

            Console.ResetColor();
            Console.WriteLine(file);

            File.WriteAllText(cacheFile, file);

            return file;
        }

        /// <summary>
        /// Launch SQL Studio, wait for it to starte, then attempt to login.
        /// </summary>
        /// <param name="execFile">Full path to executable.</param>
        /// <param name="hostname">Hostname to use.</param>
        /// <param name="username">Username to use.</param>
        /// <param name="password">Password to use.</param>
        private static void LaunchAndLoginToSqlStudio(
            string execFile,
            string hostname,
            string username,
            string password)
        {
            Console.WriteLine("Launching Microsoft SQL Server Management Studio..");
            
            try
            {
                Process.Start(execFile);
            }
            catch (Exception ex)
            {
                WriteError(ex);
                return;
            }

            IntPtr handle;
            const int threadSleepMs = 500;
            var totalMsWaited = 0;

            while (true)
            {
                handle = FindWindow(null, "Connect to Server");

                if (handle != IntPtr.Zero)
                {
                    break;
                }

                totalMsWaited += threadSleepMs;

                var totalMsWaitedFormatted = string.Format(
                    "{0}s",
                    totalMsWaited / 1000);

                Console.CursorTop--;

                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write($"[WAITING {totalMsWaitedFormatted}] ");

                Console.ResetColor();
                Console.WriteLine("Launching Microsoft SQL Server Management Studio..");

                Thread.Sleep(threadSleepMs);
            }

            SetForegroundWindow(handle);

            // Fill inn credentials.
            SendKeys.SendWait(hostname);
            SendKeys.SendWait("\t\t");

            SendKeys.SendWait(username);
            SendKeys.SendWait("\t");

            SendKeys.SendWait(password);
            SendKeys.SendWait("\r");
        }

        /// <summary>
        /// Write an exception to console.
        /// </summary>
        /// <param name="ex">Exception to write.</param>
        private static void WriteError(Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write("[ERROR] ");

            Console.ResetColor();
            Console.WriteLine(ex.Message);

            if (ex.InnerException == null)
            {
                return;
            }

            Console.WriteLine($"        {ex.InnerException.Message}");
        }

        #endregion
    }
}