# V61 计划：选关反馈 + 3D确认 + 左手控制台 + 完成特效 + 星级评分（含 4 项补充改动）

> 本文件是**可交接执行计划**。每一步都带「验收检查点」，接手 agent 完成一步即可用检查点确认，再继续下一步。
> 语言：中文说明 + 英文标识符/路径。所有路径为绝对路径根 `/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR`。

## 0. 背景与权威上下文（先读）

- 续作状态：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Docs/CONTINUATION_STATE.md`（顶部 V60/V60.1/V60.2 段落为最新）。
- 资产分解标准：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Docs/PUZZLE_ASSET_DECOMPOSITION_STANDARD_20260716_V01.md`。
- 阶段校验索引：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Docs/STAGE_VALIDATION_INDEX.md`。
- 项目 Skill（按序）：`ppvr-scene-continuity` → `ppvr-reference-art-direction` → `ppvr-generated-3d-pipeline` → `ppvr-unity-visual-qa`。
- 当前已装机版本：**V60.2**，APK `Builds/PersonallyPuzzledVR-PICO-RetroBellAndGrabFeelV60.apk`，sha256 `d69a79760a3f9e9eab2e3d0f9dbc03a6ff6c45f8ca828fadf55976543b54661a`，设备 Swan `PB321XKGL4210087B`。
- 本计划全部改动进 **V61**（用户要求一次全做）。V61 独立十场景，V60 不动（连续性契约）。

### 环境关键事实
- Unity: `/Volumes/APFS/Unity/Hub/Editor/6000.0.77f1/Unity.app/Contents/MacOS/Unity`
- adb: `/Users/bytedance/Library/Android/sdk/platform-tools/adb`
- Gradle 增量缓存易失败：出包前删 `Library/Bee/Android/Prj/IL2CPP/Gradle/{launcher,unityLibrary}/build`，并 `pkill -f org.gradle.launcher.daemon`。
- `-batchmode` 无 GPU：Splash 截图用 `CaptureSplashOnly` 在图形模式补拍。
- 外网：**Meshy 连不上**（DNS 被劫持到 Meta IP）；**Tripo 可连**（`https://api.tripo3d.com/v2/openapi`，key 走 env `TRIPO_API_KEY`，`pbr=false`）。CDN 下载慢（~26KB/s）用 `curl -C -`。
- Tripo 已生成复古台铃 GLB：`Assets/Splash/Generated3D/DeskBell_V01_Hero.glb`（41MB，留作参考，本轮铃铛已改为 3 个按钮，见步骤 4）。
- **密钥安全**：`msy_H1JP…` / `tsk_BLf…` 已在对话记录，绝不写入任何文件/日志；提醒用户轮换。

## 1. 用户需求（本轮全部，共 9 条）

原始 5 条：
1. 选关界面：看向哪个模型，该模型出现对应特效/反馈。
2. 点击选关后要有 **3D 确认牌**再进入，不能直接进。
3. 拼接过程要回退/控制台 —— **最终定为**：去掉铃铛，改为**左手专属的 3 个实体复古按钮**（中文标注）：**重置 / Home / 提示**。不做“撤销上一件”。左右手不可都交互，**只有左手**能按。
4. 拼接完成后要有**特效 + 完成**反馈（关卡内，不只在 Completion 场景）。
5. 完成后回选关界面，每关显示 **1–5 颗星**评分 + 独立的 **3D「完成」标志** + 独立的星星标志。

补充 4 条（第二轮追加）：
6. 各模型流程要统一：现在有的有展示有的没有；“想拿起来看却莫名进入下一关” → **选关只能靠确认牌进入，看模型不触发进入**。
7. 每关零件检查：很多零件**重合**或**沉到桌面下**；桌上零件**全部 1:1**（不要拿起才恢复 scale）。→ **1:1 + 重新布局消重合**。
8. 拼接中模型要能**旋转**，放在离用户近处，避免侧看/绕后。→ **右手抓转盘 + 左手控制台共存**。
9. **每个按钮触碰都要音效**。

