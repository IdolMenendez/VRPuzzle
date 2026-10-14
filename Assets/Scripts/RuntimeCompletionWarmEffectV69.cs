using UnityEngine;

/// <summary>Opaque PICO-safe warm mechanical celebration, no particles.</summary>
[DisallowMultipleComponent]
public sealed class RuntimeCompletionWarmEffectV69 : MonoBehaviour
{
    public Transform orbit;
    public Light warmLight;
    public float degreesPerSecond = 11f;
    Vector3 baseScale;

    void Awake()
    {
        if (orbit) baseScale = orbit.localScale;
    }

    void Update()
    {
        if (orbit)
        {
            orbit.Rotate(Vector3.up,
                degreesPerSecond * Time.unscaledDeltaTime, Space.World);
            float pulse = 1f + 0.035f * Mathf.Sin(Time.unscaledTime * 2.4f);
            orbit.localScale = baseScale * pulse;
        }
        if (warmLight)
            warmLight.intensity = 0.62f +
                0.10f * Mathf.Sin(Time.unscaledTime * 2.1f);
    }
}
