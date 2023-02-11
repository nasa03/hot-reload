using System;
using System.IO;
using UnityEditor;
using UnityEngine;
#if UNITY_2019_4_OR_NEWER
using System.Reflection;
using Unity.CodeEditor;
#endif

namespace SingularityGroup.HotReload.Editor {
    static class InstallUtility {
        const string installFlagPath = "Library/com.singularitygroup.hotreload/installFlag.txt";


        // HandleEditorStart is only called on editor start, not on domain reload
        public static void HandleEditorStart(string updatedFromVersion) {
            var showOnStartup = HotReloadPrefs.GetShowOnStartupEnum();
            if (showOnStartup == ShowOnStartupEnum.Always || (showOnStartup == ShowOnStartupEnum.OnNewVersion && !String.IsNullOrEmpty(updatedFromVersion))) {
                HotReloadWindow.Open();
            }
        }

        public static void CheckForNewInstall() {
            if(File.Exists(installFlagPath)) {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(installFlagPath));
            using(File.Create(installFlagPath)) { }
            //Avoid opening the window on domain reload
            EditorApplication.delayCall += HandleNewInstall;
        }
        
        static void HandleNewInstall() {
            HotReloadWindow.Open();
            //Give unity time to draw the first frame of the hot reload window.
            EditorApplication.delayCall += () => {
                var saveAssets = false;
                foreach (var presenter in RequiredSettings.Presenters) {
                    var result = presenter.ShowWarningPromptIfRequired();
                    saveAssets |= result.requiresSaveAssets;
                }
                if(saveAssets) {
                    AssetDatabase.SaveAssets();
                }
            };
        }

        internal static void RegenerateProjectFiles() {
#if UNITY_2019_4_OR_NEWER
            var editor = CodeEditor.CurrentEditor;
            if(editor.GetType().Name == "RiderScriptEditor") {
                editor.GetType().GetMethod("SyncSolution", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            } else {
                editor.SyncAll();
            }
#endif
        }
    }
}