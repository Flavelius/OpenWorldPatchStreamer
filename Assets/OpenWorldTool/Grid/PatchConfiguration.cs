using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenWorldTool.Grid
{
    public class PatchConfiguration: ScriptableObject
    {
        [SerializeField, ReadOnlyInInspector]
        public int PatchSize = 100;

        [SerializeField, ReadOnlyInInspector, Tooltip("Dont manually delete scenes from this folder!")]
        public string Folder = "";

        [Header("Unique Identifier")]
        public string PatchIdentifier = "LevelName";

        Dictionary<int, Dictionary<int, string>> _cachedPatchNames = new Dictionary<int, Dictionary<int, string>>();

        string GetCachedName(int x, int y)
        {
            Dictionary<int, string> dict1;
            if (!_cachedPatchNames.TryGetValue(x, out dict1))
            {
                dict1 = new Dictionary<int, string>();
                _cachedPatchNames.Add(x, dict1);
            }
            if (!dict1.ContainsKey(y))
            {
                dict1.Add(y, string.Format("{0}_{1}_{2}", PatchIdentifier, x, y));
            }
            return dict1[y];
        } 

        public string FormatPatchName(int x, int y)
        {
            return Application.isPlaying ? GetCachedName(x, y) : string.Format("{0}_{1}_{2}", PatchIdentifier, x, y);
        }

        readonly Dictionary<string, string> _cachedPathFolderNames = new Dictionary<string, string>(); 
        public string FormatPatchNameWithFolder(int x, int y)
        {
            if (!Application.isPlaying) return Path.Combine(Folder, FormatPatchName(x, y));
            var pName = FormatPatchName(x, y);
            string outVal;
            if (_cachedPathFolderNames.TryGetValue(pName, out outVal)) return outVal;
            outVal = Path.Combine(Folder, pName);
            _cachedPathFolderNames.Add(pName, outVal);
            return outVal;
        }

        public string FormatAbsoluteAssetPath(int x, int y)
        {
            return Path.Combine(Application.dataPath, FormatPatchNameWithFolder(x, y) + ".unity");
        }

        public string FormatLocalAssetPath(int x, int y)
        {
            return Path.Combine("Assets/", FormatPatchNameWithFolder(x, y)) + ".unity";
        }

        public bool SceneExists(int x, int y)
        {
            return File.Exists(FormatAbsoluteAssetPath(x, y));
        }

        public bool CanPatchBeLoaded(int x, int y)
        {
            var path = FormatPatchNameWithFolder(x, y);
            if (Application.isPlaying)
            {
                return Application.CanStreamedLevelBeLoaded(path);
            }
#if UNITY_EDITOR
            path = FormatLocalAssetPath(x, y);
            for (var i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                if (EditorBuildSettings.scenes[i].path == path) return true;
            }
#endif
            return false;
        }

        [ContextMenu("Add Scenes To Build settings")]
        public void AddScenesToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var patchScene in GetAssociatedPatchScenes())
            {
                var fileName = Path.GetFileName(patchScene);
                if (!IsSceneAddedToBuildSettings(patchScene) && !string.IsNullOrEmpty(fileName) && fileName.StartsWith(PatchIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    scenes.Add(new EditorBuildSettingsScene(patchScene, true));
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        string GetAbsoluteFolderPath()
        {
            var path = AssetDatabase.GetAssetPath(this);
            path = Application.dataPath.Replace("/Assets", string.Empty) + "/" + path;
            return path.Replace(Path.GetFileName(path), string.Empty);
        }

        List<string> GetAssociatedPatchScenes()
        {
            var path = GetAbsoluteFolderPath();
            var files = Directory.GetFiles(path);
            var scenes = new List<string>();
            for (var i = 0; i < files.Length; i++)
            {
                var filePath = files[i].Replace(Application.dataPath + "/", "Assets/");
                var file = AssetDatabase.LoadAssetAtPath<SceneAsset>(filePath);
                if (file != null) scenes.Add(filePath);
            }
            return scenes;
        }

        public bool IsSceneAddedToBuildSettings(string assetPath)
        {
            for (var i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                if (EditorBuildSettings.scenes[i].path == assetPath) return true;
            }
            return false;
        }
    }
}
