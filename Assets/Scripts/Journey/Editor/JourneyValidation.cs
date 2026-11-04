#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    const string Shots = "JourneyValidation.Shots";
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
        SessionState.SetString(Shots, Argument(args, "-journeyShots", "")); // 있으면 여정 중 실행에서만 보이는 효과를 찍는다
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
            if (EditorApplication.timeSinceStartup - started > 900)
                throw new TimeoutException("Validation exceeded 900 seconds.");
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
        // 식도 첫 안내("위를 올려다봐! 후두덮개가…")의 원래 길이: 후두덮개는 이 대사가 나오는 동안 닫혀야 한다.
        var esophagus = manager.stages.FirstOrDefault(s => s != null && s.stageId == "Esophagus");
        float lidLine = esophagus != null && esophagus.introLines.Length > 0 ? (esophagus.introLines[0].voice != null ? esophagus.introLines[0].voice.length : esophagus.introLines[0].holdSeconds) : 0;
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
        // 헤드셋 없이 돌리면 머리 추적 값이 0 이라 눈(VRCamera)이 발 높이에 붙는다. 이제는 플레이어의 눈높이 맞춤(PlayerEyeHeight)이
        // 실제 플레이(에디터)와 똑같이 서 있는 눈높이로 올린다 — 검사가 따로 올리면 에디터에서 바닥에 낀 화면을 놓친다.
        var head = GameObject.FindWithTag("VRCamera");
        foreach (var gaze in Object.FindObjectsByType<GazeSelector>(FindObjectsSortMode.None)) gaze.enabled = false;
        shotDir = SessionState.GetString(Shots, "");
        if (ShotsOn) Directory.CreateDirectory(shotDir);
        yield return Delay(2);
        var spawnPoint = GameObject.Find("SpawnPoint");
        float eyeHeight = head != null ? head.transform.position.y - (spawnPoint != null ? spawnPoint.transform.position.y : 0f) : -1f;
        Check(eyeHeight > 1.4f && eyeHeight < 1.8f, $"병원: 헤드셋 없이도 눈이 서 있는 높이 (바닥에서 {eyeHeight:0.00}m)");
        var nurse = Object.FindObjectsByType<HospitalNpc>(FindObjectsSortMode.None).FirstOrDefault(n => n.npcName == "간호사");
        Check(nurse != null && nurse.nodes.Length > 4 && nurse.nodes[4].onEnter.GetPersistentEventCount() > 0 && nurse.nodes[4].onEnter.GetPersistentTarget(0) != null, "병원: 간호사의 '의사 선생님께 가 보세요'가 할 일 판에 이어져 있다");
        UiShots();
        GuideNarrator.Instance.Skip();
        Check(SceneManager.GetActiveScene().name == "00_Lobby", "Lobby bootstrap");
        var portal = Object.FindFirstObjectByType<StageExitPortal>();
        portal.playOutro = false;
        portal.SendMessage("OnTriggerEnter", GameObject.FindWithTag("Player").GetComponent<CharacterController>());
        yield return Stage("01_Mouth", playerId, persistent);

        // 입: 씹기 → 양치를 안 하면?(충치가 생기는 과정) → 충치 치료 → 치태 세균 → 삼키기 → 식도
        var chew = Object.FindFirstObjectByType<ChewingSequence>();
        chew.chewCount = 2;
        chew.chewsPerSecond = 5;
        var lapse = Object.FindFirstObjectByType<DecayTimeLapse>();
        var treat = Object.FindFirstObjectByType<CavityTreatment>();
        var plaque = GameObject.Find("치태_세균_없애기").GetComponent<GermBlaster>();
        Check(treat.hiddenUntilBegin && treat.decay != null && !treat.decay.gameObject.activeSelf && !treat.IsTreating, "입: 처음엔 충치가 없다");
        lapse.stepSeconds = ShotsOn ? 1.6f : 0.3f; lapse.startLines = Lines(0.05f); lapse.cavityLines = Lines(0.05f);
        treat.doneLines = Lines(0.05f); plaque.clearedLines = Lines(0.05f);
        var zone = GameObject.Find("ChewZone").GetComponent<NarrationZone>();
        zone.lines = Lines(0.1f);
        zone.SendMessage("OnTriggerEnter", GameObject.FindWithTag("Player").GetComponent<CharacterController>());
        var spotList = lapse.spots.Where(t => t != null).ToList();
        Vector3 teeth = spotList.Aggregate(Vector3.zero, (a, t) => a + t.position) / Mathf.Max(1, spotList.Count);
        if (ShotsOn)
        {
            yield return Wait(() => chew.crumbs != null && chew.crumbs.particleCount > 0, "Chewing crumbs", 30);
            var cp = chew.chewPoint.position; var side = cp + new Vector3(cp.x < 0 ? 2.4f : -2.4f, 1.3f, -1.6f);
            Shot("1_씹기_부스러기_침", cp, 55, side);
        }
        yield return Wait(() => lapse.IsRunning || lapse.IsFinished, "Chewing finishes and the decay time-lapse starts", 40);
        Check(chew.ChewsDone >= chew.chewCount, "입: 씹기가 끝난 뒤 충치가 생기는 과정이 시작된다");
        if (ShotsOn)
        {
            yield return Delay(0.8 + 1.6 * 1.5);
            Shot("2_시간흐름_세균_늘어남", teeth, 70);
            Shot("2_시간흐름_충치_자라남", treat.decay.position, 40);
        }
        yield return Wait(() => treat.IsTreating, "Cavity treatment starts after the time-lapse", 30);
        Check(treat.decay.gameObject.activeSelf && treat.decay.localScale.sqrMagnitude > 0, "입: 시간이 흐른 뒤 충치가 생기고 치료가 시작된다");
        if (ShotsOn) { Shot("3_충치_생김", treat.decay.position, 40); Shot("3_세균_가득", teeth, 70); }
        treat.TreatNow();
        yield return Wait(() => plaque.IsActive, "Plaque mission starts after the cavity is treated", 20);
        Check(plaque.Remaining > 0, "입: 충치 치료 뒤 치태 세균 없애기(양치)가 시작된다");
        if (ShotsOn) { yield return Delay(0.4); Shot("4_치료끝_치태세균", teeth, 70); }
        plaque.DefeatAll();
        yield return Stage("02_Esophagus", playerId, persistent);
        Check(true, "Mouth: chew -> decay time-lapse -> cavity treatment -> plaque -> swallow -> esophagus");

        // 식도: 후두덮개가 첫 안내 대사가 나오는 동안 닫히고 침방울이 튄다 (실제로는 이 대사가 끝나기 전 출발점에 있다)
        var lid = Object.FindFirstObjectByType<HingeClose>();
        if (lid != null)
        {
            if (ShotsOn) { yield return Delay(lid.delay + lid.seconds * .5f - .2f); Shot("1_후두덮개_닫히는중", lid.transform.position + Vector3.down * .8f, 75); }
            float from = Time.time;
            yield return Wait(() => Quaternion.Angle(lid.transform.rotation, lid.closed) < 2f, "Epiglottis closes", 12);
            float took = Time.time - from;
            Check(took <= lidLine, $"식도: 후두덮개가 첫 안내 대사 동안 닫힘 ({took:0.0}초 ≤ {lidLine:0.0}초)");
            bool splashed = false; // 다 닫힌 그 순간 튄다 (닫히는 마지막 몇 프레임은 2° 안이라 조금 기다린다)
            double splashEnd = EditorApplication.timeSinceStartup + 1;
            while (!splashed && EditorApplication.timeSinceStartup < splashEnd) { splashed = lid.splash == null || lid.splash.isPlaying || lid.splash.particleCount > 0; yield return null; }
            Check(splashed, "식도: 후두덮개가 닫히며 침방울이 튐");
            if (ShotsOn) Shot("2_후두덮개_닫힘_침방울", lid.transform.position + Vector3.down * .8f, 75);
        }
        else Check(false, "식도: 후두덮개 경첩이 없음");
        yield return RideTo("03_Stomach", playerId, persistent);

        // 위: 둥실거리는 것(음식·단백질 사슬)이 멀미 나지 않게 느리게 움직인다
        var floats = Object.FindObjectsByType<GentleFloat>(FindObjectsSortMode.None);
        if (ShotsOn && floats.Length > 0) Shot("1_음식_위액_거품", floats.OrderBy(f => (f.transform.position - EyePos()).sqrMagnitude).First().transform.position, 60);
        var last = floats.ToDictionary(f => f, f => (f.transform.position, f.transform.rotation));
        float fastest = 0, turning = 0;
        double sampleEnd = EditorApplication.timeSinceStartup + 2;
        while (EditorApplication.timeSinceStartup < sampleEnd)
        {
            yield return null;
            float dt = Mathf.Max(1e-3f, Time.deltaTime);
            foreach (var f in floats)
            {
                if (f == null) continue;
                var (p, r) = last[f];
                fastest = Mathf.Max(fastest, Vector3.Distance(p, f.transform.position) / dt);
                turning = Mathf.Max(turning, Quaternion.Angle(r, f.transform.rotation) / dt);
                last[f] = (f.transform.position, f.transform.rotation);
            }
        }
        Check(floats.Length > 0 && fastest < .2f && turning < 30f, $"위: 둥실거림이 느리다 ({floats.Length}개, 가장 빠름 {fastest:0.00}m/s · {turning:0}°/s)");

        var churn = Object.FindFirstObjectByType<StomachChurn>();
        churn.digestDuration = 0.5f;
        var churnZone = GameObject.Find("ChurnZone").GetComponent<NarrationZone>();
        churnZone.lines = Lines(0.1f);
        churnZone.SendMessage("OnTriggerEnter", GameObject.FindWithTag("Player").GetComponent<CharacterController>());
        var gate = Object.FindObjectsByType<SphincterGate>(FindObjectsSortMode.None).Single();
        yield return Wait(() => gate.IsOpen, "Stomach pylorus opens after digestion", 60);
        Check(gate.IsOpen, "Stomach digestion opens pylorus");
        if (ShotsOn) { yield return Delay(2.5); Shot("2_미즙_흐름_유문", new Vector3(-1.2f, .4f, 7f), 55); }
        // 위는 잠수정으로 위액 위를 지나 유문을 통과한다. 유문 앞 정차는 유문 열림과 헬리코박터균 퇴치가 둘 다 끝나야 풀린다.
        yield return RideThroughMissions("04_SmallIntestine", playerId, persistent);
        var absorption = Object.FindFirstObjectByType<NutrientAbsorption>();
        absorption.completeLines = Lines(0.1f);
        if (ShotsOn) yield return SmallIntestineShots(absorption);
        bool absorbedAll = false;
        absorption.onComplete.AddListener(() => absorbedAll = true);
        foreach (var nutrient in Object.FindObjectsByType<Nutrient>(FindObjectsSortMode.None)) absorption.Absorb(nutrient);
        yield return Wait(() => absorbedAll, "Nutrient absorption completion", 20);
        Check(true, "All nutrients absorb and completion event fires");
        // 소장: 효소 짝 맞추기 3라운드, 대장: 해로운 균 물리치기 → 유익균 먹이 주기. 모두 끝내야 잠수정이 다시 출발한다.
        yield return RideThroughMissions("05_LargeIntestine", playerId, persistent);
        yield return RideThroughMissions("06_Theater", playerId, persistent);

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
        if (ShotsOn) { yield return Delay(1.0); UiShots(); } // 안내판이 자리 잡은 뒤
        var player = GameObject.FindWithTag("Player");
        Check(player != null && player.GetInstanceID() == playerId, name + " keeps the original player");
        Check(persistent != null && JourneyManager.Instance.transform.root == persistent && persistent.parent == null,
            name + " keeps the original persistent root");
        Check(GuideNarrator.Instance != null, name + " has a narrator");
    }

    /// <summary>잠수정을 태우고, 정차 지점에서 시작되는 겨냥 임무는 바로 끝내며 다음 장면까지 간다.</summary>
    static IEnumerator RideThroughMissions(string next, int playerId, Transform persistent)
    {
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        if (ride == null) throw new Exception("Missing submarine before " + next);
        float realSpeed = ride.speed, realEase = ride.easeSeconds;
        ride.speed = 0.5f; ride.easeSeconds = 0.1f;
        foreach (var stop in ride.stops) stop.lines = Lines(0.1f);
        foreach (var hunt in Object.FindObjectsByType<GermBlaster>(FindObjectsSortMode.None)) { hunt.startLines = Lines(0.05f); hunt.clearedLines = Lines(0.05f); hunt.helpfulHitLines = Lines(0.05f); }
        ride.Board();
        int cleared = 0;
        double end = EditorApplication.timeSinceStartup + 150;
        string here = SceneManager.GetActiveScene().name;
        ResetEye();
        while (SceneManager.GetActiveScene().name != next)
        {
            SampleEye();
            foreach (var hunt in Object.FindObjectsByType<GermBlaster>(FindObjectsSortMode.None))
                if (hunt.IsActive)
                {
                    yield return Delay(0.5); // 표적이 떠 있는 동안도 눈과의 거리를 잰다
                    SampleEye();
                    if (ShotsOn && hunt != null && hunt.IsActive)
                    {
                        var targets = hunt.transform.Cast<Transform>().Where(t => t.gameObject.activeSelf).ToList();
                        if (targets.Count > 0) Shot("임무_" + hunt.name, targets.Aggregate(Vector3.zero, (a, t) => a + t.position) / targets.Count, 75);
                        if (hunt.feedSource != null) yield return FeedShots(hunt);
                    }
                    if (hunt != null && hunt.IsActive)
                    {
                        // 임무가 끝나 다시 출발하는 몇 초는 실제 속도로 달려, 남은 표적이 비켜 가기 전에 잠수정이 지나가는지 본다
                        ride.speed = realSpeed; ride.easeSeconds = realEase;
                        hunt.DefeatAll(); cleared++;
                        double settle = EditorApplication.timeSinceStartup + 2.5;
                        while (EditorApplication.timeSinceStartup < settle) { SampleEye(); yield return null; }
                        ride.speed = 0.5f; ride.easeSeconds = 0.1f;
                    }
                }
            if (EditorApplication.timeSinceStartup > end) throw new TimeoutException("Ride through missions to " + next);
            yield return null;
        }
        Check(cleared > 0, "Missions on the way to " + next + " start and clear (" + cleared + ")");
        CheckEye(here);
        yield return Stage(next, playerId, persistent);
    }

    // ───── 실행 중 촬영 (-journeyShots 폴더): 실행해야만 생기는 효과(부스러기·세균·빛 꼬리·흐르는 적혈구 등) ─────
    static string shotDir, lastDiagScene;
    static bool ShotsOn => !string.IsNullOrEmpty(shotDir);
    static Vector3 EyePos() { var h = GameObject.FindWithTag("VRCamera"); return h != null ? h.transform.position : Vector3.zero; }
    static void Shot(string view, Vector3 look, float fov, Vector3? from = null)
    {
        var eye = from ?? EyePos();
        if ((look - eye).sqrMagnitude < 1e-4f) look = eye + Vector3.forward;
        // 헤드셋 화면 전환 막(OVRScreenFade)은 배치 실행에서 프레임 끝 대기가 돌지 않아 반쯤 덮인 채 남는다 → 찍는 동안만 끈다
        // 멀미 완화 테두리도 눈(헤드셋 카메라) 앞에만 있어야 하므로 다른 쪽을 보는 촬영 카메라에서는 끈다
        var hide = new List<Renderer>();
        foreach (var f in Object.FindObjectsByType<OVRScreenFade>(FindObjectsSortMode.None)) { var fr = f.GetComponent<MeshRenderer>(); if (fr != null && fr.enabled) hide.Add(fr); }
        var ring = GameObject.Find("멀미완화_테두리")?.GetComponent<MeshRenderer>();
        if (ring != null && ring.enabled) hide.Add(ring);
        foreach (var r in hide) r.enabled = false;
        // 융털 카펫은 실행 중 눈 가까운 구간만 그린다 → 촬영 카메라 자리 가까운 구간을 잠깐 켠다
        var carpetOn = new List<Renderer>();
        foreach (var carpet in Object.FindObjectsByType<VilliCarpet>(FindObjectsSortMode.None))
            foreach (var chunk in carpet.Chunks)
                if (chunk != null && !chunk.enabled && chunk.bounds.SqrDistance(eye) < carpet.drawRange * carpet.drawRange) { chunk.enabled = true; carpetOn.Add(chunk); }
        string scene = SceneManager.GetActiveScene().name;
        if (lastDiagScene != scene)
        {
            lastDiagScene = scene;
            var near = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject.activeInHierarchy && r.bounds.SqrDistance(eye) < 1f)
                .Select(r => $"{r.transform.root.name}/{r.name}({r.GetType().Name} {r.bounds.size:F1})");
            var podRide = Object.FindFirstObjectByType<CapsuleRide>();
            string podEye = podRide != null ? $" / 잠수정 기준 눈 {podRide.transform.InverseTransformPoint(eye):F2} (탑승 {podRide.IsRiding})" : "";
            Debug.Log($"[촬영] {scene} 눈 1m 안 렌더러: {string.Join(", ", near)}{podEye}");
        }
        JourneyRealism.Shoot(shotDir, scene, view, eye, Quaternion.LookRotation(look - eye), fov);
        foreach (var r in hide) r.enabled = true;
        foreach (var r in carpetOn) r.enabled = false;
    }

    /// <summary>탐험가가 보는 그대로: 정면과 30° 아래(걷는 장면 안내판 자리). Quest 시야 약 95°.</summary>
    static void UiShots()
    {
        if (!ShotsOn) return;
        var eye = JourneyEye.Get(); if (eye == null) return;
        var fwd = Vector3.ProjectOnPlane(eye.forward, Vector3.up); if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward; fwd.Normalize();
        Shot("UI_앞", eye.position + fwd * 5, 95);
        Shot("UI_아래", eye.position + Quaternion.AngleAxis(30, Vector3.Cross(Vector3.up, fwd)) * fwd * 5, 95);
    }

    static IEnumerator SmallIntestineShots(NutrientAbsorption absorption)
    {
        var ride = Object.FindFirstObjectByType<CapsuleRide>();
        var shell = GameObject.Find("VillusZoom")?.transform.Find("융털_겉");
        if (ride != null && ride.cart != null && ride.cart.Spline != null && shell != null)
        {
            var spline = ride.cart.Spline; var target = shell.GetComponent<Renderer>().bounds.center;
            float lo = 0, hi = 1;
            for (int k = 0; k < 30; k++) { float mid = (lo + hi) * .5f; if (((Vector3)spline.EvaluatePosition(mid)).z < target.z - 3f) lo = mid; else hi = mid; }
            Vector3 at = spline.EvaluatePosition(lo);
            Shot("1_융털_적혈구_지방산", target, 45, at);
            var vessel = GameObject.Find("벽_혈관")?.transform.Find("혈관_1")?.GetComponent<Renderer>();
            if (vessel != null)
            {
                Vector3 lane = spline.EvaluatePosition(.55f);
                var mid = vessel.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v => vessel.transform.TransformPoint(v)).OrderBy(v => Mathf.Abs(v.z - lane.z)).First();
                Shot("2_벽_혈관_적혈구", mid, 45, mid + (lane - mid).normalized * 2.2f + Vector3.back);
            }
        }
        var n = Object.FindObjectsByType<Nutrient>(FindObjectsSortMode.None).OrderBy(x => (x.transform.position - EyePos()).sqrMagnitude).FirstOrDefault();
        if (n != null)
        {
            var start = n.transform.position; var flying = n.transform;
            absorption.Absorb(n);
            yield return Delay(.35);
            Shot("3_영양소_빛꼬리", flying != null ? (start + flying.position) / 2 : start, 70); // 날아가는 길 가운데를 본다
        }
    }

    /// <summary>유익균 먹이 주기: 식이섬유가 맞힌 유익균에게 날아가 먹히는 모습.</summary>
    static IEnumerator FeedShots(GermBlaster hunt)
    {
        var germ = hunt.transform.Cast<Transform>().FirstOrDefault(t => t.gameObject.activeSelf && t.name.Contains("Probiotic"));
        if (germ == null) yield break;
        var food = hunt.feedSource.transform.Cast<Transform>().Where(t => t.gameObject.activeInHierarchy).OrderBy(t => (t.position - germ.position).sqrMagnitude).FirstOrDefault();
        var feed = typeof(GermBlaster).GetMethod("Feed", BindingFlags.NonPublic | BindingFlags.Instance);
        if (food == null || feed == null) yield break;
        hunt.StartCoroutine((IEnumerator)feed.Invoke(hunt, new object[] { food, germ }));
        yield return Delay(.35);
        Shot("식이섬유_날아감", germ.position, 55);
        yield return Delay(.6);
        Shot("유익균_배부름", germ.position, 45);
    }

    // 떠다니는 것(무리·흐름·임무 표적·둥실거리는 것)이 탐험가 눈에서 1.2m 안으로 들어오는지 잰다 (눈앞을 지나가면 어지럽다).
    static float nearestToEye; static string nearestName; static bool eyeSeen;
    static void ResetEye() { nearestToEye = float.MaxValue; nearestName = "-"; eyeSeen = false; }
    static void SampleEye()
    {
        // 탐험가 눈(CenterEyeAnchor, VRCamera 태그). 헤드셋 없이 돌리면 Camera.main 이 빌 수 있다.
        var cam = GameObject.FindWithTag("VRCamera") ?? (Camera.main != null ? Camera.main.gameObject : null);
        if (cam == null) return;
        eyeSeen = true;
        var eye = cam.transform.position;
        void Test(Transform t)
        {
            if (t == null) return;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is TrailRenderer) continue;
                // 움직이는 캐릭터(스킨 메시)의 renderer.bounds 는 부모 크기가 걸리면 엉뚱하게 커진다 → 메시 자체 크기로 잰다
                var mesh = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                Vector3 centre = r.bounds.center; float d;
                if (mesh != null)
                {
                    centre = r.transform.TransformPoint(mesh.bounds.center);
                    var s = r.transform.lossyScale;
                    float radius = Vector3.Scale(mesh.bounds.extents, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z))).magnitude;
                    d = Mathf.Max(0, Vector3.Distance(centre, eye) - radius);
                }
                else d = Mathf.Sqrt(r.bounds.SqrDistance(eye));
                if (d < nearestToEye) { nearestToEye = d; nearestName = $"{r.transform.root.name}/{r.name} 물체 {centre:F1} 눈 {eye:F1}"; }
            }
        }
        foreach (var s in Object.FindObjectsByType<FloatingSwarm>(FindObjectsSortMode.None)) foreach (Transform c in s.transform) Test(c);
        foreach (var f in Object.FindObjectsByType<SplineFlow>(FindObjectsSortMode.None)) foreach (Transform c in f.transform) Test(c);
        foreach (var h in Object.FindObjectsByType<GermBlaster>(FindObjectsSortMode.None)) foreach (Transform c in h.transform) Test(c);
        foreach (var g in Object.FindObjectsByType<GentleFloat>(FindObjectsSortMode.None)) Test(g.transform);
    }
    static void CheckEye(string scene)
    {
        if (!eyeSeen) { Check(false, scene + ": 눈 둘레 거리 — 눈 위치(VRCamera)를 찾지 못함"); return; }
        if (nearestToEye == float.MaxValue) { Check(true, scene + ": 눈 둘레 거리 — 떠다니는 것 없음"); return; }
        Check(nearestToEye >= 1.2f, $"{scene}: 떠다니는 것이 눈 1.2m 안으로 안 들어옴 (가장 가까이 {nearestToEye:0.00}m, {nearestName})");
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
        string here = SceneManager.GetActiveScene().name;
        ResetEye();
        double end = EditorApplication.timeSinceStartup + 90;
        while (SceneManager.GetActiveScene().name != next)
        {
            SampleEye();
            foreach (var hunt in Object.FindObjectsByType<GermBlaster>(FindObjectsSortMode.None)) if (hunt.IsActive) hunt.DefeatAll(); // 식도 위산 방울 등
            if (EditorApplication.timeSinceStartup > end) throw new TimeoutException("Ride to " + next);
            yield return null;
        }
        CheckEye(here);
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

