using System;
using System.IO;
using System.Threading.Tasks;
using SingularityGroup.HotReload.Editor.Semver;
using SingularityGroup.HotReload.Newtonsoft.Json;
using SingularityGroup.HotReload.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace SingularityGroup.HotReload.Editor {
    internal class PackageUpdateChecker {
        const string persistedFile = "Library/com.singularitygroup.hotreload/updateChecker.json";
        readonly JsonSerializer jsonSerializer = JsonSerializer.CreateDefault();
        SemVersion newVersionDetected;
        bool started;

        private static TimeSpan RetryInterval => TimeSpan.FromSeconds(30);
        private static TimeSpan CheckInterval => TimeSpan.FromHours(1);

        
        public async void StartCheckingForNewVersion() {
            if(started) {
                return;
            }
            started = true;
            
            for (;;) {
                try {
                    await PerformVersionCheck();
                    if(newVersionDetected != null) {
                        break;
                    }
                } catch(Exception ex) {
                    Debug.LogWarning($"encountered exception when checking for new Hot Reload package version:\n{ex}");
                }
                await Task.Delay(RetryInterval);
            }
        }
        
        public bool TryGetNewVersion(out SemVersion version) {
             return !ReferenceEquals(version = newVersionDetected, null);
        }
        
        async Task PerformVersionCheck() { 
            var state = await LoadPersistedState();
            var currentVersion = SemVersion.Parse(PackageConst.Version, strict: true);
            if(state != null) {
                var newVersion = SemVersion.Parse(state.lastRemotePackageVersion);
                if(newVersion > currentVersion) {
                    newVersionDetected = newVersion;
                    return;
                }
                if(DateTime.UtcNow - state.lastVersionCheck < CheckInterval) {
                    return;
                }
            }
            
            var response = await GetLatestPackageVersion();
            if(response.err != null) {
                if(response.statusCode == 0 || response.statusCode == 404) {
                    // probably no internet, fail silently and retry
                } else {
                    Debug.LogWarning($"version check failed: {response.err}");
                }
            } else {
                var newVersion = response.data;
                if (response.data > currentVersion) {
                    newVersionDetected = newVersion;
                }
                await Task.Run(() => PersistState(response.data));
            }
        }

        void PersistState(SemVersion newVersion) {
            // ReSharper disable once AssignNullToNotNullAttribute
            var fi = new FileInfo(persistedFile);
            fi.Directory.Create();
            using (var streamWriter = new StreamWriter(fi.OpenWrite()))
            using (var writer = new JsonTextWriter(streamWriter)) {
                jsonSerializer.Serialize(writer, new State {
                    lastVersionCheck = DateTime.UtcNow,
                    lastRemotePackageVersion = newVersion.ToString()
                });
            }
        }
        
        Task<State> LoadPersistedState() {
            return Task.Run(() => {
                var fi = new FileInfo(persistedFile);
                if(!fi.Exists) {
                    return null;
                }
                
                using(var streamReader = fi.OpenText())
                using(var reader = new JsonTextReader(streamReader)) {
                    return jsonSerializer.Deserialize<State>(reader);
                }
            });
        }
        


        static async Task<Response<SemVersion>> GetLatestPackageVersion() {
            const string versionUrl = "https://gitlab.com/api/v4/projects/42504521/repository/files/package.json/raw?ref=production";
            using(var www = UnityWebRequest.Get(versionUrl)) {
                await RequestHelper.SendRequestAsync(www);
                if (!string.IsNullOrEmpty(www.error)) {
                    return new Response<SemVersion>(null, www.error, www.responseCode);
                }
                var json = www.downloadHandler.text;
                var o = JObject.Load(new JsonTextReader(new StringReader(json)));
                SemVersion newVersion;
                JToken value;
                if (!o.TryGetValue("version", out value)) {
                    return Response.FromError<SemVersion>("Invalid package.json");
                } else if(!SemVersion.TryParse(value.Value<string>(), out newVersion, strict: true)) {
                    return Response.FromError<SemVersion>($"Invalid version in package.json: '{value.Value<string>()}'");
                } else {
                    return Response.FromResult(newVersion);
                }
            }
        }
        
        public async Task UpdatePackageAsync(SemVersion newVersion) {
            if(await IsUsingGitRepo()) {
                //Package can be updated by updating the git url via the package manager
                if(EditorUtility.DisplayDialog($"Update To v{newVersion}", $"By pressing 'Update' the Hot Reload package will be updated to v{newVersion}", "Update", "Cancel")) {
                    var err = UpdateGitUrlInManifest(newVersion);
                    if(err != null) {
                        Debug.LogWarning($"Encountered issue when updating Hot Reload: {err}");
                    } else {
                        //Delete state to force another version check after the package is installed
                        File.Delete(persistedFile);
                        #if UNITY_2020_3_OR_NEWER
                        UnityEditor.PackageManager.Client.Resolve();
                        #else
                        AssetDatabase.Refresh();
                        #endif
                    }
                }
            } else {
                Application.OpenURL(Constants.DownloadUrl);
            }
        }
        
        string UpdateGitUrlInManifest(SemVersion newVersion) {
            const string repoUrl = "git+https://gitlab.com/singularitygroup/hot-reload-for-unity.git";
            const string manifestJsonPath = "Packages/manifest.json";
            var repoUrlToNewVersion = $"{repoUrl}#{newVersion}";
            if(!File.Exists(manifestJsonPath)) {
                return "Unable to find manifest.json";
            }
            
            var root = JObject.Load(new JsonTextReader(new StringReader(File.ReadAllText(manifestJsonPath))));
            JObject deps;
            var err = TryGetManfestDeps(root, out deps);
            if(err != null) {
                return err;
            }
            deps[PackageConst.PackageName] = repoUrlToNewVersion;
            root["dependencies"] = deps;
            File.WriteAllText(manifestJsonPath, root.ToString(Formatting.Indented));
            return null;
        }
        
        static string TryGetManfestDeps(JObject root, out JObject deps) {
            JToken value;
            if(!root.TryGetValue("dependencies", out value)) {
                deps = null;
                return "no dependencies object found in manifest.json";
            }
            deps = value.Value<JObject>();
            if(deps == null) {
                return "dependencies object null in manifest.json";
            }
            return null;
        }

        static async Task<bool> IsUsingGitRepo() {
            var respose = await Task.Run(() => IsUsingGitRepoThreaded(PackageConst.PackageName));
            if(respose.err != null) {
                Debug.LogWarning($"Unable to find package. message: {respose.err}");
                return false;
            } else {
                return respose.data;
            }
        }
        
        static Response<bool> IsUsingGitRepoThreaded(string packageId) {
            var fi = new FileInfo("Packages/manifest.json");
            if(!fi.Exists) {
                return "Unable to find manifest.json";
            }
            
            using(var reader = fi.OpenText()) {
                var root = JObject.Load(new JsonTextReader(reader));
                JObject deps;
                var err = TryGetManfestDeps(root, out deps);
                if(err != null) {
                    return "no dependencies specified in manifest.json";
                }
                JToken value;
                if(!deps.TryGetValue(packageId, out value)) {
                    //Likely a local package directly in the packages folder of the unity project
                    //or the package got moved into the Assets folder
                    return Response.FromResult(false);
                }
                var pathToPackage = value.Value<string>();
                if(pathToPackage.StartsWith("git+", StringComparison.Ordinal)) {
                    return Response.FromResult(true);
                }
                if(pathToPackage.StartsWith("https://", StringComparison.Ordinal)) {
                    return Response.FromResult(true);
                }
                return Response.FromResult(false);
            }
        }

        class Response<T> {
            public readonly T data;
            public readonly string err;
            public readonly long statusCode;
            public Response(T data, string err, long statusCode) {
                this.data = data;
                this.err = err;
                this.statusCode = statusCode;
            }
            
            public static implicit operator Response<T>( string err) {
                return Response.FromError<T>(err);
            }
        }
        
        static class Response {
            public static Response<T> FromError<T>(string error) {
                return new Response<T>(default(T), error, -1);
            }
            public static Response<T> FromResult<T>(T result) {
                return new Response<T>(result, null, 200);
            }
        }
        
        class State {
            public DateTime lastVersionCheck;
            public string lastRemotePackageVersion;
        }
    }
    
    
}