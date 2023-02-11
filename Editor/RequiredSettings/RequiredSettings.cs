using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SingularityGroup.HotReload.Editor {
    struct ShowResult {
        public bool shown;
        public bool requiresSaveAssets;
    }
    
    static class RequiredSettings {
        static IRequiredSettingPresenter[] _lazyPresenters;
        public static IRequiredSettingPresenter[] Presenters => _lazyPresenters ?? (_lazyPresenters = Init());
            
        static IRequiredSettingPresenter[] Init() {
            var presenters = new List<IRequiredSettingPresenter>();
            
            Add(presenters, new RequiredSettingData(
                checker: new AllowHttpSettingChecker(),
                cacheKey: HotReloadPrefs.AllowHttpSettingCacheKey,
                installPromptRenderData: new PromptRenderData {
                    title = "Allow http",
                    message = "Hot Reload needs to send http requests locally to work properly, it is required to set the 'Allow downloads over HTTP' setting to 'Allowed for Development Builds'.",
                    ok = "Allow",
                    cancel  = "Not now",
                },
                helpBoxRenderData: new HelpBoxRenderData {
                    description = "The 'Allow downloads over HTTP' setting is disabled. Hot Reload needs to enable it for the editor and development builds to work.",
                    buttonText = "Allow http",
                    messageType = MessageType.Error,
                }
            ));
            
            Add(presenters, new RequiredSettingData(
                checker: new AutoRefreshSettingChecker(),
                cacheKey: HotReloadPrefs.AutoRefreshSettingCacheKey,
                installPromptRenderData: new PromptRenderData {
                    title = "Disable Auto Refresh",
                    message = "For the best Hot Reload experience, it is recommended to disable Unity's Auto Refresh setting",
                    ok = "Disable Auto Refresh",
                    cancel  = "Not now",
                },
                helpBoxRenderData: new HelpBoxRenderData {
                    description = "The Unity Auto Refresh setting is enabled. Hot Reload works best with Auto Refresh disabled.",
                    buttonText = "Disable Unity Auto Refresh",
                    messageType = MessageType.Warning,
                }
            ));
            
            const string suggested = "Recompile After Finished Playing";
            Add(presenters, new RequiredSettingData(
                checker: new ScriptCompilationSettingChecker(),
                cacheKey: HotReloadPrefs.ScriptCompilationSettingCacheKey,
                installPromptRenderData: new PromptRenderData {
                    title = suggested,
                    message = $"For the best Hot Reload experience, it is recommended to set Unity's 'Script Changes While Playing' to {suggested}",
                    ok = "Apply Suggestion",
                    cancel  = "Not now",
                },
                helpBoxRenderData: new HelpBoxRenderData {
                    description = $"Hot Reload works best when the Editor setting 'Script Changes While Playing' is set to '{suggested}'",
                    buttonText = "Apply suggestion",
                    messageType = MessageType.Info,
                }
            ));
            
            Add(presenters, new SlnFileSettingPresenter(new RequiredSettingData(
                checker: new SlnFileSettingChecker(),
                cacheKey: HotReloadPrefs.SlnFileSettingCacheKey,
                installPromptRenderData: null, //No prompt on install
                helpBoxRenderData: new HelpBoxRenderData {
                    description = "No .sln found. Likely the code editor that is selected doesn't support project generation.",
                    buttonText = "Change code editor",
                    messageType = MessageType.Warning,
                }
            )));
            
            Add(presenters, new RequiredSettingData(
                checker: new ProjectGenerationSettingsChecker(),
                cacheKey: HotReloadPrefs.ProjectGenerationSettingCacheKey,
                installPromptRenderData: new PromptRenderData {
                    title = "Generate csproj files for local packages",
                    message = "For the best Hot Reload experience, it is recommended to enable csproj file generation for local packages",
                    ok = "Enable", 
                    cancel = "Not now"
                },
                helpBoxRenderData: new HelpBoxRenderData {
                    description = "Project generation for local packages is disabled. Enable it to be able to Hot Reload code from local packages.",
                    buttonText = "Apply suggestion",
                    messageType = MessageType.Info,
                }
            ));
            
            return presenters.ToArray();
        }
        
        static void Add(List<IRequiredSettingPresenter> list, RequiredSettingData data) {
            list.Add(new DefaulRequiredSettingPresenter(data));
        }
        
        static void Add(List<IRequiredSettingPresenter> list, IRequiredSettingPresenter presenter) {
            list.Add(presenter);
        }
    }
    
    class RequiredSettingData {
        public readonly IRequiredSettingChecker checker;
        public readonly PromptRenderData installPromptRenderData;
        public readonly HelpBoxRenderData helpBoxRenderData;
        public readonly string cacheKey;
        public RequiredSettingData(IRequiredSettingChecker checker, PromptRenderData installPromptRenderData, HelpBoxRenderData helpBoxRenderData, string cacheKey) {
            this.checker = checker;
            this.installPromptRenderData = installPromptRenderData;
            this.helpBoxRenderData = helpBoxRenderData;
            this.cacheKey = cacheKey;
        }
    }
    
    class HelpBoxRenderData {
        public string description, buttonText;
        public MessageType messageType;
    }
    
    class PromptRenderData {
        public string title, message, ok, cancel;
    }
}