### 关键决策（已和用户确认）
- 星级：**时间 + 提示次数映射到 5 档**（步骤 5 给出映射表）。
- 左手控制台：**重置 / Home / 提示** 三个实体按钮，中文，仅左手食指触碰。
- 旋转：**右手抓转盘 + 左手控制台共存**（需防右手抓件/抓盘冲突）。
- 进关：**看模型不进，只有确认牌能进**（保留“居中→再确认”两步，但第二步是明确的 3D 确认牌）。
- 零件：**1:1 + 重新布局消重合**。

## 2. 现状分析（Phase 1 探索结论，均已核实行号）

### 选关 / 进入
- 命中：`Assets/Scripts/RuntimeIntentRayV46.cs` `StableSelectionTargetV46`（L98）= 当前注视展品；**无 hover 回调**，需自建“进入/离开”合成。
- 展品对象：`Assets/Selection/Scripts/SelectionExhibitTargetV09.cs`，`RequestFocus()`（L24-39）是唯一入口；`centerOnlyEntersPuzzleV38`（L19）控制“未居中→只居中”。
- 视觉根：`Assets/Selection/Scripts/SelectionCarouselController.cs` `exhibits[]`（L15），`CurrentExhibit`（L122），`TryGetSelectionBoundsV46`（RuntimeIntentRayV46 L218）可取包围盒放特效。
- 进入链：`RequestFocus` → `SelectionDirectPuzzleIntentV31.RequestEnterExhibit`（L38）→ `FocusThenEnter`（L58）→ `PolishedRuntimeStagePortalV02.TryConfirmCurrentSelectionForV31`（L383）→ `HandleConfirmedAction`（L242, Selection 分支 L270-343）→ `selectionPreviewGate.RequestCommit`（L313，返回 false 则不进）→ `QueueStageAdvance`（L338）→ `Update`（L142）`bootstrap.BeginLevel` → `RequestStage`。
- 现成两步门：`Assets/Scripts/SelectionPreviewGateV07.cs` `RequestCommit`（L70）——**这是插 3D 确认牌的接缝**。
- 选关星级显示接缝：`Assets/Scripts/RuntimeSelectionContinuityV05.cs` `Refresh`（L55），已有 `levelByExhibit`（L13）、`bootstrap.Save`（L66）；当前只改文字牌，**未读 stars**。

### 拼接 / 完成 / 星级
- 完成检测：`Assets/Scripts/RuntimePuzzleLevelController.cs` `NotifySnapped`（L77-87），最后一件 → `completed=true`（L84）→ `CompleteAfterCeremony`（L104-113）→ `bootstrap.CompleteCurrentLevel`（L112）。**无 C# 完成事件**（`completed` 私有，L26）。关卡内**无粒子/特效**，只有文字。
- 星级写入：`Assets/Scripts/PersistentPuzzleBootstrap.cs` `CompleteCurrentLevel`（L532-540），当前 1-3 档：`hintsUsed==0 && time<=StarTime ? 3 : hintsUsed<=2 ? 2 : 1`（L534）；`Save.stars[level]=Max(...)`（L535）；`StarTime`（L965-969）。`stars` 是 `int[5]`（L29）。
- 完成场景：enum `RuntimePuzzleStage.Completion`（L19），scene `Runtime_Completion_20260716_V01`（L114）。`Assets/Assembly/Scripts/RuntimeCompletionPresentationV23.cs` 有星星 FX 但**硬编码 3 颗、不读 Save.stars**（L22, L92-109）。`CompletionInteractionController.cs` 自由把玩转盘。
- 左手：`Assets/Scripts/RuntimePuzzleHandInteractor.cs` `leftHand`（L10），`HolderId`（L56）；左手已负责**转盘抓取**（`if(leftHand)` L311-342 / L630-650）和**非捏合指尖拍击**（`TryGetIndexTipWorldV59` L174-181 → `TryFingertipLampTapAtV59` L255-273）。`visual`（L11）随手每帧定位。
- 转盘：`Assets/Scripts/RuntimePuzzleBaseRotator.cs`，场景对象非手挂；`CanGrab/BeginGrab/MoveGrab/EndGrab`（L38-70）；`grabRadius`（L8）。
- 铃铛/提示：`Assets/Scripts/RuntimeHintLampPullV13.cs` `TryFingertipTapV25`（L303），`tapTogglesHintsV59`（L41）翻转 `RuntimePuzzlePiece.HintsEnabledV59`。V60.2 已把铃铛建于 lampRoot（`RetroBellAndGrabFeelV60Builder.BuildRetroDeskBell`）。
- 零件 scale：`RuntimePuzzlePiece.cs` `grabAtAssembledScaleV58`（L78，现 public）；`authoredWorldScale`（L67）vs `assembledWorldScale`（L70）；`ApplyWorldScale`（L763）；`ResetForPlay`（L280）。
- 音频：`RuntimeAudioCue.Touch`（RuntimeAudioConductor.cs L8），`RuntimeAudioRouter.PlayCue(cue, pos)`（L25）。

