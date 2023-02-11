using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SingularityGroup.HotReload.DTO;
using SingularityGroup.HotReload.Editor.Cli;
using UnityEditor;
using UnityEngine;
using Task = System.Threading.Tasks.Task;
#if UNITY_2019_4_OR_NEWER
using Unity.CodeEditor;
#endif

namespace SingularityGroup.HotReload.Editor {
    internal struct LicenseErrorData {
        public readonly string description;
        public readonly bool showBuyButton;
        public readonly string buyButtonText;
        public readonly bool showLoginButton;
        public readonly string loginButtonText;
        public readonly bool showSupportButton;
        public readonly string supportButtonText;
        public readonly bool showManageLicenseButton;
        public readonly string manageLicenseButtonText;

        public LicenseErrorData(string description, bool showManageLicenseButton = false, string manageLicenseButtonText = "", string loginButtonText = "", bool showSupportButton = false, string supportButtonText = "", bool showBuyButton = false, string buyButtonText = "", bool showLoginButton = false) {
            this.description = description;
            this.showManageLicenseButton = showManageLicenseButton;
            this.manageLicenseButtonText = manageLicenseButtonText;
            this.loginButtonText = loginButtonText;
            this.showSupportButton = showSupportButton;
            this.supportButtonText = supportButtonText;
            this.showBuyButton = showBuyButton;
            this.buyButtonText = buyButtonText;
            this.showLoginButton = showLoginButton;
        }
    }
    
    internal class HotReloadRunTab : HotReloadTabBase {
        [CanBeNull] private LoginStatusResponse _status;

        private const int SERVER_POLL_FREQUENCY_ON_STARTUP_MS = 500;
        private const int SERVER_POLL_FREQUENCY_AFTER_STARTUP_MS = 2000;

        private string _pendingEmail;
        private string _pendingPassword;

        private bool _requestingServerInfo;
        private bool _requestingLoginInfo;

        private bool _running;

        private long _lastServerPoll;

        private Tuple<string, MessageType> _activateInfoMessage;
        private Tuple<float, string> _startupProgress;
        /// <summary>
        /// We have a button to stop the Hot Reload server.<br/>
        /// Store task to ensure only one stop attempt at a time. 
        /// </summary>
        private Task _stopTask;
        private DateTime? _serverStartedAt;
        bool createdAccount;

        public bool FreeLicense => _status?.isFree == true;
        
        // Has Indie or Pro license (even if not currenctly active)
        public bool HasPayedLicense => _status != null && (_status.isIndieLicense || _status.isBusinessLicense);
        public bool TrialLicense => _status != null && (_status?.isTrial == true);

        private Vector2 _patchedMethodsScrollPos;

        bool Starting => _startupProgress != null && _startupProgress.Item1 < 1f;

        public HotReloadRunTab(HotReloadWindow window) : base(window, "Run", "forward", "Run and monitor the current Hot Reload session.") { }

        public override void OnGUI() {
            EditorGUILayout.Space();
            using(new EditorGUILayout.VerticalScope()) {
                OnGUICore();
            }
        }
        
        void OnGUICore() {
            // Migration for new versions
            if (TrialLicense || HasPayedLicense) {
                HotReloadPrefs.FirstLogin = false;
            }

            var showSuggestions = RequiredSettings.Presenters.Any(x => x.CanShowHelpBox());

            if (showSuggestions) {
                RenderSuggestions(showApplyAll: RequiredSettings.Presenters.Count(x => x.CanShowHelpBox() && x.CanAutoApply()) > 1);
            }
            if (createdAccount && _status?.isLicensed == true) {
                RenderCreatedAccountInfo();
            }
            
            if (_running && !Starting && _status?.isLicensed != true && _status?.isFree != true) {
                RenderConsumption();
            }

            var renderFirstLogin = HotReloadPrefs.FirstLogin && !_running && !Starting;
            if (renderFirstLogin) {
                RenderLoginScreen();
            } else if (_running && !Starting) {
                RenderLicenseActionInformation();
            }

            if (!_running && !Starting && !renderFirstLogin) {
                RenderStart();
            }

            if (Starting || _status != null && (_status.isLicensed || !_status.freeSessionFinished) && !HotReloadPrefs.FirstLogin) {
                RenderProgressBar();
            }
            if (_running && !Starting) {
                RenderChanges();
            }
        }

