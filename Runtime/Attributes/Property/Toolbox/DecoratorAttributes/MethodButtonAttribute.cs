using System;
using System.Diagnostics;

namespace UnityEngine
{
    /// <summary>
    /// Creates a buttom in the editor to invoke the function
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    [Conditional("UNITY_EDITOR")]
    public sealed class MethodButtonAttribute : PropertyAttribute
    {
        public enum ShowMode
        {
            Always,
            EditorOnly,
            PlayModeOnly
        }

        public readonly string label;
        public readonly ShowMode showMode;
        public readonly bool confirm;
        public readonly string confirmMessage;

        public MethodButtonAttribute(
            string label = null,
            ShowMode showMode = ShowMode.Always,
            bool confirm = false,
            string confirmMessage = "Invoke method?")
        {
            this.label = label;
            this.showMode = showMode;
            this.confirm = confirm;
            this.confirmMessage = confirmMessage;
        }
    }
}