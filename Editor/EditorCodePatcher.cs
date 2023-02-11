using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SingularityGroup.HotReload.DTO;
using UnityEditor;
using UnityEngine;

namespace SingularityGroup.HotReload.Editor {
    [InitializeOnLoad]
    public static class EditorCodePatcher {
        const string sessionFilePath = "Library/com.singularitygroup.hotreload/sessionId.txt";
        

        static Timer timer; 
        static readonly string PatchFilePath = null;
        
        static EditorCodePatcher() {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))) {
                return;
            }
            UnityHelper.Init();
            //Use synchonization context if possible because it's more reliable.
            ThreadUtility.InitEditor();
            
            timer = new Timer(OnIntervalThreaded, (Action) OnIntervalMainThread, 500, 500);

            UpdateHost();
            PatchFilePath = PersistencePaths.GetPatchesFilePath(Application.persistentDataPath);
            var compileChecker = CompileChecker.Create();
            compileChecker.onCompilationFinished += OnCompilationFinished;

            EditorApplication.delayCall += InstallUtility.CheckForNewInstall;
            DetectEditorStart();
        }

        // CheckEditorStart distinguishes between domain reload and first editor open
        // We have some separate logic on editor start (InstallUtility.HandleEditorStart)
        private static void DetectEditorStart() {
            var editorId = EditorAnalyticsSessionInfo.id;
            var currVersion = PackageConst.Version;
            Task.Run(() => {
                try {
                    var lines = File.Exists(sessionFilePath) ? File.ReadAllLines(sessionFilePath) : Array.Empty<string>();

                    long prevSessionId = -1;
                    string prevVersion = null;
                    if (lines.Length >= 2) {
                        long.TryParse(lines[1], out prevSessionId);
                    }
                    if (lines.Length >= 3) {
                        prevVersion = lines[2].Trim();
                    }
                    var updatedFromVersion = (prevSessionId != -1 && currVersion != prevVersion) ? prevVersion : null;

                    if (prevSessionId != editorId && prevSessionId != 0) {
                        // back to mainthread
                        ThreadUtility.RunOnMainThread(() => {
                            InstallUtility.HandleEditorStart(updatedFromVersion);

                            var newEditorId = EditorAnalyticsSessionInfo.id;
                            if (newEditorId != 0) {
                                Task.Run(() => {
                                    try {
                                        // editorId isn't available on first domain reload, must do it here
                                        File.WriteAllLines(sessionFilePath, new[] {
                                            "1", // serialization version
                                            newEditorId.ToString(),
                                            currVersion,
                                        });

                                    } catch (IOException) {
                                        // ignore
                                    }
                                });
                            }
                        });
                    }

                } catch (IOException) {
                    // ignore
                } catch (Exception e) {
                    ThreadUtility.LogException(e);
                }
            });
        }

        public static void UpdateHost() {
            string host;
            if (HotReloadPrefs.RemoteServer) {
                host = HotReloadPrefs.RemoteServerHost;
                RequestHelper.ChangeAssemblySearchPaths(Array.Empty<string>());
            } else {
                host = "localhost";
            }
            var rootPath = Path.GetFullPath(".");
            RequestHelper.SetServerInfo(new PatchServerInfo(host, null, rootPath, HotReloadPrefs.RemoteServer));
        }

        static void OnIntervalThreaded(object o) {
            ServerHealthCheck.I.CheckHealth();
            ThreadUtility.RunOnMainThread((Action)o);
        }
        
        static void OnIntervalMainThread() {
            if(ServerHealthCheck.I.IsServerHealthy) {
                RequestHelper.PollMethodPatches(resp => HandleResponseReceived(resp));
            }
        }
        
        static void HandleResponseReceived(MethodPatchResponse response) {
            if(response.patches.Length > 0) {
                CodePatcher.I.RegisterPatches(response);
                HotReloadWindow.RegisterPatch(response);
                var window = HotReloadWindow.Current;
                if(window) {
                    window.Repaint();
                }
            }
            if(response.failures.Length > 0) {
                HotReloadWindow.RegisterWarnings(response.failures);
            }
        }

        static void OnCompilationFinished() {
            ServerHealthCheck.I.CheckHealth();
            if(ServerHealthCheck.I.IsServerHealthy) {
                RequestHelper.RequestCompile().Forget();
            }
            Task.Run(() => File.Delete(PatchFilePath));
        }
    }
}