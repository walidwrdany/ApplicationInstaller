using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace ApplicationInstaller
{
    public class Application
    {
        public string Name { get; set; }
        public string Arguments { get; set; }
        public string FileName { get; set; }
    }

    public class Program
    {
        private static List<Application> _applications = new List<Application>();
        private static readonly string FilesPath = Path.Combine(Directory.GetCurrentDirectory(), "files");
        private static readonly string JsonPath = Path.Combine(Directory.GetCurrentDirectory(), "applications.json");
        
        private const ConsoleColor HighlightColor = ConsoleColor.Cyan;
        private const ConsoleColor SuccessColor = ConsoleColor.Green;
        private const ConsoleColor WarningColor = ConsoleColor.Yellow;
        private const ConsoleColor ErrorColor = ConsoleColor.Red;

        public static void Main(string[] args)
        {
            if (!IsAdministrator())
            {
                RestartAsAdmin();
                return;
            }

            Console.Title = "Application Installer";
            Console.SetWindowSize(120, 35);

            if (!LoadApplications()) return;

            MainLoop();
        }

        #region Core Functionality
        private static bool LoadApplications()
        {
            try
            {
                if (!File.Exists(JsonPath))
                {
                    WriteMessage($" * ERROR: JSON file '{JsonPath}' not found. Exiting...", ErrorColor);
                    Pause();
                    return false;
                }

                var json = File.ReadAllText(JsonPath);
                _applications = JsonConvert.DeserializeObject<List<Application>>(json);
                return true;
            }
            catch
            {
                WriteMessage($" * ERROR: Failed to parse JSON file '{JsonPath}'. Exiting...", ErrorColor);
                Pause();
                return false;
            }
        }

        private static void MainLoop()
        {
            while (true)
            {
                ShowMenu();
                ProcessChoice(ReadSafeInput());
            }

            // ReSharper disable once FunctionNeverReturns
        }

        #endregion

        #region Menu System
        private static void ShowMenu()
        {
            Console.Clear();
            WriteHeader("Application Installer Menu");

            for (var i = 0; i < _applications.Count; i++)
            {
                var app = _applications[i];
                Console.Write($" {i + 1}. {app.Name} ");

                if (IsApplicationInstalled(app.Name))
                {
                    WriteMessage("[Installed]", SuccessColor);
                }
                else
                {
                    Console.WriteLine();
                }
            }

            Console.WriteLine(" A. Install All Applications");
            Console.WriteLine(" Q. Quit");
            WriteSeparator();
            Console.WriteLine("Please select an option (e.g., 1, 1,2,3, 1-3, A, Q)");
        }

        private static void ProcessChoice(string choice)
        {
            if (string.IsNullOrWhiteSpace(choice)) return;

            switch (choice.ToUpper())
            {
                case "Q":
                    ExitApplication();
                    break;
                case "A":
                    InstallAllApplications();
                    break;
                default:
                    ProcessComplexChoice(choice);
                    break;
            }
        }

        private static void ProcessComplexChoice(string choice)
        {
            if (choice.Contains(",") || choice.Contains("-"))
            {
                InstallMultiple(choice);
            }
            else if (int.TryParse(choice, out var num))
            {
                InstallSingle(num);
            }
            else
            {
                WriteMessage("Invalid option. Please try again.", ErrorColor);
            }

            Pause();
        }
        #endregion

        #region Installation Logic
        private static void InstallSingle(int appNumber)
        {
            if (appNumber < 1 || appNumber > _applications.Count)
            {
                WriteMessage(" * Invalid selection. Skipping...", ErrorColor);
                return;
            }

            var app = _applications[appNumber - 1];
            ProcessApplicationInstall(app);
        }

        private static void InstallMultiple(string input)
        {
            var numbers = ParseInputNumbers(input);
            foreach (var num in numbers.Where(n => n > 0 && n <= _applications.Count))
            {
                InstallSingle(num);
            }
        }

        private static void InstallAllApplications()
        {
            foreach (var app in _applications)
            {
                ProcessApplicationInstall(app);
            }
            Pause();
        }

        private static void ProcessApplicationInstall(Application app)
        {
            var installerPath = Path.Combine(FilesPath, app.FileName);

            if (!File.Exists(installerPath))
            {
                WriteMessage($" * ERROR: Installer for {app.Name} ('{app.FileName}') not found. Skipping...", ErrorColor);
                return;
            }

            if (IsApplicationInstalled(app.Name) && !ConfirmReinstall(app.Name))
            {
                WriteMessage($" * Skipping installation of {app.Name}.", WarningColor);
                return;
            }

            ExecuteInstaller(app, installerPath);
        }

        private static void ExecuteInstaller(Application app, string installerPath)
        {
            WriteMessage($" + Installing {app.Name}...", ConsoleColor.Cyan);

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    WorkingDirectory = FilesPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                if (app.FileName.EndsWith(".msi"))
                {
                    processInfo.FileName = "msiexec.exe";
                    processInfo.Arguments = $"/i \"{installerPath}\" {app.Arguments}";
                }
                else
                {
                    processInfo.FileName = installerPath;
                    processInfo.Arguments = app.Arguments;
                }

                using (var process = Process.Start(processInfo))
                {
                    if (process == null) return;
                    process.WaitForExit();
                    HandleExitCode(app, process.ExitCode);
                }
            }
            catch (Exception ex)
            {
                WriteMessage($" + ERROR: {app.Name} installation failed: {ex.Message}", ErrorColor);
            }
        }
        #endregion

        #region Helper Methods
        private static bool IsApplicationInstalled(string appName)
        {
            var registryPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                foreach (var path in registryPaths)
                {
                    using (var key = baseKey.OpenSubKey(path))
                    {
                        if (key == null) continue;
                        if (CheckSubKeys(key, appName)) return true;
                    }
                }
            }

            using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key != null && CheckSubKeys(key, appName)) return true;
                }
            }

            return false;
        }

        private static bool CheckSubKeys(RegistryKey key, string appName)
        {
            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using (var subKey = key.OpenSubKey(subKeyName))
                {
                    var displayName = subKey?.GetValue("DisplayName")?.ToString();
                    if (displayName != null &&
                        displayName.IndexOf(appName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool ConfirmReinstall(string appName)
        {
            WriteMessage($" * {appName} is already installed.", WarningColor);
            Console.WriteLine(" Do you want to reinstall it? (Y/N) [Default: N, timeout in 10 seconds]");

            var response = GetTimedResponse(TimeSpan.FromSeconds(10));
            if (response == 'Y') return true;

            WriteMessage(" No input received. Skipping.", WarningColor);
            return false;
        }

        private static char GetTimedResponse(TimeSpan timeout)
        {
            var start = DateTime.Now;
            var key = new ConsoleKeyInfo();

            while (DateTime.Now - start < timeout)
            {
                if (Console.KeyAvailable)
                {
                    key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Y || key.Key == ConsoleKey.N)
                        break;
                }
                var remaining = (int)(timeout - (DateTime.Now - start)).TotalSeconds;
                Console.Write($"\rTimeout in {remaining} seconds... ");
                Thread.Sleep(250);
            }

            Console.WriteLine();
            return char.ToUpper(key.KeyChar);
        }
        #endregion

        #region Utility Methods
        private static IEnumerable<int> ParseInputNumbers(string input)
        {
            if (!input.Contains("-"))
                return input.Split(',')
                    .Select(s => int.TryParse(s.Trim(), out var num) ? num : -1)
                    .Where(n => n > 0);

            var range = input.Split('-');
            if (range.Length == 2 && int.TryParse(range[0], out var start) && int.TryParse(range[1], out var end))
            {
                return Enumerable.Range(start, end - start + 1);
            }
            
            return input.Split(',')
                .Select(s => int.TryParse(s.Trim(), out var num) ? num : -1)
                .Where(n => n > 0);
        }

        private static void HandleExitCode(Application app, int exitCode)
        {
            if (exitCode == 0)
            {
                WriteMessage($" + {app.Name} installed successfully!", SuccessColor);
            }
            else
            {
                WriteMessage($" + ERROR: {app.Name} installation failed with code {exitCode}.", ErrorColor);
            }
        }

        private static string ReadSafeInput() => (Console.ReadLine() ?? "").Trim();

        private static void Pause()
        {
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey();
        }
        #endregion

        #region UI Helpers
        private static void WriteHeader(string text)
        {
            WriteSeparator();
            WriteMessage(text, HighlightColor);
            WriteSeparator();
        }

        private static void WriteSeparator()
        {
            WriteMessage("=============================================", HighlightColor);
        }

        private static void WriteMessage(string message, ConsoleColor color, bool newLine = true)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;

            if (newLine) Console.WriteLine(message);
            else Console.Write(message);

            Console.ForegroundColor = originalColor;
        }
        #endregion

        #region Admin Elevation
        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RestartAsAdmin()
        {
            var processModule = Process.GetCurrentProcess().MainModule;
            if (processModule == null) return;
            var exeName = processModule.FileName;
            var startInfo = new ProcessStartInfo(exeName)
            {
                Verb = "runas",
                UseShellExecute = true
            };

            try
            {
                Process.Start(startInfo);
                Environment.Exit(0);
            }
            catch
            {
                WriteMessage(" * ERROR: Failed to request administrative privileges.", ErrorColor);
                Pause();
            }
        }

        private static void ExitApplication()
        {
            WriteSeparator();
            WriteMessage(" Installation complete. Restart if necessary.", HighlightColor);
            WriteSeparator();
            Pause();
            Environment.Exit(0);
        }
        #endregion
    }
}