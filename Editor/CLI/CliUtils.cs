using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace SingularityGroup.HotReload.Editor.Cli {
    internal static class CliUtils {
        public static string GetHotReloadTempDir() {
            if (UnityHelper.Platform == RuntimePlatform.OSXEditor) {
                // project specific temp directory that is writeable on MacOS (Path.GetTempPath() wasn't when run through HotReload.app)
                return Path.GetFullPath("Library/com.pancake.hotreload/HotReloadServerTemp");
            } else {
                return Path.Combine(Path.GetTempPath(), "HotReloadTemp");
            }
        }
        
        public static string GetCliTempDir() {
            return Path.Combine(GetHotReloadTempDir(), "MethodPatches");
        }
        
        public static void Chmod(string targetFile, string flags = "+x") {
            // ReSharper disable once PossibleNullReferenceException
            Process.Start(new ProcessStartInfo("chmod", $"{flags} \"{targetFile}\"") {
                UseShellExecute = false,
            }).WaitForExit(2000);
        }
        
        public static bool TryFindServerExecutable(ICliController controller, out string path) {
            const string serverBasePath = "Packages/com.pancake.hotreload/Server";
            var executablePath = controller.GetDefaultExecutablePath(serverBasePath);
            
            if(File.Exists(executablePath)) {
                path = Path.GetFullPath(executablePath);
                return true;
            }
            
            //Not found in packages. Try to find in assets folder.
            //fast path - this is the expected folder
            var alternativeExecutablePath = executablePath.Replace(serverBasePath, "Assets/HotReload/Server");
            if(File.Exists(alternativeExecutablePath)) {
                path = Path.GetFullPath(alternativeExecutablePath);
                return true;
            }
            //slow path - try to find the executable somewhere in the assets folder
            var files = Directory.GetFiles("Assets", Path.GetFileName(executablePath), SearchOption.AllDirectories);
            if(files.Length > 0) {
                path = Path.GetFullPath(files[0]);
                return true;
            }
            path = null;
            return false;
        }
        
        public static string GetPidFilePath(string hotreloadTempDir) {
            return Path.GetFullPath(Path.Combine(hotreloadTempDir, "server.pid"));
        }
        
        public static void KillLastKnownHotReloadProcess() {
            var pidPath = GetPidFilePath(GetHotReloadTempDir());
            try {
                var pid = int.Parse(File.ReadAllText(pidPath));
                Process.GetProcessById(pid).Kill();
            }
            catch {
                //ignore
            }
            File.Delete(pidPath);
        }
    }
}