using System;
using System.Diagnostics;

namespace UnityEngine
{
    /// <summary>
    /// Begins horizontal group of properties. Has to be closed by the <see cref="EndHorizontalAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    [Conditional("UNITY_EDITOR")]
    public class BeginHorizontalAttribute : ToolboxDecoratorAttribute
    {
        public BeginHorizontalAttribute()
        { }

        /// <summary>
        /// Indicates whether layout elements should be sized automatically or using associated properties.
        /// Associated properties: <see cref="ElementsInLayout"/>, <see cref="WidthOffset"/>, <see cref="WidthOffsetPerElement"/>.
        /// </summary>
        public bool ControlFieldWidth { get; set; }
        /// <summary>
        /// Indicates how many elements are placed in the layout.
        /// Used to specify how big should be the field width for each element.
        /// Used only when the <see cref="ControlFieldWidth"/> is set to <see langword="true"/>.
        /// </summary>
        public int ElementsInLayout { get; set; } = -1;
        /// <summary>
        /// Value substracted from the available space when calculating field width for each element.
        /// Used only when the <see cref="ControlFieldWidth"/> is set to <see langword="true"/>.
        /// </summary>
        public float WidthOffset { get; set; }
        /// <summary>
        /// Value substracted from the available space when calculating field width for each element.
        /// Used only when the <see cref="ControlFieldWidth"/> is set to <see langword="true"/>.
        /// </summary>
        public float WidthOffsetPerElement { get; set; }
        /// <summary>
        /// Overrides label width within the layout for each element.
        /// Set to 0 to keep the default width.
        /// </summary>
        public float LabelWidth { get; set; } = 100.0f;
    }
}