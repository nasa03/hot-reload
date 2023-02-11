using UnityEditor;
using UnityEngine;

namespace SingularityGroup.HotReload.Editor {
    interface IRequiredSettingPresenter {
        ShowResult ShowWarningPromptIfRequired();
        ShowResult ShowHelpBoxIfRequired();
        ShowResult Apply();
        bool CanShowHelpBox();
        bool CanShowWarningPrompt();
        bool CanAutoApply();
        void DebugReset();
    }
    
    class DefaulRequiredSettingPresenter : RequiredSettingPresenter {
        public DefaulRequiredSettingPresenter(RequiredSettingData data) : base(data) { }

        public override ShowResult ShowWarningPromptIfRequired() {
            return ShowWarningPromptIfRequiredCommon();
        }

        public override ShowResult ShowHelpBoxIfRequired() {
            return ShowHelpBoxIfRequiredCommon();
        }

        public override ShowResult Apply() {
            return ApplyCommon();
        }

        public override bool CanShowHelpBox() {
            return CanShowHelpBoxCommon();
        }

        public override bool CanShowWarningPrompt() {
            return CanShowWarningPromptCommon();
        }

        public override bool CanAutoApply() {
            return true;
        }
    }
    
    class SlnFileSettingPresenter : RequiredSettingPresenter {
        public SlnFileSettingPresenter(RequiredSettingData data) : base(data) { }

        public override ShowResult ShowWarningPromptIfRequired() {
            //no op
            return default(ShowResult);
        }

        public override ShowResult ShowHelpBoxIfRequired() {
#if UNITY_2019_4_OR_NEWER
            return ShowHelpBoxIfRequiredCommon();
#else
            if (CanShowHelpBox()) {
                EditorGUILayout.HelpBox("No .sln found. Please open any C# file to generate the missing .sln file.", MessageType.Warning);
                if (GUILayout.Button("Hide")) {
                    EditorPrefs.SetBool(data.cacheKey, false);
                }
                return new ShowResult {
                    shown = true, 
                    requiresSaveAssets = false
                };
            }
            return default(ShowResult);
#endif
        }

        public override ShowResult Apply() {
#if UNITY_2019_4_OR_NEWER
            return ApplyCommon();
#else
            return default(ShowResult);
#endif
        }

        public override bool CanShowHelpBox() {
#if UNITY_2019_4_OR_NEWER
            return CanShowHelpBoxCommon();
#else
            return !data.checker.IsApplied() && EditorPrefs.GetBool(data.cacheKey, true);
#endif
        }

        public override bool CanShowWarningPrompt() {
            return false;
        }

        public override bool CanAutoApply() {
            return false;
        }
    }
    
    abstract class RequiredSettingPresenter : IRequiredSettingPresenter {
        public readonly RequiredSettingData data;
        protected RequiredSettingPresenter(RequiredSettingData data) {
            this.data = data;
        }
        
        static GUILayoutOption[] _nonExpandable;
        public static GUILayoutOption[] NonExpandableLayout => _nonExpandable ?? (_nonExpandable = new [] {GUILayout.ExpandWidth(false)});

        protected ShowResult ShowWarningPromptIfRequiredCommon() {
            if (CanShowWarningPromptCommon()) {
                var rd = data.installPromptRenderData;
                var requiresSaveAssets = false;
                if(EditorUtility.DisplayDialog(rd.title, rd.message, rd.ok, rd.cancel)) {
                    data.checker.Apply();
                    requiresSaveAssets = data.checker.ApplyRequiresSaveAssets;
                }
                return new ShowResult {
                    shown = true,
                    requiresSaveAssets = requiresSaveAssets
                };
            }
            return default(ShowResult);
        }
        
        protected ShowResult ShowHelpBoxIfRequiredCommon() {
            if (CanShowHelpBoxCommon()) {
                var rd = data.helpBoxRenderData;
                EditorGUILayout.HelpBox(rd.description, rd.messageType);
                
                var allowHide = rd.messageType != MessageType.Error;

                if (allowHide) {
                    EditorGUILayout.BeginHorizontal();
                }
                var result = new ShowResult { shown = true };
                if (GUILayout.Button(rd.buttonText)) {
                    result = Apply();
                }
                if (allowHide) {
                    if (GUILayout.Button("Hide", NonExpandableLayout)) {
                        EditorPrefs.SetBool(data.cacheKey, false);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                
                return result;
            }
            return default(ShowResult);
        }
        
        protected ShowResult ApplyCommon() {
            data.checker.Apply();
            return new ShowResult {
                shown = true,
                requiresSaveAssets = data.checker.ApplyRequiresSaveAssets,
            };
        }
        
        protected bool CanShowHelpBoxCommon() {
            return data.helpBoxRenderData != null && !data.checker.IsApplied() && (EditorPrefs.GetBool(data.cacheKey, true) || data.helpBoxRenderData.messageType == MessageType.Error);
        }
        
        protected bool CanShowWarningPromptCommon() {
            return data.installPromptRenderData != null && !data.checker.IsApplied() && EditorPrefs.GetBool(data.cacheKey, true);
        }

        public abstract ShowResult ShowWarningPromptIfRequired();
        public abstract ShowResult ShowHelpBoxIfRequired();
        public abstract ShowResult Apply();
        public abstract bool CanShowHelpBox();
        public abstract bool CanShowWarningPrompt();
        public abstract bool CanAutoApply();
        public void DebugReset() {
            data.checker.DebugReset();
        }
    }
}
