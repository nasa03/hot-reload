using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SingularityGroup.HotReload.DTO;
using SingularityGroup.HotReload.Newtonsoft.Json;
using SingularityGroup.HotReload.RuntimeDependencies;
using UnityEngine;
using UnityEngine.Networking;

[assembly: InternalsVisibleTo("CodePatcherEditor")]
[assembly: InternalsVisibleTo("TestProject")]
[assembly: InternalsVisibleTo("SingularityGroup.HotReload.IntegrationTests")]

namespace SingularityGroup.HotReload {
    public static class RequestHelper {
        const ushort port = 33242;
        const string defaultServerHost = "localhost";
        
        static PatchServerInfo serverInfo = new PatchServerInfo(defaultServerHost, null, null);
        public static PatchServerInfo ServerInfo => serverInfo;
        
        static string cachedUrl;
        static string url => cachedUrl ?? (cachedUrl = CreateUrl(serverInfo.hostName));
        
        static string CreateUrl(string hostName) {
            return $"http://{hostName}:{port.ToString()}";
        }
        
        public static string customServerHost => serverInfo.hostName == defaultServerHost ? null : serverInfo.hostName;
        
        public static void SetServerInfo(PatchServerInfo info) {
            serverInfo = info;
            cachedUrl = null;
        }
        
        static string[] assemblySearchPaths;
        public static void ChangeAssemblySearchPaths(string[] paths) {
            assemblySearchPaths = paths;
        }

        internal static Task<UnityWebRequestAsyncOperation> SendRequestAsync(UnityWebRequest www) {
            var req = www.SendWebRequest();
            var tcs = new TaskCompletionSource<UnityWebRequestAsyncOperation>();
            req.completed += op => tcs.TrySetResult((UnityWebRequestAsyncOperation)op);
            return tcs.Task;
        }
        
        internal static void AbortPendingPatchRequest() {
            pendingPatchRequest?.Abort();
        }

        static bool pollPending;
        static UnityWebRequest pendingPatchRequest;
        internal static string lastPatchId;
        internal static async void PollMethodPatches(Action<MethodPatchResponse> onResponseReceived) {
            if (pollPending) return;
            if (isCompiling) return;
        
            pollPending = true;
            var searchPaths = assemblySearchPaths ?? CodePatcher.I.GetAssemblySearchPaths();
            var body = SerializeRequestBody(new MethodPatchRequest(lastPatchId, searchPaths, TimeSpan.FromSeconds(20), Path.GetDirectoryName(Application.dataPath)));
            
            await WaitForHttpAllowed();
            await ThreadUtility.SwitchToMainThread();
            
            var www = PostJson(url + "/patch", body);
            pendingPatchRequest = www;
            try {
                www.timeout = 30;
                var requestTask = SendRequestAsync(www);
                await Task.WhenAny(requestTask, WaitForCompiling());
                pendingPatchRequest = null;
                
                if(!requestTask.IsCompleted) {
                    www.Abort();
                    return;
                }
                var result = requestTask.Result;
                if(result.webRequest.responseCode == 200) {
                    var responses = JsonConvert.DeserializeObject<MethodPatchResponse[]>(result.webRequest.downloadHandler.text);
                    foreach(var response in responses) {
                        onResponseReceived(response);
                        lastPatchId = response.id;
                    }
                } else if(result.webRequest.responseCode == 0
                    || result.webRequest.responseCode == (int)HttpStatusCode.Unauthorized
                ) {
                    // Server is not running or not authorized.
                    // We don't want to spam requests in that case.
                    await Task.Delay(5000);
                } else if(result.webRequest.responseCode == 503) {
                    //Server shut down
                    await Task.Delay(5000);
                } else {
                    Debug.Log($"PollMethodPatches failed with code {result.webRequest.responseCode} {result.webRequest.error} {result.webRequest.downloadHandler.text}");
                }
            } finally {
                pollPending = false;
                pendingPatchRequest = null;
                www.Dispose();
            }
        }
        
#if UNITY_EDITOR
        static bool isCompiling => UnityEditor.EditorApplication.isCompiling && CanReloadAssemblies();

        static Func<bool> _cachedCanReloadAssmeblies;
        static bool CanReloadAssemblies() {
            if (_cachedCanReloadAssmeblies == null) {
                _cachedCanReloadAssmeblies = (Func<bool>)typeof(UnityEditor.EditorApplication)
                    .GetMethod("CanReloadAssemblies", BindingFlags.NonPublic | BindingFlags.Static)
                    .CreateDelegate(typeof(Func<bool>));
            }
            return _cachedCanReloadAssmeblies();
        }
        
