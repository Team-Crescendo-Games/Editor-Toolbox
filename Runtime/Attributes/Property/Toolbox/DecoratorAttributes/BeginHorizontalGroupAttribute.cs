using System;
using System.Diagnostics;

namespace UnityEngine
{
    /// <summary>
    /// Begins horizontal group of properties. 
    /// Additionally, creates title label and scrollbar if needed.
    /// Has to be closed by the <see cref="EndHorizontalGroupAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    [Conditional("UNITY_EDITOR")]
    public class BeginHorizontalGroupAttribute : BeginHorizontalAttribute
    {
        public BeginHorizontalGroupAttribute() : base()
        {
            WidthOffset = 32.0f;
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

        /// <summary>
        /// Indicates the fixed height of the horizontal group, if is equal to 0 then height will be auto-sized.
        /// </summary>
        public float Height { get; set; } = 0.0f;
    }
}