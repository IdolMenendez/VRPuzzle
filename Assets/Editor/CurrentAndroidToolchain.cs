using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// One policy for menu, batch and ordinary Unity Android builds.
public sealed class CurrentAndroidToolchain : IPreprocessBuildWithReport
{
    const string Sdk = "/Users/bytedance/Library/Android/sdk";
    const string Jdk = "/Library/Java/JavaVirtualMachines/jdk-17.jdk/Contents/Home";
    const string Ndk = "/Users/bytedance/Library/Android/sdk/ndk/27.2.12479018";
    public int callbackOrder => -10000;
    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android) Apply();
    }
    public static void Apply()
    {
        foreach (string path in new[] { Sdk, Jdk, Ndk })
            if (!Directory.Exists(path)) throw new BuildFailedException("Required Android toolchain missing: " + path);
        foreach (string key in new[] { "SdkUseEmbedded", "JdkUseEmbedded", "NdkUseEmbedded" }) EditorPrefs.SetBool(key, false);
        EditorPrefs.SetString("AndroidSdkRoot", Sdk);
        EditorPrefs.SetString("AndroidJdkRoot", Jdk);
        EditorPrefs.SetString("AndroidNdkRoot", Ndk);
        EditorPrefs.SetString("AndroidNdkRootR27", Ndk);
        Environment.SetEnvironmentVariable("ANDROID_HOME", Sdk);
        Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT", Sdk);
        Environment.SetEnvironmentVariable("JAVA_HOME", Jdk);
        Environment.SetEnvironmentVariable("ANDROID_NDK_ROOT", Ndk);
        Type settings = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
        if (settings == null) throw new BuildFailedException("Android build support missing.");
        Set(settings, "sdkRootPath", Sdk);
        Set(settings, "jdkRootPath", Jdk);
        Set(settings, "ndkRootPath", Ndk);
    }
    static void Set(Type settings, string name, string path)
    {
        PropertyInfo property = settings.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        if (property == null || !property.CanWrite) throw new BuildFailedException("Android setting unavailable: " + name);
        property.SetValue(null, path);
        if ((string)property.GetValue(null) != path) throw new BuildFailedException("Android setting rejected: " + name);
    }
}
