using System.IO;
using System.Xml;
using UnityEditor.Android;

public sealed class AndroidGradlePostprocessor : IPostGenerateGradleAndroidProject
{
    const string Marker = "// PersonallyPuzzledXR duplicate OpenXR loader fix";
    const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
    const string ToolsNamespace = "http://schemas.android.com/tools";

    public int callbackOrder => 1000;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        var unityLibrary = new DirectoryInfo(path);
        var gradleRoot = unityLibrary.Parent;
        if (gradleRoot == null)
            return;

        PatchVrManifest(Path.Combine(unityLibrary.FullName, "src", "main", "AndroidManifest.xml"));
        var launcherGradle = Path.Combine(gradleRoot.FullName, "launcher", "build.gradle");
        if (!File.Exists(launcherGradle))
            return;

        var text = File.ReadAllText(launcherGradle);
        if (text.Contains(Marker))
            return;

        var packagingIndex = text.IndexOf("    packaging {", System.StringComparison.Ordinal);
        if (packagingIndex < 0)
        {
            var androidEnd = text.LastIndexOf("\n}", System.StringComparison.Ordinal);
            if (androidEnd < 0)
                return;

            var block =
                "\n" +
                "    packaging {\n" +
                "        jniLibs {\n" +
                $"            {Marker}\n" +
                "            pickFirsts.add(\"**/libopenxr_loader.so\")\n" +
                "        }\n" +
                "    }\n";
            text = text.Insert(androidEnd, block);
        }
        else
        {
            var jniIndex = text.IndexOf("        jniLibs {", packagingIndex, System.StringComparison.Ordinal);
            if (jniIndex < 0)
                return;
            var insertAt = text.IndexOf('\n', jniIndex);
            if (insertAt < 0)
                return;
            insertAt += 1;
            var line =
                $"            {Marker}\n" +
                "            pickFirsts.add(\"**/libopenxr_loader.so\")\n";
            text = text.Insert(insertAt, line);
        }