### 当前构建器（V60）
- `Assets/Editor/RetroBellAndGrabFeelV60Builder.cs`：源=V59 场景，产物 `RuntimeV60_RetroBellAndGrabFeel`；`ConfigureStageInteractionFields` 已遍历 5 关卡场景写字段 + 建铃铛 + Selection 关阴影；`Validate()` 断言字段/铃铛/灯已落地。**V61 构建器基于它派生。**

## 3. 版本与构建器（步骤 0）

**Step 0：创建 V61 构建器骨架**
- 复制 `Assets/Editor/RetroBellAndGrabFeelV60Builder.cs` → `Assets/Editor/SelectionFeedbackAndAssemblyConsoleV61Builder.cs`。
- 类名 `SelectionFeedbackAndAssemblyConsoleV61Builder`；菜单 `Personally Puzzled XR/Selection Feedback And Assembly Console V61`。
- 常量：`SourceDir="Assets/Scenes/RuntimeV60_RetroBellAndGrabFeel"`，`RuntimeDir="Assets/Scenes/RuntimeV61_SelectionFeedbackAndAssemblyConsole"`，`BackupDir`、`CheckpointDir="Artifacts/Checkpoints/73_SelectionFeedbackAndAssemblyConsole_V61_20260804"`，`OutputApk="Builds/PersonallyPuzzledVR-PICO-SelectionFeedbackAndAssemblyConsoleV61.apk"`。
- `SourceScenes` 用 V60 文件名；`RuntimeScenes` 用 V61 文件名（`Runtime_<Stage>_20260804_V61_SelectionFeedbackAndAssemblyConsole.unity`）。
- 全文件 `V60→V61` 字符串替换（保留 SourceScenes 指向 V60），版本证据串 `v61_`。
- **保留 V60 全部既有配置**（returnHomeOnResumeV25=false、rightHandOnly、Selection 去阴影、字段写入等）。
- ✅ **检查点 0**：`Build And Validate` 菜单跑通，日志出现 `STATIC VALIDATION PASSED` 且 `RuntimeV61_...` 十场景生成、backup hash 匹配。命令：
  `"$UNITY" -batchmode -projectPath . -executeMethod SelectionFeedbackAndAssemblyConsoleV61Builder.BuildAndValidate -logFile <ckpt>/V61_BuildAndValidate.log -quit`

## 4. 分步实现（每步含改动点 + 验收检查点）

> 顺序建议：先做纯代码运行时组件（可 Build And Validate 快速验证），再做构建器场景装配，最后统一出包。**每步都要能被下一个 agent 独立确认。**

---

### Step 1 —（需求7）零件 1:1 + 重新布局消重合

**目标**：桌上零件全部以装配 1:1 摆放；消除重合、上浮沉桌面下的零件。

**改动**
- `Assets/Scripts/RuntimePuzzlePiece.cs`：
  - 新增 `public bool tableAtAssembledScaleV61 = true;`（默认 true）。
  - 在 `ResetForPlay()`（L280）与 `ApplyAuthoredStartPose()`（L537）中，当 `tableAtAssembledScaleV61 && assembledScaleCaptured` 时，托盘/授权姿态直接 `ApplyWorldScale(assembledWorldScale)`（不再用 `authoredWorldScale` 迷你件）。
  - 兼容：`grabAtAssembledScaleV58` 保留但当 `tableAtAssembledScaleV61` 时抓取不再改变 scale（已经是 1:1）。
