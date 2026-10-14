using UnityEngine;

/// <summary>
/// Routes authored audio through the persistent full-game conductor when it
/// exists, while keeping every independently backed-up stage fully audible.
/// </summary>
public static class RuntimeAudioRouter
{
    static RuntimeAudioConductor localConductor;

    public static RuntimeAudioConductor Conductor
    {
        get
        {
            if (PersistentPuzzleBootstrap.Instance &&
                PersistentPuzzleBootstrap.Instance.audioConductor)
                return PersistentPuzzleBootstrap.Instance.audioConductor;
            if (!localConductor)
                localConductor = Object.FindFirstObjectByType<RuntimeAudioConductor>(
                    FindObjectsInactive.Include);
            return localConductor;
        }
    }

    public static void PlayCue(RuntimeAudioCue cue, Vector3 position)
    {
        Conductor?.PlayCue(cue, position);
    }
}
