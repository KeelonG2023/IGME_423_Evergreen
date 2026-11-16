using System.Collections.Generic;
using Evergreen.UI;
using UnityEditor;
using UnityEngine;

namespace Evergreen.EditorTools
{
    /// <summary>
    /// Tools > Evergreen > Build Skill Tree: rebuilds the SkillTree prefab's "Research UI" from its Nodes list,
    /// using the HUD's build card as the detail card template.
    /// Also runs once automatically after scripts reload if the Nodes list and the built cards don't match.
    /// </summary>
    [InitializeOnLoad]
    static class SkillTreeBuildTool
    {
        const string TreePath = "Assets/Prefabs/SkillTree.prefab";
        const string HudPath = "Assets/Prefabs/HUD.prefab";

        static SkillTreeBuildTool()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (IsStale()) { Debug.Log("SkillTreeBuildTool: Nodes list changed, rebuilding the skill tree."); Build(); }
            };
        }

        static bool IsStale()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePath);
            var view = prefab != null ? prefab.GetComponentInChildren<SkillTreeView>(true) : null;
            if (view == null) return false;
            var so = new SerializedObject(view);
            var wanted = new HashSet<string>();
            SerializedProperty nodes = so.FindProperty("nodes");
            for (int i = 0; i < nodes.arraySize; i++)
            {
                SerializedProperty n = nodes.GetArrayElementAtIndex(i);
                bool hasContent = n.FindPropertyRelative("building").objectReferenceValue != null
                               || n.FindPropertyRelative("upgrade").objectReferenceValue != null;
                if (hasContent) wanted.Add(n.FindPropertyRelative("id").stringValue);
            }
            var built = new HashSet<string>();
            SerializedProperty uis = so.FindProperty("nodeUIs");
            for (int i = 0; i < uis.arraySize; i++) built.Add(uis.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue);
            return !wanted.SetEquals(built);
        }

        [MenuItem("Tools/Evergreen/Build Skill Tree")]
        public static void Build()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            BuildMenu menu = hud != null ? hud.GetComponentInChildren<BuildMenu>(true) : null;
            if (menu == null || menu.CardTemplate == null)
            {
                Debug.LogError("SkillTreeBuildTool: couldn't find the BuildMenu card template in " + HudPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(TreePath);
            try
            {
                SkillTreeView view = root.GetComponentInChildren<SkillTreeView>(true);
                if (view == null) { Debug.LogError("SkillTreeBuildTool: no SkillTreeView in " + TreePath); return; }
                view.EditorBuild(menu.CardTemplate, menu.Icons);
                PrefabUtility.SaveAsPrefabAsset(root, TreePath);
                Debug.Log("SkillTreeBuildTool: skill tree rebuilt.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
