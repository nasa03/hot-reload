using System;
using System.Threading;
using System.Threading.Tasks;
using SingularityGroup.HotReload.Editor.Cli;
using UnityEditor;
using UnityEngine;

namespace SingularityGroup.HotReload.Editor {
    internal sealed class ExposeServerOption : HotReloadOptionBase {
        private readonly string dataPath;

        private const string optionSummary = "Allow Mobile Builds to Connect (WiFi)";

        public ExposeServerOption() : base(
            optionSummary,
            null,
            HotReloadOptionCategory.Mobile) {

            // get dataPath on main thread.
            dataPath = Application.dataPath;
        }

        protected override void InternalOnGUI() {
            var description = "";
            if (GetValue()) {
                description = "The HotReload server is reachable from devices on the same Wifi network";
            } else {
                description = "The HotReload server is available to your computer only. Other devices cannot connect to it.";
            }
            EditorGUILayout.LabelField(description, HotReloadWindowStyles.WrapStyle);
        }

        protected override void SetValue(bool val) {
            if (val == HotReloadPrefs.ExposeServerToLocalNetwork) {
                return;
            }

            HotReloadPrefs.ExposeServerToLocalNetwork = val;
            RunTask(() => {
                RunOnMainThreadSync(() => {
                    var isRunningResult = ServerHealthCheck.I.IsServerHealthy;
                    if (isRunningResult) {
                        var restartServer = EditorUtility.DisplayDialog("Hot Reload",
                            $"When changing '{optionSummary}', the Hot Reload server must be restarted for this to take effect." +
                            "\nDo you want to restart it now?",
                            "Restart server", "Don't restart");
                        if (restartServer) {
                            bool exposeServerToNetwork = HotReloadPrefs.ExposeServerToLocalNetwork;
                            CodePatcher.I.ClearPatchedMethods();
                            RunTask(() => HotReloadCli.RestartAsync(dataPath, exposeServerToNetwork));
                        }
                    }
                });
            });
        }

        protected override bool GetValue() {
            return HotReloadPrefs.ExposeServerToLocalNetwork;
        }

        void RunTask(Action action) {
            var token = HotReloadWindow.Current.cancelToken;
            Task.Run(() => {
                if (token.IsCancellationRequested) return;
                try {
                    action();
                } catch (Exception ex) {
                    ThreadUtility.LogException(ex, token);
                }
            }, token);
        }
        
        void RunTask(Func<Task> action) {
            var token = HotReloadWindow.Current.cancelToken;
            Task.Run(async () => {
                if (token.IsCancellationRequested) return;
                try {
                    await action();
                } catch (Exception ex) {
                    ThreadUtility.LogException(ex, token);
                }
            }, token);
        }

        void RunOnMainThreadSync(Action action) {
            ThreadUtility.RunOnMainThread(action, HotReloadWindow.Current.cancelToken);
        }
    }
}
