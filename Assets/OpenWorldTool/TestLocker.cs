using OpenWorldTool.Grid;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenWorldTool
{
    public class TestLocker : MonoBehaviour
    {

        public Transform toLock;
        bool _locked;

        void OnGUI()
        {
            if (!_locked)
            {
                if (!GUILayout.Button("lock")) return;
                FindObjectOfType<Grid.GridPatchHandler>().LoadAndLockPatch(toLock.position, OnLoaded);
                _locked = true;
            }
            else
            {
                if (!GUILayout.Button("Unlock")) return;
                FindObjectOfType<Grid.GridPatchHandler>().UnlockPatch(toLock.position);
                _locked = false;
            }
        }

        void OnLoaded()
        {
            Debug.Log("loaded");
        }
	
    }
}
