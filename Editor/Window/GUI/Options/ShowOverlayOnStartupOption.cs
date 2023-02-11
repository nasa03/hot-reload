using UnityEditor;

namespace SingularityGroup.HotReload.Editor {
    internal class ShowOverlayOnStartupOption : HotReloadOptionBase, ISerializedProjectOption {
        public ShowOverlayOnStartupOption() : base(
            "[Player] Show options popup when the app starts",
            null,
            HotReloadOptionCategory.Mobile) { }

        protected override bool GetValue() {
            return Property.boolValue;
        }
        
        protected override void SetValue(bool value) {
            Property.boolValue = value;
        }

        private SerializedProperty Property {
            get {
                return SettingsObject.FindProperty(nameof(HotReloadSettingsObject.ShowOptionsPopupOnStartup));
            }
        }

        public SerializedObject SettingsObject { get; set; }

        protected override void InternalOnGUI() {
            var description =
                "The popup lets you enter an ip address to connect to your Hot Reload server.";
            EditorGUILayout.LabelField(description, HotReloadWindowStyles.WrapStyle);
        }
    }
}