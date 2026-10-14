#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEditor;

public sealed class WindmillStudySmoke : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if(Environment.GetEnvironmentVariable("PPVR_WINDMILL_STUDY_SMOKE")!="1")return;
        Environment.SetEnvironmentVariable("PPVR_WINDMILL_STUDY_SMOKE",null);
        FindFirstObjectByType<WindmillStudyDirector>().gameObject.AddComponent<WindmillStudySmoke>();
    }
    IEnumerator Start()
    {
        Debug.Log("[Windmill Study] Smoke runner started");
        var flow=GetComponent<WindmillStudyDirector>();
        yield return null;yield return null;yield return null;
        var run=Check(flow);
        while(true)
        {
            object current;
            try { if(!run.MoveNext())break;current=run.Current; }
            catch(Exception error) {Debug.LogException(error);EditorApplication.Exit(1);yield break;}
            yield return current;
        }
        Debug.Log("[Windmill Study] PLAY VALIDATION PASSED - dependency, grab/release, wrong release, save/restore, activation, reset");
        EditorApplication.Exit(0);
    }

    IEnumerator Check(WindmillStudyDirector flow)
    {
        var pieces=flow.level.pieces;
        Require(flow.Count==0,"fresh state");
        Require(pieces[0].IsAvailable&&!pieces[2].IsAvailable&&!pieces[5].IsAvailable,"dependency before base");
        Require(!pieces[5].CanExternalGrab(8302),"locked sail rejects grab");
        var foundation=pieces[0];
        Vector3 localCenter=foundation.transform.InverseTransformPoint(WindmillStudyDirector.VisualBounds(foundation).center);
        Vector2 down=flow.view.WorldToScreenPoint(WindmillStudyDirector.VisualBounds(foundation).center);
        Debug.Log("[Windmill Study] Pointer test screen="+Screen.width+"x"+Screen.height+" down="+down);
        flow.BeginPointerGrab(down);
        yield return null;yield return null;
        Require(foundation.IsHeld,"screen-space pointer picks visible piece");
        Vector3 solvedCenter=foundation.targetSocket.position+foundation.targetSocket.rotation*Vector3.Scale(localCenter,foundation.AssembledWorldScaleV45);
        Vector2 destination=flow.view.WorldToScreenPoint(solvedCenter);
        float dragUntil=Time.unscaledTime+1.5f;
        while(Time.unscaledTime<dragUntil)
        {
            flow.MovePointerGrab(destination);
            yield return null;
        }
        flow.EndPointerGrab();
        yield return null;yield return null;
        Require(foundation.IsSnapped,"screen-space pointer drag and release connects");
        flow.ResetStudy();
        down=flow.view.WorldToScreenPoint(WindmillStudyDirector.VisualBounds(foundation).center);
        flow.BeginPointerGrab(down);
        Quaternion beforeTurn=foundation.transform.rotation;
        float turnUntil=Time.unscaledTime+.5f;
        while(Time.unscaledTime<turnUntil)
        {flow.MovePointerGrab(down,60f*Time.unscaledDeltaTime);yield return null;}
        Require(Quaternion.Angle(beforeTurn,foundation.transform.rotation)>5,"manual rotation changes the held piece");
        flow.EndPointerGrab();yield return new WaitForSeconds(.7f);flow.ResetStudy();
        foreach(int i in new[]{0,2,3,4,7,6,5,1})
        {
            var p=pieces[i];Require(p.IsAvailable,"available "+i);p.gameObject.SetActive(true);
            p.BeginExternalGrab(8302,new Pose(p.transform.position,p.transform.rotation));
            Require(p.IsHeld,"held "+i);
            if(i==0)
            {
                p.EndExternalGrab(8302);Require(!p.IsSnapped,"wrong release rejected");
                yield return new WaitForSeconds(.7f);
                Require(Vector3.Distance(p.transform.position,p.AuthoredPositionV45)<.001f,"wrong release returns home");
                p.BeginExternalGrab(8302,new Pose(p.transform.position,p.transform.rotation));
            }
            float settleUntil=Time.unscaledTime+1.25f;
            while(Time.unscaledTime<settleUntil)
            {
                p.MoveExternalGrab(8302,p.HolderPoseForDesiredPiecePoseV69(new Pose(p.targetSocket.position,p.targetSocket.rotation)));
                yield return null;
            }
            p.EndExternalGrab(8302);Require(p.IsSnapped,"release connects "+i);
            yield return null;yield return null;
            if(i==0)Require(pieces[1].IsAvailable&&pieces[2].IsAvailable,"garden/structure branch");
            if(i==3)
            {
                flow.SaveProgress();string saved=File.ReadAllText(flow.SavePath);
                int count=flow.Count;flow.ResetStudy();Require(flow.Count==0,"reset clears progress");
                File.WriteAllText(flow.SavePath,saved);flow.RestoreProgress();flow.RefreshPresentation();
                Require(flow.Count==count,"mid-level restore");
            }
        }
        Require(flow.Count==8&&!flow.Awakened,"construction is not activation");
        flow.AddTurn(30);flow.AddTurn(-30);Require(Mathf.Abs(flow.TurnedDegrees)<.001f&&!flow.Awakened,"back-and-forth does not accumulate progress");
        for(int i=0;i<6;i++)flow.AddTurn(-30);
        Require(flow.Awakened,"manual turn activates either direction");
        flow.SaveProgress();string complete=File.ReadAllText(flow.SavePath);
        flow.ResetStudy();Require(!flow.Awakened&&flow.Count==0,"clean restart");
        File.WriteAllText(flow.SavePath,complete);flow.RestoreProgress();flow.RefreshPresentation();
        Require(flow.Awakened&&flow.Count==8,"completed collectible restored");
        yield return null;
    }
    static void Require(bool pass,string message)
    { if(!pass)throw new InvalidOperationException("[Windmill Study] FAIL "+message);Debug.Log("[Windmill Study] PASS "+message); }
}
#endif
