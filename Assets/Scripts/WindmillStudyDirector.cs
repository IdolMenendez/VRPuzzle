using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Isolated design study. No dependency on the release save or scene flow.
[DefaultExecutionOrder(100)]
public sealed class WindmillStudyDirector : MonoBehaviour
{
    public RuntimePuzzleLevelController level;
    public Camera view;
    public TextMesh chapterLabel;
    public TextMesh instructionLabel;
    public Transform rotor;
    public Light celebrationLight;
    public bool inputEnabled = true;
    public string saveOverride;
    public string[] partNames = { "Oak foundation", "Garden terrace", "Stone tower", "Balcony", "Roof", "First sail pair", "Second sail pair", "Axle hub" };
    public int Count => level.pieces.Count(p => p.IsSnapped);
    public bool Awakened { get; private set; }
    public float TurnedDegrees { get; private set; }
    public string Phase => Count == 8 ? (Awakened ? "04  YOUR LITTLE WORLD" : "03  BRING IT TO LIFE") : level.pieces[3].IsSnapped ? "02  CATCH THE WIND" : "01  BUILD THE HOME";
    public string SavePath => string.IsNullOrEmpty(saveOverride) ? Path.Combine(Application.persistentDataPath,"windmill-study-v1.json") : saveOverride;
    RuntimePuzzlePiece held;
    Quaternion desiredRotation;
    Quaternion rotorRest;
    Vector3 desiredPosition;
    Vector3 heldVisualCenterLocal;
    Vector3[] socketPositions;
    Quaternion[] socketRotations;
    bool ready, turning;
    bool rotationAssistance;
    float lastAngle, lastHint, resetConfirmUntil;
    int previousMask = -1;
    string feedback = "Choose the oak foundation. Build at your own pace.";
    const int MouseHolder = 8301;
    [Serializable] public sealed class Progress { public int version = 1; public int mask; public bool awakened; }

    void Awake()
    {
        if(level) level.keyboardShortcutsEnabled=false;
    }

