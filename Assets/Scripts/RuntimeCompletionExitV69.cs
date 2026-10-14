using UnityEngine;

/// <summary>Physical completion-stage exit; no hidden generic pinch.</summary>
[DisallowMultipleComponent]
public sealed class RuntimeCompletionExitV69 : MonoBehaviour
{
    public Collider touchCollider;
    public Transform buttonCap;
    [Min(0.01f)] public float touchRadius = 0.04f;
    float nextAllowed;

    void Update()
    {
        if (!touchCollider || !RuntimeRealHandPinchV47.Instance ||
            Time.unscaledTime < nextAllowed)
            return;
        if (Touches(true) || Touches(false))
        {
            nextAllowed = Time.unscaledTime + 1.0f;
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Touch,
                buttonCap ? buttonCap.position : transform.position);
            RuntimeHaptics.PulseAll(0.34f, 0.06f);
            Debug.Log("[PPVR V69 Completion] event=return-selection");
            PersistentPuzzleBootstrap.Instance?.EnterSelection();
        }
    }

    bool Touches(bool left)
    {
        if (!RuntimeRealHandPinchV47.Instance.TryGetIndexTipWorldV59(
                left, out Vector3 tip))
            return false;
        return Vector3.Distance(touchCollider.ClosestPoint(tip), tip) <= touchRadius;
    }
}
