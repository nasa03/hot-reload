using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace SingularityGroup.HotReload.Editor.Cli {

    class LinuxCliController : ICliController {
        Process process;

        public string GetDefaultExecutablePath(string serverBasePath) {
            return $"{serverBasePath}/linux-x64/CodePatcherCLI";
        }

        public Task Start(StartArgs args) {
            var startScript = Path.Combine(args.executableSourceDir, "hotreload-start-script.sh");
            if (!File.Exists(startScript)) {
                throw new FileNotFoundException(startScript);
            }
            File.WriteAllText(startScript, File.ReadAllText(startScript).Replace("\r\n", "\n"));
            CliUtils.Chmod(startScript);

            var title = CodePatcher.TAG + "Server " + new DirectoryInfo(args.unityProjDir).Name;
            title = title.Replace(" ", "-");
            title = title.Replace("'", "");

            var cliargsfile = Path.GetTempFileName();
            File.WriteAllText(cliargsfile,args.cliArguments);
            var codePatcherProc = Process.Start(new ProcessStartInfo {
                FileName = startScript,
                Arguments =
                    $"--title \"{title}\""
                    + $" --executables-source-dir \"{args.executableSourceDir}\" "
                    + $" --pidfile \"{CliUtils.GetPidFilePath(args.hotreloadTempDir)}\""
                    + $" --cli-arguments-file \"{cliargsfile}\""
                    + $" --tmp-path \"{args.hotreloadTempDir}\""
                    + $" --method-patch-dir \"{args.cliTempDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (codePatcherProc == null) {
                if (File.Exists(cliargsfile)) {
                    File.Delete(cliargsfile);
                }
                throw new Exception("Could not start code patcher process.");
            }
            codePatcherProc.BeginErrorReadLine();
            codePatcherProc.BeginOutputReadLine();
            codePatcherProc.OutputDataReceived += (_, a) => {
                var s = a.Data.Trim();
                if (!string.IsNullOrWhiteSpace(s)) {
                    Debug.Log("[" + CodePatcher.TAG + "] " + s);
                }
            };
            // error data can also mean we kill the proc beningly
            codePatcherProc.ErrorDataReceived += (_, a) => {
                var s = a.Data.Trim();
                if (!string.IsNullOrWhiteSpace(s)) {
                    Debug.LogError("[" + CodePatcher.TAG + "] " + s);
                }
            };
            process = codePatcherProc;
            return Task.CompletedTask;
        }

        public Task Stop() {
            // process.CloseMainWindow throws if proc already exited.
            // also we just rely on the pid file it is fine
            process = null;
            CliUtils.KillLastKnownHotReloadProcess();
            return Task.CompletedTask;
        }
    }
}