        static void RenderSuggestions(bool showApplyAll) {
            var saveAssets = false;
            if (showApplyAll) {
                using(new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.LabelField("Apply recommended settings", applyRecommendedSettingsStyle);
                    if (GUILayout.Button("Apply all")) {
                        foreach (var presenter in RequiredSettings.Presenters) {
                            if (presenter.CanAutoApply()) {
                                saveAssets |= presenter.Apply().requiresSaveAssets;
                            }
                        }
                    }
                }
                EditorGUILayout.Space();
            }
            foreach (var presenter in RequiredSettings.Presenters) {
                var result = presenter.ShowHelpBoxIfRequired();
                saveAssets |= result.requiresSaveAssets;
            }
            EditorGUILayout.Space();
            if (saveAssets) {
                AssetDatabase.SaveAssets();
            }
        }

        public void RenderConsumption() {
            if (_status == null) {
                return;
            }
            EditorGUILayout.LabelField($"Using Free license", HotReloadWindowStyles.MediumMiddleCenterStyle);
            EditorGUILayout.Space();
            if (_status.consumptionsUnavailableReason == ConsumptionsUnavailableReason.NetworkUnreachable) {
                HotReloadGUIHelper.HelpBox("Free charges unavailabe. Please check your internet connection.", MessageType.Warning, fontSize: 11);
            } else if (_status.consumptionsUnavailableReason == ConsumptionsUnavailableReason.UnrecoverableError) {
                HotReloadGUIHelper.HelpBox("Free charges unavailabe. Please contact support if the issue persists.", MessageType.Error, fontSize: 11);
            } else if (_status.freeSessionFinished) {
                var now = DateTime.UtcNow;
                var sessionRefreshesAt = (now.AddDays(1).Date - now).Add(TimeSpan.FromMinutes(5));
                var sessionRefreshString = $"Next Free Session: {(sessionRefreshesAt.Hours > 0 ? $"{sessionRefreshesAt.Hours}h " : "")}{sessionRefreshesAt.Minutes}min";
                HotReloadGUIHelper.HelpBox(sessionRefreshString, MessageType.Warning, fontSize: 11);
            } else if (_status.freeSessionRunning && _status.freeSessionEndTime != null) {
                var sessionEndsAt = _status.freeSessionEndTime.Value - DateTime.Now;
                var sessionString = $"Daily Free Session: {(sessionEndsAt.Hours > 0 ? $"{sessionEndsAt.Hours}h " : "")}{sessionEndsAt.Minutes}min Left";
                EditorGUILayout.LabelField(sessionString, HotReloadWindowStyles.H3TitleStyle);
                EditorGUILayout.Space();
            } else if (_status.freeSessionEndTime == null) {
                EditorGUILayout.LabelField($"Daily Free Session: Make code changes to start", HotReloadWindowStyles.H3TitleStyle);
                EditorGUILayout.Space();
            }
        }

        static void RenderCreatedAccountInfo() {
            var cachedColor = GUI.backgroundColor;
            try {
                GUI.backgroundColor = new Color(0, 0, 0, 0.1f);
                EditorGUILayout.LabelField($"Account Created successfully. You should receive an email with your password shortly.", accountCreatedStyle);
            } finally {
                GUI.backgroundColor = cachedColor;
            }
        }

        public override void Update() {
            if (!_requestingServerInfo) {
                RequestServerInfo().Forget();
            }
        }

        private void RenderChanges() {
            GUILayout.Space(12);
            GUILayout.Label($"Patches ({CodePatcher.I.PatchesApplied})", HotReloadWindowStyles.H2TitleStyle);

            if (CodePatcher.I.PatchesApplied == 0) {
                GUILayout.Label("  Edit code to see changes in the game");
            }

            var patchedMethods = CodePatcher.I.PatchedMethods;
            if (patchedMethods.Count == 0) {
                return;
            }

            using (var scope = new EditorGUILayout.ScrollViewScope(_patchedMethodsScrollPos, GUIStyle.none, GUI.skin.verticalScrollbar)) {
                _patchedMethodsScrollPos.y = scope.scrollPosition.y;
                for (var i = 0; i < patchedMethods.Count; i++) {
                    using (new GUILayout.HorizontalScope()) {
                        var method = patchedMethods[i];
                        var displayName = method.displayName ?? "";
                        var spaceIndex = displayName.IndexOf(" ", StringComparison.Ordinal);
                        if (spaceIndex > 0) {
                            displayName = displayName.Substring(spaceIndex);
                        }

                        GUILayout.Label(displayName);
                    }
                }
            }
        }

