using UnityEngine;

public sealed class RuntimeHintLamp : MonoBehaviour
{
    public RuntimePuzzleLevelController level;
    public float touchRadius = 0.10f;
    [Tooltip("When enabled, the lamp body is visual/accessibility context only. " +
             "The authored physical pull-chain must request the hint.")]
    public bool requirePullInteraction;
    [Tooltip(
        "V25 routes the lamp through the current level's gold-Part, cyan-path " +
        "and exact-socket guidance instead of the retained legacy Stage 6 " +
        "single-ghost controller.")]
    public bool useGuidedLevelHintV25;
    float nextTrigger;

    void OnMouseDown()
    {
        if (!requirePullInteraction)
            Trigger();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!requirePullInteraction &&
            (other.name.Contains("Hand") ||
             other.name.Contains("Controller")))
            Trigger();
    }

    public void Trigger()
    {
        if (Time.unscaledTime < nextTrigger || !level) return;
        nextTrigger = Time.unscaledTime + 1f;
        RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Hint, transform.position);
        // The polished lamp is the visible interaction surface, while the
        // retained Stage 6 controller owns the authored exact-model ghost.
        // Prefer that precise hint when present and fall back to the generic
        // level pulse in scenes without the dedicated controller.
        Stage6LampGhostHintV06Controller preciseHint =
            FindFirstObjectByType<Stage6LampGhostHintV06Controller>();
        if (useGuidedLevelHintV25)
            level.PulseNextHint();
        else if (preciseHint)
            preciseHint.TriggerHint();
        else
            level.PulseNextHint();
        RuntimeHaptics.PulseAll(0.25f, 0.055f);
    }
}
