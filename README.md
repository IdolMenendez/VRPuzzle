# 拼接世界 · PICO Android Gradle 工程

**本仓库 main 当前只提供 1.0.0（versionCode 2）的 Android Gradle 交付工程。** 它由 Unity 6000.0.77f1 的 Export Project 实际导出，再完成可独立构建的整理。根目录直接包含 `gradlew`、`settings.gradle`、`launcher` 和 `unityLibrary`，不需要先打开 Unity。

[下载完整标准 Unity 导出工程](https://github.com/IdolMenendez/VRPuzzle/releases/download/android-gradle-1.0.0-code2/PuzzleWorld-1.0.0-code2-Unity-Android-Gradle-Export.zip) · [当前可独立构建工程 ZIP](https://github.com/IdolMenendez/VRPuzzle/releases/download/android-gradle-1.0.0-code2/PuzzleWorld-1.0.0-code2-Portable-Gradle.zip) · [APK、校验值与交付记录](https://github.com/IdolMenendez/VRPuzzle/releases/tag/android-gradle-1.0.0-code2)

## 应用信息

| 项目 | 值 |
| --- | --- |
| 应用名称 | 拼接世界 |
| Android 包名 / applicationId | `com.bytedance.personallypuzzled.xr`，保留旧包名 |
| 版本名称 / versionName | `1.0.0` |
| 版本号 / versionCode | `2`，旧 V82 为 `1` |
| 支持设备 / 架构 | PICO Android VR / `arm64-v8a` |
| 运行时 | Unity IL2CPP，未改成 Mono |
| 最低 Android API | `29`（Android 10） |
| 目标 / 编译 Android API | `35`（Android 15） |
| 应用图标 | 已替换 Unity 默认图标，使用定制拼图图标 |
| 入口 | `com.unity3d.player.UnityPlayerActivity` |
| VR 配置 | PICO VR categories、OpenXR、手势追踪权限及配置 |

保留包名便于与旧版本保持应用身份一致；覆盖安装仍要求签名证书一致。APK 内已验证显示名称、包名、版本和 ARM64 架构。

## 构建依赖：准确版本

| 依赖 | 版本 / 要求 |
| --- | --- |
| JDK | **17**，配置 `JAVA_HOME` |
| Gradle | **8.13**，仓库自带官方 Wrapper，含分发 SHA-256 校验 |
| Android Gradle Plugin | **8.10.0** |
| Android SDK Platform | **platforms;android-35** |
| Android SDK Build Tools | **build-tools;36.0.0** |
| Android NDK | **27.2.12479018（r27c）** |
| SDK 路径 | 通过 `ANDROID_HOME` / `ANDROID_SDK_ROOT` 或自有 `local.properties` 设置 |
| 网络仓库 | Google Maven、Maven Central、Gradle 官方分发服务 |
| Unity Editor / 授权 | 当前 main 构建**不需要**；生成这些输入时使用 Unity **6000.0.77f1** |
| CMake | 当前 main **不需要**；原始标准 Unity 导出工程需要 **3.22.1** |
| Git LFS | 当前 main **不需要**，所有构建输入均为真实文件 |

可用 Android SDK Manager 安装：

```bash
sdkmanager "platforms;android-35" "build-tools;36.0.0" "ndk;27.2.12479018"
```

请在构建环境完成 SDK 许可配置。源码不会覆盖服务器的 SDK / JDK / NDK 安装路径，也不包含开发者电脑的 `local.properties`。

## 构建 APK

```bash
git clone --depth 1 --single-branch --branch main https://github.com/IdolMenendez/VRPuzzle.git
cd VRPuzzle
./gradlew :launcher:assembleRelease --no-daemon
```

Windows 对应命令为 `gradlew.bat :launcher:assembleRelease --no-daemon`。Android Studio 可直接导入仓库根目录。审核平台应拉取**当前 main 的提交**，而不是旧 Unity 源码提交 d408a7fc。

| 构建 | 命令 | 默认输出 |
| --- | --- | --- |
| 发布包，交平台签名 | `./gradlew :launcher:assembleRelease` | `launcher/build/outputs/apk/release/launcher-release-unsigned.apk` |
| 测试包，自动调试签名 | `./gradlew :launcher:assembleDebug` | `launcher/build/outputs/apk/debug/launcher-debug.apk` |

## 发布签名

Release 默认不签名，供平台采用其发布证书签名；**不会用调试签名冒充正式签名**。如要求工程自行签名，由构建环境同时注入：

- `PICO_KEYSTORE_FILE`
- `PICO_KEYSTORE_PASSWORD`
- `PICO_KEY_ALIAS`
- `PICO_KEY_PASSWORD`

四项齐全时 Release 会输出签名 APK（`launcher-release.apk`）；只提供部分参数会立即报错。证书和密码不得提交到仓库。本次发布的 Debug 测试包证书与旧 V82 相同；其他机器自行生成的调试证书不保证相同，正式版本须使用固定的平台证书或自有证书。

## Unity 导出与服务器适配

标准 Unity 导出压缩包保留 `launcher`、`unityLibrary`、IL2CPP 源码和编译器、CMake 配置及原始 Gradle 模板。它是编辑器实际导出的工程，需在接收环境设置 SDK/NDK 路径；本次导出的 IL2CPP 编译器来自 macOS。原始 Unity Release 模板使用调试签名，发布前应由平台接管签名。

main 使用同一次导出的场景和资源，并提前编译其 IL2CPP 与原生库；移除与本机系统相关的编译器、CMake 构建步骤和绝对路径。平台仍正常编译 Android Java 启动层、合并清单和资源、组装 APK 并签名。游戏运行时、模型、纹理和玩法没有因此更换。所有 542 个 APK 资源/原生库文件与原始 Gradle 导出构建的 APK 一致。

[输入来源与原生库哈希](DELIVERY.json) · [APK 验证结果](APK_VALIDATION.json)。Unity 源码保存在[历史提交 7c15c62](https://github.com/IdolMenendez/VRPuzzle/tree/7c15c62afab2ab44fbbfe0d07f9775a38770e8f7)；修改玩法后应在 Unity 中重新导出，不能混用不同版本的 `libil2cpp.so` 和 `global-metadata.dat`。

## 验证状态

- 本机不调用 Unity 的干净 Gradle 构建通过：Release + Debug，183 项任务，25 秒。
- Release APK：230,592,260 字节，未签名；Debug APK：237,203,714 字节，测试签名。
- APK ZIP 完整性、包名/版本/名称/ARM64、手势/OpenXR 权限、图标、542 个资源/原生库一致性通过。
- 20 张后台 Unity 截图与 V82 基线对比，最大平均 RGB 通道差异 0.006143 / 255，画面保持一致。
- PICO 服务端重新构建、正式签名及审核尚待平台验证；本次没有代为提交审核。

## 游戏画面

挑选喜爱的作品，拿起零件、旋转并拼合，在虚拟工坊中完成小屋、古典帆船、埃菲尔铁塔、宝塔和荷兰风车，五个主题共 53 个部件。

| 选关 | 风车拼装 |
| --- | --- |
| ![选关](Docs/Media/selection.jpg) | ![风车拼装](Docs/Media/windmill.jpg) |

[查看 46 秒实操预告片](https://github.com/IdolMenendez/VRPuzzle/releases/download/showcase-20261014/puzzle-world-trailer-v1-1080p.mp4)。视频为 V80 历史实操剪辑；截图来自本版本沿用的 V82 场景。

项目许可见 [LICENSE](LICENSE)；第三方 Unity/PICO/Android 运行库仍适用各自许可。