        private async Task RequestServerInfo() {
            _requestingServerInfo = true;
            try {
                await RequestServerInfoCore();
            } finally {
                _requestingServerInfo = false;
            }
        }

        private async Task RequestServerInfoCore() {
            var pollFrequency = GetPollFrequency();
            // Delay until we've hit the poll request frequency
            var waitMs = (int)Mathf.Clamp(pollFrequency - ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - _lastServerPoll), 0, pollFrequency);
            await Task.Delay(waitMs);

            var oldRunning = _running;
            
            var newRunning = ServerHealthCheck.I.IsServerHealthy;
            _running = newRunning;

            if (_running) {
                var resp = await RequestHelper.GetLoginStatus(30);
                bool consumptionsChanged = _status?.freeSessionRunning != resp.freeSessionRunning || _status?.freeSessionEndTime != resp.freeSessionEndTime;
                if (resp.consumptionsUnavailableReason == ConsumptionsUnavailableReason.UnrecoverableError
                    && _status?.consumptionsUnavailableReason != ConsumptionsUnavailableReason.UnrecoverableError
                ) {
                    Debug.LogError($"[{CodePatcher.TAG}] Free charges unavailabe. Please contact support if the issue persists.");
                }
                if (!_requestingLoginInfo && resp.requestError == null) {
                    _status = resp;
                }
                if (resp.lastLicenseError == null) {
                    // If we got success, we should always show an error next time it comes up
                    HotReloadPrefs.ErrorHidden = false;
                }

                var oldStartupProgress = _startupProgress;
                var newStartupProgress = Tuple.Create(
                    resp.startupProgress, 
                    string.IsNullOrEmpty(resp.startupStatus) ? "Starting Hot Reload" : resp.startupStatus);

                _startupProgress = newStartupProgress;

                // Repaint when the startup has finished
                if (oldStartupProgress == null
                    || Math.Abs(oldStartupProgress.Item1 - newStartupProgress.Item1) > 0
                    || oldStartupProgress.Item2 != newStartupProgress.Item2
                    || consumptionsChanged
                ) {
                    Repaint();
                }
            }

            if (!_running && !StartedServerRecently()) {
                // Reset startup progress
                _startupProgress = null;
            }

            // Repaint if the running status has changed since the layout changes quite a bit
            if (oldRunning != newRunning) {
                Repaint();
            }

            _lastServerPoll = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        }

        private int GetPollFrequency() {
            return (_startupProgress != null && _startupProgress.Item1 < 1) || StartedServerRecently()
                    ? SERVER_POLL_FREQUENCY_ON_STARTUP_MS
                    : SERVER_POLL_FREQUENCY_AFTER_STARTUP_MS;
        }

        static GUIStyle _startButtonStyle;
        static GUIStyle startButtonStyle => _startButtonStyle ?? (_startButtonStyle = new GUIStyle(GUI.skin.button) {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
        });
        
        static GUIStyle _signUpToStartStyle;
        static GUIStyle signUpToStartStyle {
            get {
                if (_signUpToStartStyle != null) {
                    return _signUpToStartStyle;
                }
                _signUpToStartStyle = new GUIStyle(GUI.skin.box) {
                    alignment = TextAnchor.MiddleCenter,
                    stretchWidth = true,
                    fontStyle = FontStyle.Bold,
                    fontSize = 16,
                };
                _signUpToStartStyle.normal.textColor = EditorGUIUtility.isProSkin ? EditorStyles.label.normal.textColor : GUI.skin.box.normal.textColor;
                return _signUpToStartStyle;
            }
        }


        static GUIStyle _accountCreatedStyle;
        static GUIStyle accountCreatedStyle {
            get {
                if (_accountCreatedStyle != null) {
                    return _accountCreatedStyle;
                }
                _accountCreatedStyle = new GUIStyle(GUI.skin.box) {
                    stretchWidth = true,
                    fontSize = 13,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(10, 10, 5, 5),
                    fontStyle = FontStyle.Bold,
                };
                _accountCreatedStyle.normal.textColor = EditorGUIUtility.isProSkin ? EditorStyles.label.normal.textColor : GUI.skin.box.normal.textColor;
                return _accountCreatedStyle;
            }
        }
        
