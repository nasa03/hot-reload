using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace SingularityGroup.HotReload.Editor.Cli {
    class StartArgs {
        public string hotreloadTempDir;
        // aka method patch temp dir
        public string cliTempDir;
        public string executableTargetDir;
        public string executableSourceDir;
        public string executableSourcePath;
        public string cliArguments;
        public string unityProjDir;
    }
    
    [InitializeOnLoad]
    static class HotReloadCli {
        static readonly ICliController controller;
        
        //InitializeOnLoad ensures controller gets initialized on unity thread
        static HotReloadCli() {
            controller =
    #if UNITY_EDITOR_OSX
                new OsxCliController();
    #elif UNITY_EDITOR_LINUX
                new LinuxCliController();
    #elif UNITY_EDITOR_WIN
                new WindowsCliController();
    #else
                new FallbackCliController();
    #endif
        }
        
        public static Task StartAsync(string dataPath, bool exposeServerToNetwork) {
            StartArgs args;
            if(TryGetStartArgs(dataPath, exposeServerToNetwork, out args)) {
                return controller.Start(args);
            }
            return Task.CompletedTask;
        }
        
        public static Task StopAsync() {
            return controller.Stop();
        }
        
        /// <summary>
        /// Stop the server, then start it again.
        /// </summary>
        /// <remarks>
        /// Used when the user changes server options that can only be applied by restarting it.<br/>
        /// Should be called on the thread pool.
        /// </remarks>
        public static async Task RestartAsync(string dataPath, bool exposeServerToNetwork) {
            StartArgs args;
            if(TryGetStartArgs(dataPath, exposeServerToNetwork, out args)) {
                RequestHelper.AbortPendingPatchRequest();
                await controller.Stop().ConfigureAwait(false);
                await controller.Start(args).ConfigureAwait(false);
            }
        }
        
        static bool TryGetStartArgs(string dataPath, bool exposeServerToNetwork, out StartArgs args) {
            string executableSourcePath;
            if(!CliUtils.TryFindServerExecutable(controller, out executableSourcePath)) {
                Debug.LogWarning($"Failed to start the Hot Reload Server. " +
                                 $"Unable to locate the CodePatcherCLI executable. " +
                                 $"Make sure the executable for your platform is " +
                                 $"somewhere in the Assets folder or in the HotReload package");
                args = null;
                return false;
            }
            
            var hotReloadTmpDir = CliUtils.GetHotReloadTempDir();
            var cliTempDir = CliUtils.GetCliTempDir();
            // Versioned path so that we only need to extract the binary once. User can have multiple projects
            //  on their machine using different HotReload versions.
            var executableTargetDir = Path.Combine(hotReloadTmpDir, $"executables_{PackageConst.Version.Replace('.', '-')}");
            Directory.CreateDirectory(executableTargetDir); // ensure exists
            var executableSourceDir = Path.GetDirectoryName(executableSourcePath);
            var unityProjDir = Path.GetDirectoryName(dataPath);
            var slnPath = Path.Combine(unityProjDir, Path.GetFileName(unityProjDir) + ".sln");

            if (!File.Exists(slnPath)) {
                Debug.LogWarning($"No .sln file found. Open any c# file to generate it so Hot Reload can work properly");
            }
            
            var searchAssemblies = string.Join(";", CodePatcher.I.GetAssemblySearchPaths());
            var cliArguments = $@"-s ""{slnPath}"" -t ""{cliTempDir}"" -a ""{searchAssemblies}""";
            if (exposeServerToNetwork) {
                // server will listen on local network interface (default is localhost only)
                cliArguments += " -e true";
            }
            args = new StartArgs {
                hotreloadTempDir = hotReloadTmpDir,
                cliTempDir = cliTempDir,
                executableTargetDir = executableTargetDir,
                executableSourceDir = executableSourceDir,
                executableSourcePath = executableSourcePath,
                cliArguments = cliArguments,
                unityProjDir = unityProjDir,
            };
            return true;
        }
    }
}
