using UnityEngine;

/// <summary>
/// Routes the V08 completion bloom and haptic accent without modifying the
/// shared puzzle runtime.
/// </summary>
public sealed class Stage5V08CompletionAudio : MonoBehaviour
{
    public RuntimePuzzleLevelController level;
    bool announced;

    void Update()
    {
        if (!level) return;
        if (!level.Completed)
        {
            announced = false;
            return;
        }
        if (announced) return;
        announced = true;
        RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Complete, transform.position);
        RuntimeHaptics.PulseAll(0.48f, 0.14f);
    }
}