- 新建构建器步骤 `RelayoutLevelPartsV61(scene)`（在 V61 builder，遍历 5 关卡）：
  - 对每关 `RuntimePuzzleLevelController.pieces`：读 `authoredPosition`；把所有件按 `assembledWorldScale` 归一后，用**网格/圆弧托盘布局**重新摆放在桌面上方（桌面 Y 用现有 lamp/exhibit 参考或 socket 平均 Y），间距 = 各件包围盒最大边 × 1.15，消除重合；确保最低点 ≥ 桌面 Y（不沉桌下）。
  - 调 `piece.CaptureAuthoredPose()` 固化新托盘姿态；写 `tableAtAssembledScaleV61=true`。
  - 断言：任意两件世界包围盒不相交（容差）、所有件 `min.y >= tabletopY - ε`。

**验收检查点 1**（Build And Validate 后，用脚本读 V61 Cottage 场景）：
- 每件 `tableAtAssembledScaleV61: 1`；托盘 scale == assembled（比值≈1）。
- 脚本校验：13 件两两包围盒不重叠；无件 y < 桌面。
- 构建器日志打印 `[V61] relayout: level=Cottage pieces=13 overlaps=0 belowTable=0`。

---

### Step 2 —（需求2+6）3D 确认牌进关，看模型不进

**目标**：注视/靠近展品**不进关**；居中后必须在**独立 3D 确认牌**上再触发一次才进。

**改动**
- 新建 `Assets/Scripts/SelectionEnterConfirmV61.cs`（运行时组件，挂 Selection 场景）：
  - 字段：`SelectionCarouselController selection; RuntimeIntentRayV46 ray; Transform confirmPlaqueRoot; TextMesh confirmLabel; Collider confirmCollider;` + 中文文案“确认进入 / CONFIRM ENTER”。
  - 逻辑：当有居中展品时显示确认牌（浮在展品前下方，面向玩家）；仅当**捏合命中确认牌 collider**（右手捏合射线）或**左手指尖触碰确认牌**时，才调用 `directPuzzleIntentV31.RequestEnterExhibit(currentIndex)`（或直接 `portal.TryConfirmCurrentSelectionForV31`）。
  - 未居中时：注视+捏合只 `BeginSwapToIndex` 居中，不进关。
- `SelectionExhibitTargetV09` / `SelectionPreviewGateV07`：设 `centerOnlyEntersPuzzleV38=true` 且 `singleConfirmWhenCentered=false`，使“居中后再触发”成立；把最终 commit 交给确认牌（`SelectionEnterConfirmV61` 调用 `RequestCommit` 或直接进入路径）。确保**只有确认牌**能触发 `BeginLevel`。
- 构建器 `ConfigureSelectionEnterConfirmV61(selection)`：实例化确认牌（可用 primitives + TextMesh，`SelectionExhibit` 层外的独立层避免与选关命中混淆），接引用，禁用任何“居中即进”的旧路径（检查 `SelectionRemoteConfirmV33` / `SelectionExhibitInputV09` 是否会直接进，必要时关掉其直接进入）。

**验收检查点 2**：
- 场景中存在 `SelectionEnterConfirmV61` + 确认牌对象；`centerOnlyEntersPuzzleV38: 1`（全展品）。
- 真机日志（或 editor smoke）：注视展品 N 秒 + 捏合，只出现 `BeginSwapToIndex`/居中日志，**无** `BeginLevel`；只有确认牌交互后才出现 `[Portal] ... BeginLevel`。
- 断言：`Components<SelectionEnterConfirmV61>(selection).Any()`。

---

### Step 3 —（需求1）选关注视特效

**目标**：注视某展品时该展品出现反馈特效（如暖色描边光/上升粒子/轻微放大脉冲）。

**改动**
- 新建 `Assets/Scripts/SelectionGazeHighlightV61.cs`（挂 Selection 场景）：
  - 引用 `RuntimeIntentRayV46 ray; SelectionCarouselController selection;`。
  - 每帧读 `ray.StableSelectionTargetV46`，与缓存比较，合成 `OnGazeEnter(index)/OnGazeExit(index)`。
  - 进入：对 `selection.exhibits[index]` 施加特效——建议轻量：一个跟随包围盒顶的 `Light`（暖色）渐亮 + 可选 `ParticleSystem` 上升微粒 + 展品 `localScale` 脉冲（±3%，不破坏布局）。用 `ray.TryGetSelectionBoundsV46` 定位。
  - 离开：渐隐恢复。
  - **音效**：进入时 `RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Hint, pos)`（或 Touch）。
