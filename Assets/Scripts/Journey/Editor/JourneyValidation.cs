#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Batch-only Play mode regression and scene-flow checks; never saves scenes.</summary>
[InitializeOnLoad]
public static class JourneyValidation
{
    const string Active = "JourneyValidation.Active";
    const string Mode = "JourneyValidation.Mode";
    const string Report = "JourneyValidation.Report";
    static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
    static readonly List<string> checks = new List<string>();
    static readonly HashSet<string> errors = new HashSet<string>();
    static double started;
    static bool finishing;
    static int failures;

    static JourneyValidation()
    {
        EditorApplication.playModeStateChanged += StateChanged;
        if (SessionState.GetBool(Active, false))
            EditorApplication.delayCall += Resume;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run with -batchmode; this runner does not alter open editor sessions.");
        var args = Environment.GetCommandLineArgs();
        string mode = Argument(args, "-journeyMode", "regression");
        SessionState.SetString(Mode, mode);
        SessionState.SetString(Report, Argument(args, "-journeyReport", "Tools/journey_validation.txt"));
        SessionState.SetBool(Active, true);
        if (mode == "polish")
            JourneyPolishValidation.PrepareScenes();
        else if (mode == "effects")
            JourneyEffectValidation.PrepareScenes();
        else if (mode == "journey")
            EditorSceneManager.OpenScene("Assets/Scenes/Journey/00_Lobby.unity");
        else
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Save neither the scene nor project settings. XR device/input checks remain separate.
        EditorApplication.EnterPlaymode();
    }

