using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenWorldTool.Grid
{
    [ExecuteInEditMode]
    public class GridPatchHandler : MonoBehaviour
    {
        public PatchConfiguration PatchConfiguration;

        public int PatchVisibilityExtend = 2;

        readonly Queue<PatchAction> _queuedActions = new Queue<PatchAction>();

        readonly HashSet<string> _lockedPatches = new HashSet<string>();

        Coroutine _updateRoutine;

        bool QueueContains(string patchName, PatchAction.PatchActionType type)
        {
            foreach (var action in _queuedActions)
            {
                if (action.Patch == patchName && action.ActionType == type) return true;
            }
            return false;
        }

        public class PatchAction
        {
            public enum PatchActionType
            {
                Load,
                Unload
            }

            public readonly string Patch;
            public readonly PatchActionType ActionType;
            readonly Action _onReadyCallback;

            PatchAction(string patch, PatchActionType actionType, Action onReadyCallback)
            {
                Patch = patch;
                ActionType = actionType;
                _onReadyCallback = onReadyCallback;
            }

            public void OnReady(Scene s)
            {
                if (_onReadyCallback != null) _onReadyCallback();
            }

            public static PatchAction Load(string patch, Action onReady)
            {
                return new PatchAction(patch, PatchActionType.Load, onReady);
            }

            public static PatchAction Unload(string patch, Action onReady)
            {
                return new PatchAction(patch, PatchActionType.Unload, onReady);
            }
        }

        /// <summary>
        /// target patch will be loaded and locked until unlocked and next regular unload cycle
        /// </summary>
        /// <param name="point">load patch that contains this point</param>
        /// <param name="OnFinished">will be called when the patch is loaded</param>
        public void LoadAndLockPatch(Vector3 point, Action OnFinished = null)
        {
            if (PatchConfiguration == null) throw new NullReferenceException("Patch Configuration null");
            var x = RoundToInt(point.x/PatchConfiguration.PatchSize);
            var y = RoundToInt(point.z/PatchConfiguration.PatchSize);
            var pName = PatchConfiguration.FormatPatchName(x, y);
            if (IsPatchLoaded(pName))
            {
                if (OnFinished != null) OnFinished();
                return;
            }
            LockPatch(pName);
            LoadPatch(x, y, true, OnFinished);
        }

        /// <summary>
        /// Remove the lock so the patch the point is contained in can be unloaded during the next regular cycle
        /// </summary>
        /// <param name="point"></param>
        public void UnlockPatch(Vector3 point)
        {
            if (PatchConfiguration == null) throw new NullReferenceException("Patch Configuration null");
            var x = RoundToInt(point.x / PatchConfiguration.PatchSize);
            var y = RoundToInt(point.z / PatchConfiguration.PatchSize);
            var pName = PatchConfiguration.FormatPatchName(x, y);
            UnlockPatch(pName);
        }

        bool IsPatchLoaded(string patchName)
        {
            var s = SceneManager.GetSceneByName(patchName);
            return s.IsValid() && s.isLoaded;
        }

        void LockPatch(string patchName)
        {
            _lockedPatches.Add(patchName);
        }

        void UnlockPatch(string patchName)
        {
            _lockedPatches.Remove(patchName);
        }

        bool IsPatchLocked(string patchName)
        {
            return _lockedPatches.Contains(patchName);
        }

        /// <summary>
        /// Unloads all patches and stops the range/visibility calculations
        /// </summary>
        public void StopHandler()
        {
            if (_updateRoutine != null)
            {
                StopCoroutine(_updateRoutine);
            }
            UnloadAllPatches();
        }

        void LoadPatch(int x, int y, bool skipUnlock, Action onFinishedCallback = null)
        {
            var patchName = PatchConfiguration.FormatPatchName(x, y);
            if (!PatchConfiguration.CanPatchBeLoaded(x,y)) return;
            if (Application.isPlaying)
            {
                var scene = SceneManager.GetSceneByName(patchName);
                if (!skipUnlock)
                {
                    UnlockPatch(patchName);
                }
                if (scene.IsValid() && scene.isLoaded) return;
                if (!QueueContains(patchName, PatchAction.PatchActionType.Load))
                {
                    _queuedActions.Enqueue(PatchAction.Load(patchName, onFinishedCallback));
                }
            }
            else
            {
#if UNITY_EDITOR
                EditorSceneManager.OpenScene(PatchConfiguration.FormatLocalAssetPath(x, y), OpenSceneMode.Additive);
#endif
            }
        }

        void UnloadPatch(string patchName, Action callback = null)
        {
            if (!patchName.StartsWith(PatchConfiguration.PatchIdentifier, StringComparison.OrdinalIgnoreCase)) return;
            var s = SceneManager.GetSceneByName(patchName);
            if (Application.isPlaying)
            {
                if (s.IsValid() && s.isLoaded)
                {
                    if (!IsPatchLocked(patchName) && !QueueContains(patchName, PatchAction.PatchActionType.Unload))
                    {
                        _queuedActions.Enqueue(PatchAction.Unload(patchName, callback));
                    }
                }
            }
            else
            {
#if UNITY_EDITOR
                if (s.IsValid() && s.isLoaded)
                {
                    EditorSceneManager.CloseScene(s, true);
                }
#endif
            }
        }

        void Start()
        {
            if (Application.isPlaying)
            {
                UnloadAllPatches();
            }
        }

        void Update()
        {
            if (Application.isPlaying)
            {
                UpdatePatchVisibility();
            }
        }

        IEnumerator ExecuteQueuedActions()
        {
            while (true)
            {
                yield return null;
                if (_queuedActions.Count <= 0) continue;
                var a = _queuedActions.Dequeue();
                var scene = SceneManager.GetSceneByName(a.Patch);
                if (a.ActionType == PatchAction.PatchActionType.Load)
                {
                    if (scene.IsValid() && scene.isLoaded) continue;
                    var loadOperation = SceneManager.LoadSceneAsync(a.Patch, LoadSceneMode.Additive);
                    loadOperation.allowSceneActivation = true;
                    while (!loadOperation.isDone)
                    {
                        yield return null;
                    }
                    a.OnReady(SceneManager.GetSceneByPath(a.Patch));
                    continue;
                }
                if (a.ActionType != PatchAction.PatchActionType.Unload) continue;
                if (!scene.IsValid() || !scene.isLoaded) continue;
                SceneManager.UnloadScene(scene.buildIndex);
                a.OnReady(scene);
            }
            // ReSharper disable once FunctionNeverReturns
        }

        int RoundToInt(float point)
        {
            return Mathf.RoundToInt(point);
        }

        readonly List<string> newPatches = new List<string>(9);
        void UpdatePatchVisibility()
        {
            if (PatchConfiguration == null)
            {
                return;
            }
            var widthExtend = Mathf.Max(1, PatchVisibilityExtend);
            var xPoint = RoundToInt(transform.position.x/PatchConfiguration.PatchSize);
            var zPoint = RoundToInt(transform.position.z/PatchConfiguration.PatchSize);
            newPatches.Clear();
            for (var x = xPoint - widthExtend; x <= xPoint + widthExtend; x++)
            {
                for (var z = zPoint - widthExtend; z <= zPoint + widthExtend; z++)
                {
                    newPatches.Add(PatchConfiguration.FormatPatchName(x, z));
                    LoadPatch(x, z, false);
                }
            }
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                var path = Path.GetFileName(scene.name);
                if (!newPatches.Contains(path))
                {   
                    UnloadPatch(path);
                }
            }
        }

        [ContextMenu("Unload All Patches")]
        void UnloadAllPatches()
        {
            for (var i = SceneManager.sceneCount;i-->0;)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name.StartsWith(PatchConfiguration.PatchIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    if (Application.isPlaying)
                    {
                        SceneManager.UnloadScene(scene.name);
                    }
                    else
                    {
#if UNITY_EDITOR
                        EditorSceneManager.CloseScene(scene, true);
#endif
                    }
                }
            }
        }

