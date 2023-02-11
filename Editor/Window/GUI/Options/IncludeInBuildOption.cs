using UnityEditor;

namespace SingularityGroup.HotReload.Editor {
    internal class IncludeInBuildOption : HotReloadOptionBase, ISerializedProjectOption {
        public IncludeInBuildOption() : base(
            "[Player] Include Hot Reload in player builds",
            null,
            HotReloadOptionCategory.Mobile) {
        }
        
        public SerializedObject SettingsObject { get; set; }

        private SerializedProperty Property {
            get {
                return SettingsObject.FindProperty(nameof(HotReloadSettingsObject.IncludeInBuild));
            }
        }

        protected override bool GetValue() {
            return Property.boolValue;
        }

        protected override void SetValue(bool value) {
            Property.boolValue = value;
        }
        
        protected override void InternalOnGUI() {
            string description;
            if (GetValue()) {
                description = "The Hot Reload runtime is included in development builds that use the Mono scripting backend.";
            } else {
                description = "The Hot Reload runtime will not be included in any build. Use this option to disable HotReload without removing it from your project.";
            }
            description += "\nHot Reload is always available in the Unity Editor's Playmode.";
            EditorGUILayout.LabelField(description, HotReloadWindowStyles.WrapStyle);
        }
    }
}