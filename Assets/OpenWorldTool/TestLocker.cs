using System.Collections;
using OpenWorldTool.Scripts;
using UnityEngine;

namespace OpenWorldTool
{
    public class TestLocker : MonoBehaviour
    {
        bool _locked;

        void OnGUI()
        {
            if (!_locked)
            {
                if (!GUILayout.Button("lock")) return;
                StartCoroutine(LoadWhen());
                _locked = true;
            }
            else
            {
                if (!GUILayout.Button("Unlock")) return;
                StartCoroutine(UnloadWhen());
                _locked = false;
            }
        }

        IEnumerator LoadWhen()
        {
            Debug.Log("lock started: " + Time.time);
            yield return FindObjectOfType<GridPatchHandler>().LoadAndLockPatchAsync(transform.position);
            Debug.Log("lock finished: " + Time.time);
        }

        IEnumerator UnloadWhen()
        {
            Debug.Log("unload started: " + Time.time);
            yield return FindObjectOfType<GridPatchHandler>().StopAndUnloadPatchesAsync();
            Debug.Log("unload finished: " + Time.time);
        }
	
    }
}
