using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

/// <summary>
/// Unity-editor-only neutral head pose and bounded six-axis inspection.
/// A PICO/OpenXR build keeps the TrackedPoseDriver and never executes this
/// fallback. In the editor, where no real OpenXR runtime is available, this
/// prevents stale simulated poses from putting the camera inside the table or
/// authored hands while still allowing a player to inspect every stage in
/// space.
/// </summary>
public sealed class RuntimeDesktopHeadNavigatorV01 : MonoBehaviour
{
    public static bool DesktopEditorPreviewActive { get; private set; }

    public Transform rigRoot;
    public Camera sceneCamera;
    public Vector3 neutralEye = new Vector3(0f, 1.60f, 0f);
    [Min(0.1f)] public float moveSpeed = 0.72f;
    [Min(0.01f)] public float lookSensitivity = 0.12f;
    public Vector3 maximumRoomOffset = new Vector3(0.85f, 0.42f, 0.75f);
    public bool showGameViewInstructions = true;

    float yaw;
    float pitch;
    Vector3 roomOffset;

    public bool DesktopPreviewActive { get; private set; }
    public bool EditorTrackedPoseSuppressed =>
        !sceneCamera || !sceneCamera.GetComponent<TrackedPoseDriver>();

    void Awake()
    {
#if UNITY_EDITOR
        if (!rigRoot)
            rigRoot = transform;
        if (!sceneCamera)
            sceneCamera = Camera.main;
        TrackedPoseDriver trackedPose = sceneCamera
            ? sceneCamera.GetComponent<TrackedPoseDriver>()
            : null;
        if (trackedPose)
            DestroyImmediate(trackedPose);
        DesktopEditorPreviewActive = true;
        DesktopPreviewActive = true;
        ResetToNeutralForAutomation();
#endif
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        Application.onBeforeRender += ApplyEditorPose;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        Application.onBeforeRender -= ApplyEditorPose;
#endif
    }

    void OnDestroy()
    {
#if UNITY_EDITOR
        DesktopEditorPreviewActive = false;
#endif
    }

    void Update()
    {
#if UNITY_EDITOR
        if (!DesktopPreviewActive || !sceneCamera || !rigRoot)
            return;
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        bool inspecting = mouse != null && mouse.rightButton.isPressed;

        if (inspecting)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * lookSensitivity;
            pitch = Mathf.Clamp(
                pitch - delta.y * lookSensitivity,
                -28f,
                32f);
            ApplyViewRotation();

            if (keyboard != null)
            {
                Vector3 input = Vector3.zero;
                if (keyboard.wKey.isPressed) input += Vector3.forward;
                if (keyboard.sKey.isPressed) input += Vector3.back;
                if (keyboard.aKey.isPressed) input += Vector3.left;
                if (keyboard.dKey.isPressed) input += Vector3.right;
                if (keyboard.qKey.isPressed) input += Vector3.down;
                if (keyboard.eKey.isPressed) input += Vector3.up;
                if (input.sqrMagnitude > 0.001f)
                {
                    Vector3 horizontalForward =
                        Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    Vector3 horizontalRight =
                        Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                    Vector3 deltaPosition =
                        horizontalRight * input.x +
                        Vector3.up * input.y +
                        horizontalForward * input.z;
                    Vector3 next = roomOffset +
                        deltaPosition.normalized * moveSpeed *
                        Time.unscaledDeltaTime;
                    roomOffset = new Vector3(
                        Mathf.Clamp(next.x, -maximumRoomOffset.x, maximumRoomOffset.x),
                        Mathf.Clamp(next.y, -maximumRoomOffset.y, maximumRoomOffset.y),
                        Mathf.Clamp(next.z, -maximumRoomOffset.z, maximumRoomOffset.z));
                }
            }
        }

        if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            ResetToNeutralForAutomation();
#endif
    }

    void LateUpdate()
    {
#if UNITY_EDITOR
        ApplyEditorPose();
#endif
    }

    public void ResetToNeutralForAutomation()
    {
#if UNITY_EDITOR
        if (!rigRoot)
            rigRoot = transform;
        if (!sceneCamera)
            sceneCamera = Camera.main;
        roomOffset = Vector3.zero;
        yaw = 0f;
        pitch = 0f;
        ApplyEditorPose();
#endif
    }

    public void SetInspectionPoseForAutomation(
        Vector3 roomOffset,
        float yawDegrees,
        float pitchDegrees)
    {
#if UNITY_EDITOR
        ResetToNeutralForAutomation();
        this.roomOffset = new Vector3(
            Mathf.Clamp(roomOffset.x, -maximumRoomOffset.x, maximumRoomOffset.x),
            Mathf.Clamp(roomOffset.y, -maximumRoomOffset.y, maximumRoomOffset.y),
            Mathf.Clamp(roomOffset.z, -maximumRoomOffset.z, maximumRoomOffset.z));
        yaw = Mathf.Clamp(yawDegrees, -35f, 35f);
        pitch = Mathf.Clamp(pitchDegrees, -20f, 20f);
        ApplyEditorPose();
#endif
    }

    void ApplyViewRotation()
    {
        if (!rigRoot || !sceneCamera)
            return;
        rigRoot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        sceneCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void ApplyEditorPose()
    {
#if UNITY_EDITOR
        if (!DesktopPreviewActive || !rigRoot || !sceneCamera)
            return;
        // TrackedPoseDriver can receive a final BeforeRender update from an
        // editor XRHMD device even after OpenXR initialization failed. Disable
        // it and reassert the desktop pose at the same callback boundary so a
        // stale simulator pose cannot leak into the submitted Game view.
        TrackedPoseDriver trackedPose =
            sceneCamera.GetComponent<TrackedPoseDriver>();
        if (trackedPose && trackedPose.enabled)
            trackedPose.enabled = false;
        // XROrigin can retain a host-side locomotion offset on its persistent
        // parent even after the camera driver is removed. The authored stages
        // are registered around world origin, so the editor preview must also
        // reassert that invariant. This mirrors the real Swan fallback in
        // PersistentPuzzleBootstrap without changing the serialized rig.
        Transform persistentRoot = rigRoot.root;
        if (persistentRoot &&
            persistentRoot.GetComponent<PersistentPuzzleBootstrap>())
        {
            persistentRoot.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            persistentRoot.localScale = Vector3.one;
        }
        rigRoot.SetLocalPositionAndRotation(
            roomOffset,
            Quaternion.Euler(0f, yaw, 0f));
        Transform cameraParent = sceneCamera.transform.parent;
        if (cameraParent && cameraParent != rigRoot)
            cameraParent.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
        sceneCamera.transform.SetLocalPositionAndRotation(
            neutralEye,
            Quaternion.Euler(pitch, 0f, 0f));
#endif
    }

    void OnGUI()
    {
#if UNITY_EDITOR
        if (!showGameViewInstructions ||
            !DesktopPreviewActive ||
            !PersonallyPuzzledExperienceModeState.IsPlayerManual)
            return;
        GUI.Box(
            new Rect(18f, 106f, 390f, 64f),
            "空间检查 · SPATIAL INSPECTION\n" +
            "按住右键转头并用 WASD/QE 移动 · F 回到设计稿正视角");
#endif
    }
}
