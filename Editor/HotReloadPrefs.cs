using System;
using UnityEditor;

namespace SingularityGroup.HotReload.Editor {
    internal static class HotReloadPrefs {
        private const string AutoApplyKey = "HotReloadWindow.AutoApply";
        private const string RemoteServerKey = "HotReloadWindow.RemoteServer";
        private const string RemoteServerHostKey = "HotReloadWindow.RemoteServerHost";
        private const string LicenseEmailKey = "HotReloadWindow.LicenseEmail";
        private const string RenderAuthLoginKey = "HotReloadWindow.RenderAuthLogin";
        private const string FirstLoginCachedKey = "HotReloadWindow.FirstLoginCachedKey";
        private const string ShowOnStartupKey = "HotReloadWindow.ShowOnStartup";
        private const string PasswordCachedKey = "HotReloadWindow.PasswordCached";
        private const string ExposeServerToLocalNetworkKey = "HotReloadWindow.ExposeServerToLocalNetwork";
        private const string ErrorHiddenCachedKey = "HotReloadWindow.ErrorHiddenCachedKey";


        static string[] settingCacheKeys;
        public static string[] SettingCacheKeys = settingCacheKeys ?? (settingCacheKeys = new[] {
            AllowHttpSettingCacheKey,
            AutoRefreshSettingCacheKey,
            ScriptCompilationSettingCacheKey,
            SlnFileSettingCacheKey,
            ProjectGenerationSettingCacheKey,
        });
        
        public const string AllowHttpSettingCacheKey = "HotReloadWindow.AllowHttpSettingCacheKey";
        public const string AutoRefreshSettingCacheKey = "HotReloadWindow.AutoRefreshSettingCacheKey";
        public const string ScriptCompilationSettingCacheKey = "HotReloadWindow.ScriptCompilationSettingCacheKey";
        public const string SlnFileSettingCacheKey = "HotReloadWindow.SlnFileSettingCacheKey";
        public const string ProjectGenerationSettingCacheKey = "HotReloadWindow.ProjectGenerationSettingCacheKey";

        public static bool AutoApply {
            get { return EditorPrefs.GetBool(AutoApplyKey, true); }
            set { EditorPrefs.SetBool(AutoApplyKey, value); }
        }

        public static bool RemoteServer {
            get { return EditorPrefs.GetBool(RemoteServerKey, false); }
            set { EditorPrefs.SetBool(RemoteServerKey, value); }
        }

        public static string RemoteServerHost {
            get { return EditorPrefs.GetString(RemoteServerHostKey); }
            set { EditorPrefs.SetString(RemoteServerHostKey, value); }
        }

        public static string LicenseEmail {
            get { return EditorPrefs.GetString(LicenseEmailKey); }
            set { EditorPrefs.SetString(LicenseEmailKey, value); }
        }
        
        public static string LicensePassword {
            get { return EditorPrefs.GetString(PasswordCachedKey); }
            set { EditorPrefs.SetString(PasswordCachedKey, value); }
        }
        
        public static bool RenderAuthLogin { // false = render free trial
            get { return EditorPrefs.GetBool(RenderAuthLoginKey); }
            set { EditorPrefs.SetBool(RenderAuthLoginKey, value); }
        }
        
        public static bool FirstLogin {
            get { return EditorPrefs.GetBool(FirstLoginCachedKey, true); }
            set { EditorPrefs.SetBool(FirstLoginCachedKey, value); }
        }

        public static string ShowOnStartup { // WindowAutoOpen
            get { return EditorPrefs.GetString(ShowOnStartupKey); }
            set { EditorPrefs.SetString(ShowOnStartupKey, value); }
        }


        public static bool ErrorHidden {
            get { return EditorPrefs.GetBool(ErrorHiddenCachedKey); }
            set { EditorPrefs.SetBool(ErrorHiddenCachedKey, value); }
        }

        public static ShowOnStartupEnum GetShowOnStartupEnum() {
            ShowOnStartupEnum showOnStartupEnum;
            if (Enum.TryParse(HotReloadPrefs.ShowOnStartup, true, out showOnStartupEnum)) {
                return showOnStartupEnum;
            }
            return ShowOnStartupEnum.Always;
        }
        
        public static bool ExposeServerToLocalNetwork {
            get { return EditorPrefs.GetBool(ExposeServerToLocalNetworkKey, false); }
            set { EditorPrefs.SetBool(ExposeServerToLocalNetworkKey, value); }
        }
    }
}