#if UNITY_EDITOR
        void Awake()
        {
            if (!Application.isPlaying)
            {
                SceneView.onSceneGUIDelegate += DrawSceneGUI;
            }
        }

        void OnEnable()
        {
            if (Application.isPlaying)
            {
                if (_updateRoutine == null)
                {
                    _updateRoutine = StartCoroutine(ExecuteQueuedActions());
                }
                return;
            }
            SceneView.onSceneGUIDelegate -= DrawSceneGUI;
            SceneView.onSceneGUIDelegate += DrawSceneGUI;
        }

        void OnDisable()
        {
            if (Application.isPlaying && _updateRoutine != null)
            {
                StopCoroutine(_updateRoutine);
                _updateRoutine = null;
            }
        }

        void OnDestroy()
        {
            if (Application.isPlaying) return;
            SceneView.onSceneGUIDelegate -= DrawSceneGUI;
            Tools.hidden = false;
        }

#region SceneGUI

        void OnDrawGizmos()
        {
            if (PatchConfiguration != null)
            {
                var size = PatchVisibilityExtend*PatchConfiguration.PatchSize*2;
                Gizmos.DrawWireCube(transform.position, new Vector3(size, 0, size));
            }
            Handles.color = Color.white;
        }

        public enum PatchToolMode
        {
            None,
            AddDelete,
            LoadUnload,
            SelectLoaded
        }

        PatchToolMode _toolMode;
        Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);
        readonly Color _existingColor = new Color(1, 1, 1, 0.4f);
        readonly Color _missingColor = new Color(1, 0, 0, 0.05f);
        readonly Color _activeColor = new Color(0.5f, 1, 0, 0.75f);

        void DrawSceneGUI(SceneView view)
        {
            if (PatchConfiguration == null)
            {
                return;
            }
            _toolMode = PatchToolMode.None;
            var ev = Event.current;
            var mouseInScreen = new Rect(0, 0, Screen.width, Screen.height).Contains(ev.mousePosition);
            if (mouseInScreen)
            {
                if (ev.shift) _toolMode = PatchToolMode.LoadUnload;
                if (ev.control) _toolMode = PatchToolMode.AddDelete;
                if (ev.control && ev.shift) _toolMode = PatchToolMode.SelectLoaded;
            }
            Tools.hidden = _toolMode != PatchToolMode.None;
            if (_toolMode == PatchToolMode.SelectLoaded)
            {
                if (mouseInScreen)
                {
                    var r = HandleUtility.GUIPointToWorldRay(ev.mousePosition);
                    float hitDist;
                    if (_groundPlane.Raycast(r, out hitDist))
                    {
                        var pos = r.GetPoint(hitDist);
                        var x = RoundToInt(pos.x/PatchConfiguration.PatchSize);
                        var z = RoundToInt(pos.z/PatchConfiguration.PatchSize);
                        var patchName = PatchConfiguration.FormatPatchName(x, z);
                        var selectedPatch = SceneManager.GetSceneByName(patchName);
                        if (selectedPatch.IsValid())
                        {
                            SceneManager.SetActiveScene(selectedPatch);
                            _toolMode = PatchToolMode.None;
                        }
                    }
                }
            }
            var activeScene = SceneManager.GetActiveScene();
            var patchSize = PatchConfiguration.PatchSize;
            var widthExtend = Mathf.Max(1, PatchVisibilityExtend);
            var xPoint = RoundToInt(transform.position.x/patchSize);
            var zPoint = RoundToInt(transform.position.z/patchSize);
            for (var x = xPoint - widthExtend; x <= xPoint + widthExtend; x++)
            {
                for (var z = zPoint - widthExtend; z <= zPoint + widthExtend; z++)
                {
                    var pos = new Vector3(x*patchSize, 0, z*patchSize);
                    var centerPos = HandleUtility.WorldToGUIPoint(pos);
                    Vector3[] verts =
                    {
                        new Vector3(pos.x - patchSize*0.5f, pos.y, pos.z - patchSize*0.5f),
                        new Vector3(pos.x + patchSize*0.5f, pos.y, pos.z - patchSize*0.5f),
                        new Vector3(pos.x + patchSize*0.5f, pos.y, pos.z + patchSize*0.5f),
                        new Vector3(pos.x - patchSize*0.5f, pos.y, pos.z + patchSize*0.5f)
                    };
                    var patchName = PatchConfiguration.FormatPatchName(x, z);
                    var patchExists = PatchConfiguration.SceneExists(x, z);
                    if (patchName == activeScene.name)
                    {
                        Handles.DrawSolidRectangleWithOutline(verts, Color.clear, _activeColor);
                    }
                    else
                    {
                        if (patchExists)
                        {
                            Handles.DrawSolidRectangleWithOutline(verts, Color.clear, _existingColor);
                        }
                        else
                        {
                            Handles.DrawSolidRectangleWithOutline(verts, _missingColor, _missingColor);
                        }
                    }
                    if (_toolMode == PatchToolMode.AddDelete)
                    {
                        DrawAddRemoveHandles(centerPos, x, z, patchExists);
                    }
                    if (_toolMode == PatchToolMode.LoadUnload)
                    {
                        DrawLoadUnloadHandles(centerPos, x, z, patchExists);
                    }
                }
            }
        }

        void DrawAddRemoveHandles(Vector3 centerPos, int x, int z, bool exists)
        {
            var r = new Rect(new Vector2(centerPos.x - 8, centerPos.y - 9), new Vector2(17, 17));
            if (exists)
            {
                Handles.BeginGUI();
                if (GUI.Button(r, "-", EditorStyles.miniButton))
                {
                    RemovePatch(x, z, PatchConfiguration);
                }
                Handles.EndGUI();
            }
            else
            {
                Handles.BeginGUI();
                if (GUI.Button(r, "+", EditorStyles.miniButton))
                {
                    CreateNewPatch(x, z, PatchConfiguration);
                }
                Handles.EndGUI();
            }
        }

        void DrawLoadUnloadHandles(Vector3 centerPos, int x, int z, bool exists)
        {
            if (!exists) return;
            var scene = SceneManager.GetSceneByName(PatchConfiguration.FormatPatchName(x, z));
            if (scene.IsValid() && scene.isLoaded)
            {
                var r = new Rect(new Vector2(centerPos.x - 22, centerPos.y - 9), new Vector2(48, 17));
                Handles.BeginGUI();
                if (GUI.Button(r, "Unload", EditorStyles.miniButton))
                {
                    UnloadPatch(scene.name);
                }
                Handles.EndGUI();
            }
            else
            {
                var r = new Rect(new Vector2(centerPos.x - 18, centerPos.y - 9), new Vector2(40, 17));
                Handles.BeginGUI();
                if (GUI.Button(r, "Load", EditorStyles.miniButton))
                {
                    LoadPatch(x, z, false);
                }
                Handles.EndGUI();
            }
        }

        void CreateNewPatch(int x, int z, PatchConfiguration ps)
        {
            if (!EditorUtility.DisplayDialog("", "Create Patch?", "Create", "Cancel")) return;
            var activeScene = SceneManager.GetActiveScene();
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(s, ps.FormatLocalAssetPath(x, z));
            //add to build settings
            if (!ps.IsSceneAddedToBuildSettings(s.path))
            {
                EditorBuildSettings.scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
                { new EditorBuildSettingsScene(s.path, true) }.ToArray();
            }
            //end add to build settings
            EditorUtility.SetDirty(ps);
            if (activeScene.IsValid())
            {
                SceneManager.SetActiveScene(activeScene);
            }
        }

        void RemovePatch(int x, int z, PatchConfiguration ps)
        {
            if (!EditorUtility.DisplayDialog("Remove Patch?", string.Format("The patch ({0}) will be deleted!", ps.FormatPatchName(x,z)), "Remove", "Cancel")) return;
            var s = SceneManager.GetSceneByPath(ps.FormatLocalAssetPath(x, z));
            if (s.IsValid())
            {
                EditorSceneManager.CloseScene(s, true);
            }
            //remove from build settings
            var path = ps.FormatLocalAssetPath(x, z);
            var settingsScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (var i = 0; i < settingsScenes.Count; i++)
            {
                if (!settingsScenes[i].path.Equals(path)) continue;
                settingsScenes.RemoveAt(i);
                break;
            }
            EditorBuildSettings.scenes = settingsScenes.ToArray();
            //end remove from build settings
            EditorUtility.SetDirty(ps);
            AssetDatabase.DeleteAsset(path);
        }

#endregion

#endif
    }
}