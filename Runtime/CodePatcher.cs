
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SingularityGroup.HotReload.DTO;
using JetBrains.Annotations;
using SingularityGroup.HotReload.HarmonyLib;
using SingularityGroup.HotReload.Newtonsoft.Json;
using UnityEngine;
using Debug = UnityEngine.Debug;

[assembly: InternalsVisibleTo("SingularityGroup.HotReload.Editor")]

namespace SingularityGroup.HotReload {
    public class CodePatcher {
        public static readonly CodePatcher I = new CodePatcher();
        /// <summary>Tag for use in Debug.Log.</summary>
        public const string TAG = "HotReload";
        
        public bool AutoApply { get; set; } = true;
        public string PersistencePath { get; private set ; }
        public int PatchesApplied { get; private set; }
        
        readonly List<MethodPatchResponse> pendingPatches;
        readonly Dictionary<MethodBase, IDisposable> patchRecords;
        readonly List<MethodPatchResponse> patchHistory;
        readonly List<SMethod> patchedMethods;
        string[] assemblySearchPaths;
        SymbolResolver symbolResolver;
        
        CodePatcher() {
            pendingPatches = new List<MethodPatchResponse>();
            patchRecords = new Dictionary<MethodBase, IDisposable>();
            patchHistory = new List<MethodPatchResponse>(); 
            patchedMethods = new List<SMethod>();
            string tmpDir;
            if(Application.isEditor) {
                tmpDir = "Library/com.singularitygroup.hotreload";
            } else {
                tmpDir = Application.temporaryCachePath;
            }
            Directory.CreateDirectory(tmpDir);
            FileLog.logPath = Path.Combine(tmpDir, "harmony.log.txt");
        }

        
        internal IReadOnlyList<SMethod> PatchedMethods => patchedMethods;
        internal IReadOnlyList<MethodPatchResponse> PendingPatches => pendingPatches;
        
        
        public string[] GetAssemblySearchPaths() {
            EnsureSymbolResolver();
            return assemblySearchPaths;
        }
       
        internal void RegisterPatches(MethodPatchResponse patches) {
            Log("Register patches.\nWarnings: {0} \nMethods:\n{1}", string.Join("\n", patches.failures), string.Join("\n", patches.patches.SelectMany(p => p.modifiedMethods).Select(m => m.displayName)));
            pendingPatches.Add(patches);
            if(AutoApply) {
                ApplyPatches();
            } 
        }
        
        public void RemovePatch(string patchId) {
            for (int i = pendingPatches.Count - 1; i >= 0; i--) {
                if(pendingPatches[i].id == patchId) {
                    pendingPatches.RemoveAt(i);
                    break;
                }
            }
        }
        
        public void ApplyPatches() {
            Log("ApplyPatches. {0} patches pending.", pendingPatches.Count);
            EnsureSymbolResolver();

            try {
                int count = 0;
                foreach(var response in pendingPatches) {
                    HandleMethodPatchResponse(response);
                    patchHistory.Add(response);

                    count += response.patches.Length;
                }
                if (count > 0) {
                    Dispatch.OnHotReload().Forget();
                }
            } catch(Exception ex) {
                Debug.LogWarning($"Exception occured when handling method patch. Exception:\n{ex}");
            } finally {
                pendingPatches.Clear();
                RemoveDuplicateMethods();
            }
            
            if(PersistencePath != null) {
                SaveAppliedPatches(PersistencePath).Forget();
            }

            PatchesApplied++;
        }
        
        internal void ClearPatchedMethods() {
            patchedMethods.Clear();
            PatchesApplied = 0;
        }
        
        void RemoveDuplicateMethods() {
            var seen = new HashSet<SMethod>(SimpleMethodComparer.I);
            patchedMethods.RemoveAll(m => !seen.Add(m));
        }
        
        internal void UndoPatch(SMethod sMethod) {
            EnsureSymbolResolver();
            MethodBase method = null;
            try {
                method = symbolResolver.Resolve(sMethod);
                TryUndoPatch(method, true);
                patchedMethods.RemoveAll(m => m.metadataToken == sMethod.metadataToken);
            } catch(Exception ex) {
                Debug.LogWarning($"Exception occured when undoing method patch. Exception:\n{ex}");
                if(method != null) {
                    patchRecords.Remove(method); 
                }
                patchedMethods.Remove(sMethod);
            }
        }

        void TryUndoPatch(MethodBase method, bool requireSuccess) {
            IDisposable state;
            if (patchRecords.TryGetValue(method, out state)) {
                Log("Undo patch for method {0}", method);
                try {
                    state.Dispose();
                } catch {
                    if(requireSuccess) {
                        throw;
                    }
                }
                    
                patchRecords.Remove(method);
            }
        }


