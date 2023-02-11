using System;
using System.IO;
using SingularityGroup.HotReload.Editor.Cli;

namespace SingularityGroup.HotReload.Editor {
    public class ServerHealthCheck {
        private static readonly TimeSpan heartBeatTimeout = TimeSpan.FromMilliseconds(5000);
        public static readonly ServerHealthCheck I = new ServerHealthCheck();
        ServerHealthCheck() { }
        
        public bool IsServerHealthy { get; private set; }
        
        public void CheckHealth() {
            var fi = new FileInfo(Path.Combine(CliUtils.GetCliTempDir(), "health"));
            IsServerHealthy = fi.Exists && DateTime.UtcNow - fi.LastWriteTimeUtc < heartBeatTimeout;
        }
    }
}