        static GUIStyle _openSettingsStyle;
        static GUIStyle openSettingsStyle => _openSettingsStyle ?? (_openSettingsStyle = new GUIStyle(GUI.skin.button) {
            fontStyle = FontStyle.Normal,
            fixedHeight = 25,
        });
        
        static GUIStyle _applyRecommendedSettingsStyle;
        static GUIStyle applyRecommendedSettingsStyle => _applyRecommendedSettingsStyle ?? (_applyRecommendedSettingsStyle = new GUIStyle(EditorStyles.label) {
            fontStyle = FontStyle.Normal,
            wordWrap = true,
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
        });

        static GUILayoutOption[] _bigButtonHeight;
        static GUILayoutOption[] bigButtonHeight => _bigButtonHeight ?? (_bigButtonHeight = new [] {GUILayout.Height(25)});
        
        private void RenderStart() {
            // we show the Run and Stop button in the same position.
            if (_startupProgress != null || !SlnFileSettingChecker.I.IsApplied() || _running) {
                return;
            }
            EditorGUILayout.Space();
            GUILayout.Label("Run Hot Reload to proceed", signUpToStartStyle);
            EditorGUILayout.Space();
            if (GUILayout.Button("Run Hot Reload", startButtonStyle)) {
                _startupProgress = Tuple.Create(0f, "Starting Hot Reload");
                StartCodePatcher();
            }
            EditorGUILayout.Space();
        }

        private void StartCodePatcher() {
            var dataPath = Application.dataPath;
            var exposeToNetwork = HotReloadPrefs.ExposeServerToLocalNetwork;
            CodePatcher.I.ClearPatchedMethods();
            Task.Run(async () => {
                try {
                    await HotReloadCli.StartAsync(dataPath, exposeToNetwork).ConfigureAwait(false);
                    _serverStartedAt = DateTime.UtcNow;
                } catch (Exception ex) {
                    ThreadUtility.LogException(ex);
                }
            });
        }

        private bool StartedServerRecently() {
            return DateTime.UtcNow - _serverStartedAt < TimeSpan.FromSeconds(2);
        }

        private static GUIContent indieLicenseContent;
        private static GUIContent businessLicenseContent;

        internal void RenderLicenseStatusInfo(bool allowHide = true, bool verbose = false) {
            string message = null;
            MessageType messageType = default(MessageType);
            Action customGUI = null;
            GUIContent content = null;
            if (_status == null) {
                // no info
            } else if (_status.isFree) {
                if (verbose) {
                    message = " Free license active";
                    messageType = MessageType.Info;
                    if (businessLicenseContent == null) {
                        businessLicenseContent = new GUIContent(message, EditorGUIUtility.FindTexture("TestPassed"));
                    }
                    content = businessLicenseContent;
                }
            } else if (_status.lastLicenseError != null) {
                messageType = !_status.freeSessionFinished ? MessageType.Warning : MessageType.Error;
                message = GetMessageFromError(_status.lastLicenseError);
            } else if (_status.isTrial) {
                message = $"Using Trial license, valid until {_status.licenseExpiresAt.ToShortDateString()}";
                messageType = MessageType.Info;
            } else if (_status.isIndieLicense) {
                if (verbose) {
                    message = " Indie license active";
                    messageType = MessageType.Info;
                    customGUI = () => {
                        EditorGUILayout.LabelField($"License will renew on {_status.licenseExpiresAt.ToShortDateString()}.");
                        EditorGUILayout.Space();
                    };
                    if (indieLicenseContent == null) {
                        indieLicenseContent = new GUIContent(message, EditorGUIUtility.FindTexture("TestPassed"));
                    }
                    content = indieLicenseContent;
                }
            } else if (_status.isBusinessLicense) {
                if (verbose) {
                    message = " Business license active";
                    messageType = MessageType.Info;
                    if (businessLicenseContent == null) {
                        businessLicenseContent = new GUIContent(message, EditorGUIUtility.FindTexture("TestPassed"));
                    }
                    content = businessLicenseContent;
                }
            }

            if (messageType != MessageType.Info && HotReloadPrefs.ErrorHidden && allowHide) {
                return;
            }
            if (message != null) {
                if (messageType != MessageType.Info) {
                    using(new EditorGUILayout.HorizontalScope()) {
                        HotReloadGUIHelper.HelpBox(message, messageType, fontSize: 11);
                        if (allowHide) {
                            if (GUILayout.Button("Hide", GUILayout.ExpandHeight(true))) {
                                HotReloadPrefs.ErrorHidden = true;
                            }
                        }
                    }
                } else if (content != null) {
                    EditorGUILayout.LabelField(content);
                    EditorGUILayout.Space();
                } else {
                    EditorGUILayout.LabelField(message);
                    EditorGUILayout.Space();
                }
                customGUI?.Invoke();
            }
        }

