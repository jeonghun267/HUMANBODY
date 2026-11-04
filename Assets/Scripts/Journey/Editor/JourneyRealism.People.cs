#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 병원의 사람들(의사·간호사)과 대화, 마지막 진료실(퀴즈·보고서·만든 사람), 체험관 운영 장치.
/// 사람 모형: Assets/Models/Sketchfab (CC BY 4.0, 출처는 CREDITS.md).
/// </summary>
public static partial class JourneyRealism
{
    const string PeopleDir = "Assets/Models/Sketchfab";

    // ───────────────────────────── 대사 ─────────────────────────────

    static NarrationLine TalkLine(string stage, string key)
    {
        if (content == null) content = JObject.Parse(System.IO.File.ReadAllText("Assets/Data/journey_content.json"));
        var s = content["stages"].FirstOrDefault(x => (string)x["stageId"] == stage);
        string text = (string)s?["dialogue"]?[key]?["text"] ?? "";
        return new NarrationLine { text = text, voice = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Sounds/Narration/{stage}_talk_{key}.mp3"), holdSeconds = 4f };
    }

    static DialogueNode Node(string stage, string key, int next = -1, params DialogueChoice[] choices)
    {
        var line = TalkLine(stage, key);
        return new DialogueNode { text = line.text, voice = line.voice, next = next, choices = choices, onEnter = new UnityEvent() };
    }

    static DialogueChoice Choice(string label, int next) => new DialogueChoice { label = label, next = next };

    // ───────────────────────────── 사람 ─────────────────────────────

    static GameObject Person(string file, string name, Vector3 position, float yaw, float height, Transform parent)
    {
        var old = parent != null ? parent.Find(name) : null; if (old != null) Object.DestroyImmediate(old.gameObject);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PeopleDir}/{file}");
        if (prefab == null) { Debug.LogWarning("[사람] 모형 없음: " + file); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        StandUp(go, yaw);
        // 모형에 든 움직임은 쓰지 않는다: 의사 모형의 움직임은 두 팔을 옆으로 든 채 손짓만 하는 동작이라 가만히 서 있으면 T자세처럼 보였다.
        // 팔은 몸 옆으로 내리고(RelaxArms), 숨쉬기·고개 돌리기·끄덕임(HospitalNpc)으로 살아 있게 한다.
        foreach (var a in go.GetComponentsInChildren<Animation>(true)) { a.playAutomatically = false; a.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(a); }
        RelaxArms(go);
        var b = PersonBounds(go);
        if (b.size.y > 1e-3f) go.transform.localScale *= height / b.size.y;
        b = PersonBounds(go);
        go.transform.position += new Vector3(position.x - b.center.x, -b.min.y, position.z - b.center.z);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true; }
        b = PersonBounds(go);
        Debug.Log($"[사람] {name}: 키 {b.size.y:0.00}m, 폭 {b.size.x:0.00}×{b.size.z:0.00}m, 발 {b.min.y:0.00}");
        return go;
    }

