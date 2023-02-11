using System;
using UnityEditor;
using UnityEngine;

namespace SingularityGroup.HotReload.Editor {
    internal abstract class HotReloadOptionBase : IGUIComponent {
        private readonly string _text;
        private readonly string _tooltip;

        public HotReloadOptionCategory Category { get; }

        public HotReloadOptionBase(string text, string tooltip, HotReloadOptionCategory category) {
            _text = text;
            _tooltip = tooltip;

            Category = category;
        }

        public void OnGUI() {
            EditorGUILayout.BeginVertical(HotReloadWindowStyles.BoxStyle);

            var val = EditorGUILayout.BeginToggleGroup(new GUIContent(_text.StartsWith(" ") ? _text : " " + _text, _tooltip), GetValue());
            SetValue(val);

            InternalOnGUI();

            EditorGUILayout.EndToggleGroup();
            EditorGUILayout.EndVertical();
        }

        protected abstract void SetValue(bool value);
        protected abstract bool GetValue();
        protected abstract void InternalOnGUI();
    }
}
