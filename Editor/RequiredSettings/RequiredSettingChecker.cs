using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#if UNITY_2019_4_OR_NEWER
using Unity.CodeEditor;
#endif

namespace SingularityGroup.HotReload.Editor {
    interface IRequiredSettingChecker {
        bool IsApplied();
        void Apply();
        void DebugReset();
        bool ApplyRequiresSaveAssets {get;}
    }

    
    class AllowHttpSettingChecker : IRequiredSettingChecker {
        public bool IsApplied() {
#           if (UNITY_2022_1_OR_NEWER)
                return PlayerSettings.insecureHttpOption != InsecureHttpOption.NotAllowed;
#           else
                return true;
#endif
        }

        public void Apply() {
#           if (UNITY_2022_1_OR_NEWER)
                PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
#endif
        }

        public void DebugReset() {
#           if (UNITY_2022_1_OR_NEWER)
                PlayerSettings.insecureHttpOption = InsecureHttpOption.NotAllowed;
#endif
        }

        public bool ApplyRequiresSaveAssets => true;
    }

    class AutoRefreshSettingChecker : IRequiredSettingChecker {
        const string autoRefreshKey = "kAutoRefresh";
        const string autoRefreshModeKey = "kAutoRefreshMode";

        private int AutoRefreshPreference {
            get {
                // From Unity 2021.3 onwards, the key is "kAutoRefreshMode".
                #if UNITY_2021_3_OR_NEWER
                return EditorPrefs.GetInt(autoRefreshModeKey);
                #else
                return EditorPrefs.GetInt(autoRefreshKey);
                #endif
            }
            set {
                #if UNITY_2021_3_OR_NEWER
                EditorPrefs.SetInt(autoRefreshModeKey, value);
                #else
                EditorPrefs.SetInt(autoRefreshKey, value);
                #endif
            }
        }

        public bool IsApplied() {
            // Before Unity 2021.3, value is 0 or 1. Only value of 1 is a problem.
            // From Unity 2021.3 onwards, the key is "kAutoRefreshMode".
            // kAutoRefreshMode options are:
            //   0: disabled
            //   1: enabled 
            //   2: enabled outside playmode
            // only option 1 is a problem
            return AutoRefreshPreference != 1;
        }

        public void Apply() {
            #if UNITY_2021_3_OR_NEWER
            // On these newer Unity versions, Visual Studio is also checking the kAutoRefresh setting (but it should only check kAutoRefreshMode).
            // In previous HotReload version, we were also changing kAutoRefresh to 0 (which breaks Visual Studio triggering auto refresh
            // even after the user reset the Auto Refresh mode in Unity preferences).
            // To fix it for these users, we set kAutoRefresh to 1 when they apply the setting (Unity 2021.3+ doesn't use it).
            if (EditorPrefs.GetInt(autoRefreshKey) == 0) {
                EditorPrefs.SetInt(autoRefreshKey, 1);
            }
            AutoRefreshPreference = 2; // enabled outside playmode
            #else
            AutoRefreshPreference = 0; // disabled
            #endif
            // Dialog is rather annoying. Assume the user also wants the other one, to avoid 2 dialogs
            ScriptCompilationSettingChecker.I.Apply();
        }

        public bool ApplyRequiresSaveAssets => ScriptCompilationSettingChecker.I.ApplyRequiresSaveAssets;

        public void DebugReset() {
            AutoRefreshPreference = 1;
            // Dialog is rather annoying. Assume the user also wants the other one, to avoid 2 dialogs
            ScriptCompilationSettingChecker.I.DebugReset();
        }
    }
    
    class ScriptCompilationSettingChecker : IRequiredSettingChecker {
        public static readonly ScriptCompilationSettingChecker I = new ScriptCompilationSettingChecker(); 
        
        const string scriptCompilationKey = "ScriptCompilationDuringPlay";
        
        public bool IsApplied() {
            var status = EditorPrefs.GetInt(scriptCompilationKey);
#           if (UNITY_2021_1_OR_NEWER)
                // we can be sure that all 3 options are available, so recommend 'Recompile After Finished Playing'
                // (Unity removed/re-added the setting in multiple builds, so we don't know what's available)
                return status != 2;
#           else
                // earlier unity versions didn't have the messy settings problem
                return status == GetRecommendedAutoScriptCompilationKey();
#endif
        }

