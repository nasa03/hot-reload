
namespace SingularityGroup.HotReload.Editor {
    internal abstract class HotReloadTabBase : IGUIComponent {
        protected readonly HotReloadWindow _window;

        public string Title { get; }
        public string Icon { get; }
        public string Tooltip { get; }

        public HotReloadTabBase(HotReloadWindow window, string title, string icon, string tooltip) {
            _window = window;

            Title = title;
            Icon = icon;
            Tooltip = tooltip;
        }

        protected void Repaint() {
            _window.Repaint();
        }

        public virtual void Update() { }

        public abstract void OnGUI();
    }
}
