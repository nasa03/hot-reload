using System.Text.RegularExpressions;

namespace SingularityGroup.HotReload.Editor {
    internal static class EditorWindowHelper {
        private static readonly Regex ValidEmailRegex = new Regex(@"^(?!\.)(""([^""\r\\]|\\[""\r\\])*""|"
            + @"([-a-z0-9!#$%&'*+/=?^_`{|}~]|(?<!\.)\.)*)(?<!\.)"
            + @"@[a-z0-9][\w\.-]*[a-z0-9]\.[a-z][a-z\.]*[a-z]$", RegexOptions.IgnoreCase);

        public static bool IsValidEmailAddress(string email) {
            return ValidEmailRegex.IsMatch(email);
        }
    }
}
