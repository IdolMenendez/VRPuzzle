# 花园小屋：无 Blender 的“原图 → 完整部件 → Unity 矩阵组装”V01

日期：2026-08-19  
状态：`UNITY BUILD / GPU MULTIVIEW / STATIC VALIDATION PASSED；等待用户视觉验收`

## 1. 本次实际完成的技术路线

本场景没有把一份完整 FBX 裁碎，也没有用 Blender、布尔切割或网格级拆件。

1. 保存用户原图，并先生成一张构图、材质和色板统一的干净母图。
2. 从母图与原图中提炼九个本身完整、外观可独立成立的语义部件图：完整花园地基；完整小屋墙体；完整蓝瓦屋顶与烟囱；完整遮阳棚花店；完整树木；完整花藤；完整围栏与招牌；完整园艺工作台；完整邮箱、黑板与工具组。
3. 九张部件图分别生成九份独立 3D。完整母体 3D 只做风格与结构参照，不参与最终英雄模型拼装。
4. 生成服务端将 GLB 转成带贴图 FBX；没有经过本地网格编辑。
5. Unity 原生 `ModelImporter` 导入九个 FBX，并提取各自 base-color、normal、metallic、roughness 贴图。最终英雄层只允许引用这些生成 FBX。
6. Builder 对每个部件计算可审计的相似变换矩阵，并同时生成组装态和爆炸态预制体。

## 2. Unity 自动配准算法

实现文件：

`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Editor/GardenCottageNoBlenderV01Builder.cs`

对每个完整部件执行：

1. 遍历全部 Renderer 的 `localBounds` 八个角点，将它们变换到该部件根节点坐标系，得到统一局部包围盒。
2. 枚举保持直立的 yaw 候选；同时保留 X/Z 轴纠正候选用于处理导出坐标轴异常，但对横躺候选施加强惩罚，防止围栏、招牌或房屋被错误放倒。
3. 对候选旋转后的包围盒尺寸与语义目标尺寸计算对数比例残差：`score = Σ(log(targetAxis/sourceAxis) - meanLogScale)^2`。
4. 选择最低分的直立朝向；对正反面存在同尺寸解的部件，用从原图抽取的语义正面方向作确定性 tie-breaker。
5. 使用统一比例，不进行 X/Y/Z 非等比拉伸：`s = 0.985 × min(targetSize.x/rotatedSize.x, targetSize.y/rotatedSize.y, targetSize.z/rotatedSize.z)`。
6. 计算平移：`t = targetCenter - up × overlap - rotatedBounds.center × s`。
7. 最终矩阵：`M = T(t) × R(q) × S(s)`。
8. 花园地基按宽深等比放大，并把生成时偏厚的下半部受控埋入物理地面；它自身就是唯一可见底座，不再叠加外置矩形展台。其余完整部件保留 0.055–0.095 m 受控交叠，用于隐藏独立生成模型之间不可避免的微小接缝。

每个部件最终选中的旋转、统一缩放、语义目标中心与 4×4 矩阵均记录在：

`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/GardenCottage/V01/GardenCottage_V01_AssemblyMatrices.json`

## 3. 生成与供应商记录

本轮曾尝试 Meshy，但当前执行环境无法稳定连通 Meshy API。为避免同一模型族混用两个供应商造成材质和造型风格漂移，本轮锁定 Tripo 作为单一回退供应商。凭据只在交互进程内读取，没有写入脚本、元数据或项目文件。

生成结果包括一份完整母体和九份完整语义部件。服务端 `convert_model` 的十项 FBX 转换全部成功：

`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/94_GardenCottage_NoBlender_V01_20260819/Provider/TRIPO_FBX_CONVERSION_SUMMARY.json`

部件 03 的第一次轮询超时，随后以新的已完成任务恢复；最终权威记录为：

`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/94_GardenCottage_NoBlender_V01_20260819/Provider/Part_03_BlueRoofChimney_TRIPO_TASK.json`

## 4. Unity 产物

- 独立场景：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/GardenCottage_20260819_V01_NoBlenderGeneratedAssembly.unity`
- 同内容备份：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/Backups/GardenCottage_20260819_V01_NoBlenderGeneratedAssembly_VALIDATED_BACKUP.unity`
- 组装预制体：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/GardenCottage/V01/Prefabs/GardenCottage_V01_Assembled.prefab`
- 爆炸预制体：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/GardenCottage/V01/Prefabs/GardenCottage_V01_Exploded.prefab`
- 母图与原图：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/GardenCottage/V01/Source`
- 九张完整部件图：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/GardenCottage/V01/PartSources`
- 九份最终部件 FBX：`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/GardenCottage/V01/Parts3D`

## 5. 验证结果

Unity 自动验证结果为 `PASSED`：组装态与爆炸态各九个完整语义模块；英雄层全部几何来自部件 FBX；部件与其支撑间距在合同容差内；矩阵、预制体、六张 GPU 证据存在且有效；场景和备份 SHA-256 一致：`e121f877d308c4aea581f620016dc4433ad4df612934156ad30b735d2572a43d`。

独立视觉 reviewer 对最终去除外置矩形展台的版本给出 `PASS`：正、左前、右前、后视均接地自然，九个完整语义部件在装配态和爆炸态中均可辨，未发现横躺、非预期悬浮、粉色错误材质、黑面、纸片或环绕视角背景穿帮。非阻断差异是正视图下缘略紧、吊牌/黑板文字面为空白，以及摄影棚墙地折线仍可见。

权威验证文档：

`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/94_GardenCottage_NoBlender_V01_20260819/VALIDATION.md`

最终正视图：

`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/94_GardenCottage_NoBlender_V01_20260819/GardenCottage_V01_FRONT_FINAL.png`

多角度、爆炸态与参考图对照表：

`/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/94_GardenCottage_NoBlender_V01_20260819/GardenCottage_V01_CONTACT_SHEET_FINAL.png`
