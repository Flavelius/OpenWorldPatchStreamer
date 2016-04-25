using OpenWorldTool.Grid;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace OpenWorldTool
{
    [CustomEditor(typeof (GridPatchHandler))]
    public class GridPatchHandlerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var wg = target as GridPatchHandler;
            EditorGUILayout.HelpBox("Patches are loaded around this transform, based on the ViewDistance setting of the configuration.\n\nEditor controls:\n\n[Shift] to load or unload patches,\n[Ctrl] to add or delete them.\n[Ctrl+Shift] to select a patch with mouseover.", MessageType.None);
            EditorGUI.BeginChangeCheck();
            if (wg.PatchConfiguration == null)
            {
                EditorGUILayout.BeginHorizontal();
                wg.PatchConfiguration = EditorGUILayout.ObjectField("Existing:", wg.PatchConfiguration, typeof (PatchConfiguration), false) as PatchConfiguration;
                if (GUILayout.Button("New", EditorStyles.miniButton, GUILayout.Width(36)))
                {
                    var ps = CreateInstance<PatchConfiguration>();
                    var path = EditorUtility.SaveFilePanelInProject("Save", "PatchConfiguration", "asset", "");
                    if (string.IsNullOrEmpty(path))
                    {
                        return;
                    }
                    AssetDatabase.CreateAsset(ps, path);
                    Selection.activeObject = ps;
                    wg.PatchConfiguration = ps;
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                var delete = false;
                EditorGUILayout.BeginHorizontal();
                wg.PatchConfiguration = EditorGUILayout.ObjectField("Patch Configuration:", wg.PatchConfiguration, typeof (PatchConfiguration), false) as PatchConfiguration;
                if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(36)))
                {
                    delete = true;
                }
                EditorGUILayout.EndHorizontal();
                wg.PatchVisibilityExtend = EditorGUILayout.IntField("Visibility Extend:", wg.PatchVisibilityExtend);
                if (wg.PatchVisibilityExtend < 1) wg.PatchVisibilityExtend = 1;
                if (delete)
                {
                    Undo.RecordObject(wg, "GridPatchHandler");
                    wg.StopHandler();
                    wg.PatchConfiguration = null;
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                if (wg.PatchConfiguration != null)
                {
                    EditorUtility.SetDirty(wg.PatchConfiguration);
                }
                SceneView.RepaintAll();
            }
        }
    }
}