- 构建器 `ConfigureSelectionGazeHighlightV61(selection)`：实例化每展品特效根 + 接引用。

**验收检查点 3**：
- 场景存在 `SelectionGazeHighlightV61`；每展品挂有特效根（Light/PS）。
- 真机：看向侧边展品，该展品亮起/微粒/脉冲；移开注视恢复；有音效。
- 断言：`Components<SelectionGazeHighlightV61>(selection).Any()` 且特效根数 == 展品数。

---

### Step 4 —（需求3+8+9）左手 3D 复古控制台（重置/Home/提示）+ 右手转盘旋转 + 按钮音效

**目标**：去掉铃铛，改左手专属 3 个复古实体按钮（中文：重置、Home、提示）；右手可抓转盘旋转拼接中模型（转盘移近用户）；所有按钮触碰有音效。

**交互图**（写入本计划，构建器按此装配）：
```
左手腕前方 ~12cm 处，一块复古木质斜面控制台（随左手 visual 移动或固定在桌左侧，二选一见下）：
┌──────────────────────────────┐
│  [重置]     [Home]     [提示]   │   ← 3 个圆形黄铜实体按钮，顶面中文刻字
│  RESET      HOME       HINT    │
└──────────────────────────────┘
- 仅左手食指尖(TryGetIndexTipWorldV59(true)) 触碰触发；右手/捏合不触发。
- 触碰任一按钮：下压动画 + RuntimeAudioCue.Touch 音效 + 触感脉冲。
- 重置 → level.ResetLevel()（全关归位）。
- Home  → bootstrap.RequestStage(Selection)（回选关；等价现有回家路径）。
- 提示 → 翻转 RuntimePuzzlePiece.HintsEnabledV59（沿用铃铛原逻辑）。
右手转盘：RuntimePuzzleBaseRotator 移到离用户近处（socket 群前方 ~0.4m），右手靠近抓握旋转。
```
决策：控制台**固定在桌左前方**（世界空间，非手挂），只允许左手指尖触碰——最稳、不随手抖动；与需求“放离用户近处”一致。

**改动**
- 新建 `Assets/Scripts/RuntimeAssemblyConsoleV61.cs`：
  - 3 个按钮：每个含 `Collider` + 世界坐标；`TryFingertipPressV61(Vector3 leftIndexTip, ButtonId)`（半径参考 `fingertipTapRadiusV25`≈0.06-0.075，命中即触发，带 0.5s 去抖 + 下压动画）。
  - 引用 `RuntimePuzzleLevelController level; PersistentPuzzleBootstrap bootstrap;`。
  - 每次命中 `RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Touch, buttonPos)` + `RuntimeHaptics.PulseAll`。
  - 三个动作如交互图。
- `Assets/Scripts/RuntimePuzzleHandInteractor.cs`：在 `TryFingertipLampTapAtV59`（L255）里，除现有 compass/lamp 外，增加对 `RuntimeAssemblyConsoleV61` 的左手指尖派发；**保证右手不触发**（该方法仅左手调用，已满足）。
- 需求8 旋转：构建器把每关 `RuntimePuzzleBaseRotator` 的 `pivot`/`gripAnchor` 移到 socket 群前方近处；确认右手 `RuntimePuzzleHandInteractor` 非 leftHand 分支能抓转盘（目前抓盘在 `if(leftHand)`，需**放开给右手**或新增右手抓盘路径——注意与右手抓零件冲突：优先抓零件，附近无零件且靠近转盘 grip 时抓盘）。
- 移除/停用铃铛：V61 构建器不再调 `BuildRetroDeskBell`（或改造为 `BuildAssemblyConsoleV61`）；隐藏原 lamp 渲染/灯光沿用 V60.2 逻辑。
- **全局按钮音效**：确认牌（Step2）、控制台三键、选关特效触发都接 `RuntimeAudioCue.Touch`。

