using System;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Serialization;

namespace SingularityGroup.HotReload {
    /// <summary>
    /// HotReload runtime settings. These can be changed while the app is running.
    /// </summary>
    /// <remarks>
    /// ScriptableObject that may be included in Resources/ folder.
    /// See also Editor/PrebuildIncludeResources.cs
    /// </remarks>
    // [CreateAssetMenu(fileName = resourceName + ".asset",
    //     menuName = "ScriptableObjects/" + resourceName)]
    [Serializable]
    public class HotReloadSettingsObject : ScriptableObject {
        #region singleton
        private static HotReloadSettingsObject _I;
        public static HotReloadSettingsObject I {
            get {
                if (_I == null) {
                    _I = LoadSettings();
                }
                return _I;
            }
        }

        /// <summary>Create settings inside Assets/ because user cannot edit files that are included inside a Unity package</summary>
        public const string editorAssetPath = "Assets/HotReload/HotReloadSettingsObject.asset";

        private const string resourceName = "HotReloadSettingsObject";
        
        public static bool TryLoadSettings(out HotReloadSettingsObject settings) {
            try {
                settings = LoadSettings();
                return settings != null;
            } catch(FileNotFoundException) {
                settings = null;
                return false;
            }
        }

        private static HotReloadSettingsObject LoadSettings() {
            HotReloadSettingsObject settings;
            if (Application.isEditor) {
                #if UNITY_EDITOR
                settings = AssetDatabase.LoadAssetAtPath<HotReloadSettingsObject>(editorAssetPath);
                #else
                settings = null;
                #endif
            } else {
                // load from Resources (assumes that build includes the resource)
                settings = Resources.Load<HotReloadSettingsObject>(resourceName);
            }
            if (settings != null) {
                return settings;
            } else {
                throw new FileNotFoundException("No HotReloadSettingsObject found.");
            }
        }
        #endregion

        #region settings

        #if UNITY_EDITOR
        /// <summary>Set default values.</summary>
        /// <remarks>
        /// This is called by the Unity editor when the ScriptableObject is first created.
        /// This function is only called in editor mode.
        /// </remarks>
        private void Reset() {
            EnsurePrefabSetCorrectly();
        }

        // Call this during build, just to be sure the field is correct. (I had some issues with it while editing the prefab)
        public void EnsurePrefabSetCorrectly() {
            SettingsOverlayPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(SettingsOverlay.prefabAssetPath);
        }
        #endif
        
        // put the stored settings here

        [Header("Build Settings")]
        [Tooltip("Should the Hot Reload runtime be included in development builds? HotReload is never included in release builds.")]
        public bool IncludeInBuild = true;

        [Header("Player Settings")]
        public bool ShowOptionsPopupOnStartup = true;

        /// Reference to the Prefab, for loading it at runtime
        [HideInInspector]
        public GameObject SettingsOverlayPrefab;
        
        #endregion settings
    }
}