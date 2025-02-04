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
    public class Configuration
    {
        public string FilesPath { get; set; }
        public List<Package> Packages { get; set; }
    }

    public class Package
    {
        public string Name { get; set; }
        public string Arguments { get; set; }
        public string FileName { get; set; }
    }

    public class Program
    {
        private static List<Package> _packages = new List<Package>();
        private static readonly string JsonPath = Path.Combine(Directory.GetCurrentDirectory(), "applications.json");

        public static Configuration Configuration { get; private set; }

        private const ConsoleColor HighlightColor = ConsoleColor.Cyan;
        private const ConsoleColor SuccessColor = ConsoleColor.Green;
        private const ConsoleColor WarningColor = ConsoleColor.Yellow;
        private const ConsoleColor ErrorColor = ConsoleColor.Red;



        public static void Main(string[] args)
        {
            try
            {
                if (!IsAdministrator())
                {
                    RestartAsAdmin(args);
                    return;
                }

                Console.Title = "Package Installer";
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Black;

                // Ensure configuration exists and is valid
                EnsureConfiguration();

                // Handle command-line arguments
                if (args.Length > 0)
                {
                    HandleCommandLineArguments(args);
                    return;
                }

                MainLoop();
            }
            catch (Exception ex)
            {
                WriteMessage($" A critical error occurred. {ex.Message}\n{ex}", ErrorColor);
                Pause();
            }
        }

        #region Core Functionality
        private static void HandleCommandLineArguments(string[] args)
        {
            // Early exit for help
            if (args.Contains("-h"))
            {
                DisplayHelp();
                return;
            }

            // Track valid arguments
            var validArgs = new HashSet<string> { "-a", "-y" };
            var invalidArgs = args.Where(arg => !validArgs.Contains(arg)).ToList();

            // Handle invalid arguments
            if (invalidArgs.Any())
            {
                WriteMessage($" * ERROR: Invalid arguments: {string.Join(", ", invalidArgs)}", ErrorColor);
                DisplayHelp();
                return;
            }

            // Process valid arguments
            var installAll = args.Contains("-a");
            var confirmReinstall = args.Contains("-y");

            if (installAll)
                InstallAllApplications(confirmReinstall);
            else
                WriteMessage(" * No action specified. Use '-a' to install all packages.", WarningColor);
        }

        private static void EnsureConfiguration()
        {
            try
            {
                if (!IsConfigurationValid())
                    CreateDefaultConfiguration();

                LoadApplications();
            }
            catch (Exception ex)
            {
                WriteMessage($" * ERROR: Failed to load or create configuration. {ex.Message}. Exiting...", ErrorColor);
                Pause();
                Environment.Exit(1);
            }
        }

        private static bool IsConfigurationValid()
        {
            if (!File.Exists(JsonPath))
            {
                WriteMessage($" * ERROR: Configuration file '{JsonPath}' not found.", ErrorColor);
                return false;
            }

            try
            {
                var json = File.ReadAllText(JsonPath);
                JsonConvert.DeserializeObject<Configuration>(json);
                return true;
            }
            catch (Exception ex)
            {
                WriteMessage($" * WARNING: Configuration validation failed. {ex.Message}", WarningColor);
                return false;
            }
        }

        private static void CreateDefaultConfiguration()
        {
            var defaultApps = new Configuration
            {
                FilesPath = "test",
                Packages = new List<Package>
                {
                    new Package
                    {
                        Name = "7-Zip",
                        Arguments = "/S",
                        FileName = "7z2408-x64.exe"
                    }
                }
            };

            try
            {
                var json = JsonConvert.SerializeObject(defaultApps, Formatting.Indented);
                File.WriteAllText(JsonPath, json);
                WriteMessage(" * Created default configuration file.", SuccessColor);
            }
            catch (Exception ex)
            {
                WriteMessage($" * ERROR: Failed to create default configuration. {ex.Message}", ErrorColor);
                throw; // Rethrow to ensure EnsureConfiguration handles the exception
            }
        }

        private static void LoadApplications()
        {
            try
            {
                var json = File.ReadAllText(JsonPath);
                Configuration = JsonConvert.DeserializeObject<Configuration>(json);
                _packages = Configuration?.Packages ?? new List<Package>();

                if (_packages.Count == 0)
                    WriteMessage(" * WARNING: No packages loaded from configuration.", WarningColor);
            }
            catch (Exception ex)
            {
                WriteMessage($" * ERROR: Failed to parse JSON file '{JsonPath}'. {ex.Message}. Exiting...", ErrorColor);
                Pause();
                Environment.Exit(1);
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
        private static void DisplayHelp()
        {
            WriteHeader("Command-Line Options");
            Console.WriteLine(" -a: Install all packages.");
            Console.WriteLine(" -y: Confirm reinstall if package exists.");
            Console.WriteLine(" -h: Display this help message.");
            WriteSeparator();

            Pause();
        }

        private static void ShowMenu()
        {
            Console.Clear();
            WriteHeader("Package Installer Menu");

            for (var i = 0; i < _packages.Count; i++)
            {
                var app = _packages[i];
                Console.Write($" {i + 1}. {app.Name} ");

                if (IsApplicationInstalled(app.Name))
                    WriteMessage("[Installed]", SuccessColor);
                else
                    Console.WriteLine();
            }

            Console.WriteLine(" A. Install All Packages");
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
                    InstallAllApplications(false);
                    break;
                default:
                    ProcessComplexChoice(choice);
                    break;
            }
        }

        private static void ProcessComplexChoice(string choice)
        {
            if (choice.Contains(",") || choice.Contains("-"))
                InstallMultiple(choice);
            else if (int.TryParse(choice, out var num))
                InstallSingle(num);
            else
                WriteMessage("Invalid option. Please try again.", ErrorColor);

            Pause();
        }
        #endregion

        #region Installation Logic
        private static void InstallSingle(int appNumber)
        {
            if (appNumber < 1 || appNumber > _packages.Count)
            {
                WriteMessage(" * Invalid selection. Skipping...", ErrorColor);
                return;
            }

            var app = _packages[appNumber - 1];
            ProcessApplicationInstall(app, false);
        }

        private static void InstallMultiple(string input)
        {
            var numbers = ParseInputNumbers(input);
            foreach (var num in numbers.Where(n => n > 0 && n <= _packages.Count))
                InstallSingle(num);
        }

        private static void InstallAllApplications(bool confirmReinstall)
        {
            foreach (var app in _packages)
                ProcessApplicationInstall(app, confirmReinstall);
            Pause();
        }

        private static void ProcessApplicationInstall(Package app, bool confirmReinstall)
        {
            var installerPath = Path.Combine(Configuration.FilesPath, app.FileName);

            if (!File.Exists(installerPath))
            {
                WriteMessage($" * ERROR: Installer for {app.Name} ('{app.FileName}') not found. Skipping...", ErrorColor);
                return;
            }

            if (IsApplicationInstalled(app.Name) && !confirmReinstall && !ConfirmReinstall(app.Name))
            {
                WriteMessage($" * Skipping installation of {app.Name}.", WarningColor);
                return;
            }

            ExecuteInstaller(app, installerPath);
        }

        private static void ExecuteInstaller(Package app, string installerPath)
        {
            WriteMessage("", ConsoleColor.White);
            WriteMessage($" + Installing {app.Name}...", ConsoleColor.Cyan);
            WriteMessage($" + Run {installerPath}", ConsoleColor.White);

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    WorkingDirectory = Configuration.FilesPath,
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
            WriteMessage("\n\n", ConsoleColor.Black);
            WriteMessage($" * {appName} is already installed.", WarningColor);

            var response = GetTimedResponse(TimeSpan.FromSeconds(10));
            switch (response)
            {
                case 'Y':
                    return true;
                case 'N':
                    WriteMessage(" * Cancel by user", ErrorColor);
                    return false;
                default:
                    WriteMessage(" * No input received. Skipping.", WarningColor);
                    return false;
            }
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
                Console.Write($"\r  Do you want to reinstall it? (Y/N) [Default: N] Timeout in {remaining} seconds... ");
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

        private static void HandleExitCode(Package app, int exitCode)
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

        private static void RestartAsAdmin(string[] args)
        {
            var processModule = Process.GetCurrentProcess().MainModule;
            if (processModule == null) return;
            var exeName = processModule.FileName;
            var startInfo = new ProcessStartInfo(exeName)
            {
                Verb = "runas",
                UseShellExecute = true,
                Arguments = string.Join(" ", args)
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