using System;
using System.Diagnostics;

using Toolbox.Attributes.Property;

namespace UnityEngine
{
    /// <summary>
    /// Replaces old label with <see cref="NewLabel"/> value.
    /// 
    /// <para>Supported types: all.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    [Conditional("UNITY_EDITOR")]
    public class NewLabelAttribute : PropertyAttribute, ILabelProcessorAttribute
    {
        public NewLabelAttribute(string newLabel)
        {
            NewLabel = newLabel;
        }

        /// <summary>
        /// New label that will be used in the Inspector.
        /// </summary>
        public string NewLabel { get; private set; }
    }
}