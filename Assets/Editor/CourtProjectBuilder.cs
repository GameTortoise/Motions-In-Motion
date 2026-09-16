using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using TMPro;

public static class CourtProjectBuilder
{
    private static T Asset<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path);
    private static Sprite Sprite(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    [MenuItem("Court/Build Court Assets and Validate")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Resources");
        var assets = Asset<CourtAssets>("Assets/Resources/CourtAssets.asset");
        if (assets == null) { assets = ScriptableObject.CreateInstance<CourtAssets>(); AssetDatabase.CreateAsset(assets,"Assets/Resources/CourtAssets.asset"); }
        assets.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        assets.cases = AssetDatabase.FindAssets("t:CaseData",new[]{"Assets/GameData"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).Select(Asset<CaseData>).ToArray();
        assets.paper = Sprite("Assets/Sprites/white-crumpled-paper-texture-background_1373-162.png");
        assets.wood = Sprite("Assets/Sprites/wood_table_worn_diff_4k.jpg");
        assets.goobers = AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Sprites/Goobers"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).Select(Sprite).ToArray();
        assets.bodyFont = Asset<TMP_FontAsset>("Assets/Fonts/Embolism Spark.asset");
        assets.titleFont = Asset<TMP_FontAsset>("Assets/Fonts/Rockybilly.asset");
        assets.legacyFont = Asset<Font>("Assets/Fonts/Embolism Spark.ttf");
        assets.opening = Asset<AudioClip>("Assets/Audio/CamsAmazingSongJAckboxJam0dot3.wav");
        assets.middle = Asset<AudioClip>("Assets/Audio/MainGameLoopSongGameJamLaywerSongButCouldBeSwitchedForEvidenceSong.wav");
        assets.finale = Asset<AudioClip>("Assets/Audio/AcidFastSongforJam0dot2.wav");
        assets.objection = Asset<AudioClip>("Assets/Audio/CongoObjectionBeat0dot4.wav");
        assets.lobby = Asset<AudioClip>("Assets/Audio/lobbymenusong0dot7.wav");
        assets.evidenceCard = Asset<GameObject>("Assets/Prefabs/EvidenceCard.prefab");
        assets.playerPrefab = Asset<GameObject>("Assets/Prefabs/FreeCamPlayer.prefab");
        var original = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var evidenceScene = EditorSceneManager.OpenScene("Assets/Scenes/evidenceSelection.unity");
            var board = UnityEngine.Object.FindAnyObjectByType<EvidenceSelectionUI>();
            var canvas = board.evidenceContainer.GetComponentInParent<Canvas>();
            assets.evidenceBackground = GameObject.Find("BG").GetComponent<SpriteRenderer>().sprite;
            // Reuse the actual evidence scene canvas, preserving its grid, title and layout.
            var copy = UnityEngine.Object.Instantiate(canvas.gameObject);
            var copyBoard = copy.AddComponent<EvidenceSelectionUI>();
            copyBoard.evidenceContainer = copy.GetComponentsInChildren<Transform>(true).First(t=>t.name == board.evidenceContainer.name);
            copyBoard.evidenceCardPrefab = assets.evidenceCard;
            copyBoard.stickyNoteSprites = board.stickyNoteSprites;
            copyBoard.enabled = false;
            assets.evidenceCanvas = PrefabUtility.SaveAsPrefabAsset(copy,"Assets/Prefabs/CourtEvidenceCanvas.prefab");
            UnityEngine.Object.DestroyImmediate(copy);
            EditorSceneManager.OpenScene("Assets/Scenes/CaseSelection.unity");
            if (UnityEngine.Object.FindAnyObjectByType<PurrNet.NetworkManager>() == null)
                PrefabUtility.InstantiatePrefab(Asset<GameObject>("Assets/Externals/GameJam/Prefabs/SessionManager Jam.prefab"));
            if (UnityEngine.Object.FindAnyObjectByType<PurrNet.PlayerSpawner>() == null)
            {
                var spawner = new GameObject("Court Player Spawner").AddComponent<EvidencePlayerSpawner>();
                var serialized = new SerializedObject(spawner);
                serialized.FindProperty("_playerPrefab").objectReferenceValue = assets.playerPrefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.OpenScene("Assets/Scenes/MainGame.unity");
            var terrainShapes = UnityEngine.Object.FindObjectsByType<SpriteShapeController>(FindObjectsSortMode.None);
            if (terrainShapes.Length != 2) throw new Exception("Expected two terrain tracks.");
            foreach (var shape in terrainShapes)
            {
                if (shape.transform.Find("FinishLine") != null) continue;
                var cars = UnityEngine.Object.FindObjectsByType<CarDrawingWheelInstaller>(FindObjectsSortMode.None);
                var car = cars.OrderBy(c=>Vector3.Distance(c.transform.position,shape.transform.position)).First();
                // Finish at the far end of the authored terrain, opposite the starting chassis.
                var points = Enumerable.Range(0,shape.spline.GetPointCount()).Select(i=>shape.transform.TransformPoint(shape.spline.GetPosition(i))).ToArray();
                float min = points.Min(p=>p.x), max = points.Max(p=>p.x);
                float x = Mathf.Abs(car.transform.position.x-min) > Mathf.Abs(car.transform.position.x-max) ? min + .5f : max - .5f;
                var line = new GameObject("FinishLine",typeof(BoxCollider2D),typeof(CourtFinishLine));
                line.transform.SetParent(shape.transform,true);
                line.transform.position = new Vector3(x,points.Max(p=>p.y),shape.transform.position.z);
                var collider = line.GetComponent<BoxCollider2D>(); collider.isTrigger = true; collider.size = new Vector2(1,40);
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s=>s.path.EndsWith("CaseSelection.unity"))) scenes.Insert(1,new EditorBuildSettingsScene("Assets/Scenes/CaseSelection.unity",true));
            EditorBuildSettings.scenes = scenes.ToArray();
            assets.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
            EditorUtility.SetDirty(assets); AssetDatabase.SaveAssets();
            Validate();
        }
        catch (Exception error) { Debug.LogException(error); throw; }
        finally { if (original.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(original); }
    }
    public static void Validate()
    {
        var assets = CourtAssets.Load();
        if (assets == null || assets.cases.Length == 0 || assets.cases.Any(c=>c.evidence.Count < 2) ||
            assets.evidenceCanvas == null || assets.evidenceCard == null || assets.objection == null ||
            assets.bodyFont == null || assets.titleFont == null || assets.paper == null || assets.wood == null)
            throw new Exception("Court assets are incomplete.");
        if (TrialRules.Role(0) != PlayerType.Judge || TrialRules.Role(1) != PlayerType.Prosecutor || TrialRules.Role(2) != PlayerType.Defendant)
            throw new Exception("Role assignment failed.");
        float[] boundaries = {0,240,480,720,960,1020,1080,1140};
        for (int i=0;i<boundaries.Length;i++)
        {
            if (TrialRules.Turn(boundaries[i]) != i || TrialRules.Turn(TrialRules.TurnEnd(i)-.01f) != i ||
                TrialRules.Team(i) != (i%2==0?PlayerType.Prosecutor:PlayerType.Defendant)) throw new Exception("Turn schedule failed.");
        }
        if (TrialRules.TurnEnd(7) != 1200) throw new Exception("Trial length failed.");
        for (int i=0;i<10;i++)
            if (TrialRules.OwnsEvidence(PlayerType.Prosecutor,i) == TrialRules.OwnsEvidence(PlayerType.Defendant,i)) throw new Exception("Evidence partition failed.");
        Debug.Log("COURT VALIDATION PASSED");
    }
    [MenuItem("Court/Build WebGL Submission")]
    public static void BuildWebGL()
    {
        Validate();
        PlayerSettings.productName = "Motions in Motions";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.runInBackground = true;
        var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(), "Builds/CourtWebGL", BuildTarget.WebGL, BuildOptions.None);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("WebGL build failed: " + report.summary.result);
    }
    public static void BuildSmoke()
    {
        var original = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var source = Asset<GameObject>("Assets/Externals/GameJam/Prefabs/SessionManager Jam.prefab").GetComponent<PurrNet.NetworkManager>();
            var root = new GameObject("Smoke Network Manager");
            var manager = root.AddComponent<PurrNet.NetworkManager>();
            EditorUtility.CopySerialized(source, manager);
            var transport = root.AddComponent<PurrNet.Transports.WebTransport>();
            transport.serverPort = 15091; transport.address = "127.0.0.1"; transport.enableSSL = false;
            manager.transport = transport;
            manager.startServerFlags = 0; manager.startClientFlags = 0;
            root.AddComponent<CourtSmoke>();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/CourtSmoke.unity");
            var paths = new[]{"Assets/CourtSmoke.unity","Assets/Scenes/CaseSelection.unity","Assets/Scenes/MainGame.unity"};
            var report = BuildPipeline.BuildPlayer(paths, "Builds/CourtSmoke/CourtSmoke.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
            if(report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Smoke player build failed.");
        }
        catch (Exception error) { Debug.LogException(error); throw; }
        finally { if (original.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(original); }
    }
    public static void BuildFinalSmoke()
    {
        Build();
        BuildSmoke();
    }
}
