using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SingularityGroup.HotReload.Editor {
    public static class HotReloadSettingsEditor {

        /// Ensure settings asset is created and return it
        public static HotReloadSettingsObject LoadSettings() {
            return EnsureSettingsCreated();
        }

        /// Load existing settings asset or return the default settings
        public static HotReloadSettingsObject LoadSettingsOrDefault() {
            if (SettingsExists()) {
                return AssetDatabase.LoadAssetAtPath<HotReloadSettingsObject>(HotReloadSettingsObject.editorAssetPath);
            } else {
                // create an instance with default values
                return ScriptableObject.CreateInstance<HotReloadSettingsObject>();
            }
        }

        /// Ensure settings asset file is created and saved
        public static void EnsureSettingsCreated(HotReloadSettingsObject asset) {
            if (!SettingsExists()) {
                CreateNewSettingsFile(asset);
            }
        }

        /// Ensure settings asset file is created and saved
        public static HotReloadSettingsObject EnsureSettingsCreated() {
            if (SettingsExists()) {
                return AssetDatabase.LoadAssetAtPath<HotReloadSettingsObject>(HotReloadSettingsObject.editorAssetPath);
            } else {
                return CreateNewSettingsFile(null);
            }
        }

        /// <summary>
        /// Create settings asset file
        /// </summary>
        /// <remarks>Assume that settings asset doesn't exist yet</remarks>
        /// <returns>The settings asset</returns>
        public static HotReloadSettingsObject CreateNewSettingsFile(HotReloadSettingsObject asset) {
            // create new settings asset
            var editorAssetPath = HotReloadSettingsObject.editorAssetPath;
            // ReSharper disable once AssignNullToNotNullAttribute
            Directory.CreateDirectory(Path.GetDirectoryName(editorAssetPath));
            if (asset == null) {
                asset = ScriptableObject.CreateInstance<HotReloadSettingsObject>();
            }
            AssetDatabase.CreateAsset(asset, editorAssetPath);
            // Saving the asset isn't needed right after you created it. Unity will save it at the appropriate time. 
            return asset;
        }
        #region include/exclude in build

        private const string builtAssetPath =
            "Assets/HotReload/Resources/HotReloadSettingsObject.asset";

        private static bool SettingsExists() {
            return AssetExists(HotReloadSettingsObject.editorAssetPath);
        }

        private static bool AssetExists(string assetPath) {
            return AssetDatabase.GetMainAssetTypeAtPath(assetPath) != null;
        }

        /// At build time:
        ///  - ensure CreateSettings()
        ///  - ensure settings is copied to the Assets/HotReload/Resources/x.asset
        ///     overwrite anything that is already there.
        /// If source settings asset doesn't exist,
        public static void AddOrRemoveFromBuild(bool includeSettingsInBuild) {
            if (includeSettingsInBuild) {
                AssetDatabase.StartAssetEditing();
                try {
                    HotReloadSettingsObject.I.EnsurePrefabSetCorrectly();
                    EnsureSettingsCreated();
                    CopyAssetIntoBuild();
                } finally {
                    AssetDatabase.StopAssetEditing();
                }
            } else {
                RemoveAssetFromBuild();
            }
        }

        public static void CopyAssetIntoBuild() {
            // ReSharper disable once AssignNullToNotNullAttribute
            Directory.CreateDirectory(Path.GetDirectoryName(builtAssetPath));
            if (!AssetDatabase.CopyAsset(HotReloadSettingsObject.editorAssetPath, builtAssetPath)) {
                throw new BuildFailedException(
                    $"AssetDatabase.CopyAsset to {builtAssetPath} failed");
            }
        }

        public static void RemoveAssetFromBuild() {
            if (AssetExists(builtAssetPath)) {
                if (!AssetDatabase.DeleteAsset(builtAssetPath)) {
                    throw new BuildFailedException(
                        $"AssetDatabase.DeleteAsset at {builtAssetPath} failed");
                }
            }
        }

        #endregion
    }
}