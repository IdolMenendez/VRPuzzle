using UnityEngine;

/// <summary>
/// Marks an authored screenshot/reference hand that must never compete with the
/// live tracked avatar. Source-stage previews remain available in their backed
/// up scenes, while promoted runtime stages keep their renderer disabled.
/// </summary>
public sealed class RuntimeStaticHandPreviewSuppressorV01 : MonoBehaviour
{
    public Renderer previewRenderer;
    public string sourceAsset;

    void Awake()
    {
        Suppress();
    }

    void OnEnable()
    {
        Suppress();
    }

    void LateUpdate()
    {
        Suppress();
    }

    void Suppress()
    {
        if (previewRenderer)
            previewRenderer.enabled = false;
    }
}
