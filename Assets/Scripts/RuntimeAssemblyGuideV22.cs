using UnityEngine;

/// <summary>
/// V22 physical workflow guidance.  The central blueprint answers what is
/// being assembled, while a large diegetic plaque explains the next action.
/// No screen-space UI follows the player's head.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuntimeAssemblyGuideV22 : MonoBehaviour
{
    public RuntimePuzzleLevelController level;
    public TextMesh instructionLabel;
    public RuntimePuzzlePiece[] blueprintPieces;
    public GameObject[] blueprintModules;
    public string localizedSubject;
    public bool keepBlueprintUntilComplete;
    public bool physicalHintButtonV79;
    public bool hideInstructionOnCompleteV79;
    public bool eventDrivenRefreshV80;

    int lastSnapped = -1;
    bool lastCompleted;

    void Awake()
    {
        if (!level)
            level = GetComponent<RuntimePuzzleLevelController>();
        Refresh();
    }

    void LateUpdate()
    {
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
        if (eventDrivenRefreshV80 && !changed)
            return;
        int count = Mathf.Min(
            blueprintPieces != null ? blueprintPieces.Length : 0,
            blueprintModules != null ? blueprintModules.Length : 0);
        for (int i = 0; i < count; i++)
        {
            if (!blueprintModules[i])
                continue;
            bool visible = keepBlueprintUntilComplete
                ? !level.Completed
                : blueprintPieces[i] &&
                  !blueprintPieces[i].IsSnapped;
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
            "从两侧拿起零件，对准中央蓝色轮廓\n" +
            (physicalHintButtonV79
                ? "需要帮助：食指按下左前方「提示」按钮"
                : "需要帮助：拉动台灯绳");
    }
}
