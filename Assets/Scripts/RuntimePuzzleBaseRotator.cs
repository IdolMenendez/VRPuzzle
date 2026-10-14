using UnityEngine;
using UnityEngine.InputSystem;

public sealed class RuntimePuzzleBaseRotator : MonoBehaviour
{
    public Transform pivot;
    public Transform gripAnchor;
    public float grabRadius = 0.13f;
    public float minimumYaw = -145f;
    public float maximumYaw = 145f;
    public float keyboardDegreesPerSecond = 62f;

    [SerializeField] Quaternion authoredLocalRotation = Quaternion.identity;
    [SerializeField] float currentYaw;
    [SerializeField] bool initialized;
    Quaternion grabStartRotation = Quaternion.identity;
    Vector3 grabStartDirection;
    float grabStartYaw;
    int holderId = -1;

    public float CurrentYaw => currentYaw;

    void Awake()
    {
        EnsureInitialized();
    }

    void Update()
    {
        if (holderId >= 0 || Keyboard.current == null) return;
        float direction = 0f;
        if (Keyboard.current.qKey.isPressed || Keyboard.current.leftArrowKey.isPressed) direction -= 1f;
        if (Keyboard.current.eKey.isPressed || Keyboard.current.rightArrowKey.isPressed) direction += 1f;
        if (Mathf.Abs(direction) > 0.01f)
            RotateBy(direction * keyboardDegreesPerSecond * Time.unscaledDeltaTime);
    }

    public bool CanGrab(int candidateHolderId, Pose pose)
    {
        EnsureInitialized();
        if (holderId >= 0 && holderId != candidateHolderId) return false;
        Transform anchor = gripAnchor ? gripAnchor : pivot;
        return anchor && Vector3.Distance(pose.position, anchor.position) <= grabRadius;
    }

    public bool BeginGrab(int candidateHolderId, Pose pose)
    {
        if (!CanGrab(candidateHolderId, pose)) return false;
        holderId = candidateHolderId;
        grabStartRotation = pivot.localRotation;
        grabStartYaw = currentYaw;
        grabStartDirection = HorizontalDirection(pose.position);
        RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Grab, pivot.position);
        return true;
    }

    public void MoveGrab(int candidateHolderId, Pose pose)
    {
        if (holderId != candidateHolderId) return;
        Vector3 currentDirection = HorizontalDirection(pose.position);
        if (grabStartDirection.sqrMagnitude < 0.0001f || currentDirection.sqrMagnitude < 0.0001f) return;
        float delta = Vector3.SignedAngle(grabStartDirection, currentDirection, pivot.up);
        pivot.localRotation = grabStartRotation;
        SetYaw(grabStartYaw + delta);
    }

    public void EndGrab(int candidateHolderId)
    {
        if (holderId == candidateHolderId) holderId = -1;
    }

    public void SetReferenceYaw(float yaw)
    {
        if (!pivot) pivot = transform;
        authoredLocalRotation = pivot.localRotation;
        initialized = true;
        currentYaw = 0f;
        SetYaw(yaw);
    }

    public void RotateBy(float deltaYaw)
    {
        EnsureInitialized();
        SetYaw(currentYaw + deltaYaw);
    }

    public void SetYawAbsolute(float yaw)
    {
        EnsureInitialized();
        SetYaw(yaw);
    }

    void SetYaw(float yaw)
    {
        currentYaw = Mathf.Clamp(yaw, minimumYaw, maximumYaw);
        pivot.localRotation = authoredLocalRotation * Quaternion.Euler(0f, currentYaw, 0f);
    }

    Vector3 HorizontalDirection(Vector3 handPosition)
    {
        Vector3 direction = handPosition - pivot.position;
        return Vector3.ProjectOnPlane(direction, pivot.up).normalized;
    }

    void EnsureInitialized()
    {
        if (!pivot) pivot = transform;
        if (initialized) return;
        authoredLocalRotation = pivot.localRotation;
        currentYaw = 0f;
        initialized = true;
    }
}
