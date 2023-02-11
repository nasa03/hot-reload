using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace SingularityGroup.HotReload.Editor {
    internal static class HotReloadBuildHelper {
        /// <summary>
        /// Should HotReload runtime be included in the current build?
        /// </summary>
        public static bool IncludeInThisBuild() {
            HotReloadSettingsObject settings;
            var buildTypeSupported = IsMonoScripting() && EditorUserBuildSettings.development;
            return buildTypeSupported && HotReloadSettingsObject.TryLoadSettings(out settings) && settings.IncludeInBuild;
        }

        private static bool IsMonoScripting() {
#pragma warning disable CS0618
            return PlayerSettings.GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup)
#pragma warning restore CS0618
                == ScriptingImplementation.Mono2x;
        }
    }
    
#pragma warning disable CS0618
    /// <summary>Includes HotReload Resources only in development builds</summary>
    /// <remarks>
    /// This build script ensures that HotReload Resources are not included in release builds.
    /// <para>
    /// When HotReload is enabled:<br/>
    ///   - include HotReloadSettingsObject in development Android builds.<br/>
    ///   - exclude HotReloadSettingsObject from the build.<br/>
    /// When HotReload is disabled:<br/>
    ///   - excludes HotReloadSettingsObject from the build.<br/>
    /// </para>
    /// </remarks>
    internal class PrebuildIncludeResources : IPreprocessBuild {
#pragma warning restore CS0618
        public int callbackOrder => 10;
        
        public void OnPreprocessBuild(BuildTarget target, string path) {
            try {
                if (HotReloadBuildHelper.IncludeInThisBuild()) {
                    // move scriptable object into Resources/ folder
                    HotReloadSettingsEditor.AddOrRemoveFromBuild(true);
                } else {
                    // make sure HotReload resources are not in the build
                    HotReloadSettingsEditor.AddOrRemoveFromBuild(false);
                }
            } catch (BuildFailedException) {
                throw;
            } catch (Exception ex) {
                throw new BuildFailedException(ex);
            }
        }
    }

#pragma warning disable CS0618
    internal class PostbuildIncludeResources : IPostprocessBuild {
#pragma warning restore CS0618
        public int callbackOrder => 10;
        public void OnPostprocessBuild(BuildTarget target, string path) {
            // Ensure duplicate file is removed (avoid user editing it and thinking the changes will be saved for next build)
            HotReloadSettingsEditor.RemoveAssetFromBuild();
        }
    }

#pragma warning disable CS0618
    /// <remarks>
    /// <para>
    /// This class sets option in the AndroidManifest that you choose in HotReload build settings.
    /// </para>
    /// <para>
    /// - To connect to the HotReload server through the local network, we need to permit access to http://192...<br/>
    /// - Starting with Android 9, insecure http requests are not allowed by default and must be whitelisted
    /// </para>
    /// </remarks>
    internal class PostbuildModifyAndroidManifest : IPostGenerateGradleAndroidProject {
#pragma warning restore CS0618
        public int callbackOrder => 10;

        private const string manifestFileName = "AndroidManifest.xml";

        public void OnPostGenerateGradleAndroidProject(string path) {
            if (!HotReloadBuildHelper.IncludeInThisBuild()) {
                return;
            }
            // Assume path is to {gradleProject}/unityLibrary/ which is roughly the same across Unity versions 2018/2019/2020/2021
            var manifestFilePath = FindAndroidManifest(path);
            if (manifestFilePath == null) {
                // not found
                Debug.LogWarning($"[{CodePatcher.TAG}] Unable to find {manifestFileName}");
                return;
            }
            Debug.Log($"[{CodePatcher.TAG}] Found manifest, modifying it for the settings you chose in Hot Reload build settings." + 
                      $"\nManifest filepath: {manifestFilePath}");
            
            // todo: option in HotReload EditorWindow to skip this (user may have their own configuration for usesCleartextTraffic)
            SetUsesCleartextTraffic(manifestFilePath);
        }

        private static string FindAndroidManifest(string unityLibraryPath) {
            // find the AndroidManifest.xml file which we can edit
            var dir = new DirectoryInfo(unityLibraryPath);
            var manifestFilePath = Path.Combine(dir.FullName, "src", "main", manifestFileName);
            if (File.Exists(manifestFilePath)) {
                return manifestFilePath;
            }

            Debug.Log($"[{CodePatcher.TAG}] Did not find {manifestFileName} at {manifestFilePath}, searching for manifest file inside {dir.FullName}");
            var manifestFiles = dir.GetFiles(manifestFileName, SearchOption.AllDirectories);
            if (manifestFiles.Length == 0) {
                return null;
            }

            foreach (var file in manifestFiles) {
                if (file.FullName.Contains("src")) {
                    // good choice
                    return file.FullName;
                }
            }
            // fallback to the first file found
            return manifestFiles[0].FullName;
        }

        /// <summary>
        /// Set option android:usesCleartextTraffic="true"

        /// </summary>
        /// <param name="manifestFilePath">Absolute filepath to the unityLibrary AndroidManifest.xml file</param>
        private static void SetUsesCleartextTraffic(string manifestFilePath) {
            // Ideally we would create or modify a "Network Security Configuration file" to permit access to local ip addresses
            // https://developer.android.com/training/articles/security-config#manifest
            // but that becomes difficult when the user has their own configuration file - would need to search for it and it may be inside an aar.
            var contents = File.ReadAllText(manifestFilePath);
            if (contents.Contains("android:usesCleartextTraffic=")) {
                // user has already set this themselves, don't replace it
                return;
            }
            var newContents = Regex.Replace(contents,
                @"<application\s",
                "<application android:usesCleartextTraffic=\"true\" "
            );
            newContents += $"\n<!-- [{CodePatcher.TAG}] Added android:usesCleartextTraffic=\"true\" to permit connecting to the Hot Reload http server running on your machine. -->";
            newContents += $"\n<!-- [{CodePatcher.TAG}] This change only happens in Unity development builds. You can disable this in the Hot Reload settings window. -->";
            File.WriteAllText(manifestFilePath, newContents);
        }
    }
}