        void HandleMethodPatchResponse(MethodPatchResponse response) {
            EnsureSymbolResolver();
            foreach(var patch in response.patches) {
                try {
                    var asm = Assembly.Load(patch.patchAssembly);
                    var module = asm.GetLoadedModules()[0];
                    foreach(var sMethod in patch.newMethods) {
                        var newMethod = module.ResolveMethod(sMethod.metadataToken);
                        MethodUtils.DisableVisibilityChecks(newMethod);
                    }
                    
                    symbolResolver.AddAssembly(asm);
                    for (int i = 0; i < patch.modifiedMethods.Length; i++) {
                        PatchMethod(module, patch.modifiedMethods[i], patch.patchMethods[i]);
                    }
                } catch(Exception ex) {
                    Debug.LogWarning($"Failed to apply patch with id: {patch.patchId}\n{ex}");
                } finally {
                    FileLog.FlushBuffer();
                }
            }
        }

        void PatchMethod(Module module, SMethod sOriginalMethod, SMethod sPatchMethod) {
            try {
                var patchMethod = module.ResolveMethod(sPatchMethod.metadataToken);
                var start = DateTime.UtcNow;
                var state = TryResolveMethod(sOriginalMethod, patchMethod);

                if (DateTime.UtcNow - start > TimeSpan.FromMilliseconds(500)) {
                    Debug.Log($"Hot Reload apply took {(DateTime.UtcNow - start).TotalMilliseconds}");
                }

                if(state.match == null) {
                    Debug.LogWarningFormat(
                        "Method mismatch: {0}, patch: {1}. This can have multiple reasons:\n"
                        + "1. You are running the Editor multiple times for the same project using symlinks, and are making changes from the symlink project\n"
                        + "2. A bug in Hot Reload. Please send us a reproduce (code before/after), and we'll get it fixed for you\n"
                        , sOriginalMethod.simpleName, patchMethod.Name
                    );

                    return;
                }
                if(BurstChecker.IsBurstCompiled(state.match)) {
                    Debug.Log($"Skipped Hot Reload for '{sOriginalMethod.displayName}' because it was compiled with burst");
                    return;
                }

                TryUndoPatch(state.match, false);
                Log("Detour method {0:X8} {1}, offset: {2}", sOriginalMethod.metadataToken, patchMethod.Name, state.offset);
                DetourResult result;
                DetourApi.DetourMethod(state.match, patchMethod, out result);

                if (result.success) {
                    patchRecords.Add(state.match, result.patchRecord);
                    patchedMethods.Add(sOriginalMethod);
                    if (RequestHelper.ServerInfo.isRemote) {
                        File.WriteAllText(Path.Combine(Path.GetTempPath(),"code-patcher-detour-log"), $"success {patchMethod.Name}");
                    }
                } else {
                    HandleMethodPatchFailure(sOriginalMethod, result.exception);
                }
            } catch(Exception ex) {
                HandleMethodPatchFailure(sOriginalMethod, ex);
            }
        }
        
        struct ResolveMethodState {
            public readonly SMethod originalMethod;
            public readonly int offset;
            public readonly bool tryLowerTokens;
            public readonly bool tryHigherTokens;
            public readonly MethodBase match;
            public ResolveMethodState(SMethod originalMethod, int offset, bool tryLowerTokens, bool tryHigherTokens, MethodBase match) {
                this.originalMethod = originalMethod;
                this.offset = offset;
                this.tryLowerTokens = tryLowerTokens;
                this.tryHigherTokens = tryHigherTokens;
                this.match = match;
            }

            public ResolveMethodState With(bool? tryLowerTokens = null, bool? tryHigherTokens = null, MethodBase match = null, int? offset = null) {
                return new ResolveMethodState(
                    originalMethod, 
                    offset ?? this.offset, 
                    tryLowerTokens ?? this.tryLowerTokens,
                    tryHigherTokens ?? this.tryHigherTokens,
                    match ?? this.match);
            }
        }
        
        struct ResolveMethodResult {
            public readonly MethodBase resolvedMethod;
            public readonly bool tokenOutOfRange;
            public ResolveMethodResult(MethodBase resolvedMethod, bool tokenOutOfRange) {
                this.resolvedMethod = resolvedMethod;
                this.tokenOutOfRange = tokenOutOfRange;
            }
        }
        
