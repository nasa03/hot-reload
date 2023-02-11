using System.Threading.Tasks;

namespace SingularityGroup.HotReload.Editor.Cli {
    interface ICliController {
        string GetDefaultExecutablePath(string serverBasePath);
        
        Task Start(StartArgs args);
        
        Task Stop();
    }
}