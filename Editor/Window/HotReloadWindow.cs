
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using SingularityGroup.HotReload.DTO;
using SingularityGroup.HotReload.Editor.Semver;
using UnityEditor;
using UnityEngine;

namespace SingularityGroup.HotReload.Editor {
    public class HotReloadWindow : EditorWindow {
        public static HotReloadWindow Current { get; private set; }

        private static readonly Dictionary<string, PatchInfo> pendingPatches = new Dictionary<string, PatchInfo>();

        private List<HotReloadTabBase> tabs;
        private int selectedTab;

        private Vector2 scrollPos;


        internal HotReloadRunTab runTab;
        internal HotReloadOptionsTab optionsTab;
        internal HotReloadAboutTab aboutTab;

        ShowOnStartupEnum _showOnStartupOption;

        /// <summary>
        /// This token is cancelled when the EditorWindow is disabled.
        /// </summary>
        /// <remarks>
        /// Use it for all tasks.
        /// When token is cancelled, scripts are about to be recompiled and this will cause tasks to fail for weird reasons.
        /// </remarks>
        public CancellationToken cancelToken;
        private CancellationTokenSource cancelTokenSource;

        private static readonly PackageUpdateChecker packageUpdateChecker = new PackageUpdateChecker();

        [MenuItem("Window/Hot Reload &#H")]
        internal static void Open() {
            if (Current) {
                Current.Show();
                Current.Focus();
            } else {
                Current = GetWindow<HotReloadWindow>();
            }
        }

        internal static void RegisterPatch(MethodPatchResponse response) {
            if (!HotReloadPrefs.AutoApply) {
                pendingPatches.Add(response.id, new PatchInfo(response));
            }
        }

        internal static void RegisterWarnings(IEnumerable<string> newWarnings) {
            foreach (var warning in newWarnings) {
                Debug.LogWarningFormat("[{0}] {1}", CodePatcher.TAG, warning);
            }
        }

        private void OnEnable() {
            CodePatcher.I.AutoApply = HotReloadPrefs.AutoApply;

            Current = this;

            if (cancelTokenSource != null) {
                cancelTokenSource.Cancel();
            }
            cancelTokenSource = new CancellationTokenSource();
            cancelToken = cancelTokenSource.Token;

            runTab = new HotReloadRunTab(this);
            optionsTab = new HotReloadOptionsTab(this);
            aboutTab = new HotReloadAboutTab(this);
            tabs = new List<HotReloadTabBase> {
                runTab,
                optionsTab,
                aboutTab,
            };

            this.minSize = new Vector2(300, 150f);
            var tex = Resources.Load<Texture>(EditorGUIUtility.isProSkin ? "Icon_DarkMode" : "Icon_LightMode");
            this.titleContent = new GUIContent(" Hot Reload", tex);
            this._showOnStartupOption = HotReloadPrefs.GetShowOnStartupEnum();

            foreach (var patch in CodePatcher.I.PendingPatches) {
                pendingPatches.Add(patch.id, new PatchInfo(patch));
            }
            packageUpdateChecker.StartCheckingForNewVersion();
        }

        private void Update() {
            foreach (var tab in tabs) {
                tab.Update();
            }
        }

        private void OnDisable() {
            if (cancelTokenSource != null) {
                cancelTokenSource.Cancel();
                cancelTokenSource = null;
            }

            if (Current == this) {
                Current = null;
            }
        }

        internal void SelectTab(Type tabType) {
            selectedTab = tabs.FindIndex(x => x.GetType() == tabType);
        }

        private void OnGUI() {
            using(var scope = new EditorGUILayout.ScrollViewScope(scrollPos, false, false)) {
                scrollPos = scope.scrollPosition;
                // RenderDebug();
                RenderTabs();
            }
            GUILayout.FlexibleSpace(); // GUI below will be rendered on the bottom
            RenderBottomBar();
        }

        private void RenderDebug() {
            if (GUILayout.Button("RESET WINDOW")) {
                OnDisable();

                RequestHelper.RequestLogin("test", "test", 1).Forget();

                HotReloadPrefs.AutoApply = true;
                HotReloadPrefs.RemoteServer = false;
                HotReloadPrefs.RemoteServerHost = null;
                HotReloadPrefs.LicenseEmail = null;
                HotReloadPrefs.ExposeServerToLocalNetwork = true;
                HotReloadPrefs.LicensePassword = null;
                HotReloadPrefs.RenderAuthLogin = false;
                HotReloadPrefs.FirstLogin = true;
                foreach (var settingCache in HotReloadPrefs.SettingCacheKeys) {
                    EditorPrefs.SetBool(settingCache, true);
                }
                foreach (var presenter in RequiredSettings.Presenters) {
                    presenter.DebugReset();
                }
                OnEnable();
            }
        }

