using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SingularityGroup.HotReload.Editor {
    internal class HotReloadOptionsTab : HotReloadTabBase {
        private readonly HotReloadOptionBase[] _options;
        private readonly Dictionary<HotReloadOptionCategory, bool> _categoryFoldouts;

        /// <remarks>
        /// Opening options tab does not automatically create the settings asset file.
        ///  - The Options UI shows defaults if the object asset doesn't exist.
        ///  - If you change one of the `[Player]` options then we ensure the asset file exists.
        ///  - When a build starts, we also ensure the asset file exists.
        /// </remarks>
        private SerializedObject projectSettingsStorage;
        private HotReloadSettingsObject hotReloadSettingsObject;

        public HotReloadOptionsTab(HotReloadWindow window) : base(window, "Settings", "Settings", "Customize the behavior of Unity Hot Reload.") {
            _options = new HotReloadOptionBase[] {
                // new AutoApplyOption(),
                new ExposeServerOption(),
                new IncludeInBuildOption(),
                new ShowOverlayOnStartupOption()
            };

            _categoryFoldouts = new Dictionary<HotReloadOptionCategory, bool>();
            foreach (var category in Enum.GetValues(typeof(HotReloadOptionCategory)) as HotReloadOptionCategory[]) {
                _categoryFoldouts.Add(category, true);
            }
            hotReloadSettingsObject = HotReloadSettingsEditor.LoadSettingsOrDefault();
            projectSettingsStorage = new SerializedObject(hotReloadSettingsObject);
        }

        public void FocusFoldoutCategory(HotReloadOptionCategory category) {
            foreach (var foldout in _categoryFoldouts.Keys.ToArray()) {
                _categoryFoldouts[foldout] = foldout == category;
            }
        }

        public override void OnGUI() {
            EditorGUILayout.Space();

            EditorGUILayout.Space();
            if (!_window.runTab.FreeLicense) {
                var licenseUnfolded = _categoryFoldouts[HotReloadOptionCategory.License] = EditorGUILayout.Foldout(_categoryFoldouts[HotReloadOptionCategory.License], HotReloadOptionCategory.License.ToString(), true, HotReloadWindowStyles.FoldoutStyle);
                if (licenseUnfolded) {
                    _window.runTab.RenderSettingsLicenseInfo();
                }
            }
            EditorGUILayout.Space();
            if(hotReloadSettingsObject == null) {
                hotReloadSettingsObject = HotReloadSettingsEditor.LoadSettingsOrDefault();
                projectSettingsStorage = new SerializedObject(hotReloadSettingsObject);
            }
            

            var so = projectSettingsStorage;
            so.Update(); // must update in-case asset was modified externally

            HotReloadOptionCategory? lastCategory = null;

            foreach (var option in _options) {
                if (option.Category != lastCategory) {
                    EditorGUILayout.Space();

                    _categoryFoldouts[option.Category] = EditorGUILayout.Foldout(_categoryFoldouts[option.Category], option.Category.ToString(), true, HotReloadWindowStyles.FoldoutStyle);

                    lastCategory = option.Category;
                }

                var projectOption = option as ISerializedProjectOption;
                if (projectOption != null) {
                    projectOption.SettingsObject = so;
                }

                if (_categoryFoldouts[option.Category]) {
                    option.OnGUI();
                }
            }

            // commit any changes to the underlying ScriptableObject
            if (so.hasModifiedProperties) {
                so.ApplyModifiedProperties();
                // Ensure asset file exists on disk, because we initially create it in memory (to provide the default values)
                // This does not save the asset, user has to do that by saving assets in Unity (e.g. press hotkey Ctrl + S)
                var target = so.targetObject as HotReloadSettingsObject;
                if (target == null) {
                    Debug.LogWarning("Unexpected problem unable to save HotReloadSettingsObject");
                } else {
                    HotReloadSettingsEditor.EnsureSettingsCreated(target);
                }
            }
        }
    }
}
