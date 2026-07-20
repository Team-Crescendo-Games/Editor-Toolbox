using System.Collections.Generic;
using System.Linq;

using UnityEditor;
using UnityEditor.Build;

namespace Toolbox.Editor
{
    public static class ScriptingUtility
    {
        public static List<string> GetDefines()
        {
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            var defines = PlayerSettings.GetScriptingDefineSymbols(target);
            return defines.Split(';').ToList();
        }

        public static void SetDefines(List<string> definesList)
        {
            var defines = string.Join(";", definesList.ToArray());
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.SetScriptingDefineSymbols(target, defines);
        }

        public static void AppendDefine(string define)
        {
            var definesList = GetDefines();
            if (definesList.Contains(define))
            {
                return;
            }

            definesList.Add(define);
            SetDefines(definesList);
        }

        public static void RemoveDefine(string define)
        {
            var definesList = GetDefines();
            if (definesList.RemoveAll(s => s == define) == 0)
            {
                return;
            }

            SetDefines(definesList);
        }
    }
}