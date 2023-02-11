using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SingularityGroup.HotReload {
    internal static class TaskExtensions {
        public static async void Forget(this Task task, CancellationToken token = new CancellationToken()) {
            try {
                await task;
                if(task.IsFaulted) {
                    throw task.Exception ?? new Exception("unknown exception " + task);
                }
                token.ThrowIfCancellationRequested();
            } 
            catch(OperationCanceledException) {
                // ignore
            } catch(Exception ex) {
                if(!token.IsCancellationRequested) {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
