#if UNITY_2019_2_OR_NEWER
#define SUPPORTS_deepLinkActivated
#endif

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace SingularityGroup.HotReload {
    public delegate void ShowPromptCallback(string warning,
        UnityAction onSubmit,
        UnityAction onCancel);

    public class SettingsOverlay : MonoBehaviour {
        [Header("UI controls")]
        public Button hideButton;
        public Toggle enabledToggle;
        public Button testConnectionButton;
        public Text infoText;
        
        [Header("Other")]
        [Tooltip("Used when your project does not create an EventSystem early enough")]
        public GameObject fallbackEventSystem;
        
        private GameObject prefabRoot;
        private bool HasUI = false;

        private static SettingsOverlay _I;
        private static SettingsOverlay I {
            get {
                if (_I == null) {
                    var go = Instantiate(HotReloadSettingsObject.I.SettingsOverlayPrefab, new Vector3(0, 0, 0), Quaternion.identity);
                    go.name = nameof(SettingsOverlay)+"_singleton";
                    if (Application.isPlaying) {
                        DontDestroyOnLoad(go);
                    }
                    _I = go.GetComponentInChildren<SettingsOverlay>();
                    _I.prefabRoot = go;
                }

                return _I;
            }
        }

        /// <summary>
        /// Path to the prefab asset file.
        /// </summary>
        internal const string prefabAssetPath = "Packages/com.singularitygroup.hotreload/" +
                                                          "Runtime/SettingsOverlay.prefab";

        public static bool RuntimeSupportsHotReload {
            get {
                #if ENABLE_MONO
                // We try to support any platform that uses the mono scripting backend.
                // This includes Playmode in the Unity Editor, Android, Standalone MacOS/Windows/Linux, ...
                // Only development builds are supported - we exclude some CodePatcher things from release builds.
                return Debug.isDebugBuild;
                #else
                return false;
                #endif
            }
        }

        #if ENABLE_MONO
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        #endif
        private static void ShowOnAppLoad() {
            if (CanOpen() && HotReloadSettingsObject.I.ShowOptionsPopupOnStartup) {
                I.prefabRoot.SetActive(true);
            }
            SetupDeepLinkHandling();
        }

        [Conditional("UNITY_ANDROID")]
        private static void SetupDeepLinkHandling() {
            if (!RuntimeSupportsHotReload) {
                return;
            }
            if (!Application.isEditor) {
                Debug.LogFormat("[{0}] Started checking for deeplink", CodePatcher.TAG);
                #if SUPPORTS_deepLinkActivated
                Application.deepLinkActivated += url => I.TryHandleDeepLink(url);
                #endif
                // Check if a deeplink launched the app
                I.TryHandleDeepLink(GetDeepLink());
            }
        }

        #if !SUPPORTS_deepLinkActivated
        private static AndroidJavaClass _deepLinkForwarderActivity;
        private static AndroidJavaClass DeepLinkForwarderActivity {
            get {
                if (_deepLinkForwarderActivity == null) {
                    _deepLinkForwarderActivity =
                        new AndroidJavaClass("com.singularitygroup.deeplinkforwarder.DeepLinkForwarderActivity");
                }
                return _deepLinkForwarderActivity;
            }
        }

        // We don't get a callback when deeplink is used, so always check for a new one on app resumed.
        private void OnApplicationFocus(bool hasFocus) {
            if (hasFocus && Application.platform == RuntimePlatform.Android) {
                // on this old Unity version, Application.absoluteURL also doesn't contain the deeplink 
                var unreadDeepLink = GetDeepLink();
                if (unreadDeepLink != null) {
                    I.TryHandleDeepLink(unreadDeepLink);
                }
            }
        }

        #endif
        private static string GetDeepLink() {
            if (Application.platform == RuntimePlatform.Android) {
                #if !SUPPORTS_deepLinkActivated
                // On old Unity versions, Application.absoluteURL doesn't contain the deeplink on Android
                return DeepLinkForwarderActivity.CallStatic<string>("getUnreadDeepLink");
                #else
                return Application.absoluteURL;
                #endif
            } else {
                return null;
            }
        }

        private void Awake() {
            HasUI = infoText;
        }

        string commitHash = null;
        string[] defineSymbols;
        string projectExclusionRegex;

        Action<string> displayMessage = Debug.Log;
        ShowPromptCallback showPrompt = (w, onSubmit, onCancel) => {
            Debug.LogFormat("[{0}] Would show prompt with message: {1}", CodePatcher.TAG, w);
            onCancel();
        };
        Action<PatchServerInfo> onSuccess = (info) => { };

        void Start() {
            if (!HasUI) {
                return;
            }
            
            enabledToggle.isOn = CodePatcher.I.AutoApply;
            enabledToggle.onValueChanged.AddListener(autoApply => { CodePatcher.I.AutoApply = autoApply; });

            testConnectionButton.onClick.AddListener(() => {
                if (RequestHelper.customServerHost == null) {
                    displayMessage("Unknown server address. Scan QR-Code first.");
                } else {
                    TestServerReachability(RequestHelper.ServerInfo).Forget();
                }
            });
            hideButton.onClick.AddListener(() => {
                prefabRoot.SetActive(false);
            });
            StartCoroutine(DelayedEnsureEventSystem());
        }

        private bool userTriedToInteract = false;

        private int pending = -1;
        private int patched = -1;

        void Update() {
            if (!HasUI) {
                return;
            }

            if (!userTriedToInteract) {
                // when user interacts with the screen, make sure overlay can handle taps
                if (Input.touchCount > 0 || Input.GetMouseButtonDown(0)) {
                    userTriedToInteract = true;
                    DoEnsureEventSystem();
                }
            }
            pending = CodePatcher.I.PendingPatches.Count;
            patched = CodePatcher.I.PatchedMethods.Count;
            var commitHashText = commitHash ?? PatchServerInfo.UnknownCommitHash;
            var hostNameText = RequestHelper.customServerHost ?? "unknown";
            infoText.text = $@"Server IP: {hostNameText}
Commit: {commitHashText}
Pending Patches: {pending}
Patched Methods: {patched}";
        }

        private IEnumerator DelayedEnsureEventSystem() {
            // allow some delay in-case the project loads the EventSystem asynchronously (perhaps in a second scene)
            if (EventSystem.current == null) {
                yield return new WaitForSeconds(1f);
                DoEnsureEventSystem();
            }
        }

        /// Scene must contain an EventSystem and StandaloneInputModule, otherwise clicking/tapping on the overlay does nothing.
        private void DoEnsureEventSystem() {
            if (EventSystem.current == null) {
                Debug.Log($"[{CodePatcher.TAG}]No EventSystem, enabling an EventSystem inside Hot Reload {name} prefab." + 
                          " An EventSystem and an Input module is required for tapping buttons on the overlay.");
                fallbackEventSystem.SetActive(true);
            }
        }

        public void Initialize(
            string commitHash,
            string[] defineSymbols,
            string projectExclusionRegex,
            Action<string> displayMessage,
            ShowPromptCallback showPrompt,
            Action<PatchServerInfo> onSuccess) {
            if (commitHash == null) {
                throw new ArgumentNullException(nameof(commitHash));
            }
            if (defineSymbols == null) {
                throw new ArgumentNullException(nameof(defineSymbols));
            }
            if (projectExclusionRegex == null) {
                throw new ArgumentNullException(nameof(projectExclusionRegex));
            }
            if (displayMessage == null) {
                throw new ArgumentNullException(nameof(displayMessage));
            }
            if (showPrompt == null) {
                throw new ArgumentNullException(nameof(showPrompt));
            }
            if (onSuccess == null) {
                throw new ArgumentNullException(nameof(onSuccess));
            }
            
            this.defineSymbols = defineSymbols;
            this.projectExclusionRegex = projectExclusionRegex;
            this.displayMessage = displayMessage;
            this.showPrompt = showPrompt;
            this.onSuccess = onSuccess;
            this.displayMessage += msg => Debug.Log("[PATCHER] " + msg);
            this.showPrompt += (msg, _, __) => Debug.Log("[PATCHER] " + msg);
        }

        public static bool CanOpen() {
            if (Application.isEditor) {
                // In the Unity Editor (Playmode), the user is instead expected to set options inside the HotReload EditorWindow.
                // So we don't show the options popup.
                //return true; // uncomment to test popup in editor
                return false;
            } else {
                return RuntimeSupportsHotReload;
            }
        }

        void TryHandleDeepLink(string deeplink) {
            if (String.IsNullOrEmpty(deeplink)) {
                return;
            }

            Debug.LogFormat("[{0}] Found deeplink: {1}", CodePatcher.TAG, deeplink);

            var uri = new Uri(deeplink);
            PatchServerInfo info;
            var error = PatchServerInfo.TryParse(uri, out info);
            if (error != null) {
                displayMessage($"The URI was invalid: {error}");
                return;
            }

            if (SameCommit(info.commitHash)) {
                RequestHandshake(info).Forget();
            } else {
                ShowCommitHashWarningPrompt(commitHash, info);
            }
        }

        /// <summary>
        /// Checks whether <paramref name="commit"/> is equivalent to the commit hash of this build.
        /// </summary>
        /// <param name="commit"></param>
        /// <returns>False if the commit hashes are definately different,
        /// True if the commit hashes are equivalent or if <see cref="Initialize"/> has not set the build commit hash.</returns>
        private bool SameCommit(string commit) {
            if (commitHash == null) {
                // view doesn't know commit hash of the build, so approve anything
                return true;
            }

            if (commitHash.Length == commit.Length) {
                return true;
            } else if (commitHash.Length >= 6 && commit.Length >= 6) {
                // depending on OS, the 
                var longerHash = commitHash.Length > commit.Length ? commitHash : commit; 
                var shorterHash = commitHash.Length < commit.Length ? commitHash : commit;
                return longerHash.StartsWith(shorterHash);
            }
            return false;
        }

        async Task RequestHandshake(PatchServerInfo info) {
            Debug.LogFormat("[{0}] Request handshake to server with IP: {1}", CodePatcher.TAG, info.hostName);
            var response = await RequestHelper.RequestHandshake(info, defineSymbols, projectExclusionRegex);
            RenderServerReachabilityMessage(response.error);
            if (response.error == null) {
                RequestHelper.SetServerInfo(info);
                onSuccess(info);
            }
        }

        async Task<bool> TestServerReachability(PatchServerInfo info) {
            RequestHelper.SetServerInfo(info);
            var success = await RequestHelper.PingServer(info, 10);
            RenderServerReachabilityMessage(success ? null : "");
            return success;
        }

        async void RenderServerReachabilityMessage(string error) {
            var message = error == null
                ? "Success! Can receive code patches after restarting the app."
                : "Can not connect. No Internet? Wrong Wifi? Server not running?";
            displayMessage(message);

            if (!string.IsNullOrEmpty(error)) {
                await Task.Delay(2000);
                displayMessage(error);
            }
        }

        void ShowCommitHashWarningPrompt(string buildCommitHash, PatchServerInfo info) {
            var message = $"Commit hash of the server ({info.commitHash}) differs from the commit hash of the device ({buildCommitHash}). Continue regardless?";
            showPrompt(message, () => { RequestHandshake(info).Forget(); }, () => { });
        }
    }
}
