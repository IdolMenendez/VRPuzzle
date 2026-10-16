# PICO Android Gradle 交付

## 为什么之前无法打包

GitHub `main` 是 Unity 源工程，不是 Android Gradle 工程。只执行 Gradle 的服务器不能把 Assets / Packages / ProjectSettings 直接编译成 APK。Unity 的常规 Android 导出还包含与导出电脑系统相关的 IL2CPP 编译器，以及本地 SDK / NDK 路径。

本次保留 Unity 源码在 `main`，另以 `codex/pico-android-gradle` 提供根目录即可构建的 Android 工程。审核应选取该分支对应的提交，不要继续使用旧 Unity 源码提交 d408a7fc。

## 应用标识

| 字段 | 值 |
| --- | --- |
| 显示名称 | 拼接世界 |
| Android applicationId / 包名 | com.bytedance.personallypuzzled.xr |
| versionName | 1.0.0 |
| versionCode | 2 |
| 架构 / 运行时 | ARM64 / IL2CPP |
| 最低 / 目标 Android API | 29 / 35 |
| 图标 | V81 定制拼图图标，沿用 V82 交付资源 |
| Unity | 6000.0.77f1 |
| Android Gradle Plugin / Gradle / Java | 8.10.0 / 8.13 / 17 |
| Android Build Tools / NDK | 36.0.0 / 27.2.12479018 |

按用户要求保留旧包名。versionCode 从 1 增至 2；覆盖安装仍必须使用与旧包一致的签名。不同证书即使包名相同也不能直接覆盖，平台签名策略需保持一致。

## 审核服务器构建

```bash
git clone --depth 1 --branch codex/pico-android-gradle https://github.com/IdolMenendez/VRPuzzle.git
cd VRPuzzle
./gradlew :launcher:assembleRelease --no-daemon
```

工程根目录包含 `settings.gradle`、`build.gradle`、`gradlew`、`launcher`、`unityLibrary`。此交付分支不使用 Git LFS，拉取后即有真实二进制输入。服务器需配置 Java 17、Android SDK（使用 ANDROID_HOME / ANDROID_SDK_ROOT 或自己的 local.properties），安装表内 SDK/NDK 版本，并可访问 Google Maven、Maven Central 和 Gradle 分发服务。

Unity 的游戏代码、场景、资源和原生运行库已经由 Unity 导出并编译。Gradle 仍会正常编译 Android 启动层、合并资源/清单、打包并按配置签名；无需 Unity Editor、Unity 授权、CMake 或原电脑的 IL2CPP 可执行程序。没有把 IL2CPP 改成 Mono，也没有改变游戏运行时格式。

## 签名与输出

- `:launcher:assembleDebug` 生成自动测试签名的 APK，用于安装验证，不作为正式发布签名。
- `:launcher:assembleRelease` 默认生成 unsigned APK，供平台签名。不会把调试证书伪装成发布证书。
- 平台如果要求工程自行签名，通过环境变量同时注入 `PICO_KEYSTORE_FILE`、`PICO_KEYSTORE_PASSWORD`、`PICO_KEY_ALIAS`、`PICO_KEY_PASSWORD`。四项不完整时立即报错。证书及密码不上传仓库。
- Release 输出在 `launcher/build/outputs/apk/release/`，Debug 输出在 `launcher/build/outputs/apk/debug/`。

本次验证包括实际 Gradle 构建、APK 清单元数据、原生库与资源一致性、图标、ZIP 完整性以及测试签名。平台自身构建和最终审核结果仍需以 PICO 返回为准。

## 源码更新后如何重新交付

1. 在 Unity 源码仓库调整玩法或资源，保留现有统一外部工具链策略。
2. 调用 `LatestProjectTools.ExportPicoGradle`；可用 `PPVR_GRADLE_PATH` 指定全新导出目录。该入口恢复 PICO 手势配置、检查 11 个场景与字体，并在结束后恢复编辑器的导出模式。
3. 对原始导出执行 `:launcher:assembleRelease`，得到与当前导出匹配的原生库。
4. 使用 `Tools/prepare_pico_gradle_delivery.py`，传入原始导出、一个不存在的目标目录，以及官方 Gradle 8.13 Wrapper 所在目录。脚本从该次 APK 提取匹配的原生库，去除本机编译器、缓存、绝对路径和签名，并拒绝覆盖已有交付。
5. 从干净副本再次构建，验证资产/原生库与原始 APK 对应，再更新 Gradle 分支和交付记录。不要手工混用不同版本的 libil2cpp.so 与 global-metadata.dat。

参考：[Unity Android 工程导出](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/editoruserbuildsettings/exportasgoogleandroidproject)、[Gradle Wrapper](https://docs.gradle.org/current/userguide/gradle_wrapper.html)。