        static async Task WaitForCompiling() {
            while(!isCompiling) {
                await Task.Delay(100);
            }
        }
#else 
        static bool isCompiling => false;
        
        static Task WaitForCompiling() {
            return Task.Delay(global::System.Threading.Timeout.Infinite);
        }
#endif
        
        internal static async Task<LoginStatusResponse> GetLoginStatus(int timeoutSeconds) {
            await WaitForHttpAllowed();
            await ThreadUtility.SwitchToMainThread();
            
            var tcs = new TaskCompletionSource<LoginStatusResponse>();
            LoginRequestUtility.RequestLoginStatus(url, timeoutSeconds, resp => tcs.TrySetResult(resp));
            return await tcs.Task;
        }
        
        internal static async Task<LoginStatusResponse> RequestLogin(string email, string password, int timeoutSeconds) {
            await WaitForHttpAllowed();
            await ThreadUtility.SwitchToMainThread();

            var tcs = new TaskCompletionSource<LoginStatusResponse>();
            LoginRequestUtility.RequestLogin(url, email, password, timeoutSeconds, resp => tcs.TrySetResult(resp));
            return await tcs.Task;
        }
        
        public static async Task KillServer() {
            await WaitForHttpAllowed();
            await ThreadUtility.SwitchToMainThread();
            
            using (var www = UnityWebRequest.Get(CreateUrl(serverInfo.hostName) + "/kill")) {
                www.useHttpContinue = false;
                await SendRequestAsync(www);
            }
        }
        public static async Task<bool> PingServer(PatchServerInfo info, int timeoutSeconds) {
            await WaitForHttpAllowed();
            await ThreadUtility.SwitchToMainThread();
            using (var www = UnityWebRequest.Get(CreateUrl(info.hostName) + "/ping")) {
                www.useHttpContinue = false;
                www.timeout = timeoutSeconds * 2;
                var wwwTask = SendRequestAsync(www);
                await Task.WhenAny(wwwTask, Task.Delay(1000 * timeoutSeconds));
                if(!wwwTask.IsCompleted) {
                    www.Abort();
                    return false;
                }
                return www.responseCode == 200;
            }
        }
        
        public static async Task RequestCompile() {
            var body = SerializeRequestBody(new CompileRequest(serverInfo.rootPath));
            await WaitForHttpAllowed();
            await ThreadUtility.SwitchToMainThread();
            
            using (var www = PostJson(url + "/compile", body)) {
                www.timeout = 10;
                await SendRequestAsync(www);
            }
        }
        
        internal static async Task<bool> Post(string route, string json) {
            await WaitForHttpAllowed();
            await ThreadUtility.SwitchToMainThread();
            
            using (var www = PostJson(url + route, json)) {
                www.timeout = 10;
                await SendRequestAsync(www);
                return www.responseCode == 200;
            }
        }

        internal static async Task<MobileHandshakeResponse> RequestHandshake(PatchServerInfo info, string[] defineSymbols, string projectExclusionRegex) {
            var body = SerializeRequestBody(new MobileHandshakeRequest(defineSymbols, projectExclusionRegex));
            await WaitForHttpAllowed();
            await ThreadUtility.SwitchToMainThread();
            
            using(var www = PostJson(CreateUrl(info.hostName) + "/handshake", body)) {
                www.timeout = 120;

                await SendRequestAsync(www);

                if (string.IsNullOrEmpty(www.error)) {
                    return JsonConvert.DeserializeObject<MobileHandshakeResponse>(www.downloadHandler.text);
                }
                else {
                    return new MobileHandshakeResponse(null, www.error);
                }
            }
        }
        
        static string SerializeRequestBody<T>(T request) {
            return JsonConvert.SerializeObject(request);
        }
        
        static async Task WaitForHttpAllowed() {
#if UNITY_EDITOR && UNITY_2022_1_OR_NEWER
            await ThreadUtility.SwitchToMainThread();
            while(UnityEditor.PlayerSettings.insecureHttpOption == UnityEditor.InsecureHttpOption.NotAllowed) {
                await Task.Delay(1000);
            }
#else
            //keep compiler happy
            await Task.CompletedTask;
#endif
        }
        
        static UnityWebRequest PostJson(string uri, string json) {
            var www = new UnityWebRequest(uri, "POST");
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            www.uploadHandler.contentType = "application/json";
            www.downloadHandler = new DownloadHandlerBuffer();
            www.useHttpContinue = false;
            return www;
        }
    }
}