        private void RenderLogo() {
            var isDarkMode = EditorGUIUtility.isProSkin;
            var tex = Resources.Load<Texture>(isDarkMode ? "Logo_HotReload_DarkMode" : "Logo_HotReload_LightMode");
            //Can happen during player builds where Editor Resources are unavailable
            if(tex == null) {
                return;
            }
            var targetHeight = tex.height;
            var targetWidth = tex.width;
            const int topPadding = 22;
            GUILayout.Space(topPadding);
            // reserve layout space for the texture
            var rect = GUILayoutUtility.GetRect(targetWidth, targetHeight);
            // draw the texture into that reserved space
            GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
        }

        private void RenderTabs() {
            using(new EditorGUILayout.VerticalScope(HotReloadWindowStyles.BoxStyle)) {
                selectedTab = GUILayout.Toolbar(
                    selectedTab,
                    tabs.Select(t => new GUIContent(t.Title.StartsWith(" ", StringComparison.Ordinal) ? t.Title : " " + t.Title, EditorGUIUtility.IconContent(t.Icon).image, t.Tooltip)).ToArray()
                );
                RenderLogo();

                tabs[selectedTab].OnGUI();
            }
        }

        private void RenderBottomBar() {
            SemVersion newVersion;
            var updateAvailable = packageUpdateChecker.TryGetNewVersion(out newVersion); 
            // var updateAvailable = true;
            // newVersion = SemVersion.Parse("9.9.9");
            using(new EditorGUILayout.HorizontalScope("ProjectBrowserBottomBarBg", GUILayout.ExpandWidth(true), GUILayout.Height(updateAvailable ? 28f : 25f))) {
                RenderBottomBarCore(updateAvailable, newVersion);
            }
        }

        private void RenderBottomBarCore(bool updateAvailable, SemVersion newVersion) {
            if (updateAvailable) {
                var btn = EditorStyles.miniButton;
                var prevStyle = btn.fontStyle;
                var prevSize = btn.fontSize;
                try {
                    btn.fontStyle = FontStyle.Bold;
                    btn.fontSize = 11;
                    if (GUILayout.Button($"Update To v{newVersion}", btn, GUILayout.MaxWidth(140), GUILayout.ExpandHeight(true))) {
                        var _ = packageUpdateChecker.UpdatePackageAsync(newVersion);
                    }
                } finally {
                    btn.fontStyle = prevStyle;
                    btn.fontSize = prevSize;
                }
            }

            GUILayout.FlexibleSpace();
            using(var changeScope = new EditorGUI.ChangeCheckScope()) {
                var prevLabelWidth = EditorGUIUtility.labelWidth;
                try {
                    EditorGUIUtility.labelWidth = 105f;

                    using (new GUILayout.VerticalScope()) {
                        GUILayout.FlexibleSpace();
                        _showOnStartupOption = (ShowOnStartupEnum)EditorGUILayout.EnumPopup("Show On Startup", _showOnStartupOption, GUILayout.Width(218f));
                        GUILayout.FlexibleSpace();
                    }
                } finally {
                    EditorGUIUtility.labelWidth = prevLabelWidth;
                }
                
                if(changeScope.changed) {
                    HotReloadPrefs.ShowOnStartup = _showOnStartupOption.ToString();
                }
            }
        }

        struct PatchInfo {
            public readonly string patchId;
            public readonly bool apply;
            public readonly string[] methodNames;

            public PatchInfo(MethodPatchResponse response) : this(response.id, apply: true, methodNames: GetMethodNames(response)) { }

            PatchInfo(string patchId, bool apply, string[] methodNames) {
                this.patchId = patchId;
                this.apply = apply;
                this.methodNames = methodNames;
            }


            static string[] GetMethodNames(MethodPatchResponse response) {
                var methodNames = new string[MethodCount(response)];
                var methodIndex = 0;
                for (int i = 0; i < response.patches.Length; i++) {
                    for (int j = 0; j < response.patches[i].modifiedMethods.Length; j++) {
                        var method = response.patches[i].modifiedMethods[j];
                        var displayName = method.displayName;

                        var spaceIndex = displayName.IndexOf(" ", StringComparison.Ordinal);
                        if (spaceIndex > 0) {
                            displayName = displayName.Substring(spaceIndex);
                        }

                        methodNames[methodIndex++] = displayName;
                    }
                }
                return methodNames;
            }

            static int MethodCount(MethodPatchResponse response) {
                var count = 0;
                for (int i = 0; i < response.patches.Length; i++) {
                    count += response.patches[i].modifiedMethods.Length;
                }
                return count;
            }
        }
    }

    public enum ShowOnStartupEnum {
        Always,
        OnNewVersion,
        Never,
    }
}