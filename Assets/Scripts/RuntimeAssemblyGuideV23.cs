using UnityEngine;

/// <summary>
/// V23 physical workflow guidance.  The central blueprint answers what is
/// being assembled, while a large diegetic plaque explains the next action.
/// No screen-space UI follows the player's head.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuntimeAssemblyGuideV23 : MonoBehaviour
{
    public RuntimePuzzleLevelController level;
    public TextMesh instructionLabel;
    public RuntimePuzzlePiece[] blueprintPieces;
    public GameObject[] blueprintModules;
    public string localizedSubject;
    public bool keepBlueprintUntilComplete;
    public bool guidedPlacementV25;
    public bool completeReferenceAndTouchHintV36;
    public bool physicalHintButtonV79;
    public bool hideInstructionOnCompleteV79;
    [Header("V80 on-demand blueprint")]
    public bool eventDrivenRefreshV80;
    [Tooltip(
        "Hides the duplicated full high-poly blueprint at rest. A physical " +
        "Hint press reveals only the current exact target module.")]
    public bool onDemandBlueprintV80;

    int lastSnapped = -1;
    bool lastCompleted;
    float explicitHintRemaining;

    void Awake()
    {
        if (!level)
            level = GetComponent<RuntimePuzzleLevelController>();
        Refresh();
    }

    void LateUpdate()
    {
        if (explicitHintRemaining > 0f)
        {
            explicitHintRemaining = Mathf.Max(
                0f,
                explicitHintRemaining - Time.unscaledDeltaTime);
            if (explicitHintRemaining <= 0f)
            {
                lastSnapped = -1;
                lastCompleted = !level || !level.Completed;
            }
        }
        Refresh();
    }

    public void Refresh()
    {
        if (!level)
            return;
        int snapped = level.SnappedCount;
        bool completed = level.Completed;
        bool changed =
            snapped != lastSnapped || completed != lastCompleted;
        if (changed)
        {
            lastSnapped = snapped;
            lastCompleted = completed;
            RefreshText(snapped, completed);
        }
        if (eventDrivenRefreshV80 &&
            !changed &&
            !(onDemandBlueprintV80 &&
              explicitHintRemaining > 0f))
            return;
        int count = Mathf.Min(
            blueprintPieces != null ? blueprintPieces.Length : 0,
            blueprintModules != null ? blueprintModules.Length : 0);
        for (int i = 0; i < count; i++)
        {
            if (!blueprintModules[i])
                continue;
            bool visible;
            if (onDemandBlueprintV80)
            {
                RuntimePuzzlePiece target =
                    level.guidance
                        ? level.guidance.RecommendedPiece
                        : null;
                visible =
                    explicitHintRemaining > 0f &&
                    target &&
                    blueprintPieces[i] == target &&
                    !target.IsSnapped;
            }
            else
            {
                visible = keepBlueprintUntilComplete
                    ? !level.Completed
                    : blueprintPieces[i] &&
                      !blueprintPieces[i].IsSnapped;
            }
            if (blueprintModules[i].activeSelf != visible)
                blueprintModules[i].SetActive(visible);
        }
    }

    void RefreshText(int snapped, bool completed)
    {
        if (!instructionLabel)
            return;
        if (completed && hideInstructionOnCompleteV79)
        {
            instructionLabel.gameObject.SetActive(false);
            return;
        }
        if (!instructionLabel.gameObject.activeSelf)
            instructionLabel.gameObject.SetActive(true);
        string subject = string.IsNullOrWhiteSpace(localizedSubject)
            ? level.levelTitle
            : localizedSubject;
        if (completed)
        {
            instructionLabel.text =
                "组装完成 · " + subject + "\n" +
                "杰作展示即将开启";
            return;
        }
        instructionLabel.text =
            "正在组装 · " + subject + "   " +
            snapped + "/" + Mathf.Max(1, level.TotalCount) + "\n" +
            (completeReferenceAndTouchHintV36
                ? "捏合拿起零件 → 随手旋转 → 参考桌侧完整成品\n" +
                  (physicalHintButtonV79
                      ? "松手吸附；未对准会回到原位 · 食指按左前方提示按钮查看下一步"
                      : "松手吸附；未对准会回到原位 · 触碰桌面提示物查看下一步")
                : guidedPlacementV25
                ? "拿起两侧零件 → 放入中央同形蓝框\n" +
                  (physicalHintButtonV79
                      ? "靠近会自动对齐，松手即吸附 · 食指按左前方提示按钮"
                      : "靠近会自动对齐，松手即吸附 · 食指轻触灯绳圆珠看位置")
                : "从两侧拿起零件，对准中央蓝色轮廓\n" +
                  (physicalHintButtonV79
                      ? "需要帮助：食指按下左前方「提示」按钮"
                      : "需要帮助：拉动台灯绳"));
    }

    public void ShowExplicitHintV25(float seconds)
    {
        if (!guidedPlacementV25 || !instructionLabel)
            return;
        explicitHintRemaining = Mathf.Max(
            explicitHintRemaining,
            Mathf.Max(0.8f, seconds));
        Refresh();
        instructionLabel.text =
            "提示：拿起正在发金光的零件\n" +
            "沿蓝线移到亮起的同形插槽\n" +
            "靠近后松手，零件会自动吸附";
    }
}
