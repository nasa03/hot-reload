using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SingularityGroup.HotReload.Editor.Semver;
using Debug = UnityEngine.Debug;

namespace SingularityGroup.HotReload.Editor.Cli {
    class OsxCliController : ICliController {
        Process process;

        public string GetDefaultExecutablePath(string serverBasePath) {
            return $"{serverBasePath}/osx-x64/HotReload.app.zip";
        }

        /// In MacOS 13 Ventura, our app cannot launch a terminal window.
        /// We use a custom app that launches HotReload server and shows it's output (just like a terminal would). 
        //  Including MacOS 12 Monterey as well so I can dogfood it -Troy
        private static bool UseCustomConsoleApp() => MacOSVersion.Value.Major >= 12;

        // dont use static because null comparison on SemVersion is broken
        private static readonly Lazy<SemVersion> MacOSVersion = new Lazy<SemVersion>(() => {
            //UnityHelper.OperatingSystem; // in Unity 2018 it returns 10.16 on monterey (no idea why)
            //Environment.OSVersion returns unix version like 21.x
            var startinfo = new ProcessStartInfo {
                FileName = "/usr/bin/sw_vers",
                Arguments = "-productVersion",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            var process = Process.Start(startinfo);

            string osVersion = process.StandardOutput.ReadToEnd().Trim();

            SemVersion macosVersion;
            if (SemVersion.TryParse(osVersion, out macosVersion)) {
                //Debug.LogWarning($"macosVersion is {macosVersion}");
                return macosVersion;
            }
            // should never happen
            Debug.LogWarning("Failed to detect MacOS version, if Hot Reload fails to start, please contact support.");
            return SemVersion.None;
        });

        public async Task Start(StartArgs args) {
            // Unzip the .app.zip to temp folder .app
            var appExecutablePath = $"{args.executableTargetDir}/HotReload.app/Contents/MacOS/HotReload";
            var cliExecutablePath = $"{args.executableTargetDir}/HotReload.app/Contents/Resources/CodePatcherCLI";
            
            // ensure running on threadpool
            await ThreadUtility.SwitchToThreadPool();

            // executableTargetDir is versioned, so only need to extract once.
            if (!File.Exists(appExecutablePath)) {
                UnzipMacOsPackage(args.executableSourcePath, args.executableTargetDir + "/");
            }
            
            if (UseCustomConsoleApp()) {
                await StartCustomConsole(args, appExecutablePath);
            } else {
                await StartTerminal(args, cliExecutablePath);
            }
        }

        public Task StartCustomConsole(StartArgs args, string executablePath) {
            process = Process.Start(new ProcessStartInfo {
                // Path to the HotReload.app
                FileName = executablePath,
                Arguments = args.cliArguments,
                UseShellExecute = false,
            });
            return Task.CompletedTask;
        }

        public Task StartTerminal(StartArgs args, string executablePath) {
            var pidFilePath = CliUtils.GetPidFilePath(args.hotreloadTempDir);
            // To run in a Terminal window (so you can see compiler logs), we must put the arguments into a script file
            // and run the script in Terminal. Terminal.app does not forward the arguments passed to it via `open --args`.
            // *.command files are opened with the user's default terminal app.
            var executableScriptPath = Path.Combine(Path.GetTempPath(), "Start_HotReloadServer.command");
            // You don't need to copy the cli executable on mac
            // omit hashbang line, let shell use the default interpreter (easier than detecting your default shell beforehand)

            File.WriteAllText(executableScriptPath, $"echo $$ > \"{pidFilePath}\"" +
                                                    $"\ncd \"{Environment.CurrentDirectory}\"" + // set cwd because 'open' launches script with $HOME as cwd.
                                                    $"\n\"{executablePath}\" {args.cliArguments} || read");

            CliUtils.Chmod(executableScriptPath); // make it executable
            CliUtils.Chmod(executablePath); // make it executable

            Directory.CreateDirectory(args.hotreloadTempDir);
            Directory.CreateDirectory(args.executableTargetDir);
            Directory.CreateDirectory(args.cliTempDir);
            
            process = Process.Start(new ProcessStartInfo {
                FileName = "open",
                Arguments = $"'{executableScriptPath}'",
                UseShellExecute = true,
            });

            if (process.WaitForExit(1000)) {
                if (process.ExitCode != 0) {
                    Debug.LogWarning($"[{CodePatcher.TAG}] Failed to the run the start server command. ExitCode={process.ExitCode}\nFilepath: {executableScriptPath}");
                }
            }
            else {
                process.EnableRaisingEvents = true;
                process.Exited += (_, __) => {
                    if (process.ExitCode != 0) {
                        Debug.LogWarning($"[{CodePatcher.TAG}] Failed to the run the start server command. ExitCode={process.ExitCode}\nFilepath: {executableScriptPath}");
                    }
                };
            }
            return Task.CompletedTask;
        }

        public async Task Stop() {
            // kill HotReload server process (on mac it has different pid to the window which started it)
            await RequestHelper.KillServer();

            // process.CloseMainWindow throws if proc already exited.
            // We rely on the pid file for killing the trampoline script (in-case script is just starting and HotReload server not running yet)
            process = null;
            CliUtils.KillLastKnownHotReloadProcess();
        }

        static void UnzipMacOsPackage(string zipPath, string unzippedFolderPath) {
            if (!zipPath.EndsWith(".zip")) {
                throw new ArgumentException($"Expected to end with .zip, but it was: {zipPath}", nameof(zipPath));
            }

            if (!File.Exists(zipPath)) {
                throw new ArgumentException($"zip file not found {zipPath}", nameof(zipPath));
            }
            var processStartInfo = new ProcessStartInfo {
                FileName = "unzip",
                Arguments = $"\"{zipPath}\"",
                WorkingDirectory = unzippedFolderPath, // unzip extracts to working directory by default
                UseShellExecute = true,
                CreateNoWindow = true
            };

            Process process = Process.Start(processStartInfo);
            process.WaitForExit();
            if (process.ExitCode != 0) {
                throw new Exception($"unzip failed with ExitCode {process.ExitCode}");
            }
            //Debug.Log($"did unzip to {unzippedFolderPath}");
        }
    }
}