        const string GetAnotherLicense = "Get Another License";
        const string CreateAccount = "Create Account";
        const string ContactSupport = "Contact Support";
        const string ReactivateLicense = "Reactivate License";
        const string UpgradeLicense = "Upgrade License";
        const string ManageLicense = "Manage License";
        internal Dictionary<string, LicenseErrorData> _licenseErrorData;
        internal Dictionary<string, LicenseErrorData> LicenseErrorData => _licenseErrorData ?? (_licenseErrorData = new Dictionary<string, LicenseErrorData> {
            { "DeviceNotLicensedException", new LicenseErrorData(description: "Another device is using your license. Please reach out to customer support for assistance.", showSupportButton: true, supportButtonText: ContactSupport, showLoginButton: true, loginButtonText: ReactivateLicense) },
            { "DeviceBlacklistedException", new LicenseErrorData(description: "You device has been blacklisted.", showLoginButton: true, loginButtonText: ReactivateLicense) },
            { "DateHeaderInvalidException", new LicenseErrorData(description: $"Your license is not working because your computer's clock is incorrect. Please set the clock to the correct time to restore your license.", showLoginButton: true, loginButtonText: ReactivateLicense) },
            { "DateTimeCheatingException", new LicenseErrorData(description: $"Your license is not working because your computer's clock is incorrect. Please set the clock to the correct time to restore your license.", showLoginButton: true, loginButtonText: ReactivateLicense) },
            { "LicenseActivationException", new LicenseErrorData(description: "An error has occured while activating your license. Please contact customer support for assistance.", showSupportButton: true, supportButtonText: ContactSupport, showLoginButton: true, loginButtonText: ReactivateLicense) },
            { "LicenseDeletedException", new LicenseErrorData(description: $"Your license has been deleted. Please contact customer support for assistance.", showBuyButton: true, buyButtonText: GetAnotherLicense, showLoginButton: true, loginButtonText: ReactivateLicense, showSupportButton: true, supportButtonText: ContactSupport) },
            { "LicenseDisabledException", new LicenseErrorData(description: $"Your license has been disabled. Please contact customer support for assistance.", showBuyButton: true, buyButtonText: GetAnotherLicense, showLoginButton: true, loginButtonText: ReactivateLicense, showSupportButton: true, supportButtonText: ContactSupport) },
            { "LicenseExpiredException", new LicenseErrorData(description: $"Your license has expired. Please renew your license subscription using the 'Upgrade License' button below and login with your email/password to activate your license.", showBuyButton: true, buyButtonText: UpgradeLicense, showManageLicenseButton: true, manageLicenseButtonText: ManageLicense, showLoginButton: true, loginButtonText: ReactivateLicense) },
            { "LicenseInactiveException", new LicenseErrorData(description: $"Your license is currenty inactive. Please login with your email/password to activate your license.", showLoginButton: true, loginButtonText: ReactivateLicense) },
            { "LocalLicenseException", new LicenseErrorData(description: $"Your license file was damaged or corrupted. Please login with your email/password to refresh your license file.", showLoginButton: true, loginButtonText: ReactivateLicense) },
            { "MissingParametersException", new LicenseErrorData(description: "An account already exists for this device. Please login with your existing email/password.", showLoginButton: true, loginButtonText: ReactivateLicense, showBuyButton: true, buyButtonText: GetAnotherLicense) },
            { "NetworkException", new LicenseErrorData(description: "There is an issue connecting to our servers. Please check your internet connection or contact customer support if the issue persists.", showSupportButton: true, supportButtonText: ContactSupport) },
            { "TrialLicenseExpiredException", new LicenseErrorData(description: $"Your license trial has expired. Activate a license with unlimited usage or continue using the Free version. View available plans on our website.", showBuyButton: true, buyButtonText: UpgradeLicense, showLoginButton: true, loginButtonText: ReactivateLicense) },
            { "InvalidCredentialException", new LicenseErrorData(description: "Incorrect email/password. You can find your initial password in the sign-up email.", showLoginButton: true, loginButtonText: ReactivateLicense) },
            { "LicenseNotFoundException", new LicenseErrorData(description: "The account you're trying to access doesn't seem to exist yet. Please enter your email address to create a new account and receive a trial license.", showLoginButton: true, loginButtonText: CreateAccount) },
            { "LicenseIncompatibleException", new LicenseErrorData(description: "Please upgrade your license to continue using hotreload with Unity Pro.", showLoginButton: true, loginButtonText: ReactivateLicense, showManageLicenseButton: true, manageLicenseButtonText: ManageLicense) },
        });
        internal LicenseErrorData defaultLicenseErrorData = new LicenseErrorData(description: "We apologize, an error happened while verifying your license. Please reach out to customer support for assistance.", showSupportButton: true, supportButtonText: ContactSupport, showLoginButton: true, loginButtonText: ReactivateLicense);

