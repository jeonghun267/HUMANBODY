using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 걷기 발소리: 막대로 걸어간 거리만큼(한 걸음 stride m마다) 발밑에서 발소리가 난다.
/// 병원(진료실) 바닥은 딱딱한 타일 소리, 몸속은 말랑하고 축축한 소리 (Resources/Sfx/step_clinic_N, step_wet_N — Tools/make_sfx.py).
/// 잠수정에 앉아 있을 때(CharacterController 꺼짐)나 장면 시작 자리로 옮겨질 때는 소리를 내지 않는다.
/// 실제로 몸을 움직여 걷는 것(머리 추적)은 플레이어를 옮기지 않으므로 발소리가 나지 않는다 — 그때는 진짜 발소리가 있다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[AddComponentMenu("인체탐험/발소리")]
public class Footsteps : MonoBehaviour
{
    [Tooltip("이만큼 걸을 때마다 한 걸음 (m)")] public float stride = .7f;
    [Tooltip("이보다 느리면 걷는 걸로 치지 않는다 (m/s)")] public float minSpeed = .3f;
    [Range(0f, 1f)] public float volume = .32f;
    [Tooltip("딱딱한 병원 바닥 장면 — 나머지(몸속)는 축축한 바닥")] public string[] hardFloorScenes = { "00_Lobby", "06_Theater" };
    public string hardSound = "step_clinic", wetSound = "step_wet";
    [Tooltip("소리 종류 수 (_1 ~ _N)")] public int variants = 4;

    CharacterController cc;
    Vector3 last;
    bool hasLast;
    float travelled;
    int lastPick;

    /// <summary>지금까지 난 걸음 수 (검사용).</summary>
    public int Steps { get; private set; }
    /// <summary>지금 장면의 발소리 이름 앞부분.</summary>
    public string Surface => Array.IndexOf(hardFloorScenes, SceneManager.GetActiveScene().name) >= 0 ? hardSound : wetSound;

    void Awake() => cc = GetComponent<CharacterController>();

    void Update()
    {
        var pos = transform.position;
        Vector3 moved = pos - last; moved.y = 0f;
        bool walking = hasLast && cc != null && cc.enabled && Time.deltaTime > 0f && moved.magnitude < 1f && moved.magnitude / Time.deltaTime >= minSpeed;
        last = pos; hasLast = true;
        // 멈춰 있으면 반걸음만 남겨 두어 걷기 시작하자마자 첫 발소리가 난다
        if (!walking) { travelled = stride * .5f; return; }
        travelled += moved.magnitude;
        if (travelled < stride) return;
        travelled -= stride;
        Step();
    }

    void Step()
    {
        int pick = UnityEngine.Random.Range(1, variants + 1);
        if (pick == lastPick && variants > 1) pick = pick % variants + 1; // 같은 소리가 연달아 나지 않게
        lastPick = pick;
        Sfx.Play($"{Surface}_{pick}", transform.position, volume, UnityEngine.Random.Range(.94f, 1.06f));
        Steps++;
    }
}
