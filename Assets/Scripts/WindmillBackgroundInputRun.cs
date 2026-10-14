#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Test-only virtual devices. All play actions pass through the director's
// ordinary Update and Input System, never through force-snap/test adapters.
[DefaultExecutionOrder(-1000)]
public sealed class WindmillBackgroundInputRun : MonoBehaviour
{
    const string Evidence = "Artifacts/Checkpoints/177_WindmillHandsOn_20260910";
    Mouse mouse;
    Keyboard keyboard;
    WindmillStudyDirector flow;
    string phase;
    Vector2 pointer;
    bool buttonDown;
    Key[] pressedKeys=Array.Empty<Key>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        string phase = Environment.GetEnvironmentVariable("PPVR_BACKGROUND_PLAYTEST");
        if (string.IsNullOrEmpty(phase)) return;
        Environment.SetEnvironmentVariable("PPVR_BACKGROUND_PLAYTEST",null);
        var runner = FindFirstObjectByType<WindmillStudyDirector>().gameObject.AddComponent<WindmillBackgroundInputRun>();
        runner.phase = phase;
    }
    IEnumerator Start()
    {
        flow = GetComponent<WindmillStudyDirector>();
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        mouse = InputSystem.AddDevice<Mouse>("BackgroundPlaytestMouse");
        keyboard = InputSystem.AddDevice<Keyboard>("BackgroundPlaytestKeyboard");
        mouse.MakeCurrent(); keyboard.MakeCurrent();
        yield return null; yield return null; yield return null;
        Debug.Log("[Background Input] Begin " + phase + " screen=" + Screen.width + "x" + Screen.height);
        var run = Check();
        while(true)
        {
            object current;
            try { if(!run.MoveNext()) break; current=run.Current; }
            catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); yield break; }
            yield return current;
        }
        Debug.Log("[Background Input] " + phase.ToUpperInvariant() + " INPUT PLAYTEST PASSED");
        EditorApplication.Exit(0);
    }
    void Input(bool down=false, params Key[] keys)
    { buttonDown=down; pressedKeys=keys; }
    void Update()
    {
        if(mouse==null||keyboard==null) return;
        var state = new MouseState { position=pointer };
        if(buttonDown) state=state.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(mouse,state);
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(pressedKeys));
        InputSystem.Update();
    }
    IEnumerator Frames(float duration, bool down=false, Key key=Key.None)
    {
        float until=Time.unscaledTime+duration;
        do { Input(down,key==Key.None?Array.Empty<Key>():new[]{key}); yield return null; } while(Time.unscaledTime<until);
    }
    IEnumerator Tap(Key key)
    { Input(false,key); yield return null; Input(); yield return null; }
    IEnumerator Check()
    {
        if(phase=="start")
        {
            // Restart only this checkpoint's isolated progress, using real keys.
            yield return Tap(Key.R); yield return Tap(Key.R);
            Require(flow.Count==0,"double-confirm restart produces fresh state");
            Capture("01_START");
            yield return Tap(Key.H);
            Require(!FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Any(t=>t.gameObject.activeInHierarchy && t!=flow.chapterLabel && t!=flow.instructionLabel),"hint does not reactivate overlapping legacy text");
            Capture("02_HINT");
            var basePiece=flow.level.pieces[0];
            pointer=flow.view.WorldToScreenPoint(WindmillStudyDirector.VisualBounds(basePiece).center);
            Debug.Log("[Background Input] Foundation screen="+pointer+" active="+basePiece.gameObject.activeSelf+" available="+basePiece.IsAvailable+" input="+flow.inputEnabled);
            Input(true); yield return null;
            Debug.Log("[Background Input] Mouse current="+(Mouse.current==mouse)+" pressed="+mouse.leftButton.isPressed+" edge="+mouse.leftButton.wasPressedThisFrame+" position="+mouse.position.ReadValue());
            Require(basePiece.IsHeld,"mouse down selects foundation through Update");
            var before=basePiece.transform.rotation;
            yield return Frames(.35f,true,Key.E);
            Require(Quaternion.Angle(before,basePiece.transform.rotation)>5,"E rotates held piece through keyboard input");
            Input(); yield return null;
            Require(!basePiece.IsSnapped,"release away from socket rejected");
            yield return Frames(.9f);
            Require(Vector3.Distance(basePiece.transform.position,basePiece.AuthoredPositionV45)<.002f,"rejected piece returns safely to tray");
        }
        else Require(flow.Count==3,"fresh background process restores three assembled parts");

        int[] sequence=phase=="start"?new[]{0,2,3}:new[]{4,7,6,5,1};
        foreach(int index in sequence)
        {
            var part=flow.level.pieces[index];
            Require(part.IsAvailable,"part available "+index);
            Vector3 localCenter=part.transform.InverseTransformPoint(WindmillStudyDirector.VisualBounds(part).center);
            pointer=flow.view.WorldToScreenPoint(WindmillStudyDirector.VisualBounds(part).center);
            Debug.Log("[Background Input] Pickup "+index+" at "+pointer);
            Input(true); yield return null;
            Require(part.IsHeld,"pointer picks intended visible part "+index);
            Vector3 center=part.targetSocket.position+part.targetSocket.rotation*Vector3.Scale(localCenter,part.AssembledWorldScaleV45);
            Vector2 start=pointer, target=flow.view.WorldToScreenPoint(center);
            float began=Time.unscaledTime;
            while(Time.unscaledTime-began<1.6f)
            {
                pointer=Vector2.Lerp(start,target,Mathf.Clamp01((Time.unscaledTime-began)/.55f));
                Input(true); yield return null;
            }
            if(index==2) Capture("03_TOWER_HELD");
            Input(); yield return null; yield return Frames(.3f);
            Require(part.IsSnapped,"pointer drag/release assembles "+index);
            if(index==3) Capture("04_MIDPOINT_SAVED");
        }
        if(phase=="start")
        {
            Require(flow.Count==3,"three parts before process exit");
            Require(File.ReadAllText(flow.SavePath).Contains("\"mask\":13"),"automatic partial save written");
            yield break;
        }
        Require(flow.Count==8&&!flow.Awakened,"assembly waits for manual activation");
        Capture("05_ASSEMBLED");
        Vector2 hub=flow.view.WorldToScreenPoint(flow.rotor.position);
        float radius=Screen.height*.13f;
        pointer=hub+Vector2.right*radius;
        Input(true); yield return null;
        for(int angle=0;angle<=220;angle+=4)
        {
            float rad=angle*Mathf.Deg2Rad;
            pointer=hub+new Vector2(Mathf.Cos(rad),Mathf.Sin(rad))*radius;
            Input(true); yield return null;
        }
        Input(); yield return null;
        Require(flow.Awakened,"circular mouse drag activates windmill");
        Capture("06_ALIVE");
        Quaternion rotor=flow.rotor.localRotation;
        yield return Frames(.8f);
        Require(Quaternion.Angle(rotor,flow.rotor.localRotation)>5,"activated sails keep turning");
        Capture("07_ALIVE_TURNING");
        yield return Tap(Key.R);
        Require(flow.Count==8&&flow.Awakened,"first reset press preserves completion while confirming");
        yield return Tap(Key.R);
        Require(flow.Count==0&&!flow.Awakened,"second reset press clears assembly and rotor state");
        Capture("08_RESTARTED");
    }
    void Capture(string name)
    {
        Render(name+"_FRONT.png",0);
        if(name=="06_ALIVE") Render(name+"_LEFT.png",-.032f);
        Debug.Log("[Background Input] CAPTURE "+name+" count="+flow.Count+" phase="+flow.Phase);
    }
    void Render(string name,float offset)
    {
        var camera=flow.view;
        var old=camera.targetTexture; var active=RenderTexture.active; var position=camera.transform.position;
        var rt=new RenderTexture(1920,1080,24);
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        try
        {
            camera.transform.position+=camera.transform.right*offset;
            camera.targetTexture=rt; camera.Render(); camera.Render(); RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
            string path=Path.GetFullPath(Path.Combine(Evidence,name)); File.WriteAllBytes(path,image.EncodeToPNG());
            Debug.Log("[Background Input] Screenshot "+path);
        }
        finally { camera.transform.position=position; camera.targetTexture=old; RenderTexture.active=active; Destroy(rt); Destroy(image); }
    }
    static void Require(bool pass,string message)
    { if(!pass) throw new InvalidOperationException("[Background Input] FAIL "+message); Debug.Log("[Background Input] PASS "+message); }
}
#endif