        File.WriteAllText(launcherGradle, text);
    }

    static void PatchVrManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            return;

        var doc = new XmlDocument();
        doc.PreserveWhitespace = true;
        doc.Load(manifestPath);
        var manifest = doc.DocumentElement;
        if (manifest == null)
            return;

        EnsureNamespace(manifest, "xmlns:tools", ToolsNamespace);
        EnsureUsesFeature(doc, manifest, "android.hardware.vr.high_performance", true);
        EnsureUsesFeature(doc, manifest, "android.hardware.vr.headtracking", true);
        EnsureUsesFeature(doc, manifest, "android.hardware.vulkan.version", true);
        EnsureUsesPermission(doc, manifest, "org.khronos.openxr.permission.OPENXR");
        EnsureUsesPermission(doc, manifest, "org.khronos.openxr.permission.OPENXR_SYSTEM");

        var application = SelectOrCreateChild(doc, manifest, "application");
        SetAndroidAttribute(application, "requestLegacyExternalStorage", "true");
        EnsureMetaData(doc, application, "pxr.sdk.generation", "4");
        EnsureMetaData(doc, application, "pvr.app.type", "vr");
        EnsureMetaData(doc, application, "use.pxr.sdk", "2");
        EnsureMetaData(doc, application, "pvr.sdk.version", "Unity_OpenXR_5140");
        EnsureMetaData(doc, application, "pxr.sdk.version_code", "5140");

        var activity = FindUnityActivity(application);
        if (activity != null)
        {
            SetAndroidAttribute(activity, "resizeableActivity", "false");
            SetAndroidAttribute(activity, "screenOrientation", "landscape");
            SetAndroidAttribute(activity, "launchMode", "singleTask");
            EnsureMetaData(doc, activity, "unityplayer.UnityActivity", "true");
            EnsureMetaData(doc, activity, "pvr.app.type", "vr");
            EnsureMetaData(doc, activity, "unity.xr.openxr", "true");
            RemoveMetaData(activity, "WindowManagerPreference:FreeformWindowSize");
            RemoveMetaData(activity, "WindowManagerPreference:FreeformWindowOrientation");
            EnsureVrCategory(doc, activity);
        }

        doc.Save(manifestPath);
    }

    static void EnsureNamespace(XmlElement element, string name, string value)
    {
        if (!element.HasAttribute(name))
            element.SetAttribute(name, value);
    }

    static XmlElement SelectOrCreateChild(XmlDocument doc, XmlElement parent, string childName)
    {
        foreach (XmlNode child in parent.ChildNodes)
        {
            if (child is XmlElement element && element.Name == childName)
                return element;
        }

        var created = doc.CreateElement(childName);
        parent.AppendChild(created);
        return created;
    }

    static void EnsureUsesFeature(XmlDocument doc, XmlElement manifest, string name, bool required)
    {
        foreach (XmlNode child in manifest.ChildNodes)
        {
            if (child is XmlElement element && element.Name == "uses-feature" &&
                element.GetAttribute("name", AndroidNamespace) == name)
            {
                SetAndroidAttribute(element, "required", required ? "true" : "false");
                return;
            }
        }

        var feature = doc.CreateElement("uses-feature");
        SetAndroidAttribute(feature, "name", name);
        SetAndroidAttribute(feature, "required", required ? "true" : "false");
        manifest.InsertBefore(feature, manifest.FirstChild);
    }

    static void EnsureUsesPermission(XmlDocument doc, XmlElement manifest, string name)
    {
        foreach (XmlNode child in manifest.ChildNodes)
        {
            if (child is XmlElement element && element.Name == "uses-permission" &&
                element.GetAttribute("name", AndroidNamespace) == name)
                return;
        }

        var permission = doc.CreateElement("uses-permission");
        SetAndroidAttribute(permission, "name", name);
        manifest.InsertBefore(permission, manifest.FirstChild);
    }

    static XmlElement FindUnityActivity(XmlElement application)
    {
        foreach (XmlNode child in application.ChildNodes)
        {
            if (child is not XmlElement element || element.Name != "activity")
                continue;
            var name = element.GetAttribute("name", AndroidNamespace);
            if (name == "com.unity3d.player.UnityPlayerActivity" || name.EndsWith("UnityPlayerActivity", System.StringComparison.Ordinal))
                return element;
        }
        return null;
    }

    static void EnsureMetaData(XmlDocument doc, XmlElement parent, string name, string value)
    {
        foreach (XmlNode child in parent.ChildNodes)
        {
            if (child is XmlElement element && element.Name == "meta-data" &&
                element.GetAttribute("name", AndroidNamespace) == name)
            {
                SetAndroidAttribute(element, "value", value);
                return;
            }
        }

        var meta = doc.CreateElement("meta-data");
        SetAndroidAttribute(meta, "name", name);
        SetAndroidAttribute(meta, "value", value);
        parent.AppendChild(meta);
    }

    static void RemoveMetaData(XmlElement parent, string name)
    {
        for (int i = parent.ChildNodes.Count - 1; i >= 0; i--)
        {
            if (parent.ChildNodes[i] is XmlElement element && element.Name == "meta-data" &&
                element.GetAttribute("name", AndroidNamespace) == name)
                parent.RemoveChild(element);
        }
    }

    static void EnsureVrCategory(XmlDocument doc, XmlElement activity)
    {
        XmlElement launcherFilter = null;
        foreach (XmlNode child in activity.ChildNodes)
        {
            if (child is not XmlElement element || element.Name != "intent-filter")
                continue;
            if (ContainsIntentElement(element, "action", "android.intent.action.MAIN") &&
                ContainsIntentElement(element, "category", "android.intent.category.LAUNCHER"))
            {
                launcherFilter = element;
                break;
            }
        }

        if (launcherFilter == null)
        {
            launcherFilter = doc.CreateElement("intent-filter");
            activity.AppendChild(launcherFilter);
            EnsureIntentElement(doc, launcherFilter, "action", "android.intent.action.MAIN");
            EnsureIntentElement(doc, launcherFilter, "category", "android.intent.category.LAUNCHER");
        }

        EnsureIntentElement(doc, launcherFilter, "category", "com.picovr.intent.category.VR");
        EnsureIntentElement(doc, launcherFilter, "category", "com.picovr.intent.category.VR_ONLY");
    }

    static bool ContainsIntentElement(XmlElement parent, string tagName, string value)
    {
        foreach (XmlNode child in parent.ChildNodes)
        {
            if (child is XmlElement element && element.Name == tagName &&
                element.GetAttribute("name", AndroidNamespace) == value)
                return true;
        }
        return false;
    }

    static void EnsureIntentElement(XmlDocument doc, XmlElement parent, string tagName, string value)
    {
        if (ContainsIntentElement(parent, tagName, value))
            return;
        var element = doc.CreateElement(tagName);
        SetAndroidAttribute(element, "name", value);
        parent.AppendChild(element);
    }

    static void SetAndroidAttribute(XmlElement element, string localName, string value)
    {
        element.SetAttribute(localName, AndroidNamespace, value);
    }
}
