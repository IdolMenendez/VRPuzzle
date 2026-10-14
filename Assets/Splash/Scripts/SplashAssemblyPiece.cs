using UnityEngine;

/// <summary>
/// One physical fragment of the self-assembling splash mark.  The scene stores
/// the final, art-directed pose; play mode derives the exploded pose from it.
/// </summary>
public sealed class SplashAssemblyPiece : MonoBehaviour
{
    public Vector3 scatterOffset;
    public Vector3 scatterEuler;
    [Min(0f)] public float delay;
    [Min(0.1f)] public float duration = 1.1f;
    [Range(0f, 0.25f)] public float overshoot = 0.08f;

    Vector3 assembledPosition;
    Quaternion assembledRotation;
    Vector3 assembledScale;
    bool cached;

    public void CacheAndScatter()
    {
        if (!cached)
        {
            assembledPosition = transform.localPosition;
            assembledRotation = transform.localRotation;
            assembledScale = transform.localScale;
            cached = true;
        }

        transform.localPosition = assembledPosition + scatterOffset;
        transform.localRotation = assembledRotation * Quaternion.Euler(scatterEuler);
        transform.localScale = assembledScale * 0.22f;
    }

    public void Evaluate(float elapsed)
    {
        if (!cached)
            CacheAndScatter();

        float t = Mathf.Clamp01((elapsed - delay) / Mathf.Max(0.1f, duration));
        float eased = EaseOutBack(t, 1.15f + overshoot * 8f);
        transform.localPosition = Vector3.LerpUnclamped(assembledPosition + scatterOffset, assembledPosition, eased);
        transform.localRotation = Quaternion.SlerpUnclamped(
            assembledRotation * Quaternion.Euler(scatterEuler), assembledRotation, eased);
        transform.localScale = Vector3.LerpUnclamped(assembledScale * 0.22f, assembledScale, eased);
    }

    static float EaseOutBack(float t, float amount)
    {
        float x = t - 1f;
        return 1f + (amount + 1f) * x * x * x + amount * x * x;
    }
}