        internal string GetMessageFromError(string error) {
            return GetLicenseErrorDataOrDefault(error).description;
        }
        
        internal LicenseErrorData GetLicenseErrorDataOrDefault(string error) {
            if (_status?.isFree == true) {
                return default(LicenseErrorData);
            }
            if (_status == null || string.IsNullOrEmpty(error) && (!_status.isLicensed || _status.isTrial)) {
                return new LicenseErrorData(null, showBuyButton: true, buyButtonText: UpgradeLicense);
            }
            if (string.IsNullOrEmpty(error)) {
                return default(LicenseErrorData);
            }
            if (!LicenseErrorData.ContainsKey(error)) {
                return defaultLicenseErrorData;
            }
            return LicenseErrorData[error];
        }

        internal void RenderBuyLicenseButton(string buyLicenseButton) {
            OpenURLButton.Render(buyLicenseButton, Constants.ProductPurchaseURL);
        }

        private async Task RequestLogin(string email, string password) {
            try {
                int i = 0;
                while (!_running && i < 100) {
                    await Task.Delay(100);
                    i++;
                }

                _status = await RequestHelper.RequestLogin(email, password, 10);

                // set to false so new error is shown
                HotReloadPrefs.ErrorHidden = false;
                
                if (_status?.isLicensed == true) {
                    HotReloadPrefs.LicenseEmail = email;
                    HotReloadPrefs.LicensePassword = _status.initialPassword ?? password;
                    createdAccount = _status.initialPassword != null;

                } else if (_status != null && _status.lastLicenseError == "MissingParametersException") {
                    HotReloadPrefs.RenderAuthLogin = true;
                } else if (_status != null && _status.lastLicenseError == "LicenseNotFoundException") {
                    HotReloadPrefs.RenderAuthLogin = false;
                }
                
                HotReloadPrefs.FirstLogin = false;

                Repaint();
            } finally {
                _requestingLoginInfo = false;
            }
        }

        internal void RenderLoginScreen(bool? overrideRenderFreeTrial = null) {
            var renderFreeTrial = !HotReloadPrefs.RenderAuthLogin;
            if (overrideRenderFreeTrial != null) {
                renderFreeTrial = overrideRenderFreeTrial.Value;
            }

            if (_status?.lastLicenseError != null && _running && !Starting) {
                RenderLicenseStatusInfo(allowHide: false);
            } else {
                EditorGUILayout.LabelField((renderFreeTrial ? "Sign up" : "Login") + " to start", signUpToStartStyle);
            }
            EditorGUILayout.Space();

            RenderLicenseInnerPanel(overrideRenderFreeTrial);

            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }

