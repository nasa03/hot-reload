using UnityEditor;
using UnityEngine;

namespace SingularityGroup.HotReload.Editor {
    internal static class HotReloadWindowStyles {
        private static GUIStyle h1TitleStyle;
        private static GUIStyle h2TitleStyle;
        private static GUIStyle h3TitleStyle;
        private static GUIStyle h4TitleStyle;
        private static GUIStyle h5TitleStyle;
        private static GUIStyle boxStyle;
        private static GUIStyle wrapStyle;
        private static GUIStyle middleCenterStyle;
        private static GUIStyle mediumMiddleCenterStyle;
        private static GUIStyle foldoutStyle;
        private static GUIStyle h3CenterTitleStyle;

        public static GUIStyle H1TitleStyle {
            get {
                if (h1TitleStyle == null) {
                    h1TitleStyle = new GUIStyle(EditorStyles.label);
                    h1TitleStyle.normal.textColor = EditorStyles.label.normal.textColor;
                    h1TitleStyle.fontStyle = FontStyle.Bold;
                    h1TitleStyle.fontSize = 16;
                }
                return h1TitleStyle;
            }
        }

        public static GUIStyle H2TitleStyle {
            get {
                if (h2TitleStyle == null) {
                    h2TitleStyle = new GUIStyle(EditorStyles.label);
                    h2TitleStyle.normal.textColor = EditorStyles.label.normal.textColor;
                    h2TitleStyle.fontStyle = FontStyle.Bold;
                    h2TitleStyle.fontSize = 14;
                }
                return h2TitleStyle;
            }
        }

        public static GUIStyle H3TitleStyle {
            get {
                if (h3TitleStyle == null) {
                    h3TitleStyle = new GUIStyle(EditorStyles.label);
                    h3TitleStyle.normal.textColor = EditorStyles.label.normal.textColor;
                    h3TitleStyle.fontStyle = FontStyle.Bold;
                    h3TitleStyle.fontSize = 12;
                }
                return h3TitleStyle;
            }
        }
        
        public static GUIStyle H3CenteredTitleStyle {
            get {
                if (h3CenterTitleStyle == null) {
                    h3CenterTitleStyle = new GUIStyle(EditorStyles.label);
                    h3CenterTitleStyle.normal.textColor = EditorStyles.label.normal.textColor;
                    h3CenterTitleStyle.fontStyle = FontStyle.Bold;
                    h3CenterTitleStyle.alignment = TextAnchor.MiddleCenter;
                    h3CenterTitleStyle.fontSize = 12;
                }
                return h3CenterTitleStyle;
            }
        }

        public static GUIStyle H4TitleStyle {
            get {
                if (h4TitleStyle == null) {
                    h4TitleStyle = new GUIStyle(EditorStyles.label);
                    h4TitleStyle.normal.textColor = EditorStyles.label.normal.textColor;
                    h4TitleStyle.fontStyle = FontStyle.Bold;
                    h4TitleStyle.fontSize = 11;
                }
                return h4TitleStyle;
            }
        }

        public static GUIStyle H5TitleStyle {
            get {
                if (h5TitleStyle == null) {
                    h5TitleStyle = new GUIStyle(EditorStyles.label);
                    h5TitleStyle.normal.textColor = EditorStyles.label.normal.textColor;
                    h5TitleStyle.fontStyle = FontStyle.Bold;
                    h5TitleStyle.fontSize = 10;
                }
                return h5TitleStyle;
            }
        }

        public static GUIStyle BoxStyle {
            get {
                if (boxStyle == null) {
                    boxStyle = new GUIStyle(GUI.skin.box);
                    boxStyle.normal.textColor = GUI.skin.label.normal.textColor;
                    boxStyle.fontStyle = FontStyle.Bold;
                    boxStyle.alignment = TextAnchor.UpperLeft;
                }
                return boxStyle;
            }
        }

        public static GUIStyle WrapStyle {
            get {
                if (wrapStyle == null) {
                    wrapStyle = new GUIStyle(EditorStyles.label);
                    wrapStyle.fontStyle = FontStyle.Normal;
                    wrapStyle.wordWrap = true;
                }
                return wrapStyle;
            }
        }

        public static GUIStyle MiddleCenterStyle {
            get {
                if (middleCenterStyle == null) {
                    middleCenterStyle = new GUIStyle(EditorStyles.label);
                    middleCenterStyle.fontStyle = FontStyle.Normal;
                    middleCenterStyle.alignment = TextAnchor.MiddleCenter;
                }
                return middleCenterStyle;
            }
        }
        
        public static GUIStyle MediumMiddleCenterStyle {
            get {
                if (mediumMiddleCenterStyle == null) {
                    mediumMiddleCenterStyle = new GUIStyle(EditorStyles.label);
                    mediumMiddleCenterStyle.fontStyle = FontStyle.Normal;
                    mediumMiddleCenterStyle.fontSize = 12;
                    mediumMiddleCenterStyle.alignment = TextAnchor.MiddleCenter;
                }
                return mediumMiddleCenterStyle;
            }
        }

        public static GUIStyle FoldoutStyle {
            get {
                if (foldoutStyle == null) {
                    foldoutStyle = new GUIStyle(EditorStyles.foldout);
                    foldoutStyle.normal.textColor = GUI.skin.label.normal.textColor;
                    foldoutStyle.alignment = TextAnchor.MiddleLeft;
                    foldoutStyle.fontStyle = FontStyle.Bold;
                    foldoutStyle.fontSize = 12;
                }
                return foldoutStyle;
            }
        }
    }
}
