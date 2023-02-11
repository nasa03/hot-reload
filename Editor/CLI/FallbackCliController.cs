
using System.Threading.Tasks;

namespace SingularityGroup.HotReload.Editor.Cli {
    class FallbackCliController : ICliController {
        public string GetDefaultExecutablePath(string serverBasePath) {
            return "";
        }
        
        public Task Start(StartArgs args) => Task.CompletedTask;

        public Task Stop() => Task.CompletedTask;
    }
}