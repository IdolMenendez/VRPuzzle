using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class WindmillStudyBuilder
{
    public const string Source = "Assets/Scenes/RuntimeV82_ResourceOptimization/Runtime_Level_Windmill_20260910_V82_ResourceOptimization.unity";
    public const string ScenePath = "Assets/Scenes/WindmillStudy_20260910_V01_LivingWorkshop.unity";
    public const string Evidence = "Artifacts/Checkpoints/176_WindmillDesignStudy_20260910";

    public static void Audit()
    {
        Directory.CreateDirectory(Evidence);
        var scene = EditorSceneManager.OpenScene(Source);
        var lines = new System.Collections.Generic.List<string>();
        foreach (var root in scene.GetRootGameObjects())
        {
            lines.Add("ROOT " + root.name + " " + root.transform.position);
            foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (c) lines.Add("SCRIPT " + c.GetType().Name + " @ " + c.name + " enabled=" + c.enabled);
        }
        foreach (var p in UnityEngine.Object.FindObjectsByType<RuntimePuzzlePiece>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(p=>p.sequence))
        {
            lines.Add("PART " + p.sequence + " " + p.name + " position=" + p.transform.position.ToString("F3") + " socket=" + p.targetSocket.position.ToString("F3") + " rotation=" + p.targetSocket.eulerAngles + " deps=" + string.Join(",",p.prerequisiteSequences??Array.Empty<int>()));
            foreach (var r in p.solidRenderers)
                if(r) lines.Add("RENDER " + r.name + " bounds=" + r.bounds + " materials=" + string.Join(",",r.sharedMaterials.Select(m=>m?m.name:"NULL")));
        }
        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None)) lines.Add("CAM " + c.name + " " + c.transform.position.ToString("F3") + " rotation=" + c.transform.eulerAngles + " enabled=" + c.enabled);
        foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None)) lines.Add("LIGHT " + l.name + " " + l.type + " " + l.intensity + " " + l.color);
        File.WriteAllLines(Path.Combine(Evidence,"source-audit.txt"),lines);
        Debug.Log("[Windmill Study] AUDIT PASSED");
    }

    [MenuItem("Personally Puzzled XR/Design Study/Build Windmill Study")]
    public static void Build()
    {
        if(File.Exists(ScenePath)) throw new InvalidOperationException("Study scene already exists; preserve the checkpoint.");
        AssetDatabase.CopyAsset(Source,ScenePath);
        var scene=EditorSceneManager.OpenScene(ScenePath);
        const string assets="Assets/DesignStudies/WindmillV01";
        Directory.CreateDirectory(assets); AssetDatabase.Refresh();
        var pieces=UnityEngine.Object.FindObjectsByType<RuntimePuzzlePiece>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(p=>p.sequence).ToArray();
        var level=UnityEngine.Object.FindFirstObjectByType<RuntimePuzzleLevelController>();
        foreach(var root in scene.GetRootGameObjects())
        foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if(!b) continue;
            bool keep=b is RuntimePuzzlePiece || b is RuntimePuzzleLevelController || b is RuntimeAudioConductor || b is RuntimeStandaloneStageAudio || b is UniversalAdditionalCameraData || b is Volume;
            b.enabled=keep;
        }
        foreach(var text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None)) text.gameObject.SetActive(false);
        level.name="Windkeeper Study - eight complete semantic modules";
        level.pieces=pieces; level.progressLabel=null; level.accessibilityLabel=null; level.guidance=null; level.hintLamp=null;
        level.completionFxRootV61=null; level.completionBurstV61=null; level.completionLabelV61=null; level.completionGlowV61=null;
        level.completionUiRootsV79=Array.Empty<GameObject>(); level.suppressInLevelCompletionUiV79=true;
        int[][] dependencies={Array.Empty<int>(),new[]{0},new[]{0},new[]{2},new[]{3},new[]{7},new[]{7},new[]{4}};
        // Study lamps are authored independently. Preserve original scene lights.
        foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None)) light.enabled=false;
        AddLight("Study broad neutral daylight",new Vector3(-.6f,2.2f,-.1f),new Color(1f,.94f,.83f),2.3f,3.3f);
        AddLight("Study gentle cool fill",new Vector3(.55f,1.95f,.4f),new Color(.77f,.88f,1f),1.0f,2.6f);
        RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.35f,.36f,.34f);
        foreach(var volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(!volume.sharedProfile)continue;
            var profile=UnityEngine.Object.Instantiate(volume.sharedProfile); profile.name="Study warm neutral grade";
            // Deep-copy subassets before overriding, never alter shared profiles.
            profile.components=profile.components.Select(c=>UnityEngine.Object.Instantiate(c)).ToList();
            AssetDatabase.CreateAsset(profile,assets+"/StudyVolume.asset");
            foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
            if(profile.TryGet<ColorAdjustments>(out var color)) { color.colorFilter.Override(Color.white); color.postExposure.Override(.25f); color.saturation.Override(-5); }
            if(profile.TryGet<WhiteBalance>(out var white)) { white.temperature.Override(0); white.tint.Override(0); }
            volume.sharedProfile=profile;
        }
        var materialCopies=new System.Collections.Generic.Dictionary<Material,Material>();
        for(int i=0;i<pieces.Length;i++)
        {
            var p=pieces[i]; p.prerequisiteSequences=dependencies[i]; p.allowInspectionWhileLocked=false;
            p.hideWhileLocked=true; p.showCompleteReferenceV36=false; p.showTargetGhostWhileHeld=false;
            p.snapDistance=.075f; p.hintOuterDistance=.15f; p.maxAssistAngle=40; p.stabilizeHeldOrientation=true;
            p.requiresApproachCone=false; p.requiresPlayerFacing=false;
            foreach(var r in p.solidRenderers)
            {
                if(!r)continue;
                r.sharedMaterials=r.sharedMaterials.Select(m=>
                {
                    if(!m)return m;
                    if(!materialCopies.TryGetValue(m,out var copy))
                    {
                        copy=new Material(m); copy.name="Study "+m.name;
                        if(copy.HasProperty("_BaseColor"))copy.SetColor("_BaseColor",new Color(1f,.98f,.93f));
                        AssetDatabase.CreateAsset(copy,assets+"/Material_"+materialCopies.Count+".mat");materialCopies[m]=copy;
                    }
                    return copy;
                }).ToArray();
            }
        }
        var director=new GameObject("Windkeeper Study Director").AddComponent<WindmillStudyDirector>();
        director.level=level;
        var camera=UnityEngine.Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
        camera.enabled=true; camera.tag="MainCamera"; director.view=camera;
        if(!camera.GetComponent<AudioListener>())camera.gameObject.AddComponent<AudioListener>();
        var rotor=new GameObject("Study windmill rotating axle").transform;
        foreach(var p in pieces) p.ForceSnapForSmoke();
        var hub=pieces[7].solidRenderers.First(r=>r.name.Contains("center boss"));
        var backing=pieces[7].solidRenderers.First(r=>r.name.Contains("backing"));
        var pin=pieces[7].solidRenderers.First(r=>r.name.Contains("axle pin"));
        rotor.position=hub.bounds.center; rotor.rotation=Quaternion.LookRotation((pin.bounds.center-backing.bounds.center).normalized,Vector3.up);
        foreach(int i in new[]{5,6,7})pieces[i].targetSocket.SetParent(rotor,true);
        director.rotor=rotor;
        // Bounds-based shelf staging preserves each source mesh and its exact
        // solved pose, while giving the active modules separated visible space.
        for(int i=0;i<pieces.Length;i++)
        {
            var p=pieces[i]; p.ResetForPlay();
            bool left=i==0||i==2||i==4||i==5;
            Bounds b=WindmillStudyDirector.VisualBounds(p);
            float x=left?-.38f:.39f; float z=(i==0||i==2||i==7||i==1)?.42f:.68f;
            p.transform.position+=new Vector3(x-b.center.x,1.232f-b.min.y,z-b.center.z);
            p.CaptureAuthoredPose();
        }
        level.ResetLevel();
        director.chapterLabel=AddText("Study chapter",new Vector3(0,1.89f,1.28f),"01  BUILD THE HOME",.013f,64);
        director.instructionLabel=AddText("Study task",new Vector3(0,1.83f,1.28f),"A quiet place, built by hand.  0 / 8",.007f,58);
        director.celebrationLight=AddLight("Study earned warm lantern",rotor.position+Vector3.up*.08f,new Color(1f,.73f,.34f),.28f,.45f);
        director.celebrationLight.enabled=false;
        director.RefreshPresentation();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Evidence,"study-configuration.txt"),"Independent scene; existing eight semantic mesh parts; branch graph 0 -> {1,2}; 2 -> 3 -> 4 -> 7 -> {5,6}; prototype capture .075 m / 40 degrees; desktop height/rotation assistance; no build settings changed.\n");
        Debug.Log("[Windmill Study] BUILD PASSED "+ScenePath);
        Refine();
        AlignMechanicalAxisAndCapture();
    }

    static Light AddLight(string name,Vector3 position,Color color,float intensity,float range)
    {
        var light=new GameObject(name).AddComponent<Light>();light.transform.position=position;
        light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;return light;
    }

    public static void Refine()
    {
        EditorSceneManager.OpenScene(Source);
        var sign=UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.text.Contains("0/8")||t.text.Contains("0 / 8"));
        var textPosition=sign.transform.position;
        var textRotation=sign.transform.rotation;
        Debug.Log("[Windmill Study] Original sign "+textPosition);
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var director=UnityEngine.Object.FindFirstObjectByType<WindmillStudyDirector>();
        director.chapterLabel.transform.SetPositionAndRotation(textPosition+Vector3.up*.043f,textRotation);
        director.instructionLabel.transform.SetPositionAndRotation(textPosition-Vector3.up*.023f,textRotation);
        director.chapterLabel.characterSize=.0042f;director.instructionLabel.characterSize=.0026f;
        director.view.fieldOfView=65;
        foreach(var p in director.level.pieces)
        {
            p.gameObject.SetActive(true);
            Bounds b=WindmillStudyDirector.VisualBounds(p);
            bool left=p.sequence==0||p.sequence==2||p.sequence==4||p.sequence==5;
            float x=left?-.32f:.34f;
            float z=(p.sequence==0||p.sequence==1)?.74f:.58f;
            p.transform.position+=new Vector3(x-b.center.x,1.232f-b.min.y,z-b.center.z);
            p.CaptureAuthoredPose();
            foreach(var r in p.solidRenderers.Where(r=>r))
            foreach(var m in r.sharedMaterials.Where(m=>m))
            {
                if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",.15f);
                if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.26f);
                if(m.HasProperty("_GlossMapScale"))m.SetFloat("_GlossMapScale",.26f);
                EditorUtility.SetDirty(m);
            }
        }
        director.level.ResetLevel();director.RefreshPresentation();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Capture();
    }

    public static void Validate()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var director=UnityEngine.Object.FindFirstObjectByType<WindmillStudyDirector>();
        if(!director || director.level.pieces.Length!=8)throw new InvalidOperationException("Eight semantic pieces required");
        if(EditorBuildSettings.scenes.Any(s=>s.path==ScenePath&&s.enabled))throw new InvalidOperationException("Study must not replace release builds");
        foreach(var root in scene.GetRootGameObjects())
        foreach(var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)>0)throw new InvalidOperationException("Missing script "+transform.name);
            foreach(var renderer in transform.GetComponents<Renderer>())
                if(renderer.enabled&&renderer.gameObject.activeInHierarchy&&renderer.sharedMaterials.Any(m=>!m))throw new InvalidOperationException("Missing material "+renderer.name);
        }
        foreach(var piece in director.level.pieces)
            if(!piece.targetSocket||piece.solidRenderers.Length==0||piece.solidRenderers.Any(r=>!r))throw new InvalidOperationException("Invalid semantic piece");
        Debug.Log("[Windmill Study] STATIC VALIDATION PASSED");
    }

    public static void RunSmoke()
    {
        Validate();
        var director=UnityEngine.Object.FindFirstObjectByType<WindmillStudyDirector>();
        director.saveOverride=Path.GetFullPath(Path.Combine(Evidence,"smoke-progress.json"));
        director.inputEnabled=false;
        if(File.Exists(director.saveOverride))File.Delete(director.saveOverride);
        Environment.SetEnvironmentVariable("PPVR_WINDMILL_STUDY_SMOKE","1");
        EditorApplication.isPlaying=true;
    }

    public static void AlignMechanicalAxisAndCapture()
    {
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var director=UnityEngine.Object.FindFirstObjectByType<WindmillStudyDirector>();
        var assemblyObject=GameObject.Find("Study front-facing assembly");
        if(!assemblyObject)
        {
            assemblyObject=new GameObject("Study front-facing assembly");
            assemblyObject.transform.position=director.level.pieces[0].targetSocket.position;
            foreach(var p in director.level.pieces.Where(p=>p.sequence<5))p.targetSocket.SetParent(assemblyObject.transform,true);
            director.rotor.SetParent(assemblyObject.transform,true);
        }
        var assembly=assemblyObject.transform;
        assembly.rotation=Quaternion.Euler(0,180,0);
        foreach(var p in director.level.pieces){p.gameObject.SetActive(true);p.ForceSnapForSmoke();}
        var pieces=director.level.pieces;
        Bounds sails=WindmillStudyDirector.VisualBounds(pieces[5]);sails.Encapsulate(WindmillStudyDirector.VisualBounds(pieces[6]));
        Vector3 axis=sails.size.x<sails.size.y&&sails.size.x<sails.size.z?Vector3.right:sails.size.y<sails.size.z?Vector3.up:Vector3.forward;
        if(Vector3.Dot(axis,director.view.transform.position-sails.center)<0)axis=-axis;
        foreach(int i in new[]{5,6,7})pieces[i].targetSocket.SetParent(assembly,true);
        director.rotor.SetPositionAndRotation(sails.center,Quaternion.LookRotation(axis,Vector3.up));
        var hub=pieces[7].solidRenderers.First(r=>r.name.Contains("center boss"));
        var backing=pieces[7].solidRenderers.First(r=>r.name.Contains("backing"));
        var pin=pieces[7].solidRenderers.First(r=>r.name.Contains("axle pin"));
        var delta=Quaternion.FromToRotation((pin.bounds.center-backing.bounds.center).normalized,axis);
        pieces[7].targetSocket.rotation=delta*pieces[7].targetSocket.rotation;
        pieces[7].ForceSnapForSmoke();
        pieces[7].targetSocket.position+=sails.center+axis*.012f-hub.bounds.center;
        pieces[7].ForceSnapForSmoke();
        foreach(int i in new[]{5,6,7})pieces[i].targetSocket.SetParent(director.rotor,true);
        director.level.ResetLevel();director.RefreshPresentation();
        foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(light.name=="Study broad neutral daylight")light.intensity=1.65f;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("[Windmill Study] aligned actual sail-plane axis="+axis+" centre="+sails.center);
        Capture();
    }
    static TextMesh AddText(string name,Vector3 position,string value,float size,int fontSize)
    {
        var text=new GameObject(name).AddComponent<TextMesh>();text.transform.position=position;
        text.text=value;text.characterSize=size;text.fontSize=fontSize;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
        text.color=new Color(.96f,.9f,.73f);return text;
    }

    [MenuItem("Personally Puzzled XR/Design Study/Play Windmill Study")]
    public static void Play()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop the current Play session first.");
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        EditorApplication.isPlaying=true;
    }

    public static void Capture()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var director=UnityEngine.Object.FindFirstObjectByType<WindmillStudyDirector>();
        Render(director.view,"STUDY_START_FRONT.png",0);Render(director.view,"STUDY_START_LEFT.png",-.032f);
        foreach(int i in new[]{0,2,3,1,4,7,5,6})
        {var p=director.level.pieces[i];p.gameObject.SetActive(true);p.ForceSnapForSmoke();}
        director.RefreshPresentation();
        Render(director.view,"STUDY_ASSEMBLED_FRONT.png",0);Render(director.view,"STUDY_ASSEMBLED_LEFT.png",-.032f);
        // A capture of the earned state, not a claim that an input test ran.
        for(int i=0;i<6;i++)director.AddTurn(30);
        Render(director.view,"STUDY_ALIVE_FRONT.png",0);Render(director.view,"STUDY_ALIVE_LEFT.png",-.032f);
        // Discard capture-only mutations; retain the saved playable start.
        EditorSceneManager.OpenScene(ScenePath);
        Debug.Log("[Windmill Study] CAPTURE PASSED");
    }

    static void Render(Camera camera,string name,float offset)
    {
        var old=camera.targetTexture; var active=RenderTexture.active;var position=camera.transform.position;
        var rt=new RenderTexture(1920,1080,24);var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try
        {
            camera.transform.position+=camera.transform.right*offset; camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Evidence,name),image.EncodeToPNG());
        }
        finally {camera.transform.position=position;camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
    }
}
