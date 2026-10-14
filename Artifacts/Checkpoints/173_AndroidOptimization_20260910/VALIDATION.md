# Android 包体优化与交付检查 — 2026-09-10

状态：本轮优化、构建、编辑器流程验证及人工画面检查完成。正式版本和用户佩戴验收尚未完成。

## 实测包体

| 版本 | APK 字节数 | 十进制 MB |
| --- | ---: | ---: |
| 原版 V80 | 443157258 | 443.157 |
| 第一轮 ASTC | 379268287 | 379.268 |
| 第二轮 1K | 276759393 | 276.759 |
| 最终恢复工作台 2K | 280115601 | 280.116 |

最终减少 163041657 字节，36.7909%。SHA-256：`346d6e023f6324d8af4d790ba3abcc9d5d2ea14be2d45a80b974fcf76e37c496`。
APK：/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Builds/PersonallyPuzzledVR-PICO-OptimizedV81.apk。
已按只保留最后版本的要求删除被替代的旧 APK，保留测量与哈希记录。

## 为什么大，做了什么

原始构建的用户资源：贴图 440.2 MiB（约 69%）、模型网格 170.3 MiB（约 27%）、音频 5.7 MiB。这些是打包前资源大小，不等于最终 ZIP/APK 文件大小。清理历史仓库文件不会缩小仍被运行场景引用的资源。

对 106 张材质贴图应用 Android 专用配置：颜色/法线/自发光 ASTC 6x6，金属度与光滑度 ASTC 8x8。普通材质上限 1K；标题、手部和工作台颜色保留 2K。贴图降至 95.5 MiB（减少约 78.3%）；模型网格仍为 170.3 MiB。548 个原始资源哈希一致；所有备份纹理 metadata 的 Android 区块之外内容一致。原始图片、材质、场景和网格未改动。

第二轮的工作台中央网格明显偏糊，人工检查拒绝该画面。最终恢复工作台高清颜色贴图后仅增加约 3.36 MB。非核心材质细节有所降低，不宣称无损压缩。继续大幅缩包应优先审查高面数网格、重复展示模型引用和字体；需要单独做几何及近距离画质验收。

新木质/黄铜/青绿色拼装图标已设置到 18 个 Android 普通、圆形和自适应槽位。实际 APK PNG 与生成的 launcher 资源像素匹配。Unity 默认启动画面及 Logo 已关闭；游戏自己的 MODEL BUILDER 场景保留。

## 验证证据

- 构建：/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/capture-build-r03.log，退出 0，PICO BUILD PASSED，指定外部 SDK/JDK/NDK 的统一策略生效。
- 独立场景验证与最终完整流程：/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/validate-flow-final.log，退出 0，11 scenes / missing-scripts=0 / missing-references=0 / missing-active-materials=0；五关 13/13/10/9/8 部件完成、Completion、Collection、返回选关、存档磁盘写入/重新读取均通过。
- 实际输入路径回归：/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/interaction.log，退出 0；标题点击边界、V78 两次捏合确认、53 个部件的抓取/移动/错误释放回退/正确释放吸附、完成页实体返回按钮通过。
- 音频证据：上述最终流程逐场景检查背景音乐 AudioSource 播放与有效音量，检查五关音乐池不连续重复。未进行耳机主观试听或声学录音，不宣称真机听感验收。
- 最终前视截图：/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/ANDROID_OPTIMIZATION_FINAL.png。
- 最终左眼截图：/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/Final/Runtime_Splash_20260807_V80_ZeroLatencyAssembly_LEFT.png。
- 对比叠图：/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/Comparison/OVERLAY.png。
- 放大差异：/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/Comparison/DIFFERENCE_X4.png。
- 20 张渲染尺寸与数值对比：/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/render-comparison.json，最大平均 RGB 差 1.934/255；数值只作辅助，不能替代人工画质判断。
- 人工原尺寸检查：最终 Splash 前视/左眼、Eiffel 左眼、Cottage 前视、Selection 前视及实际包内图标已检查；工作台网格恢复清晰，模型、文字与构图保留，未发现新粉色材质或缺失内容。其余场景全景对比图：/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/Comparison/ALL_FRONTS.png。
- APK：ZIP CRC PASS、签名 v2 PASS、ARM64、min SDK 29、target SDK 35，PICO 手部/OpenXR manifest 保留。/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/apk-signature.txt 与 /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/apk-manifest.txt。

## 离正式版本还差什么

1. **收藏入口未接通**：当前 Completion 的旧 PolishedRuntimeStagePortalV02 被禁用（serialized m_Enabled=0），活动的 RuntimeCompletionExitV69 只返回 Selection。Collection 内部流程能跑，但自动测试调用 API 不等于玩家能找到入口。本轮未擅自更改完成页设计，需要补充明确的收藏入口并做实际输入验证。
2. **真机验收缺失**：ADB 没有连接设备。本轮未安装到 PICO，未测真实手部追踪、冷启动/暂停恢复、持续帧率、发热、内存峰值和长时间佩戴。编辑器 OpenXR loader 因没有运行时输出环境错误；这不是头显验证成功的证据。
3. **发布配置仍是测试状态**：证书为 Android Debug，versionCode=1、versionName=1.0。正式发行前应使用项目正式签名与版本递增策略；当前 APK 用于安装体验。
4. **内容边界**：运行版本是五个可玩关卡；飞机仍标注“模型预览 未开放”。独立 Garden Cottage V76 研究场景未替换游戏里的 Cottage。若这些属于最终交付范围，还需要接入与验收。

用户视觉及佩戴验收：PENDING。该检查点记录本轮完成，不标记整个游戏正式发布已接受。未修改或提交 Git 历史，未推送。

## 可恢复性

Android 导入 metadata 与 PlayerSettings 原件：/Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Artifacts/Checkpoints/173_AndroidOptimization_20260910/Backup。场景仍是 /Volumes/APFS/Unity/Projects/PersonallyPuzzledVR/Assets/Scenes/RuntimeV80_ZeroLatencyAssembly 中十个独立 V80 场景；Garden V76 为第十一个验证场景。该优化通过平台导入配置完成，不创建重复场景或复制原始模型。回滚需恢复 metadata 和 PlayerSettings 后重新导入/构建。
