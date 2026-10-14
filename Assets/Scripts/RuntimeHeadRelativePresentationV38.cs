using UnityEngine;

/// <summary>
/// Places a streamed interaction presentation once relative to the tracked
/// eye after the authoritative covered room registration has completed.
/// It never changes the XR Origin, camera, head pose, or physical Floor.
/// </summary>
[DefaultExecutionOrder(-90)]
[DisallowMultipleComponent]
public sealed class RuntimeHeadRelativePresentationV38 : MonoBehaviour
{
    public RuntimePuzzleStage stage;
    public RuntimeCoveredStageRegistrationV06 registration;
    public Transform presentationRoot;
    public Transform focusAnchor;
    [Min(0.65f)] public float distanceFromHead = 1.08f;
    public float verticalOffsetFromEye = -0.10f;
    public bool registerHorizontally = true;
    public bool registerVertically = true;

    public bool Applied { get; private set; }
    public Vector3 AppliedTranslation { get; private set; }
    public Vector3 TargetFocusPosition { get; private set; }

    void Update()
    {
        if (Applied)
            return;
        PersistentPuzzleBootstrap bootstrap =
            PersistentPuzzleBootstrap.Instance;
        if (!bootstrap ||
            bootstrap.CurrentStage != stage ||
            !bootstrap.xrCamera ||
            !registration ||
            !registration.Applied ||
            !presentationRoot ||
            !focusAnchor)
            return;

        RuntimeComfortTransitionV01 comfort =
            bootstrap.comfortTransition;
        if (comfort && !comfort.IsCovered)
            return;

        ApplyNow(bootstrap.xrCamera);
    }

    public bool ApplyNow(Camera trackedCamera)
    {
        if (Applied ||
            !trackedCamera ||
            !presentationRoot ||
            !focusAnchor)
            return false;

        Vector3 forward =
            Vector3.ProjectOnPlane(
                trackedCamera.transform.forward,
                Vector3.up);
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        TargetFocusPosition =
            trackedCamera.transform.position +
            forward * Mathf.Max(0.65f, distanceFromHead) +
            Vector3.up * verticalOffsetFromEye;
        Vector3 translation =
            TargetFocusPosition -
            focusAnchor.position;
        if (!registerHorizontally)
        {
            translation.x = 0f;
            translation.z = 0f;
        }
        if (!registerVertically)
            translation.y = 0f;

        presentationRoot.position += translation;
        Physics.SyncTransforms();
        AppliedTranslation = translation;
        Applied = true;

        Debug.Log(
            "[Personally Puzzled V38] head-relative presentation " +
            $"stage={stage} translation={AppliedTranslation} " +
            $"target={TargetFocusPosition} " +
            $"focus={focusAnchor.position} " +
            $"camera={trackedCamera.transform.position} " +
            "xrOrigin=UNCHANGED camera=UNCHANGED floor=UNCHANGED",
            this);
        return true;
    }

#if UNITY_EDITOR
    public void ResetForValidation()
    {
        Applied = false;
        AppliedTranslation = Vector3.zero;
        TargetFocusPosition = Vector3.zero;
    }
#endif
}