        ResolveMethodState TryResolveMethod(SMethod originalMethod, MethodBase patchMethod) {
            var state = new ResolveMethodState(originalMethod, offset: 0, tryLowerTokens: true, tryHigherTokens: true, match: null);
            var result = TryResolveMethodCore(state.originalMethod, patchMethod, 0);
            if(result.resolvedMethod != null) {
                return state.With(match: result.resolvedMethod);
            }
            state = state.With(offset: 1);
            const int tries = 100000;
            while(state.offset <= tries && (state.tryHigherTokens || state.tryLowerTokens)) {
                if(state.tryHigherTokens) {
                    result = TryResolveMethodCore(originalMethod, patchMethod, state.offset);
                    if(result.resolvedMethod != null) {
                        return state.With(match: result.resolvedMethod);
                    } else if(result.tokenOutOfRange) {
                        state = state.With(tryHigherTokens: false);
                    }
                }
                if(state.tryLowerTokens) {
                    result = TryResolveMethodCore(originalMethod, patchMethod, -state.offset);
                    if(result.resolvedMethod != null) {
                        return state.With(match: result.resolvedMethod);
                    } else if(result.tokenOutOfRange) {
                        state = state.With(tryLowerTokens: false);
                    }
                }
                state = state.With(offset: state.offset + 1);
            }
            return state;
        }
        
        
        ResolveMethodResult TryResolveMethodCore(SMethod methodToResolve, MethodBase patchMethod, int offset) {
            bool tokenOutOfRange = false;
            MethodBase resolvedMethod = null;
            try {
                resolvedMethod = TryGetMethodBaseWithRelativeToken(methodToResolve, offset);
                if(!MethodCompatiblity.AreMethodsCompatible(resolvedMethod, patchMethod)) {
                    resolvedMethod = null;
                }
            } catch (SymbolResolvingFailedException ex) when(ex.InnerException is ArgumentOutOfRangeException) {
                tokenOutOfRange = true;
            } catch (ArgumentOutOfRangeException) {
                tokenOutOfRange = true;
            }
            return new ResolveMethodResult(resolvedMethod, tokenOutOfRange);
        }
        
        MethodBase TryGetMethodBaseWithRelativeToken(SMethod sOriginalMethod, int offset) {
            return symbolResolver.Resolve(new SMethod(sOriginalMethod.assemblyName, 
                sOriginalMethod.displayName, 
                sOriginalMethod.metadataToken + offset,
                sOriginalMethod.genericTypeArguments, 
                sOriginalMethod.genericTypeArguments,
                sOriginalMethod.simpleName));
        }
    
        void HandleMethodPatchFailure(SMethod method, Exception exception) {
            Debug.LogWarning($"Failed to apply patch for method {method.displayName} in assembly {method.assemblyName}\n{exception}");
            Debug.LogException(exception);
        }

        void EnsureSymbolResolver() {
            if (symbolResolver == null) {
                var searchPaths = new HashSet<string>();
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var assembliesByName = new Dictionary<string, List<Assembly>>();
                for (var i = 0; i < assemblies.Length; i++) {
                    var name = assemblies[i].GetName().Name;
                    List<Assembly> list;
                    if (!assembliesByName.TryGetValue(name, out list)) {
                        assembliesByName.Add(name, list = new List<Assembly>());
                    }
                    list.Add(assemblies[i]);
                    
                    if(assemblies[i].IsDynamic) continue;
                    
                    var location = assemblies[i].Location;
                    if(File.Exists(location)) {
                        searchPaths.Add(Path.GetDirectoryName(Path.GetFullPath(location)));
                    }
                }
                symbolResolver = new SymbolResolver(assembliesByName);
                assemblySearchPaths = searchPaths.ToArray();
            }
        }
        
        
        public async Task SaveAppliedPatches(string filePath) {
            if (filePath == null) {
                throw new ArgumentNullException(nameof(filePath));
            }
            filePath = Path.GetFullPath(filePath);
            var dir = Path.GetDirectoryName(filePath);
            if(string.IsNullOrEmpty(dir)) {
                throw new ArgumentException("Invalid path: " + filePath, nameof(filePath));
            }
            Directory.CreateDirectory(dir);
            var history = patchHistory.ToList();
            
            Log("Saving {0} applied patches to {1}", history.Count, filePath);

            await Task.Run(() => {
                var json = JsonConvert.SerializeObject(history);
                File.WriteAllText(filePath, json);
            });
        }
        
        public void LoadPatchesBlocked(string filePath) {
            Log("Loading patches from file {0}", filePath);
            var file = new FileInfo(filePath);
            if(file.Exists) {
                var bytes = File.ReadAllText(filePath);
                var patches = JsonConvert.DeserializeObject<List<MethodPatchResponse>>(bytes);
                Log("Loaded {0} patches from disk", patches.Count.ToString());
                foreach (var patch in patches) {
                    RegisterPatches(patch);
                }
            }  
            
        }
        
        
        [StringFormatMethod("format")]
        static void Log(string format, params object[] args) {
#if !UNITY_EDITOR
#           if (UNITY_2019_4_OR_NEWER)
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, format, args);
#           else
                // todo: does this appear in logcat?
                Console.WriteLine(format, args);
#endif // UNITY_2019_4_OR_NEWER
#endif //!UNITY_EDITOR
        }
        
        class SimpleMethodComparer : IEqualityComparer<SMethod> {
            public static readonly SimpleMethodComparer I = new SimpleMethodComparer();
            SimpleMethodComparer() { }
            public bool Equals(SMethod x, SMethod y) => x.metadataToken == y.metadataToken;
            public int GetHashCode(SMethod x) {
                return x.metadataToken;
            }
        }
    }
}