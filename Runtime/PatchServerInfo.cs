using System;
using SingularityGroup.HotReload.Newtonsoft.Json;
using UnityEngine;

namespace SingularityGroup.HotReload {
    [Serializable]
    public class PatchServerInfo {
        public readonly string hostName;
        public readonly string commitHash;
        public readonly string rootPath;
        public readonly bool isRemote;

        public const string UnknownCommitHash = "unknown";

        public PatchServerInfo(string hostName, string commitHash, string rootPath, bool isRemote = false) {
            this.hostName = hostName;
            this.commitHash = commitHash ?? UnknownCommitHash;
            this.rootPath = rootPath;
            this.isRemote = isRemote;
        }

        /// <inheritdoc cref="TryParse(Uri,out SingularityGroup.HotReload.PatchServerInfo)"/>
        public static string TryParse(string uriString, out PatchServerInfo info) {
            return TryParse(new Uri(uriString), out info);
        }

        /// <summary>
        /// Extract server info from deeplink uri
        /// </summary>
        /// <returns>Error message string, or null on success</returns>
        public static string TryParse(Uri uri, out PatchServerInfo info) {
            info = null;
            if (!uri.IsWellFormedOriginalString()) {
                return "!IsWellFormedOriginalString";
            }

            if (!uri.AbsolutePath.Contains("connect")) {
                return $"Uri path is {uri.AbsolutePath} instead of '/connect'";
            }

            try {
                var json = Uri.UnescapeDataString(uri.Query.TrimStart('?'));
                // fixme: DeserializeObject fails with PlatformNotSupportedException if Unity Player Settings is set to use .NET Standard 2.0
                var result = JsonConvert.DeserializeObject<PatchServerInfo>(json);
                if (result != null) {
                    // success
                    info = result;
                    return null;
                } else {
                    return "DeserializeObject returned null";
                }
            } catch (Exception ex) {
                Debug.LogException(ex);
                return $"DeserializeObject failed with an exception: {ex}";
            }
        }

        /// <summary>
        /// Convert server info into a uri that launches an app via a deeplink.
        /// </summary>
        /// <returns>Uri that you can display as a QR-Code</returns>
        public Uri ToUri() {
            var json = JsonConvert.SerializeObject(this);
            var builder = new UriBuilder("hotreload-app", hostName) {
                Path = "connect",
                Query = Uri.EscapeDataString(json), 
            };
            return builder.Uri;
        }

        public string ToUriString() => ToUri().AbsoluteUri;
    }
}