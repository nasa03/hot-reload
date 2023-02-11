using UnityEditor;

namespace SingularityGroup.HotReload.Editor {
    /// <summary>
    /// An option scoped to the current Unity project.
    /// </summary>
    /// <remarks>
    /// These options are intended to be shared with collaborators and used by Unity Player builds.
    /// </remarks>
    internal interface ISerializedProjectOption {
        /// <summary>
        /// The value is <see cref="HotReloadSettingsObject"/> wrapped by SerializedObject
        /// </summary>
        SerializedObject SettingsObject { set; }
    }
}
