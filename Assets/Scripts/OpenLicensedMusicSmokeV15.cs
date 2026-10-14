using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public static class OpenLicensedMusicSmokeV15
{
    public const string EnvironmentVariable =
        "PPVR_V15_OPEN_LICENSED_MUSIC_SMOKE";
    public const string CaptureRootVariable =
        "PPVR_V15_OPEN_LICENSED_MUSIC_CAPTURE_ROOT";

#if UNITY_EDITOR
    static bool created;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateRunnerWhenRequested()
    {
        if (created ||
            Environment.GetEnvironmentVariable(
                EnvironmentVariable) != "1")
            return;
        created = true;
        Environment.SetEnvironmentVariable(
            EnvironmentVariable, null);
        GameObject runner = new GameObject(
            "V15 CC0 light-music PlayerManual runner");
        UnityEngine.Object.DontDestroyOnLoad(runner);
        runner.AddComponent<OpenLicensedMusicSmokeRunnerV15>();
    }
#endif
}

#if UNITY_EDITOR
public sealed class OpenLicensedMusicSmokeRunnerV15 : MonoBehaviour
{
    const string ExpectedScene =
        "Runtime_Level_Eiffel_20260724_V15_OpenLicensedMusic";

    IEnumerator Start()
    {
        string captureRoot = Environment.GetEnvironmentVariable(
            OpenLicensedMusicSmokeV15.CaptureRootVariable);
        Environment.SetEnvironmentVariable(
            OpenLicensedMusicSmokeV15.CaptureRootVariable, null);
        if (string.IsNullOrEmpty(captureRoot))
            captureRoot = Path.Combine(
                Application.dataPath,
                "../Artifacts/Checkpoints/" +
                "27_OpenLicensedMusicV15_20260724/" +
                "PlayerManualMusicReview");
        captureRoot = Path.GetFullPath(captureRoot);
        Directory.CreateDirectory(captureRoot);

        Require(
            UnityEngine.SceneManagement.SceneManager
                .GetActiveScene().name == ExpectedScene,
            "runner did not start in V15 Eiffel");
        yield return null;

        RuntimeAudioConductor audio =
            FindFirstObjectByType<RuntimeAudioConductor>();
        Camera camera = FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value.name.Contains(
                "RuntimeReferenceCamera",
                StringComparison.OrdinalIgnoreCase));
        Require(audio && camera,
            "V15 PlayerManual music scene is incomplete");
        Require(
            camera.enabled &&
            camera.gameObject.activeInHierarchy &&
            camera.targetTexture == null &&
            Camera.main == camera,
            "standalone V15 direct Play has no active Game-view camera");
        Require(
            FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Any(value => value && value.enabled),
            "standalone V15 direct Play has no AudioListener");

        yield return new WaitForSecondsRealtime(2.25f);
        AudioSource source = ActiveMusicSource(audio);
        Require(
            audio.HasPuzzleMusicPool &&
            audio.puzzleMusicPool.Length == 4 &&
            audio.puzzleMusicPool.All(value =>
                value &&
                value.name.StartsWith(
                    "BGM_Open_", StringComparison.Ordinal)) &&
            audio.CurrentMusicClip &&
            audio.CurrentMusicClip.name.StartsWith(
                "BGM_Open_", StringComparison.Ordinal) &&
            source &&
            source.isPlaying &&
            source.loop &&
            source.volume > 0.01f,
            "downloaded CC0 loop is not audibly playing");

        float timeBefore = source.time;
        yield return new WaitForSecondsRealtime(1.25f);
        source = ActiveMusicSource(audio);
        Require(
            source &&
            source.isPlaying &&
            source.time > timeBefore + 0.5f,
            "music playback time did not advance");

        string screenshot = Path.Combine(
            captureRoot,
            "PersonallyPuzzledVR_V15_OpenLicensedMusic_FINAL.png");
        CaptureCamera(camera, screenshot);
        Debug.Log(
            "[Open Licensed Music V15] PLAYER MANUAL MUSIC SMOKE PASSED · " +
            "CC0 source loop audible · four-track restrained random pool · " +
            "loop and playback-time progression verified · current clip: " +
            audio.CurrentMusicClip.name + " · screenshot: " + screenshot);
        Finish(0);
    }

    static AudioSource ActiveMusicSource(RuntimeAudioConductor audio)
    {
        if (audio.musicA &&
            audio.musicA.isPlaying &&
            audio.musicA.volume > 0.01f)
            return audio.musicA;
        if (audio.musicB &&
            audio.musicB.isPlaying &&
            audio.musicB.volume > 0.01f)
            return audio.musicB;
        return null;
    }

    static void CaptureCamera(Camera camera, string path)
    {
        RenderTexture texture = new RenderTexture(
            2560, 1440, 24,
            RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        Texture2D image = new Texture2D(
            2560, 1440, TextureFormat.RGB24, false);
        image.ReadPixels(
            new Rect(0f, 0f, 2560f, 1440f),
            0,
            0);
        image.Apply(false, false);
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = previousTarget;
        RenderTexture.active = previous;
        Destroy(texture);
        Destroy(image);
    }

    static void Require(bool condition, string message)
    {
        if (condition)
            return;
        Debug.LogError(
            "[Open Licensed Music V15] PLAYER MANUAL MUSIC SMOKE FAILED · " +
            message);
        Finish(1);
        throw new InvalidOperationException(message);
    }

    static void Require(
        UnityEngine.Object value,
        string message) =>
        Require(value != null, message);

    static void Finish(int exitCode)
    {
        if (Application.isBatchMode)
            UnityEditor.EditorApplication.Exit(exitCode);
        else
            UnityEditor.EditorApplication.isPlaying = false;
    }
}
#endif
