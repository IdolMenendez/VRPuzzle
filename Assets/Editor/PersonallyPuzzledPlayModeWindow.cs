#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Explicit launch surface for player-driven and presentation-driven play.
/// The selected editor mode is never serialized into Android builds.
/// </summary>
public sealed class PersonallyPuzzledPlayModeWindow : EditorWindow
{
    const string PreferredBootstrapScene =
        "Assets/Scenes/RuntimeV80_ZeroLatencyAssembly/Runtime_Bootstrap_20260807_V80_ZeroLatencyAssembly.unity";
    const string EditorPreference =
        "PersonallyPuzzledVR.SelectedEditorExperienceMode";

    PersonallyPuzzledExperienceMode selectedMode =
        PersonallyPuzzledExperienceMode.PlayerManual;

    [MenuItem("Personally Puzzled XR/Play Mode Controller/Open %#p")]
    public static void Open()
    {
        GetWindow<PersonallyPuzzledPlayModeWindow>(
            "Personally Puzzled · Play Modes");
    }

    [MenuItem("Personally Puzzled XR/Play Mode Controller/Start Player Manual _F8")]
    public static void StartPlayerManual()
    {
        StartMode(PersonallyPuzzledExperienceMode.PlayerManual);
    }

    [MenuItem("Personally Puzzled XR/Play Mode Controller/Start Auto Demo _F9")]
    public static void StartAutoDemo()
    {
        StartMode(PersonallyPuzzledExperienceMode.AutoDemo);
    }

    [MenuItem("Personally Puzzled XR/Play Mode Controller/Run Validation Smoke")]
    public static void RunValidationSmoke()
    {
        LatestProjectTools.RunInteractionSmoke();
    }

    [MenuItem("Personally Puzzled XR/Play Mode Controller/Stop Play Mode _F10")]
    public static void StopPlayMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.ExitPlaymode();
    }

    void OnEnable()
    {
        selectedMode = (PersonallyPuzzledExperienceMode)EditorPrefs.GetInt(
            EditorPreference,
            (int)PersonallyPuzzledExperienceMode.PlayerManual);
        if (selectedMode == PersonallyPuzzledExperienceMode.ValidationSmoke)
            selectedMode = PersonallyPuzzledExperienceMode.PlayerManual;
    }

    void OnGUI()
    {
        GUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "完整游戏体验模式",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "玩家手动模式是发布默认：玩家从开屏触碰、选关、逐件拼装、" +
            "通关把玩到入藏仪式，全程亲自操作。自动演示会完整展示同一流程，" +
            "但绝不会写入发布 APK。",
            MessageType.Info);

        int modeIndex = GUILayout.Toolbar(
            selectedMode == PersonallyPuzzledExperienceMode.AutoDemo
                ? 1
                : 0,
            new[]
            {
                "玩家手动 · 发布默认",
                "自动演示 · 展示专用",
            });
        selectedMode = modeIndex == 1
            ? PersonallyPuzzledExperienceMode.AutoDemo
            : PersonallyPuzzledExperienceMode.PlayerManual;
        EditorPrefs.SetInt(EditorPreference, (int)selectedMode);

        GUILayout.Space(10f);
        Color previous = GUI.backgroundColor;
        GUI.backgroundColor = selectedMode ==
            PersonallyPuzzledExperienceMode.PlayerManual
                ? new Color(0.45f, 0.88f, 0.62f)
                : new Color(1f, 0.74f, 0.28f);
        if (GUILayout.Button(
                selectedMode == PersonallyPuzzledExperienceMode.PlayerManual
                    ? "▶ 从开头开始：玩家手动完整体验  (F8)"
                    : "▶ 从开头开始：自动演示完整流程  (F9)",
                GUILayout.Height(42f)))
            StartMode(selectedMode);
        GUI.backgroundColor = previous;

        using (new EditorGUI.DisabledScope(
                   !EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("■ 停止当前体验  (F10)"))
                StopPlayMode();
        }

        GUILayout.Space(10f);
        EditorGUILayout.LabelField("开发验证", EditorStyles.boldLabel);
        if (GUILayout.Button("运行当前版本拼装交互回归"))
            RunValidationSmoke();
        EditorGUILayout.HelpBox(
            "快捷键：F8 玩家手动，F9 自动演示，F10 停止。" +
            "正常 Build PICO APK 时始终为玩家手动模式。",
            MessageType.None);
    }

    public static void ForceManualReleaseDefault()
    {
        Environment.SetEnvironmentVariable(
            PersonallyPuzzledExperienceModeState.EnvironmentVariable,
            PersonallyPuzzledExperienceMode.PlayerManual.ToString());
        Environment.SetEnvironmentVariable("PPVR_FLOW_SMOKE", null);
        EditorPrefs.SetInt(
            EditorPreference,
            (int)PersonallyPuzzledExperienceMode.PlayerManual);
    }

    static void StartMode(PersonallyPuzzledExperienceMode mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.ExitPlaymode();
        Environment.SetEnvironmentVariable("PPVR_FLOW_SMOKE", null);
        Environment.SetEnvironmentVariable(
            PersonallyPuzzledExperienceModeState.EnvironmentVariable,
            mode.ToString());
        EditorPrefs.SetInt(EditorPreference, (int)mode);
        EditorSceneManager.SaveOpenScenes();
        EditorSceneManager.OpenScene(ResolveBootstrapScene(), OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static string ResolveBootstrapScene()
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled &&
                scene.path.IndexOf("Runtime_Bootstrap_", StringComparison.Ordinal) >= 0 &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path))
                return scene.path;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreferredBootstrapScene))
            return PreferredBootstrapScene;
        throw new InvalidOperationException("Current V80 bootstrap scene is missing.");
    }
}
#endif
