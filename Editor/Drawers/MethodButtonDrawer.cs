using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Toolbox.Editor.Drawers
{
    using Editor = UnityEditor.Editor;

    /// <summary>
    /// Draws inspector buttons for methods marked with <see cref="MethodButtonAttribute"/>.
    /// Method-targeted attributes have no anchoring <see cref="SerializedProperty"/>, so they
    /// cannot be handled by the regular <see cref="ToolboxDrawer"/> pipeline and are instead
    /// drawn directly by <see cref="ToolboxEditor"/> at the end of the inspector.
    /// </summary>
    public static class MethodButtonDrawer
    {
        private struct MethodEntry
        {
            public MethodInfo method;
            public MethodButtonAttribute attr;
            public ParameterInfo[] parameters;
        }

        private class ParamState
        {
            public object[] values;
            public bool foldout;
        }

        private const BindingFlags MethodFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        private static readonly Dictionary<Type, List<MethodEntry>> entryCache = new Dictionary<Type, List<MethodEntry>>();
        // Per-session parameter editing state: target id -> method ID -> ParamState
#if UNITY_6000_4_OR_NEWER
        private static readonly Dictionary<EntityId, Dictionary<string, ParamState>> paramStates = new Dictionary<EntityId, Dictionary<string, ParamState>>();
#else
        private static readonly Dictionary<int, Dictionary<string, ParamState>> paramStates = new Dictionary<int, Dictionary<string, ParamState>>();
#endif

        public static void DrawButtons(Editor editor)
        {
            var target = editor.target;
            if (target == null)
                return;

            var entries = GetEntries(target.GetType());
            if (entries.Count == 0)
                return;

            EditorGUILayout.Space();
            ToolboxEditorGui.DrawLine();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            foreach (var entry in entries)
            {
                var isEnabled = ShouldShow(entry.attr);
                using (new EditorGUI.DisabledScope(!isEnabled))
                {
                    var label = string.IsNullOrEmpty(entry.attr.label) ? Nicify(entry.method) : entry.attr.label;
                    if (!isEnabled)
                    {
                        if (entry.attr.showMode == MethodButtonAttribute.ShowMode.PlayModeOnly)
                            label += " (Play Mode Only)";
                        else if (entry.attr.showMode == MethodButtonAttribute.ShowMode.EditorOnly)
                            label += " (Editor Only)";
                    }

                    var needParams = entry.parameters.Length > 0;
                    var state = GetOrCreateParamState(target, entry);

                    if (needParams)
                    {
                        state.foldout = EditorGUILayout.Foldout(state.foldout, label, true);
                        if (state.foldout)
                        {
                            EditorGUI.indentLevel++;
                            DrawParametersUI(entry, state);
                            EditorGUI.indentLevel--;
                            EditorGUILayout.Space(4);
                        }

                        using (new EditorGUI.DisabledScope(!GUI.enabled))
                        {
                            if (GUILayout.Button("Invoke " + label))
                            {
                                if (entry.attr.confirm && !EditorUtility.DisplayDialog("Confirm",
                                        entry.attr.confirmMessage, "OK", "Cancel"))
                                    continue;

                                InvokeOnSelection(editor, entry, state.values);
                            }
                        }
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(!GUI.enabled))
                        {
                            if (GUILayout.Button(label))
                            {
                                if (entry.attr.confirm &&
                                    !EditorUtility.DisplayDialog("Confirm", entry.attr.confirmMessage, "OK", "Cancel"))
                                    continue;

                                InvokeOnSelection(editor, entry, null);
                            }
                        }
                    }
                }
            }
        }

        private static List<MethodEntry> GetEntries(Type type)
        {
            if (entryCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var entries = new List<MethodEntry>();
            foreach (var m in type.GetMethods(MethodFlags))
            {
                var attrs = m.GetCustomAttributes(typeof(MethodButtonAttribute), true) as MethodButtonAttribute[];
                if (attrs == null || attrs.Length == 0) continue;

                var parameters = m.GetParameters();
                if (parameters.Any(p => !IsSupportedParamType(p.ParameterType)))
                {
                    Debug.LogWarning($"[MethodButton] {type.Name}.{m.Name} has unsupported parameter types. Supported are: bool, string, char, integral types, float, double, decimal, and enums.");
                    continue;
                }

                foreach (var a in attrs)
                {
                    entries.Add(new MethodEntry
                    {
                        method = m,
                        attr = a,
                        parameters = parameters
                    });
                }
            }

            entries = entries.OrderBy(e => e.attr.order).ThenBy(e => e.method.Name).ToList();
            entryCache[type] = entries;
            return entries;
        }

        private static string Nicify(MethodInfo m)
        {
            var baseName = ObjectNames.NicifyVariableName(m.Name);
            return m.IsStatic ? $"{baseName} (static)" : baseName;
        }

        private static bool ShouldShow(MethodButtonAttribute a)
        {
            switch (a.showMode)
            {
                case MethodButtonAttribute.ShowMode.EditorOnly:
                    return !EditorApplication.isPlaying;
                case MethodButtonAttribute.ShowMode.PlayModeOnly:
                    return EditorApplication.isPlaying;
                default:
                    return true;
            }
        }

        private static void InvokeOnSelection(Editor editor, MethodEntry entry, object[] values)
        {
            var targets = editor.targets;
            if (targets != null && targets.Length > 1 && !entry.method.IsStatic)
            {
                Undo.IncrementCurrentGroup();
                var group = Undo.GetCurrentGroup();
                foreach (var o in targets)
                    InvokeSafely(o, entry.method, values);
                Undo.CollapseUndoOperations(group);
            }
            else
            {
                InvokeSafely(editor.target, entry.method, values);
            }
        }

        private static void InvokeSafely(UnityEngine.Object obj, MethodInfo method, object[] args)
        {
            try
            {
                var isStatic = method.IsStatic;
                var instance = isStatic ? null : obj;

                if (!isStatic && instance is Component c)
                    Undo.RecordObject(c, $"Invoke {method.Name}");
                else if (!isStatic && instance is ScriptableObject so)
                    Undo.RecordObject(so, $"Invoke {method.Name}");

                var callArgs = PrepareArgs(method, args);
                method.Invoke(instance, callArgs);

                if (!isStatic) EditorUtility.SetDirty(obj);

                if (!Application.isPlaying)
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                        UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }
            catch (TargetInvocationException tie)
            {
                Debug.LogError($"[MethodButton] Exception invoking {method.DeclaringType?.Name}.{method.Name}: {tie.InnerException}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MethodButton] Failed invoking {method.DeclaringType?.Name}.{method.Name}: {ex}");
            }
        }

        private static object[] PrepareArgs(MethodInfo method, object[] provided)
        {
            var ps = method.GetParameters();
            if (ps.Length == 0) return null;

            var arr = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                object value;

                if (provided != null && i < provided.Length && provided[i] != null)
                {
                    value = CoerceValue(p.ParameterType, provided[i]);
                }
                else
                {
                    value = p.HasDefaultValue ? p.DefaultValue : GetDefault(p.ParameterType);
                }

                arr[i] = value;
            }

            return arr;
        }

        private static bool IsUnityObjectType(Type t)
        {
            return typeof(UnityEngine.Object).IsAssignableFrom(t);
        }

        private static bool IsSupportedParamType(Type t)
        {
            if (IsUnityObjectType(t)) return true;
            if (t.IsEnum) return true;
            if (t == typeof(string)) return true;
            if (t == typeof(bool)) return true;
            if (t == typeof(char)) return true;

            switch (Type.GetTypeCode(t))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }

        private static object GetDefault(Type t)
        {
            if (t.IsValueType) return Activator.CreateInstance(t);
            return null;
        }

        private static ParamState GetOrCreateParamState(UnityEngine.Object tgt, MethodEntry entry)
        {
#if UNITY_6000_4_OR_NEWER
            var id = tgt.GetEntityId();
#else
            var id = tgt.GetInstanceID();
#endif
            if (!paramStates.TryGetValue(id, out var map))
            {
                map = new Dictionary<string, ParamState>();
                paramStates[id] = map;
            }

            var key = MethodKey(entry.method);
            if (!map.TryGetValue(key, out var state))
            {
                state = new ParamState
                {
                    values = new object[entry.parameters.Length],
                    foldout = entry.parameters.Length > 0
                };

                for (int i = 0; i < entry.parameters.Length; i++)
                {
                    var p = entry.parameters[i];
                    state.values[i] = p.HasDefaultValue ? p.DefaultValue : GetDefault(p.ParameterType);
                }

                map[key] = state;
            }

            return state;
        }

        private static string MethodKey(MethodInfo m)
        {
            var ps = m.GetParameters();
            var sig = string.Join(",", ps.Select(p => p.ParameterType.FullName));
            return $"{m.DeclaringType.FullName}.{m.Name}({sig})";
        }

        private static void DrawParametersUI(MethodEntry entry, ParamState state)
        {
            var ps = entry.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                var label = $"{ObjectNames.NicifyVariableName(p.Name)} ({FriendlyTypeName(p.ParameterType)})";
                state.values[i] = DrawFieldForType(label, p.ParameterType, state.values[i]);
            }
        }

        private static string FriendlyTypeName(Type t)
        {
            if (IsUnityObjectType(t)) return t.Name;
            if (t.IsEnum) return t.Name;
            if (t == typeof(string)) return "string";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(char)) return "char";
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.SByte: return "sbyte";
                case TypeCode.Byte: return "byte";
                case TypeCode.Int16: return "short";
                case TypeCode.UInt16: return "ushort";
                case TypeCode.Int32: return "int";
                case TypeCode.UInt32: return "uint";
                case TypeCode.Int64: return "long";
                case TypeCode.UInt64: return "ulong";
                case TypeCode.Single: return "float";
                case TypeCode.Double: return "double";
                case TypeCode.Decimal: return "decimal";
                default: return t.Name;
            }
        }

        private static object DrawFieldForType(string label, Type t, object current)
        {
            if (IsUnityObjectType(t))
            {
                var obj = current as UnityEngine.Object;
                return EditorGUILayout.ObjectField(label, obj, t, true);
            }

            if (t.IsEnum)
            {
                var enumVal = current is Enum e ? e : (Enum)Enum.ToObject(t, 0);
                return EditorGUILayout.EnumPopup(label, enumVal);
            }

            if (t == typeof(string))
            {
                return EditorGUILayout.TextField(label, current as string ?? string.Empty);
            }

            if (t == typeof(bool))
            {
                return EditorGUILayout.Toggle(label, current is bool b && b);
            }

            if (t == typeof(char))
            {
                var str = current is char ch && ch != '\0' ? ch.ToString() : string.Empty;
                var next = EditorGUILayout.TextField(label, str);
                return string.IsNullOrEmpty(next) ? (char)0 : next[0];
            }

            switch (Type.GetTypeCode(t))
            {
                case TypeCode.SByte:
                {
                    sbyte v = current is sbyte s ? s : default;
                    int temp = EditorGUILayout.IntField(label, v);
                    temp = Mathf.Clamp(temp, sbyte.MinValue, sbyte.MaxValue);
                    return (sbyte)temp;
                }
                case TypeCode.Byte:
                {
                    byte v = current is byte b ? b : default;
                    int temp = EditorGUILayout.IntField(label, v);
                    temp = Mathf.Clamp(temp, byte.MinValue, byte.MaxValue);
                    return (byte)temp;
                }
                case TypeCode.Int16:
                {
                    short v = current is short s ? s : default;
                    int temp = EditorGUILayout.IntField(label, v);
                    temp = Mathf.Clamp(temp, short.MinValue, short.MaxValue);
                    return (short)temp;
                }
                case TypeCode.UInt16:
                {
                    ushort v = current is ushort u ? u : default;
                    int temp = EditorGUILayout.IntField(label, v);
                    temp = Mathf.Clamp(temp, ushort.MinValue, ushort.MaxValue);
                    return (ushort)Mathf.Max(temp, 0);
                }
                case TypeCode.Int32:
                {
                    int v = current is int i32 ? i32 : default;
                    return EditorGUILayout.IntField(label, v);
                }
                case TypeCode.UInt32:
                {
                    uint v = current is uint u32 ? u32 : default;
                    long temp = EditorGUILayout.LongField(label, v);
                    temp = Math.Max(0, temp);
                    temp = Math.Min(temp, uint.MaxValue);
                    return (uint)temp;
                }
                case TypeCode.Int64:
                {
                    long v = current is long i64 ? i64 : default;
                    return EditorGUILayout.LongField(label, v);
                }
                case TypeCode.UInt64:
                {
                    ulong v = current is ulong u64 ? u64 : default;
                    var text = EditorGUILayout.TextField(label, v.ToString(CultureInfo.InvariantCulture));
                    if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
                    return v;
                }
                case TypeCode.Single:
                {
                    float v = current is float f ? f : default;
                    return EditorGUILayout.FloatField(label, v);
                }
                case TypeCode.Double:
                {
                    double v = current is double d ? d : default;
                    return EditorGUILayout.DoubleField(label, v);
                }
                case TypeCode.Decimal:
                {
                    decimal v = current is decimal dec ? dec : default;
                    var text = EditorGUILayout.TextField(label, v.ToString(CultureInfo.InvariantCulture));
                    if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return parsed;
                    return v;
                }
                default:
                    EditorGUILayout.LabelField(label, "(unsupported)");
                    return current;
            }
        }

        private static object CoerceValue(Type targetType, object val)
        {
            if (IsUnityObjectType(targetType))
            {
                if (val == null) return null;
                return targetType.IsInstanceOfType(val) ? val : null;
            }

            if (targetType.IsEnum)
            {
                if (val is Enum) return val;
                try
                {
                    if (val is string s) return Enum.Parse(targetType, s, true);
                    return Enum.ToObject(targetType, Convert.ToInt32(val, CultureInfo.InvariantCulture));
                }
                catch { return GetDefault(targetType); }
            }

            if (targetType == typeof(string))
            {
                return val?.ToString() ?? string.Empty;
            }

            if (targetType == typeof(char))
            {
                if (val is char c) return c;
                var s = val.ToString();
                return string.IsNullOrEmpty(s) ? (char)0 : s[0];
            }

            try
            {
                if (targetType == typeof(decimal))
                {
                    if (val is decimal de) return de;
                    if (val is string ds && decimal.TryParse(ds, NumberStyles.Float, CultureInfo.InvariantCulture, out var dsv)) return dsv;
                    return Convert.ToDecimal(val, CultureInfo.InvariantCulture);
                }

                return Convert.ChangeType(val, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                return GetDefault(targetType);
            }
        }
    }
}
