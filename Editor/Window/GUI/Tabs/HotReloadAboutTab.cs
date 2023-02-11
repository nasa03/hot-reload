using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SingularityGroup.HotReload.Editor {
    internal class HotReloadAboutTab : HotReloadTabBase {
        private readonly List<IGUIComponent> _supportButtons;
        private readonly OpenURLButton _buyLicenseButton;
        private readonly OpenDialogueButton _manageLicenseButton;
        private readonly OpenDialogueButton _manageAccountButton;

        public HotReloadAboutTab(HotReloadWindow window) : base(window, "Help", "_Help", "Info and support for Unity Hot Reload.") {
            _supportButtons = new List<IGUIComponent> {
                new OpenURLButton("Documentation", Constants.DocumentationURL),
                // new OpenURLButton("Discord", Constants.DiscordURL),
                 new OpenURLButton("Unity Forum", Constants.ForumURL),
                new OpenURLButton("Contact", Constants.ContactURL),
            };
            _manageLicenseButton = new OpenDialogueButton("Manage License", Constants.ManageLicenseURL, "Manage License", "Upgrade/downgrade/edit your subscription and edit payment info.", "Open in browser", "Cancel");
            _manageAccountButton = new OpenDialogueButton("Manage Account", Constants.ManageAccountURL, "Manage License", "Login with company code 'naughtycult'. Use the email you signed up with. Your initial password was sent to you by email.", "Open in browser", "Cancel");
            _buyLicenseButton = new OpenURLButton("Get Another License", Constants.ProductPurchaseURL);
        }

        public override void OnGUI() {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox($"You are running Unity Hot Reload version {PackageConst.Version}. ", MessageType.Info);
            EditorGUILayout.Space();

            _buyLicenseButton.OnGUI();
            EditorGUILayout.Space();

            using(new EditorGUILayout.HorizontalScope()) {
                foreach (var button in _supportButtons) {
                    button.OnGUI();
                }
            }
            EditorGUILayout.Space();

            var hasTrial = _window.runTab.TrialLicense;
            var hasPaid = _window.runTab.HasPayedLicense;
            if (hasPaid || hasTrial) {
                using(new EditorGUILayout.HorizontalScope()) {
                    if (hasPaid) {
                        _manageLicenseButton.OnGUI();
                    }
                    _manageAccountButton.OnGUI();
                }
                EditorGUILayout.Space();
            }

            foreach (var settingKey in HotReloadPrefs.SettingCacheKeys) {
                if (!EditorPrefs.GetBool(settingKey, true)) {
                    if (GUILayout.Button("Re-enable all suggestions")) {
                        foreach (var _settingKey in HotReloadPrefs.SettingCacheKeys) {
                            EditorPrefs.SetBool(_settingKey, true);
                        }
                        _window.SelectTab(typeof(HotReloadRunTab));
                    }
                    EditorGUILayout.Space();
                    break;
                }
            }
        }
    }
}
