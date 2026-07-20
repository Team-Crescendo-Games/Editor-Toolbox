using System;
using System.Diagnostics;

namespace UnityEngine
{
    /// <summary>
    /// Begins vertical group of properties. Has to be closed by the <see cref="EndGroupAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    [Conditional("UNITY_EDITOR")]
    public class BeginGroupAttribute : ToolboxDecoratorAttribute
    {
        public BeginGroupAttribute(string label = null)
        {
            Label = label;
            Order = 1000;
        }

        /// <summary>
        /// Optional label (header) that can be displayed at the group's top.
        /// </summary>
        public string Label { get; set; }
        public bool HasLabel => !string.IsNullOrEmpty(Label);
        /// <summary>
        /// Indicates what style should be used to render the group.
        /// </summary>
        public GroupStyle Style { get; set; } = GroupStyle.Round;
    }
}