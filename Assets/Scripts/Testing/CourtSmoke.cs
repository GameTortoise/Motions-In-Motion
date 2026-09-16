#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using PurrNet;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in executable integration test; this component only exists in the generated test scene.
public sealed class CourtSmoke : MonoBehaviour
{
    private bool host, failed;
    private float started;
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        started = Time.realtimeSinceStartup;
        host = Environment.GetCommandLineArgs().Contains("-court-host");
        Application.logMessageReceived += OnLog;
        Application.runInBackground = true;
        var manager = NetworkManager.main;
        if (GameSession.Instance == null) new GameObject("GameSession").AddComponent<GameSession>();
        if (host) manager.StartHost(); else manager.StartClient();
        if (host) yield return Host(manager); else yield return Client();
    }
    private void Update()
    {
        if (Time.realtimeSinceStartup - started > 150f) Fail("Integration test timed out.");
    }
    private IEnumerator Host(NetworkManager manager)
    {
        while (manager.playerCount < 6) yield return null;
        manager.sceneModule.LoadSceneAsync("CaseSelection", LoadSceneMode.Single);
        while (FindObjectsByType<Networking>().Count(p=>p.HasAssignedType) < 6) yield return null;
        var players = FindObjectsByType<Networking>();
        Check(players.Count(p=>p.Type == PlayerType.Host)==1,"Display host count");
        Check(players.Count(p=>p.Type == PlayerType.Judge)==1,"Judge count");
        Check(players.Count(p=>p.Type == PlayerType.Prosecutor)==2,"Prosecutor count");
        Check(players.Count(p=>p.Type == PlayerType.Defendant)==2,"Defendant count");
        GameSession.Instance.SetCase(CourtAssets.Load().cases[1]);
        manager.sceneModule.LoadSceneAsync("MainGame", LoadSceneMode.Single);
        while (CourtTrial.Instance == null || CourtTrial.Instance.State.elapsed <= 0) yield return null;
        var trial = CourtTrial.Instance;
        Check(trial.State.caseIndex == 1,"Selected case propagated");
        Check(trial.State.firstEvidence != trial.State.secondEvidence,"Two distinct starting cards");
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.dataPath,"../court-host.png"));
        while (FindObjectsByType<CourtDelivery>().Length < 2) yield return null;
        Check(FindObjectsByType<CourtDelivery>().Length == 2,"One car per team under concurrent submissions");
        yield return new WaitForSecondsRealtime(.5f);
        foreach (var delivery in FindObjectsByType<CourtDelivery>())
        {
            var line = FindObjectsByType<CourtFinishLine>().OrderBy(l=>Vector3.Distance(l.transform.position,delivery.transform.position)).First();
            delivery.transform.position = line.transform.position;
        }
        yield return new WaitForSecondsRealtime(.5f);
        Check(FindObjectsByType<CourtDelivery>().Length == 0,"FinishLine destroys delivered cars");
        Check(!trial.State.prosecutionBusy && !trial.State.defenseBusy,"Team reservations released on arrival");
        while (FindObjectsByType<CourtDelivery>().Length < 2) yield return null;
        var next = FindObjectsByType<CourtDelivery>();
        foreach (var delivery in next)
        {
            if (delivery.team == 1) delivery.transform.rotation = Quaternion.Euler(0,0,180);
            else trial.Arrive(delivery.team);
        }
        yield return new WaitForSecondsRealtime(.5f);
        Check(!trial.State.defenseBusy,"Flipped car releases team");
        var defender = FindObjectsByType<Networking>().First(p => p.Type == PlayerType.Defendant);
        Check(trial.Reserve(defender, -1), "New delivery allowed after a flip");
        trial.Release(defender);
        Check(!trial.Snapshot().defenseBusy, "Cancelling releases the reservation");
        var judge = FindObjectsByType<Networking>().First(p => p.Type == PlayerType.Judge);
        Check(!trial.Reserve(judge, -1), "Judge cannot submit a delivery");
        Check(!trial.Reserve(defender, 0), "Opponent evidence is rejected");
        Check(trial.State.objectionRemaining > 0,"Objection pauses trial");
        float paused = trial.State.elapsed;
        yield return new WaitForSecondsRealtime(1f);
        Check(Mathf.Abs(trial.State.elapsed-paused)<.01f,"Trial clock remains paused");
        Set(trial,"objectionRemaining",0f);
        yield return new WaitForSecondsRealtime(.5f);
        Check(trial.State.elapsed>paused,"Trial clock resumes");
        foreach(float boundary in new[]{240f,480f,720f,960f,1020f,1080f,1140f})
        {
            Set(trial,"elapsed",boundary);
            while (trial.State.turn != TrialRules.Turn(boundary)) yield return null;
            Check(trial.State.turn == TrialRules.Turn(boundary),"Speaking turn boundary " + boundary);
        }
        Set(trial,"elapsed",1200f);
        while(trial.State.verdict == 0) yield return null;
        Check(trial.State.verdict == 1,"Judge verdict network request");
        Debug.Log("COURT NETWORK SMOKE PASSED");
        yield return new WaitForSecondsRealtime(2f);
        Application.Quit(failed?1:0);
    }
    private IEnumerator Client()
    {
        while(CourtTrial.Instance == null || CourtTrial.Instance.Local == null || CourtTrial.Instance.State.elapsed <= 0) yield return null;
        var trial = CourtTrial.Instance;
        var player = trial.Local;
        Check(trial.State.caseIndex == 1,"Client received selected case");
        if (player.Type == PlayerType.Judge)
        {
            player.RequestMotion(-1);
            yield return new WaitForSecondsRealtime(.3f);
            while(trial.State.elapsed < 1200f) yield return null;
            player.Verdict(true);
        }
        else
        {
            int index = Enumerable.Range(0,trial.CurrentCase.evidence.Count).First(i=>TrialRules.OwnsEvidence(player.Type,i) && i!=trial.State.firstEvidence && i!=trial.State.secondEvidence);
            player.RequestMotion(index);
            yield return new WaitForSecondsRealtime(.4f);
            player.SendDrawing(Wheel());
            yield return new WaitForSecondsRealtime(1f);
            while(player.Type==PlayerType.Prosecutor ? trial.State.prosecutionBusy : trial.State.defenseBusy) yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            player.RequestMotion(-1);
            yield return new WaitForSecondsRealtime(.4f);
            player.SendDrawing(Wheel());
        }
        while(trial.State.verdict==0) yield return null;
        Check(trial.State.verdict==1,"Client received verdict");
        Debug.Log("COURT CLIENT SMOKE PASSED " + player.Type);
        yield return new WaitForSecondsRealtime(1f);
        Application.Quit(failed?1:0);
    }
    private byte[] Wheel()
    {
        var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);
        var pixels=new Color32[256*256];
        var random=new System.Random(1234);
        for(int y=0;y<256;y++) for(int x=0;x<256;x++)
            pixels[y*256+x]=(x-128)*(x-128)+(y-128)*(y-128)<14400
                ?new Color32((byte)random.Next(90),(byte)random.Next(90),(byte)random.Next(90),255):new Color32(0,0,0,0);
        texture.SetPixels32(pixels); texture.Apply(); var png=texture.EncodeToPNG(); Destroy(texture); return png;
    }
    private static void Set(object obj,string field,float value) => obj.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);
    private void Check(bool condition,string description) { if(!condition) Fail(description); else Debug.Log("PASS: "+description); }
    private void OnLog(string message,string trace,LogType type) { if(type == LogType.Exception) Fail(message); }
    private void Fail(string message) { if(failed)return; failed=true; Debug.LogError("COURT SMOKE FAILED: "+message); Application.Quit(1); }
}
#endif
