using OpenWorldTool.Scripts;
using UnityEditor;

// ReSharper disable once CheckNamespace
namespace OpenWorldTool
{
    public class PatchSettingsUpdater: AssetPostprocessor
    {

        // ReSharper disable once UnusedMember.Local
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            RefreshPathIfPatchSetting(importedAssets);
            RefreshPathIfPatchSetting(movedAssets);
            RefreshPathIfPatchSetting(movedFromAssetPaths); 
        }

        static void RefreshPathIfPatchSetting(string[] fileList)
        {
            foreach (var a in fileList)
            {
                if (!a.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase)) continue;
                var ps = AssetDatabase.LoadAssetAtPath<PatchConfiguration>(a);
                if (ps == null) continue;
                ps.Folder = a.Replace(System.IO.Path.GetFileName(a), string.Empty).Replace("Assets/", string.Empty);
                EditorUtility.SetDirty(ps);
            }
        }

    }
}