    /// <summary>
    /// 뼈대에 붙은 모양은 렌더러 상자가 실제 모습과 다를 수 있어, 화면에 그려지는 것과 똑같이 정점마다 뼈 행렬 × 바인드 포즈로 옮겨 잰다.
    /// (BakeMesh 는 부모의 축척이 섞인 모형에서 단위가 어긋난다)
    /// </summary>
    static List<Vector3> WorldVertices(Renderer r)
    {
        var pts = new List<Vector3>();
        if (r is SkinnedMeshRenderer s && s.sharedMesh != null)
        {
            var mesh = s.sharedMesh; var bones = s.bones; var binds = mesh.bindposes; var weights = mesh.boneWeights; var verts = mesh.vertices;
            if (bones == null || bones.Length == 0 || binds.Length != bones.Length || weights.Length != verts.Length)
            {
                foreach (var v in verts) pts.Add(s.transform.TransformPoint(v));
                return pts;
            }
            var skin = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) skin[i] = (bones[i] != null ? bones[i].localToWorldMatrix : s.transform.localToWorldMatrix) * binds[i];
            for (int i = 0; i < verts.Length; i++)
            {
                var w = weights[i]; var v = verts[i];
                pts.Add(skin[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0 + skin[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                      + skin[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2 + skin[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3);
            }
        }
        else if (r.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh != null)
            foreach (var v in f.sharedMesh.vertices) pts.Add(r.transform.TransformPoint(v));
        return pts;
    }

    static IEnumerable<Renderer> BodyRenderers(GameObject go) =>
        go.GetComponentsInChildren<Renderer>().Where(r => (r is MeshRenderer || r is SkinnedMeshRenderer) && r.GetComponentInParent<Canvas>() == null);

    static Bounds PersonBounds(GameObject go)
    {
        var pts = BodyRenderers(go).SelectMany(WorldVertices).ToList();
        if (pts.Count == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = new Bounds(pts[0], Vector3.zero); foreach (var p in pts) b.Encapsulate(p); return b;
    }

    /// <summary>
    /// FBX 에서 바뀐 모형 중에는 누운 채로 들어오는 것이 있다(간호사). 머리카락·마스크(머리) → 신발(발) 축을 위로 세우고,
    /// 마스크(얼굴 앞)가 yaw 방향을 보게 돌린다. 이미 서 있으면 그대로 둔다.
    /// </summary>
    static void StandUp(GameObject go, float yaw)
    {
        Vector3? Centroid(params string[] keys)
        {
            var pts = BodyRenderers(go).Where(r => r.sharedMaterials.Any(m => m != null && keys.Any(k => m.name.ToLower().Contains(k)))).SelectMany(WorldVertices).ToList();
            return pts.Count > 0 ? pts.Aggregate(Vector3.zero, (a, p) => a + p) / pts.Count : (Vector3?)null;
        }
        var b = PersonBounds(go);
        if (b.size.y >= Mathf.Max(b.size.x, b.size.z) * .8f) return; // 팔 벌린 T자세는 폭이 키만 할 수 있다 — 누운 모형만 세운다
        var head = Centroid("hair", "mask", "head"); var feet = Centroid("shoe", "boot", "foot");
        Vector3 axis;
        if (head.HasValue && feet.HasValue) axis = head.Value - feet.Value;
        else if (head.HasValue)
        {
            // 신발 재질이 없는 모형(흰 간호사복): 몸 가운데 → 머리 쪽. 들어올 때 90° 단위로 눕혀 들어오므로 가장 큰 축으로 맞춘다 (반대로 세워 거꾸로 섰었다)
            var d = head.Value - b.center; var ad = new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z));
            axis = ad.x >= ad.y && ad.x >= ad.z ? Vector3.right * Mathf.Sign(d.x) : ad.y >= ad.z ? Vector3.up * Mathf.Sign(d.y) : Vector3.forward * Mathf.Sign(d.z);
        }
        else axis = b.size.x > b.size.z ? Vector3.right : Vector3.forward; // 머리 쪽을 모르면 긴 축만 세운다
        go.transform.rotation = Quaternion.FromToRotation(axis.normalized, Vector3.up) * go.transform.rotation;

        var face = Centroid("mask", "face", "eye"); var hair = Centroid("hair"); // 얼굴 앞: 마스크·얼굴, 없으면 눈
        if (face.HasValue && hair.HasValue)
        {
            var front = Vector3.ProjectOnPlane(face.Value - hair.Value, Vector3.up);
            var want = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            if (front.sqrMagnitude > 1e-6f) go.transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(front, want, Vector3.up), Vector3.up) * go.transform.rotation;
        }
        Debug.Log($"[사람] {go.name} 세움 (누운 크기 {b.size:F2}): 머리 {(head.HasValue ? "찾음" : "모름")}, 얼굴 {(face.HasValue ? "찾음" : "모름")}");
    }

    /// <summary>움직임이 없는 모형(간호사·의사)은 두 팔을 벌린 T자세라 어색하다: 위팔을 몸 옆으로 내리고 아래팔을 살짝 앞으로 굽힌다. 뼈 이름은 두 방식(LeftArm / Bip001 L UpperArm)을 다 찾는다.</summary>
    static void RelaxArms(GameObject person)
    {
        var anim = person.GetComponentInChildren<Animation>();
        if (anim != null && anim.enabled && anim.GetClipCount() > 0) return; // 자기 움직임을 쓰면 그 자세를 쓴다
        var center = PersonBounds(person).center;
        foreach (var (upperKey, foreKey, handKey) in new[] { (new[] { "LeftArm", "L UpperArm", "LUARM" }, new[] { "LeftForeArm", "L Forearm", "LFARM" }, new[] { "LeftHand", "L Hand", "LHAND" }), (new[] { "RightArm", "R UpperArm", "RUARM" }, new[] { "RightForeArm", "R Forearm", "RFARM" }, new[] { "RightHand", "R Hand", "RHAND" }) })
        {
            var upper = Bone(person, upperKey); var fore = Bone(person, foreKey); var hand = Bone(person, handKey);
            if (upper == null || fore == null) continue;
            float side = Mathf.Sign(Vector3.Dot(upper.position - center, person.transform.right));
            var want = (Vector3.down + person.transform.right * side * .16f + person.transform.forward * .04f).normalized;
            upper.rotation = Quaternion.FromToRotation(fore.position - upper.position, want) * upper.rotation;
            if (hand != null)
            {
                var bend = (Vector3.down + person.transform.forward * .45f + person.transform.right * side * .05f).normalized;
                fore.rotation = Quaternion.FromToRotation(hand.position - fore.position, bend) * fore.rotation;
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(upper); PrefabUtility.RecordPrefabInstancePropertyModifications(fore);
        }
    }

    static Transform Bone(GameObject person, params string[] keys)
    {
        foreach (var key in keys)
        {
            var t = person.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.ToLower().Contains(key.ToLower()) && x.GetComponent<Renderer>() == null);
            if (t != null) return t;
        }
        return null;
    }

    /// <summary>사람 NPC: 대화창(STILL 스타일 판) · '대화하기' 표시 · 목소리 · 고개·숨쉬기.</summary>
    static HospitalNpc Npc(GameObject person, string displayName)
    {
        var npc = person.GetComponent<HospitalNpc>(); if (npc == null) npc = person.AddComponent<HospitalNpc>();
        npc.npcName = displayName;
        npc.head = Bone(person, "Head_05", "Bip001 Head", "Head");
        npc.spine = new[] { Bone(person, "Spine1", "Spine_01", "Spine") }.Where(t => t != null).ToArray();
        var own = person.GetComponentInChildren<Animation>();
        npc.clip = own != null && own.enabled ? own : null; // 끈 움직임은 틀지 않는다 (틀면 내린 팔이 다시 올라간다)
        var audio = person.GetComponent<AudioSource>(); if (audio == null) audio = person.AddComponent<AudioSource>();
        // 대화 목소리는 또렷하게: 반만 입체로, 3m 안은 그대로 들리고 15m 까지 고르게 작아진다 (예전 70% 입체·1.5m 부터 급히 작아짐 → 잘 안 들렸다)
        audio.spatialBlend = .5f; audio.minDistance = 3f; audio.maxDistance = 15f; audio.rolloffMode = AudioRolloffMode.Linear; audio.volume = 1f; audio.playOnAwake = false;
        npc.voice = audio;
        // 걸어 다니므로 몸에 부딪히게 (대화 겨냥은 머리 뼈 자리로 하므로 상관없다). 모형은 키에 맞춰 축척이 바뀌어 있어 자식 축척을 되돌린다
        var pb = PersonBounds(person);
        var bodyGo = new GameObject("몸_충돌"); bodyGo.transform.SetParent(person.transform, false);
        bodyGo.transform.SetPositionAndRotation(new Vector3(pb.center.x, pb.min.y, pb.center.z), Quaternion.identity);
        var ls = person.transform.lossyScale; bodyGo.transform.localScale = new Vector3(1 / ls.x, 1 / ls.y, 1 / ls.z);
        var capsule = bodyGo.AddComponent<CapsuleCollider>(); capsule.radius = .28f; capsule.height = pb.size.y; capsule.center = new Vector3(0, pb.size.y / 2, 0);
        float top = PersonBounds(person).max.y;
        UiKit();

        // 대화창: 머리 위쪽 옆에 뜨는 판
        var panelGo = new GameObject("대화창", typeof(RectTransform), typeof(Canvas));
        panelGo.transform.SetParent(person.transform, true);
        var canvas = panelGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 6;
        var rect = (RectTransform)panelGo.transform; rect.sizeDelta = new Vector2(620, 330);
        // 대화하는 거리(약 2m)에서 대사 글자가 약 1.3°로 읽히게 (예전 0.0011 은 0.85° 로 작았다)
        rect.position = NpcPanelSpot(person, top);
        rect.localScale = Vector3.one * (NpcPanelScale / Mathf.Max(1e-4f, person.transform.lossyScale.x));
        var plate = panelGo.AddComponent<Image>(); Sliced(plate, KitPanel, PlateFill, 1.4f);
        var line = UiChild(rect, "외곽선", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>(); Stretch(line.rectTransform); Sliced(line, KitLine, PlateLine, 1.4f); line.transform.SetSiblingIndex(0);
        npc.nameText = UiText(rect, "이름", new Vector2(24, -14), new Vector2(570, 44), 32, new Color(.5f, .9f, 1f), FontStyles.Bold);
        npc.lineText = UiText(rect, "대사", new Vector2(24, -62), new Vector2(570, 130), 27, Color.white);
        npc.choiceRoot = UiChild(rect, "선택지", new Vector2(24, -196), new Vector2(570, 120));
        // 선택지 틀: 손으로 가리키고 방아쇠를 당긴다 (충돌 상자로 겨냥 판정)
        var template = UiChild(npc.choiceRoot, "선택지_틀", Vector2.zero, new Vector2(570, 56)).gameObject;
        DepthButtonUi((RectTransform)template.transform, "선택", 26, PrimaryTint, new Color(.3f, .72f, .95f, 1f), 8f); // STILL 입체 버튼
        var box = template.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = new Vector3(285, -28, 0); box.size = new Vector3(570, 56, 10);
        npc.choiceTemplate = template;
        npc.panel = panelGo;

        // '대화하기' 표시: 가리키면 머리 위에 뜬다
        var promptGo = new GameObject("대화하기", typeof(RectTransform), typeof(Canvas));
        promptGo.transform.SetParent(person.transform, true);
        promptGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var pr = (RectTransform)promptGo.transform; pr.sizeDelta = new Vector2(300, 64);
        pr.position = new Vector3(person.transform.position.x, top + .22f, person.transform.position.z);
        pr.localScale = Vector3.one * (NpcPromptScale / Mathf.Max(1e-4f, person.transform.lossyScale.x));
        var chip = promptGo.AddComponent<Image>(); Sliced(chip, KitPanel, new Color(.05f, .11f, .15f, .9f), 1f);
        var pt = UiText(pr, "글", new Vector2(16, -12), new Vector2(268, 40), 26, new Color(.5f, .9f, 1f), FontStyles.Bold); pt.text = "방아쇠: 대화하기"; pt.alignment = TextAlignmentOptions.Center;
        promptGo.AddComponent<Billboard>();
        npc.prompt = promptGo;
        foreach (var t in person.GetComponentsInChildren<TextMeshProUGUI>(true)) t.ForceMeshUpdate();
        // 실행하면 HospitalNpc 가 숨기지만, 장면에서도 처음부터 닫아 둔다(말을 걸 때만 뜬다)
        panelGo.SetActive(false); promptGo.SetActive(false); template.SetActive(false);
        return npc;
    }

    // ───────────────────────────── 병원 사람들 ─────────────────────────────

    static void HospitalPeople(Transform room, GermBlaster scan, MissionGate gate)
    {
        // 접수대 간호사: 입구에서 들어오면 왼쪽 앞. 처음 다가가면 먼저 인사한다.
        PackProp("desk_no_computer", new Vector3(-2.6f, 0, -2.2f), 90, 1.5f, false, room);
        var nurseGo = Person("Nurse2.glb", "간호사", new Vector3(-3.4f, 0, -2.2f), 90, 1.66f, room); // 예전 수술복 간호사는 얼굴이 어색해(좀비 같다는 의견) 흰 간호사복 모형으로
        if (nurseGo != null)
        {
            var nurse = Npc(nurseGo, "간호사");
            nurse.greetOnApproach = true; nurse.greetRange = 2.8f;
            nurse.nodes = new[]
            {
                Node("Lobby", "nurse_0", -1, Choice("이가 시려요", 1), Choice("배가 아파요", 2), Choice("둘 다 아파요", 3)),
                Node("Lobby", "nurse_1", 4), Node("Lobby", "nurse_2", 4), Node("Lobby", "nurse_3", 4),
                Node("Lobby", "nurse_4"), Node("Lobby", "nurse_5"),
            };
            nurse.startNode = 0; nurse.repeatNode = 5;
            var monitor = Object.FindFirstObjectByType<ClinicMonitor>(); // 진찰 결과 모니터의 '환자 증상'
            if (monitor != null)
            {
                UnityEventTools.AddStringPersistentListener(nurse.nodes[1].onEnter, monitor.SetSymptom, "이가 시려요");
                UnityEventTools.AddStringPersistentListener(nurse.nodes[2].onEnter, monitor.SetSymptom, "배가 아파요");
                UnityEventTools.AddStringPersistentListener(nurse.nodes[3].onEnter, monitor.SetSymptom, "이도 시리고\n배도 아파요");
            }
            // 안내판(체험_안내판)은 적용할 때마다 다시 만들어진다: 안내판을 직접 가리키면 연결이 끊기므로 간호사를 거쳐 지금 안내판에 쓴다
            UnityEventTools.AddStringPersistentListener(nurse.nodes[4].onEnter, nurse.ShowGoal, "몸 스캔 오른쪽의 의사 선생님께 말을 걸어 보세요");
        }
        // 의사 선생님: 몸 스캔 오른쪽 앞, 탐험가 쪽을 본다. 말을 걸면 진찰을 시작하고, 진찰이 끝나면 잠수정을 안내한다.
        // 예전 자리(1.35, 1.5)는 들어서는 자리에서 보면 홀로그램 빛 알갱이와 뒤쪽 잠수정 사이에 끼어 겹쳐 보였다 → 둘 다에서 비켜선 자리.
        var doctorGo = Person("Doctor.glb", "의사", new Vector3(1.8f, 0, -.4f), 210, 1.75f, room);
        if (doctorGo != null && scan != null)
        {
            var doctor = Npc(doctorGo, "의사 선생님");
            doctor.nodes = new[]
            {
                Node("Lobby", "doctor_0", -1, Choice("네, 보여 주세요", 1), Choice("몸 스캔이 뭐예요?", 2)),
                Node("Lobby", "doctor_1"),
                Node("Lobby", "doctor_2", 1),
                Node("Lobby", "doctor_3", -1, Choice("출발할게요!", 4), Choice("잠수정은 안전한가요?", 5)),
                Node("Lobby", "doctor_4"),
                Node("Lobby", "doctor_5", 4),
            };
            doctor.startNode = 0; doctor.repeatNode = 1;
            UnityEventTools.AddPersistentListener(doctor.nodes[1].onEnter, scan.Begin);
            scan.speakerNpc = doctor; scan.beginOnStart = false;
            scan.hitLines = new[] { TalkLine("Lobby", "scan_tooth"), TalkLine("Lobby", "scan_stomach"), TalkLine("Lobby", "scan_colon") };
            scan.clearedLines = new NarrationLine[0];
            ClearListeners(scan.onCleared, doctor);
            UnityEventTools.AddIntPersistentListener(scan.onCleared, doctor.SetStart, 3);
            UnityEventTools.AddIntPersistentListener(scan.onCleared, doctor.TalkFrom, 3);
            EditorUtility.SetDirty(doctor);
        }
    }

    /// <summary>시작 대기 화면과 체험관 운영(회차 시간·자동 초기화·편의 설정), 관람객 화면.</summary>
    static void ExhibitionDevices()
    {
        var old = GameObject.Find("체험_시작_대기"); if (old != null) Object.DestroyImmediate(old);
        var spawn = GameObject.Find("SpawnPoint");
        Vector3 eye = (spawn != null ? spawn.transform.position : Vector3.zero) + Vector3.up * 1.6f;
        var go = new GameObject("체험_시작_대기", typeof(RectTransform), typeof(Canvas));
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(760, 300);
        rect.position = eye + Vector3.forward * 1.8f; rect.rotation = Quaternion.identity; rect.localScale = Vector3.one * .0015f;
        UiKit();
        var plate = go.AddComponent<Image>(); Sliced(plate, KitPanel, PlateFill, 1.4f);
        var line = UiChild(rect, "외곽선", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>(); Stretch(line.rectTransform); Sliced(line, KitLine, PlateLine, 1.4f); line.transform.SetSiblingIndex(0);
        var title = UiText(rect, "제목", new Vector2(30, -30), new Vector2(700, 80), 56, Color.white, FontStyles.Bold); title.text = "인체탐험 VR"; title.alignment = TextAlignmentOptions.Top;
        var sub = UiText(rect, "안내", new Vector2(30, -120), new Vector2(700, 150), 32, new Color(.5f, .9f, 1f)); sub.alignment = TextAlignmentOptions.Top;
        sub.text = "병원에서 진찰을 받고, 초소형 잠수정을 타고 몸속을 치료해요!\n<b>방아쇠를 당기면 시작해요</b>";
        foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>()) t.ForceMeshUpdate();

        var host = GameObject.Find("체험관_운영") ?? new GameObject("체험관_운영");
        var session = host.GetComponent<ExhibitionSession>(); if (session == null) session = host.AddComponent<ExhibitionSession>();
        session.attractPanel = go;
        var spectator = GameObject.Find("관람객_화면") ?? new GameObject("관람객_화면");
        var view = spectator.GetComponent<SpectatorView>(); if (view == null) view = spectator.AddComponent<SpectatorView>();
        view.font = KoreanFont();
    }

    // ───────────────────────────── 마지막 진료실 ─────────────────────────────

    /// <summary>상영관 장면을 처음 그 진료실로 꾸미고, 의사 선생님의 결과 안내 → OX 퀴즈 → 보고서 → 작별 인사로 마무리한다.</summary>
    static void EndingClinic()
    {
        // 상영관 물건은 그대로 두되 보이지 않게 (끝내기 장치·안내는 유지)
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "병원_진료실" || root.name == "치료_보고서" || root.name == "OX_퀴즈") continue;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer)) r.enabled = false;
            foreach (var c in root.GetComponentsInChildren<Canvas>(true)) if (root.name != "체험_안내판") c.enabled = false;
        }
        var spawn = GameObject.Find("SpawnPoint");
        if (spawn != null) spawn.transform.SetPositionAndRotation(new Vector3(0, 0, -3.6f), Quaternion.identity);
        var room = ClinicRoom();
        ClinicMonitorUi(room, true); // 큰 화면: 치료 결과 (모두 치료 완료)

        // 마지막 장면은 퀴즈가 끝날 때까지 기다린다 (예전 영상 끝내기 장치는 자동 검사용으로만 남긴다)
        foreach (var ending in Object.FindObjectsByType<TheaterEnding>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { ending.playAfterIntro = false; ending.fallbackSeconds = 9999f; }

        // 들어서면(0,-3.6) 가운데 퀴즈판, 오른쪽 의사 선생님, 왼쪽 보고서, 그 사이 뒤쪽에 간호사 — 서로 가리지 않게
        var doctorGo = Person("Doctor.glb", "의사", new Vector3(2.1f, 0, -.4f), 213, 1.75f, room);
        var nurseGo = Person("Nurse2.glb", "간호사", new Vector3(-.75f, 0, .9f), 170, 1.66f, room);
        HospitalNpc doctor = null;
        if (doctorGo != null)
        {
            doctor = Npc(doctorGo, "의사 선생님");
            doctor.nodes = new[] { Node("Theater", "doctor_0", -1, Choice("좋아요!", -1)), Node("Theater", "doctor_1") };
            doctor.startNode = 0;
        }
        if (nurseGo != null) { var nurse = Npc(nurseGo, "간호사"); nurse.nodes = new[] { Node("Theater", "nurse_0") }; }

        ReportRoom();
        var report = GameObject.Find("치료_보고서");
        if (report != null) { report.transform.position = new Vector3(-1.55f, 1.55f, -1.4f); report.transform.rotation = Quaternion.LookRotation(report.transform.position - new Vector3(0, 1.6f, -3.6f)); }
        var quiz = Quiz();
        var flow = (GameObject.Find("마무리_진행") ?? new GameObject("마무리_진행")).GetComponent<EndingFlow>();
        if (flow == null) flow = GameObject.Find("마무리_진행").AddComponent<EndingFlow>();
        flow.doctor = doctor; flow.quiz = quiz; flow.report = report != null ? report.GetComponent<TreatmentReport>() : null; flow.goodbyeNode = 1;
        Credits(room);
        var amb = GameObject.Find("병원_소리") ?? new GameObject("병원_소리");
        var sa = amb.GetComponent<SceneAmbience>(); if (sa == null) sa = amb.AddComponent<SceneAmbience>(); sa.clipName = "clinic"; sa.volume = .3f;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.5f, .55f, .58f); RenderSettings.ambientEquatorColor = new Color(.4f, .44f, .46f); RenderSettings.ambientGroundColor = new Color(.26f, .28f, .3f);
    }

    static OxQuiz Quiz()
    {
        var old = GameObject.Find("OX_퀴즈"); if (old != null) Object.DestroyImmediate(old);
        var go = new GameObject("OX_퀴즈");
        var quiz = go.AddComponent<OxQuiz>();
        var audio = go.AddComponent<AudioSource>(); audio.spatialBlend = .5f; audio.playOnAwake = false; quiz.voice = audio;
        if (content == null) content = JObject.Parse(System.IO.File.ReadAllText("Assets/Data/journey_content.json"));
        var qs = content["stages"].First(x => (string)x["stageId"] == "Theater")["quiz"] as JArray;
        quiz.questions = qs.Select((q, i) => new OxQuiz.Question
        {
            text = (string)q["text"], answer = (bool)q["answer"], explain = (string)q["explain"],
            voice = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Sounds/Narration/Theater_quiz_{i + 1}_q.mp3"),
            explainVoice = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Sounds/Narration/Theater_quiz_{i + 1}_a.mp3"),
        }).ToArray();

        UiKit();
        var panelGo = new GameObject("퀴즈판", typeof(RectTransform), typeof(Canvas));
        panelGo.transform.SetParent(go.transform, false);
        panelGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)panelGo.transform; rect.sizeDelta = new Vector2(900, 560);
        rect.position = new Vector3(.6f, 1.5f, -1.2f); rect.rotation = Quaternion.LookRotation(rect.position - new Vector3(0, 1.6f, -3.6f)); rect.localScale = Vector3.one * QuizScale;
        var plate = panelGo.AddComponent<Image>(); Sliced(plate, KitPanel, PlateFill, 1.4f);
        var line = UiChild(rect, "외곽선", Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>(); Stretch(line.rectTransform); Sliced(line, KitLine, PlateLine, 1.4f); line.transform.SetSiblingIndex(0);
        quiz.progressText = UiText(rect, "차례", new Vector2(30, -22), new Vector2(840, 40), 28, new Color(.5f, .9f, 1f), FontStyles.Bold);
        quiz.questionText = UiText(rect, "문제", new Vector2(30, -70), new Vector2(840, 120), 40, Color.white, FontStyles.Bold);
        quiz.feedbackText = UiText(rect, "해설", new Vector2(30, -200), new Vector2(840, 100), 27, new Color(.92f, .96f, .98f));
        // STILL 키트의 입체 버튼 (판에서 떠 있는 앞판, 가리키면 밝아지고 누르면 들어간다)
        (Image img, BoxCollider box) Button(string name, string mark, float x, Color color, Color hover)
        {
            var b = UiChild(rect, name, new Vector2(x, -320), new Vector2(380, 210));
            var depth = DepthButtonUi(b, mark, 140, color, hover, 16f);
            var c = b.gameObject.AddComponent<BoxCollider>(); c.isTrigger = true; c.center = new Vector3(190, -105, 0); c.size = new Vector3(380, 210, 10);
            return (depth.front, c);
        }
        (quiz.oImage, quiz.oButton) = Button("O_판", "O", 50, PrimaryTint, new Color(.3f, .72f, .95f, 1f));
        (quiz.xImage, quiz.xButton) = Button("X_판", "X", 470, new Color(.78f, .33f, .3f, 1f), new Color(.95f, .48f, .42f, 1f));
        quiz.questionText.text = "퀴즈를 시작하면 여기에 문제가 나와요"; quiz.progressText.text = "OX 퀴즈";
        quiz.panel = panelGo;
        foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>()) t.ForceMeshUpdate();
        return quiz;
    }

    /// <summary>만든 사람·출처 판 (CC BY 모형의 출처 표시).</summary>
    static void Credits(Transform room)
    {
        var go = new GameObject("만든_사람", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(room, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(700, 380);
        rect.position = new Vector3(4.93f, 1.7f, 1.5f); rect.rotation = Quaternion.Euler(0, 90, 0); rect.localScale = Vector3.one * .0016f;
        var plate = go.AddComponent<Image>(); Sliced(plate, KitPanel, PlateFill, 1.4f);
        var title = UiText(rect, "제목", new Vector2(26, -18), new Vector2(650, 44), 32, new Color(.5f, .9f, 1f), FontStyles.Bold); title.text = "함께 만든 모형 (CC BY 4.0)";
        var body = UiText(rect, "내용", new Vector2(26, -70), new Vector2(650, 290), 22, Color.white);
        body.text = "이 모형: Human teeth — Alexander Antipov (Dessen)\n의사: Doctor — BrushDip\n간호사: Nurse 2 & 3 — Diddy_Blud_117 (marcosacevez57)\n진료실 소품: Sci-Fi Styled Modular Pack\n모두 sketchfab.com · 저작자 표시 4.0 국제";
        foreach (var t in go.GetComponentsInChildren<TextMeshProUGUI>()) t.ForceMeshUpdate();
    }
}
#endif