        void RenderLicenseActionInformation() {
            RenderLicenseStatusInfo(allowHide: true);
            var errInfo = GetLicenseErrorDataOrDefault(_status?.lastLicenseError); 
            if (errInfo.showBuyButton || errInfo.showManageLicenseButton) {
                using(new EditorGUILayout.HorizontalScope()) {
                    if (errInfo.showBuyButton) {
                        RenderBuyLicenseButton(errInfo.buyButtonText);
                    }
                    if (errInfo.showManageLicenseButton && !HotReloadPrefs.ErrorHidden) {
                        OpenURLButton.Render(errInfo.manageLicenseButtonText, Constants.ManageLicenseURL);
                    }
                }
            }
            if (errInfo.showLoginButton && GUILayout.Button(errInfo.loginButtonText, openSettingsStyle)) {
                _window.SelectTab(typeof(HotReloadOptionsTab));
                _window.optionsTab.FocusFoldoutCategory(HotReloadOptionCategory.License);
            }
            if (errInfo.showSupportButton && !HotReloadPrefs.ErrorHidden) {
                OpenURLButton.Render(errInfo.supportButtonText, Constants.ContactURL);
            }
        }

        internal void RenderSettingsLicenseInfo() {
            if (_status?.isLicensed != true) {
                RenderConsumption();
            }
            RenderLicenseStatusInfo(verbose: true, allowHide: false);

            RenderLicenseInnerPanel(false, "Activate License");
            
            RenderBuyLicenseButton(GetAnotherLicense);

            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }

        private void RenderLicenseInnerPanel(bool? overrideRenderFreeTrial = null, string overrideActionButton = null) {
            var renderFreeTrial = !HotReloadPrefs.RenderAuthLogin;
            if (overrideRenderFreeTrial != null) {
                renderFreeTrial = overrideRenderFreeTrial.Value;
            }

            EditorGUILayout.LabelField("Email");
            GUI.SetNextControlName("email");
            _pendingEmail = EditorGUILayout.TextField(string.IsNullOrEmpty(_pendingEmail) ? HotReloadPrefs.LicenseEmail : _pendingEmail);
            _pendingEmail = _pendingEmail.Trim();

            if (!renderFreeTrial) {
                EditorGUILayout.LabelField("Password");
                GUI.SetNextControlName("password");
                _pendingPassword = EditorGUILayout.PasswordField(string.IsNullOrEmpty(_pendingPassword) ? HotReloadPrefs.LicensePassword : _pendingPassword);
            }

            EditorGUILayout.Space();
            
            RenderSwitchAuthMode(allowSwitch: overrideRenderFreeTrial == null);
            
            var e = Event.current;
            using(new EditorGUI.DisabledScope(_requestingLoginInfo)) {
                var btnLabel = overrideActionButton;
                if (String.IsNullOrEmpty(overrideActionButton)) {
                    btnLabel = renderFreeTrial ? "Activate Free Trial" : "Login";
                }
                var focusedControl = GUI.GetNameOfFocusedControl();
                if (GUILayout.Button(btnLabel, bigButtonHeight)
                    || (focusedControl == "email" 
                        || focusedControl == "password") 
                    && e.type == EventType.KeyUp 
                    && (e.keyCode == KeyCode.Return 
                        || e.keyCode == KeyCode.KeypadEnter)
                ) {
                    if (string.IsNullOrEmpty(_pendingEmail)) {
                        _activateInfoMessage = new Tuple<string, MessageType>("Please enter your email address.", MessageType.Warning);
                    } else if (!EditorWindowHelper.IsValidEmailAddress(_pendingEmail)) {
                        _activateInfoMessage = new Tuple<string, MessageType>("Please enter a valid email address.", MessageType.Warning);
                    } else if(string.IsNullOrEmpty(_pendingPassword) && !renderFreeTrial) {
                        _activateInfoMessage = new Tuple<string, MessageType>("Please enter your password.", MessageType.Warning);
                    } else {
                        if (_status?.lastLicenseError != null && !_status.isFree) {
                            if (_status.lastLicenseError == "MissingParametersException") {
                                _activateInfoMessage = new Tuple<string, MessageType>("An account already exists for this device. Please login with your existing email/password.", MessageType.Info);
                            } else if (_status.lastLicenseError.Contains("InvalidCredentialException")) {
                                _activateInfoMessage = new Tuple<string, MessageType>("Invalid email/password. You can find your initial password in the sign-up email.", MessageType.Error);
                            } else {
                                _activateInfoMessage = new Tuple<string, MessageType>("Invalid license. Please get a valid license key and activate it to start using Unity Hot Reload.", MessageType.Error);
                            }
                            HotReloadPrefs.RenderAuthLogin = true;
                        }

                        _activateInfoMessage = null;
                        _requestingLoginInfo = true;
                        if (!_running) {
                            StartCodePatcher();
                        }

                        // when activating free trial, password is not required and must be null 
                        var pass = renderFreeTrial ? null : _pendingPassword;
                        RequestLogin(_pendingEmail, pass).Forget();
                    }
                }
            }
            if (_activateInfoMessage != null && (e.type == EventType.Layout || e.type == EventType.Repaint)) {
                EditorGUILayout.HelpBox(_activateInfoMessage.Item1, _activateInfoMessage.Item2);
            }
        }