    static string Argument(string[] args, string key, string fallback)
    {
        int i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) Resume();
    }

    static void Resume()
    {
        if (!EditorApplication.isPlaying || routines.Count > 0 || finishing) return;
        started = EditorApplication.timeSinceStartup;
        Application.logMessageReceived += Log;
        string mode = SessionState.GetString(Mode, "regression");
        routines.Push(mode == "journey" ? Journey() : mode == "effects" ? JourneyEffectValidation.RunChecks(Check) : mode == "polish" ? JourneyPolishValidation.RunChecks(Check) : Regression());
        EditorApplication.update += Tick;
    }

    static void Log(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + trace);
    }

    static void Tick()
    {
        if (finishing) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 480)
                throw new TimeoutException("Validation exceeded 480 seconds.");
            if (!EditorApplication.isPlaying) throw new Exception("Play mode ended before validation completed.");
            if (routines.Count == 0) { Finish(); return; }
            var routine = routines.Peek();
            if (!routine.MoveNext()) routines.Pop();
            else if (routine.Current is IEnumerator child) routines.Push(child);
        }
        catch (Exception exception)
        {
            Check(false, exception.ToString());
            Finish();
        }
    }

    static void Check(bool condition, string label)
    {
        checks.Add((condition ? "PASS " : "FAIL ") + label);
        if (!condition) failures++;
        Debug.Log("[JourneyValidation] " + checks[checks.Count - 1]);
    }

    static IEnumerator Wait(Func<bool> predicate, string label, double seconds = 40)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (!predicate())
        {
            if (EditorApplication.timeSinceStartup > end) throw new TimeoutException(label);
            yield return null;
        }
    }

    static IEnumerator Delay(double seconds)
    {
        double end = EditorApplication.timeSinceStartup + seconds;
        while (EditorApplication.timeSinceStartup < end) yield return null;
    }

    static NarrationLine[] Lines(float seconds) => new[] { new NarrationLine { text = "검증", holdSeconds = seconds } };

    static IEnumerator Regression()
    {
        var root = new GameObject("ValidationPersistent");
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.SetParent(root.transform);
        var cc = player.AddComponent<CharacterController>();
        var locomotion = player.AddComponent<ComfortLocomotion>();
        var guide = new GameObject("Guide");
        guide.transform.SetParent(root.transform);
        var narrator = guide.AddComponent<GuideNarrator>();
        narrator.gapBetweenLines = 0;
        narrator.holdAfterLast = 0;
        var follower = guide.AddComponent<FloatingFollower>();
        follower.player = player.transform;
        var ride = new GameObject("Capsule").AddComponent<CapsuleRide>();
        ride.advanceOnArrive = false;
        yield return Delay(0.2);

        ride.Board();
        Check(root.transform.parent == null, "Boarding keeps the persistent root outside the capsule");
        Check(guide.transform.parent == ride.transform, "Only the guide boards the guide seat");
        Check(!follower.enabled, "Guide following pauses while riding");
        Check(!cc.enabled, "Player movement is locked while riding");
        ride.GetOff();
        Check(player.transform.parent == root.transform && guide.transform.parent == root.transform,
            "Disembarking restores player and guide parents");
        cc.enabled = false;
        follower.enabled = false;
        ride.Board();
        ride.GetOff();
        Check(!cc.enabled && !follower.enabled, "Disembarking preserves previously disabled components");
        cc.enabled = true;
        follower.enabled = true;

        var fadeObject = new GameObject("ValidationFade");
        fadeObject.transform.SetParent(player.transform);
        var fade = fadeObject.AddComponent<OVRScreenFade>();
        fade.fadeOnStart = false;
        locomotion.fadeSeconds = 0.2f;
        yield return Delay(0.1);
        locomotion.StartCoroutine("Blink", new Vector3(10, 0, 0));
        yield return Delay(0.05);
        ride.Board();
        yield return Delay(0.4);
        Check(player.transform.localPosition.sqrMagnitude < 0.001f, "Boarding cancels an in-flight teleport");
        // OVRScreenFade uses WaitForEndOfFrame, which does not advance in editor batch mode.
        checks.Add("NOT TESTED Fade completion/visibility: requires Game view or a headset (WaitForEndOfFrame).");
        ride.GetOff();

        var zone = new GameObject("Zone", typeof(BoxCollider)).AddComponent<NarrationZone>();
        zone.lines = Lines(0.2f);
        int finished = 0;
        zone.onFinished.AddListener(() => finished++);
        narrator.Play(Lines(30));
        zone.SendMessage("OnTriggerEnter", cc);
        Check(finished == 0, "Replacing prior speech does not finish the new zone early");
        yield return Wait(() => !narrator.IsSpeaking, "Zone speech completion", 5);
        Check(finished == 1, "Zone completes exactly once after its own speech");

        var disabledZone = new GameObject("DisabledZone", typeof(BoxCollider)).AddComponent<NarrationZone>();
        disabledZone.lines = Lines(30);
        int staleCallbacks = 0;
        disabledZone.onFinished.AddListener(() => staleCallbacks++);
        disabledZone.SendMessage("OnTriggerEnter", cc);
        disabledZone.enabled = false;
        narrator.Skip();
        Check(staleCallbacks == 0, "Disabled zones cannot receive stale speech completion");

        var emptyZone = new GameObject("EmptyZone", typeof(BoxCollider)).AddComponent<NarrationZone>();
        emptyZone.lines = new NarrationLine[0];
        int emptyFinished = 0;
        emptyZone.onFinished.AddListener(() => emptyFinished++);
        emptyZone.SendMessage("OnTriggerEnter", cc);
        Check(emptyFinished == 1, "Empty zone narration still completes once");

        // 안내가 최소 탑승 대기 시간보다 긴 경우에도 끝까지 듣고 출발한다.
        var introStage = ScriptableObject.CreateInstance<OrganStage>();
        introStage.stageId = "NarrationBoardingCheck";
        introStage.sceneName = SceneManager.GetActiveScene().name;
        introStage.introLines = Lines(1.2f);
        var introManager = root.AddComponent<JourneyManager>();
        introManager.stages.Add(introStage);
        introManager.introDelay = .1f;
        var automaticRide = new GameObject("IntroWaitingCapsule").AddComponent<CapsuleRide>();
        automaticRide.autoBoard = true;
        automaticRide.autoBoardDelay = .05f;
        automaticRide.advanceOnArrive = false;
        yield return Wait(() => introManager.IsIntroPending && narrator.IsSpeaking, "입장 안내 시작", 5);
        yield return Delay(.2);
        Check(!automaticRide.IsRiding && narrator.IsSpeaking, "입장 안내 중에는 최소 대기 시간이 지나도 자동 출발하지 않음");
        yield return Wait(() => automaticRide.IsRiding, "입장 안내 뒤 자동 출발", 5);
        Check(!introManager.IsIntroPending && !narrator.IsSpeaking, "입장 안내가 끝난 뒤 자동 탑승 재개");
        automaticRide.GetOff();
        Object.Destroy(automaticRide.gameObject);
        Object.Destroy(introManager);
        Object.Destroy(introStage);
        yield return null;

        var marker = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == "TeleportMarker" && t.gameObject.scene.IsValid());
        var previous = SceneManager.GetActiveScene();
        var next = SceneManager.CreateScene("ValidationAfterTransition");
        SceneManager.SetActiveScene(next);
        var unload = SceneManager.UnloadSceneAsync(previous);
        yield return Wait(() => unload.isDone, "Unload regression scene");
        Check(marker != null, "Teleport marker survives scene changes with the persistent player");
        Check(player != null && narrator != null, "Player and narrator remain alive after unloading the scene");
        Object.Destroy(root);
    }

    static IEnumerator Journey()
    {
        yield return Wait(() => JourneyManager.Instance != null && GuideNarrator.Instance != null, "Lobby bootstrap");
        var manager = JourneyManager.Instance;
        var persistent = manager.transform.root;
        int playerId = GameObject.FindWithTag("Player").GetInstanceID();
        manager.fadeDuration = 0.05f;
        manager.introDelay = 0.05f;
        // Runtime clones only: accelerate narration without changing source assets.
        for (int i = 0; i < manager.stages.Count; i++)
        {
            var copy = Object.Instantiate(manager.stages[i]);
            copy.introLines = Lines(0.05f);
            copy.outroLines = Lines(0.05f);
            manager.stages[i] = copy;
        }
        GuideNarrator.Instance.gapBetweenLines = 0;
        GuideNarrator.Instance.holdAfterLast = 0;
        foreach (var gaze in Object.FindObjectsByType<GazeSelector>(FindObjectsSortMode.None)) gaze.enabled = false;
        yield return Delay(2);
        GuideNarrator.Instance.Skip();
        Check(SceneManager.GetActiveScene().name == "00_Lobby", "Lobby bootstrap");
        var portal = Object.FindFirstObjectByType<StageExitPortal>();
        portal.playOutro = false;
        portal.SendMessage("OnTriggerEnter", GameObject.FindWithTag("Player").GetComponent<CharacterController>());
        yield return Stage("01_Mouth", playerId, persistent);

        var chew = Object.FindFirstObjectByType<ChewingSequence>();
        chew.chewCount = 2;
        chew.chewsPerSecond = 5;
        var zone = GameObject.Find("ChewZone").GetComponent<NarrationZone>();
        zone.lines = Lines(0.1f);
        zone.SendMessage("OnTriggerEnter", GameObject.FindWithTag("Player").GetComponent<CharacterController>());
        yield return Stage("02_Esophagus", playerId, persistent);
        Check(true, "Mouth zone -> chewing -> swallowing -> esophagus");
        yield return RideTo("03_Stomach", playerId, persistent);

        var churn = Object.FindFirstObjectByType<StomachChurn>();
        churn.digestDuration = 0.5f;
        var churnZone = GameObject.Find("ChurnZone").GetComponent<NarrationZone>();
        churnZone.lines = Lines(0.1f);
        churnZone.SendMessage("OnTriggerEnter", GameObject.FindWithTag("Player").GetComponent<CharacterController>());
        var gate = Object.FindObjectsByType<SphincterGate>(FindObjectsSortMode.None).Single();
        yield return Wait(() => gate.IsOpen, "Stomach pylorus opens after digestion", 60);
        Check(gate.IsOpen, "Stomach digestion opens pylorus");
        // 위는 잠수정으로 위액 위를 지나 유문을 통과한다(유문 앞 정차는 문이 열리면 풀린다).
        yield return RideTo("04_SmallIntestine", playerId, persistent);
        Check(true, "Stomach submarine waits for the pylorus and continues to the small intestine");
        var absorption = Object.FindFirstObjectByType<NutrientAbsorption>();
        absorption.completeLines = Lines(0.1f);
        bool absorbedAll = false;
        absorption.onComplete.AddListener(() => absorbedAll = true);
        foreach (var nutrient in Object.FindObjectsByType<Nutrient>(FindObjectsSortMode.None)) absorption.Absorb(nutrient);
        yield return Wait(() => absorbedAll, "Nutrient absorption completion", 20);
        Check(true, "All nutrients absorb and completion event fires");
        yield return RideTo("05_LargeIntestine", playerId, persistent);
        // 대장: 정차 지점에서 해로운 균 물리치기가 시작되고, 다 없애야 잠수정이 다시 출발한다.
        var blaster = Object.FindFirstObjectByType<GermBlaster>();
        var colonRide = Object.FindFirstObjectByType<CapsuleRide>();
        colonRide.speed = 0.5f; colonRide.easeSeconds = 0.1f;
        foreach (var stop in colonRide.stops) stop.lines = Lines(0.1f);
        blaster.startLines = Lines(0.05f); blaster.clearedLines = Lines(0.05f);
        colonRide.Board();
        yield return Wait(() => blaster.IsActive, "Germ game starts at the colon stop", 30);
        Check(blaster.IsActive && blaster.Remaining > 0, "Harmful germs appear in front of the submarine");
        blaster.DefeatAll();
        yield return Stage("06_Theater", playerId, persistent);
        Check(true, "Clearing harmful germs releases the submarine to the theater");

        // Exercise the existing no-video fallback; video/audio quality needs a device run.
        var ending = Object.FindFirstObjectByType<TheaterEnding>();
        if (ending.player != null) ending.player.Stop();
        ending.StopAllCoroutines();
        ending.fallbackSeconds = 0.2f;
        ending.delayAfterVideo = 0.1f;
        ending.StartCoroutine("Fallback");
        yield return Stage("00_Lobby", playerId, persistent);
        Check(true, "Theater fallback and outro return to lobby");
        Check(Object.FindObjectsByType<JourneyManager>(FindObjectsSortMode.None).Length == 1,
            "One persistent manager after the complete round trip");
    }

    static IEnumerator Stage(string name, int playerId, Transform persistent)
    {
        yield return Wait(() => SceneManager.GetActiveScene().name == name && JourneyManager.Instance != null && !JourneyManager.Instance.IsTransitioning,
            "Reach " + name, 90);
        yield return Delay(0.2);
        var player = GameObject.FindWithTag("Player");
        Check(player != null && player.GetInstanceID() == playerId, name + " keeps the original player");
        Check(persistent != null && JourneyManager.Instance.transform.root == persistent && persistent.parent == null,
            name + " keeps the original persistent root");
        Check(GuideNarrator.Instance != null, name + " has a narrator");
    }

    static IEnumerator RideTo(string next, int playerId, Transform persistent)
    {
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        if (ride == null) throw new Exception("Missing capsule before " + next);
        ride.speed = 0.5f;
        ride.easeSeconds = 0.1f;
        foreach (var stop in ride.stops) stop.lines = Lines(0.1f);
        ride.Board();
        Check(persistent.parent == null, "Persistent root remains independent while riding toward " + next);
        yield return Stage(next, playerId, persistent);
    }

    static void Finish()
    {
        finishing = true;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Log;
        foreach (string error in errors) checks.Add("UNITY ERROR " + error);
        failures += errors.Count;
        string path = SessionState.GetString(Report, "Tools/journey_validation.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "Mode: " + SessionState.GetString(Mode, "") + "\n" +
            "UTC: " + DateTime.UtcNow.ToString("O") + "\n" + string.Join("\n", checks) +
            "\nFailures: " + failures + "\nLimit: accelerated desktop logic checks; no headset/input/audio/visual performance verification.\n");
        SessionState.SetBool(Active, false);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
#endif

