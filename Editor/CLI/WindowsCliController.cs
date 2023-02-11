using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SingularityGroup.HotReload.Editor.Cli {
    class WindowsCliController : ICliController {
        Process process;
        
        public string GetDefaultExecutablePath(string serverBasePath) {
            return $"{serverBasePath}/win-x64/CodePatcherCLI.exe";
        }

        public Task Start(StartArgs args) {
            var robocopy = Process.Start(new ProcessStartInfo {
                FileName = "robocopy",
                Arguments = $@"""{args.executableSourceDir}"" ""{args.executableTargetDir}"" /purge /MIR",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            robocopy.WaitForExit();

            var watchmanPath = Path.GetFullPath(Path.Combine(args.executableTargetDir, "watchman", "watchman.exe"));
            var watchmanWaitPath = Path.GetFullPath(Path.Combine(args.executableTargetDir, "watchman-wait", "watchman-wait.exe"));
            var cliArguments = args.cliArguments + $@" -b ""{watchmanPath}"" -w ""{watchmanWaitPath}""";

            process = Process.Start(new ProcessStartInfo {
                FileName = Path.GetFullPath(Path.Combine(args.executableTargetDir, "CodePatcherCLI.exe")),
                Arguments = cliArguments,
            });
            return Task.CompletedTask;
        }

        public async Task Stop() {
            await RequestHelper.KillServer();
            try {
                process?.CloseMainWindow();
            } catch {
                //ignored
            }  
            process = null;
        }
    }
}