using System;

namespace SingularityGroup.HotReload {
    [AttributeUsage(AttributeTargets.Method)]
    public class InvokeOnHotReload : Attribute {
    }

}
