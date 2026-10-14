using UnityEngine;

[DisallowMultipleComponent]
public sealed class RuntimeHintCompassV36 : MonoBehaviour
{
    public RuntimePuzzleLevelController level;
    public Renderer[] renderers;
    [Min(0.01f)] public float touchRadius = 0.035f;
    [Min(0.2f)] public float cooldownSeconds = 0.8f;

    float nextAllowed;
    float pulseRemaining;
    MaterialPropertyBlock block;

    public bool TryFingertipTouchV36(Vector3 fingertip)
    {
        if (Time.unscaledTime < nextAllowed)
            return false;
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        float best = float.MaxValue;
        foreach (Collider collider in colliders)
        {
            if (!collider || !collider.enabled)
                continue;
            best = Mathf.Min(
                best,
                Vector3.Distance(
                    fingertip,
                    collider.ClosestPoint(fingertip)));
        }
        if (best > Mathf.Max(0.01f, touchRadius))
            return false;
        nextAllowed = Time.unscaledTime + Mathf.Max(0.2f, cooldownSeconds);
        pulseRemaining = 0.45f;
        level?.PulseNextHint();
        RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Touch, transform.position);
        RuntimeHaptics.PulseAll(0.18f, 0.045f);
        return true;
    }

    void Update()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);
        block ??= new MaterialPropertyBlock();
        pulseRemaining = Mathf.Max(
            0f,
            pulseRemaining - Time.unscaledDeltaTime);
        float glow = pulseRemaining > 0f
            ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 18f)
            : 0f;
        foreach (Renderer renderer in renderers)
        {
            if (!renderer)
                continue;
            if (glow <= 0f)
            {
                renderer.SetPropertyBlock(null);
                continue;
            }
            renderer.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", new Color(1f, 0.48f, 0.08f) * glow);
            renderer.SetPropertyBlock(block);
        }
    }
}
