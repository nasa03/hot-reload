using UnityEditor;

namespace SingularityGroup.HotReload.Editor {
    internal sealed class AutoApplyOption : HotReloadOptionBase {
        public AutoApplyOption() : base(
            "Enable Hot Reload",
            "Disable if you want to manually apply changes",
            HotReloadOptionCategory.General) { }

        protected override void InternalOnGUI() {
            if (GetValue()) {
                EditorGUILayout.LabelField("Automatically hot reload when changes are made.\n\nDisable to temporarily stop hot reload changes from being applied.", HotReloadWindowStyles.WrapStyle);
            }
        }
        
        protected override void SetValue(bool value) {
            CodePatcher.I.AutoApply = HotReloadPrefs.AutoApply = value;
        }

        protected override bool GetValue() {
            return HotReloadPrefs.AutoApply;
        }
    }
}
