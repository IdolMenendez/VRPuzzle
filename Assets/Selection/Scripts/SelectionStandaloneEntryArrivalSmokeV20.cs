using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Confirms that direct Selection-scene play really reaches a V20 puzzle
/// scene, rather than only reporting a successful click.
/// </summary>
[DisallowMultipleComponent]
public sealed class SelectionStandaloneEntryArrivalSmokeV20 : MonoBehaviour
{
    public const string EnvironmentVariable =
        "PPVR_V20_STANDALONE_ENTRY_SMOKE";

    IEnumerator Start()
    {
#if UNITY_EDITOR
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
            yield break;
        Environment.SetEnvironmentVariable(EnvironmentVariable, null);
        yield return null;
        Debug.Log(
            "[Reliable Exhibit Selection V20] STANDALONE ENTRY ARRIVAL PASSED · " +
            gameObject.scene.name);
        if (Application.isBatchMode)
            UnityEditor.EditorApplication.Exit(0);
        else
            UnityEditor.EditorApplication.isPlaying = false;
#else
        yield break;
#endif
    }
}