        private static void RenderSwitchAuthMode(bool allowSwitch = true) {
            using(new EditorGUILayout.HorizontalScope()) {
                var color = EditorGUIUtility.isProSkin ? new Color32(0x3C, 0x8A, 0xFF, 0xFF) : (Color32)new Color(0, 0.3587302f, 0.8962264f);
                if (HotReloadGUIHelper.LinkLabel("Forgot password?", 12, FontStyle.Normal, TextAnchor.MiddleLeft, color)) {
                    if (EditorUtility.DisplayDialog("Recover password", "Use company code 'naughtycult' and the email you signed up with in order to recover your account.", "Open in browser", "Cancel")) {
                    Application.OpenURL(Constants.ForgotPasswordURL);
                }
                }
                if (allowSwitch) {
                    GUILayout.FlexibleSpace();
                    if (HotReloadGUIHelper.LinkLabel(HotReloadPrefs.RenderAuthLogin ? "Sign up for free trial" : "Sign in", 12, FontStyle.Normal, TextAnchor.MiddleRight, color)) {
                        HotReloadPrefs.RenderAuthLogin = !HotReloadPrefs.RenderAuthLogin;
                    }
                }
            }
        }

        private void RenderProgressBar() {
            using(var scope = new EditorGUILayout.VerticalScope(HotReloadWindowStyles.MiddleCenterStyle)) {
                var progress = Mathf.Clamp(_startupProgress?.Item1 ?? 0f, 0f, 1f);
                var txt = _startupProgress?.Item2 ?? "";
                if (_startupProgress?.Item1 >= 1f) {
                    txt = "Hot Reload is running";
                }
                EditorGUI.ProgressBar(scope.rect, progress, txt);

                EditorGUILayout.Space();
                EditorGUILayout.Space();
                EditorGUILayout.Space();
            }
        }
        
        
        /*
        [MenuItem("codepatcher/restart")]
        public static void TestRestart() {
            CodePatcherCLI.Restart(Application.dataPath, false);
        }
        */
        
    }

    internal static class HotReloadGUIHelper {
        public static bool LinkLabel(string labelText, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color? color = null) {
            var stl = EditorStyles.label;

            // copy
            var origSize = stl.fontSize;
            var origStyle = stl.fontStyle;
            var origAnchor = stl.alignment;
            var origColor = stl.normal.textColor;

            // temporarily modify the built-in style
            stl.fontSize = fontSize;
            stl.fontStyle = fontStyle;
            stl.alignment = alignment;
            stl.normal.textColor = color ?? origColor;
            stl.active.textColor = color ?? origColor;
            stl.focused.textColor = color ?? origColor;
            stl.hover.textColor = color ?? origColor;

            try {
                return GUILayout.Button(labelText, stl);
            }  finally{
                // set the editor style (stl) back to normal
                stl.fontSize = origSize;
                stl.fontStyle = origStyle;
                stl.alignment = origAnchor;
                stl.normal.textColor = origColor;
                stl.active.textColor = origColor;
                stl.focused.textColor = origColor;
                stl.hover.textColor = origColor;
            }
        }

        public static void HelpBox(string message, MessageType type, int fontSize) {
            var _fontSize = EditorStyles.helpBox.fontSize;
            try {
                EditorStyles.helpBox.fontSize = fontSize;
                EditorGUILayout.HelpBox(message, type);
            } finally {
                EditorStyles.helpBox.fontSize = _fontSize;
            }
        }
    }
}