**验收检查点 4**：
- 场景存在 `RuntimeAssemblyConsoleV61`（3 按钮，中文 TextMesh：重置/Home/提示）；无铃铛对象。
- 转盘 `RuntimePuzzleBaseRotator` 位置在 socket 群前方近处（脚本读坐标验证）。
- 真机日志：左手指尖按“重置”出现 `[V61 Console] press=reset` + level 归位；“Home”回选关；“提示”翻转 `HintsEnabledV59`；右手按无反应；每次按有 Touch 音效（听感 + 日志 cue）。右手靠近转盘可旋转拼接中模型。
- 断言：`Components<RuntimeAssemblyConsoleV61>(level).Any()` 且按钮数==3。

---

### Step 5 —（需求4）拼接完成关卡内特效 + 完成信号

**目标**：最后一件放好时，关卡内立即出现特效（粒子爆发/光晕/星光）+ 明确“完成”反馈，再进 Completion。

**改动**
- `Assets/Scripts/RuntimePuzzleLevelController.cs`：
  - 新增 `public System.Action OnLevelCompletedV61;`，在 `NotifySnapped` 置 `completed=true` 处（L84）触发。
  - 新增 `public Transform completionFxRootV61; public ParticleSystem completionBurstV61; public Light completionGlowV61;`，在 `CompleteAfterCeremony`（L104）开头播放：粒子 + 光晕渐亮 + `RuntimeAudioCue.Complete` + `RuntimeHaptics.PulseAll` + 文字“完成 / COMPLETE”。
- 新建 `Assets/Scripts/RuntimeLevelCompletionFxV61.cs`（可选，封装 FX 播放，供构建器接引用）。
- 构建器 `ConfigureLevelCompletionFxV61(scene)`：每关在 socket 群中心上方建 FX 根（粒子+Light），接引用。

**验收检查点 5**：
- 每关 `RuntimePuzzleLevelController.completionFxRootV61` 已接；FX 根存在。
- 真机：放好最后一件 → 关卡内出现粒子/光 + “完成”文字 + 音效，之后才进 Completion。
- 断言：`Components<RuntimePuzzleLevelController>(level).All(l => l.completionFxRootV61 != null)`。

---

### Step 6 —（需求5）选关 1–5 星评分 + 3D 完成标志 + 星星标志

**目标**：选关界面每关显示 1–5 星（已完成的关）+ 独立 3D「完成」徽标 + 独立星星徽标。

**改动**
- 星级映射（需求已定“时间+提示映射 5 档”），改 `PersistentPuzzleBootstrap.CompleteCurrentLevel`（L534）：
  ```
  int stars;
  bool noHint = hintsUsed == 0;
  float t = StarTime(level); // 现有阈值
  if (noHint && elapsed <= t*0.6f) stars = 5;
  else if (noHint && elapsed <= t) stars = 4;
  else if (hintsUsed <= 1 && elapsed <= t*1.4f) stars = 3;
  else if (hintsUsed <= 3) stars = 2;
  else stars = 1;
  Save.stars[level] = Mathf.Max(Save.stars[level], stars);
  ```
  （5 档阈值可微调；关键是 1–5 且单调。）
- 选关显示：新建 `Assets/Scripts/SelectionStarBadgeV61.cs`（挂 Selection，或扩展 `RuntimeSelectionContinuityV05`）：
  - 每展品上方建**星星徽标根**（5 个星 Transform，按 `Save.stars[level]` 点亮 N 个）+ **3D「完成」徽标**（已完成关卡显示，如金色勾/奖章 primitive + 中文“完成”）。
  - 用 `levelByExhibit` 映射；`bootstrap.Save.stars[level]`；未完成关卡（stars==0）不显示完成徽标、星星全灰。
  - 在 `Refresh`/切换展品时刷新。
- 构建器 `ConfigureSelectionStarBadgesV61(selection)`：每展品实例化星星根（5 星）+ 完成徽标，接引用。
- （可选）Completion 场景 `RuntimeCompletionPresentationV23` 的硬编码 3 星改为读 `Save.stars[level]` 点亮 1–5（新增 `achievementStars` 到 5 个）。