        public void Apply() {
            EditorPrefs.SetInt(scriptCompilationKey, GetRecommendedAutoScriptCompilationKey());
        }

        public bool ApplyRequiresSaveAssets => false;

        static int GetRecommendedAutoScriptCompilationKey() {
            var existingKey = EditorPrefs.GetInt(scriptCompilationKey);
            if (existingKey == 2) {
                return 1;
            }
#           if (UNITY_2021_1_OR_NEWER)
                return 0; // 'Recompile and Continue Playing'
#           else 
                return 1;
#endif
        }
        
        public void DebugReset() {
            EditorPrefs.SetInt(scriptCompilationKey, 2);
        }
    }
    
    class SlnFileSettingChecker : IRequiredSettingChecker {
        bool hasSlnFile;
        DateTime nextSlnFileCheck;

        public static readonly SlnFileSettingChecker I = new SlnFileSettingChecker();
        
        public bool IsApplied() {
            if(DateTime.UtcNow >= nextSlnFileCheck) {
                hasSlnFile = TryEnsureSlnFile();
                nextSlnFileCheck = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            }
            return hasSlnFile;
        }

        public void Apply() {
            var window = SettingsService.OpenUserPreferences("Preferences/External Tools");
            window.GetType()
                .GetMethod("FilterProviders", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(window, new object[] { "External Script Editor" });
        }

        public bool ApplyRequiresSaveAssets => false;

        string failedToFixEditor;
        private bool TryEnsureSlnFile() {
            var unityProjDir = Path.GetDirectoryName(Application.dataPath);
            var slnPath = Path.Combine(unityProjDir, Path.GetFileName(unityProjDir) + ".sln");
            var hasSlnFile = File.Exists(slnPath);
#if UNITY_2019_4_OR_NEWER
            var editor = CodeEditor.CurrentEditor;
            if(!hasSlnFile && (failedToFixEditor == null || editor.GetType().Name != failedToFixEditor)) {
                InstallUtility.RegenerateProjectFiles();
                hasSlnFile = File.Exists(slnPath);
                if(hasSlnFile) {
                    failedToFixEditor = null;
                } else {
                    failedToFixEditor = editor.GetType().Name;
                }
            }
#endif
            return hasSlnFile;
        }
        
        public void DebugReset() {
            var unityProjDir = Path.GetDirectoryName(Application.dataPath);
            var slnPath = Path.Combine(unityProjDir, Path.GetFileName(unityProjDir) + ".sln");
            File.Delete(slnPath);
        }
    }
    
    class ProjectGenerationSettingsChecker : IRequiredSettingChecker {
        const string generateAllKey = "unity_generate_all_csproj";
        const string generateFlagsKey = "unity_project_generation_flag";
        
        public bool IsApplied() {
            var flags = (ProjectGenerationFlag)EditorPrefs.GetInt(generateFlagsKey);
            return EditorPrefs.GetBool(generateAllKey) && (flags & ProjectGenerationFlag.Local) != 0 && (flags & ProjectGenerationFlag.Embedded) != 0;
        }

        public void Apply() {
            var flags = (ProjectGenerationFlag)EditorPrefs.GetInt(generateFlagsKey);
            flags |= ProjectGenerationFlag.Local | ProjectGenerationFlag.Embedded;
            EditorPrefs.SetBool(generateAllKey, true);
            EditorPrefs.SetInt(generateFlagsKey, (int)flags);
            InstallUtility.RegenerateProjectFiles();
        }
        
        public void DebugReset() {
            var flags = (ProjectGenerationFlag)EditorPrefs.GetInt(generateFlagsKey);
            flags &= ~ProjectGenerationFlag.Local;
            flags &= ~ProjectGenerationFlag.Embedded;
            EditorPrefs.SetBool(generateAllKey, false);
            EditorPrefs.SetInt(generateFlagsKey, (int)flags);
            InstallUtility.RegenerateProjectFiles();
        }

        public bool ApplyRequiresSaveAssets => false;

        [Flags]
        enum ProjectGenerationFlag {
            None = 0,
            Embedded = 1,
            Local = 2,
            Registry = 4,
            Git = 8,
            BuiltIn = 16,
            Unknown = 32,
            PlayerAssemblies = 64,
            LocalTarBall = 128,
        }
    }
}