    IEnumerator Start()
    {
        yield return null;
        // Disabled legacy guides still run Awake and can reactivate their label.
        foreach(var label in FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(label!=chapterLabel && label!=instructionLabel && label.gameObject.scene==gameObject.scene)
                label.gameObject.SetActive(false);
        rotorRest = rotor.localRotation;
        socketPositions = level.pieces.Select(p=>p.targetSocket.position).ToArray();
        socketRotations = level.pieces.Select(p=>p.targetSocket.rotation).ToArray();
        RestoreProgress();
        ready = true;
        RefreshPresentation();
    }

    void Update()
    {
        if (!ready) return;
        int mask = Mask();
        if (mask != previousMask)
        {
            previousMask = mask;
            RefreshPresentation();
            SaveProgress();
        }
        if (Awakened) rotor.Rotate(Vector3.forward, 18f * Time.unscaledDeltaTime, Space.Self);
        if (!inputEnabled) return;
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.hKey.wasPressedThisFrame) Hint();
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame) ConfirmReset();
        var mouse = Mouse.current;
        if (mouse == null || !view) return;
        Vector2 point = mouse.position.ReadValue();
        // Reserve the upper/lower screen strips for the study UI.
        float uiScale=Mathf.Clamp(Screen.width/1280f,.7f,2f);
        bool overUi = point.y < 84*uiScale || point.y > Screen.height - 110*uiScale;
        if (mouse.leftButton.wasPressedThisFrame && !overUi)
        {
            if (Count == 8 && !Awakened)
            {
                Vector2 hub = view.WorldToScreenPoint(rotor.position);
                float distance = Vector2.Distance(point,hub);
                if (distance < Screen.height * .30f)
                { turning = true; lastAngle = Mathf.Atan2(point.y-hub.y,point.x-hub.x)*Mathf.Rad2Deg; }
            }
            else BeginPointerGrab(point);
        }
        if (held && mouse.leftButton.isPressed)
        {
            float yaw=keyboard==null?0:(keyboard.eKey.isPressed?1:0)-(keyboard.qKey.isPressed?1:0);
            MovePointerGrab(point,yaw*85f*Time.unscaledDeltaTime);
        }
        if (turning && mouse.leftButton.isPressed)
        {
            Vector2 hub = view.WorldToScreenPoint(rotor.position);
            if (Vector2.Distance(point,hub)>20)
            {
                float angle = Mathf.Atan2(point.y-hub.y,point.x-hub.x)*Mathf.Rad2Deg;
                AddTurn(Mathf.DeltaAngle(lastAngle,angle)); lastAngle=angle;
            }
        }
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            turning=false;
            EndPointerGrab();
        }
    }

    public void MovePointerGrab(Vector2 point,float yawDegrees=0)
    {
        if(!held)return;
        if(Mathf.Abs(yawDegrees)>.0001f)held.stabilizeHeldOrientation=false;
        Ray ray=view.ScreenPointToRay(point);
        // Map the pointer to the visible socket center. Using the unsolved
        // piece's bounds here can strand an asymmetric sail outside assist range.
        Vector3 offset=held.stabilizeHeldOrientation
            ? held.targetSocket.rotation*Vector3.Scale(heldVisualCenterLocal,held.AssembledWorldScaleV45)
            : VisualBounds(held).center-held.transform.position;
        var plane=new Plane(Vector3.up,held.targetSocket.position+offset);
        if(plane.Raycast(ray,out float enter))desiredPosition=ray.GetPoint(enter)-offset;
        desiredPosition.y=held.targetSocket.position.y;
        desiredRotation=Quaternion.AngleAxis(yawDegrees,Vector3.up)*desiredRotation;
        held.MoveExternalGrab(MouseHolder,held.HolderPoseForDesiredPiecePoseV69(new Pose(desiredPosition,desiredRotation)));
    }

    public void EndPointerGrab()
    {
        if(!held)return;
        var released=held;held.EndExternalGrab(MouseHolder);held.stabilizeHeldOrientation=rotationAssistance;held=null;
        feedback=released.IsSnapped?"A perfect fit. The next pieces are ready.":"Not connected yet. Your piece returns safely to the tray.";
    }

    public void BeginPointerGrab(Vector2 point)
    {
        Ray ray=view.ScreenPointToRay(point);
        // Only the visible, available piece bounds participate; invisible
        // legacy mesh colliders cannot intercept the desktop interaction.
        float nearest=float.PositiveInfinity;
        foreach(var p in level.pieces)
        {
            if(p.IsSnapped || !p.IsAvailable || !p.gameObject.activeSelf) continue;
            Bounds bounds=VisualBounds(p); bounds.Expand(.014f);
            if(bounds.IntersectRay(ray,out float distance) && distance<nearest)
            { held=p; nearest=distance; }
        }
        if(!held) return;
        rotationAssistance=held.stabilizeHeldOrientation;
        heldVisualCenterLocal=held.transform.InverseTransformPoint(VisualBounds(held).center);
        desiredRotation=held.transform.rotation; desiredPosition=held.transform.position;
        held.BeginExternalGrab(MouseHolder,new Pose(held.transform.position,held.transform.rotation));
        if(!held.IsHeld) { held=null; return; }
        feedback=partNames[held.sequence]+" — drag to its outline; release to connect.";
        held.ShowExactHint(15);
    }

    public static Bounds VisualBounds(RuntimePuzzlePiece piece)
    {
        var renderers=piece.solidRenderers.Where(r=>r).ToArray();
        var bounds=renderers.Length>0?renderers[0].bounds:new Bounds(piece.transform.position,Vector3.one*.05f);
        foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    public void RefreshPresentation()
    {
        foreach(var p in level.pieces)
        {
            p.gameObject.SetActive(p.IsSnapped || p.IsAvailable);
            p.RefreshAvailability();
        }
        if(chapterLabel) { chapterLabel.text=Phase; chapterLabel.gameObject.SetActive(Count<8); }
        if(instructionLabel) { instructionLabel.text="A quiet place, built by hand.  "+Count+" / 8"; instructionLabel.gameObject.SetActive(Count<8); }
        if(Count==8) feedback=Awakened?"You brought it back to life. This one is yours.":"Drag the sails half a circle around the axle to wake your windmill.";
        if(celebrationLight) celebrationLight.enabled=Awakened;
    }

    public void AddTurn(float degrees)
    {
        if(Count!=8 || Awakened || !float.IsFinite(degrees)) return;
        // Clamp a single input sample so a pointer discontinuity cannot win.
        degrees=Mathf.Clamp(degrees,-40,40);
        rotor.Rotate(Vector3.forward,degrees,Space.Self);
        TurnedDegrees+=degrees;
        if(Mathf.Abs(TurnedDegrees)>=180)
        {
            Awakened=true; feedback="You brought it back to life. This one is yours.";
            RuntimeAudioRouter.PlayCue(RuntimeAudioCue.Complete,rotor.position);
            RefreshPresentation(); SaveProgress();
        }
    }

    public void Hint()
    {
        if(Time.unscaledTime-lastHint<.3f) return;
        lastHint=Time.unscaledTime;
        var next=level.pieces.FirstOrDefault(p=>!p.IsSnapped&&p.IsAvailable);
        if(next) { next.ShowExactHint(6); next.PulsePickupHighlight(6); feedback="Try the "+partNames[next.sequence].ToLowerInvariant()+". Hints are free."; }
        else feedback="Drag the sails half a circle around the axle, in either direction.";
    }

    public void ConfirmReset()
    {
        if(Time.unscaledTime<resetConfirmUntil) ResetStudy();
        else { resetConfirmUntil=Time.unscaledTime+4; feedback="Start over? Press R or Start over again within four seconds."; }
    }

    public void ResetStudy()
    {
        EndPointerGrab();
        turning=false; Awakened=false; TurnedDegrees=0; resetConfirmUntil=0;
        rotor.localRotation=rotorRest;
        for(int i=0;i<level.pieces.Length;i++)
        {
            level.pieces[i].gameObject.SetActive(true);
            if(socketPositions!=null) level.pieces[i].targetSocket.SetPositionAndRotation(socketPositions[i],socketRotations[i]);
        }
        level.ResetLevel();
        previousMask=-1; feedback="Choose the oak foundation. Build at your own pace.";
        RefreshPresentation(); SaveProgress();
    }

    int Mask() => level.pieces.Aggregate(0,(mask,p)=>p.IsSnapped?mask|(1<<p.sequence):mask);

    public void SaveProgress()
    {
        if(!Application.isPlaying) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            string temporary=SavePath+".tmp";
            File.WriteAllText(temporary,JsonUtility.ToJson(new Progress{mask=Mask(),awakened=Awakened}));
            if(File.Exists(SavePath)) File.Replace(temporary,SavePath,SavePath+".bak");
            else File.Move(temporary,SavePath);
        }
        catch(Exception e) { Debug.LogWarning("[Windmill Study] Save unavailable: "+e.Message); feedback="Progress could not be saved. Keep this session open."; }
    }

    public void RestoreProgress()
    {
        if(!File.Exists(SavePath)) return;
        try
        {
            var save=JsonUtility.FromJson<Progress>(File.ReadAllText(SavePath));
            if(save==null || save.version!=1 || save.mask<0 || save.mask>255) return;
            // Resolve the dependency graph, not numeric piece order (the hub
            // precedes the two sail pairs despite its later source sequence).
            for(int pass=0;pass<8;pass++)
                foreach(var p in level.pieces)
                    if((save.mask&(1<<p.sequence))!=0 && !p.IsSnapped && p.IsAvailable)
                    { p.gameObject.SetActive(true); p.ForceSnapForSmoke(); }
            Awakened=save.awakened&&Count==8; TurnedDegrees=Awakened?180:0;
            feedback="Welcome back. Your last assembly is waiting.";
        }
        catch(Exception e) { Debug.LogWarning("[Windmill Study] Could not restore study progress: "+e.Message); }
    }

    void OnApplicationPause(bool paused) { if(paused && ready) SaveProgress(); }
    void OnApplicationQuit() { if(ready) SaveProgress(); }

    void OnGUI()
    {
        if(!ready) return;
        float scale=Mathf.Clamp(Screen.width/1280f,.7f,2f);
        var before=GUI.matrix; GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
        float width=Screen.width/scale,height=Screen.height/scale;
        var title=new GUIStyle(GUI.skin.label){fontSize=23,fontStyle=FontStyle.Bold}; title.normal.textColor=new Color(.94f,.89f,.73f);
        var body=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true}; body.normal.textColor=Color.white;
        GUI.Box(new Rect(18,14,470,81),GUIContent.none);
        GUI.Label(new Rect(30,20,440,31),Phase,title);
        GUI.Label(new Rect(30,54,440,36),Count+" / 8 pieces  ·  No timer  ·  Free hints  ·  Autosaved",body);
        GUI.Box(new Rect(18,height-72,width-36,56),GUIContent.none);
        GUI.Label(new Rect(30,height-64,width-350,44),feedback,body);
        if(GUI.Button(new Rect(width-300,height-57,124,31),"Hint  [H]")) Hint();
        if(GUI.Button(new Rect(width-166,height-57,136,31),"Start over  [R]")) ConfirmReset();
        GUI.matrix=before;
    }
}