**验收检查点 6**：
- `CompleteCurrentLevel` 可产出 1–5（构造不同 hints/time 用例断言映射）。
- 选关每展品有 5 星根 + 完成徽标；已完成关按 stars 点亮对应数量、显示“完成”；未完成不显示。
- 断言：`Components<SelectionStarBadgeV61>(selection).Any()` 且每展品星数==5。

---

### Step 7 — V61 构建器整合断言 + 出包 + 装机 + 真机验证

**改动**
- V61 `Validate()` 增补断言（全部落地才 PASS）：Step1 布局无重合 + 1:1；Step2 确认牌存在 + centerOnly；Step3 注视特效根；Step4 控制台 3 键 + 无铃铛 + 转盘近位；Step5 完成 FX 根；Step6 星星徽标 5×展品 + 完成徽标 + 星级映射函数存在。
- `HandsOnly` manifest 审计沿用。
- 出包：删 Gradle 增量缓存 → `BuildPicoApk`（后台）→ `CaptureSplashOnly` 补真实 Splash 截图。
- 装机：`adb install -r -g` → 校验设备 base.apk sha256 == 本地。
- 真机 logcat：抓 9 项对应证据（见各步检查点）。
- 更新 `Docs/CONTINUATION_STATE.md`（V61 段）+ 写 `Artifacts/Checkpoints/73_.../VALIDATION.md`（独立场景、front/left-eye 截图、overlay、diff、日志、音频证据、人工目检、用户验收态）。

**验收检查点 7（最终）**：
- 日志 `APK AUDIT PASSED · sha256=<X> · HandsOnly`；装机 `HASH MATCH ✓`。
- 9 条需求逐条真机证据齐全，用户验收。

## 5. 假设与决策记录

- 控制台**固定在桌左前方**（世界空间），非手挂：最稳、符合“放近处”；仅左手指尖触碰（复用 `TryGetIndexTipWorldV59(true)`，右手被排除）。
- 进关**只认确认牌**：`centerOnlyEntersPuzzleV38=true`，关闭一切“居中即进”旧路径。
- 星级 5 档用**时间×提示**组合阈值（步骤 6 公式），保持 `Save.stars` int[5] 结构不变（值域扩到 1–5）。
- 零件 1:1 用**新字段 `tableAtAssembledScaleV61`**（默认 true，代码级生效，拷贝场景继承），布局在构建器重排并固化 authored 姿态。
- 旋转复用 `RuntimePuzzleBaseRotator`，右手新增“附近无零件才抓盘”避免与抓件冲突。
- 铃铛移除，其“提示开关”职能并入控制台“提示”键；`RuntimeHintLampPullV13` 逻辑保留但不再建铃铛外观。
- 全部按钮/确认/特效触发统一 `RuntimeAudioCue.Touch`（完成用 `Complete`，星光用 `StarLight`）。

## 6. 验证步骤汇总（executor 用）

1. 每步改完先 `Build And Validate`（快，无 APK）确认编译 + 断言。
2. 用 `python3`/`rg` 读 `Assets/Scenes/RuntimeV61_.../Runtime_Level_CottageHint_...unity` 校验序列化（字段/对象/坐标/无重合）。
3. 全部步骤过后 `BuildPicoApk`（后台，先删 Gradle 缓存）。
4. `CaptureSplashOnly` 补 Splash 截图并原分辨率目检。
5. `adb install -r -g` + sha256 hash 匹配。
6. 起 logcat（独立后台进程，`-T 1`），`am start` Unity Activity，佩戴验证 9 条。
7. 更新 CONTINUATION_STATE + VALIDATION.md。

## 7. 交接注意

- **不要**改 V60 场景/构建器（连续性契约，回滚安全）。
- **不要**持久化任何 API key；Tripo 可用、Meshy 不可用（本轮铃铛已被按钮取代，无需再生成模型；如需 3D 按钮高模可走 Tripo `pbr=false`）。
- 每完成一步，更新本计划对应检查点为「已确认」并在 CONTINUATION_STATE 追加一行，便于下一个 agent 断点续作。
- 提醒用户轮换 `msy_H1JP…` / `tsk_BLf…`。
