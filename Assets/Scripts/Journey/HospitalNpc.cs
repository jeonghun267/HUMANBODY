using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public class DialogueChoice
{
    public string label;
    [Tooltip("고르면 이어질 대사 번호 (-1 = 대화 끝)")] public int next = -1;
}

[Serializable]
public class DialogueNode
{
    [TextArea(2, 4)] public string text;
    public AudioClip voice;
    [Tooltip("선택지가 없으면 대사가 끝난 뒤 이 번호로 (-1 = 대화 끝)")] public int next = -1;
    public DialogueChoice[] choices = new DialogueChoice[0];
    [Tooltip("이 대사가 시작될 때 (진찰 시작, 탑승구 열기 등)")] public UnityEvent onEnter = new UnityEvent();
}

/// <summary>
/// 병원의 사람 NPC(의사·간호사). 손으로 가리키고 방아쇠를 당기면 대화가 시작되고, 선택지도 손으로 골라 방아쇠로 고른다.
/// 말하는 동안 탐험가 쪽으로 고개를 돌리고, 평소에는 숨쉬듯 몸이 움직인다(움직임 파일이 없는 모형도 사람답게).
/// </summary>
[AddComponentMenu("인체탐험/병원 사람")]
public class HospitalNpc : MonoBehaviour
{
    public string npcName = "간호사";
    public DialogueNode[] nodes = new DialogueNode[0];
    [Tooltip("다시 말을 걸면 시작할 대사 번호")] public int startNode;
    [Tooltip("첫 대화가 끝난 뒤 다시 말을 걸면 시작할 대사 번호 (-1 = 처음과 같음)")] public int repeatNode = -1;
    [Tooltip("처음 가까이 오면 먼저 말을 건다")] public bool greetOnApproach;
    public float greetRange = 2.6f, talkRange = 4f;

    [Header("몸")]
    public Transform head;
    public Transform[] spine;
    public Animation clip; // 모형에 들어 있던 움직임 (있으면 반복 재생)

    [Header("대화창")]
    public GameObject panel;
    public TextMeshProUGUI nameText, lineText;
    public RectTransform choiceRoot;
    public GameObject choiceTemplate;
    public GameObject prompt;
    public AudioSource voice;

    public UnityEvent onConversationEnd = new UnityEvent();
    public bool IsTalking { get; private set; }

    readonly List<(BoxCollider box, Image image, int next)> buttons = new List<(BoxCollider, Image, int)>();
    Quaternion headRest; Quaternion[] spineRest;
    Transform eye;
    bool greeted;
    float look;
    int chosen = int.MinValue;

    void Start()
    {
        if (head != null) headRest = head.localRotation;
        spineRest = spine != null ? Array.ConvertAll(spine, s => s != null ? s.localRotation : Quaternion.identity) : new Quaternion[0];
        if (panel != null) panel.SetActive(false);
        if (prompt != null) prompt.SetActive(false);
        if (choiceTemplate != null) choiceTemplate.SetActive(false);
        if (clip != null && clip.clip != null) { clip.wrapMode = WrapMode.Loop; clip.Play(); }
    }

    Transform Eye() { if (eye == null) eye = JourneyEye.Get(); return eye; }

    void Update()
    {
        var e = Eye();
        if (e == null) return;
        float dist = Vector3.Distance(Flat(e.position), Flat(transform.position));
        if (!IsTalking)
        {
            bool waiting = JourneyManager.HoldIntro || (GuideNarrator.Instance != null && GuideNarrator.Instance.IsSpeaking);
            if (greetOnApproach && !greeted && dist < greetRange && !waiting) { greeted = true; Talk(); return; }
            var ray = JourneyAim.Ray();
            bool aimed = dist < talkRange && head != null && JourneyAim.Near(ray, head.position, .7f, talkRange + 1, out _);
            if (prompt != null) prompt.SetActive(aimed);
            if (aimed && JourneyAim.TriggerDown()) Talk();
            return;
        }
        // 대화 중: 선택지 겨냥·고르기
        if (buttons.Count > 0)
        {
            var ray = JourneyAim.Ray();
            int hover = -1;
            if (Physics.Raycast(ray, out var hit, 8f, ~0, QueryTriggerInteraction.Collide))
                for (int i = 0; i < buttons.Count; i++) if (hit.collider == buttons[i].box) hover = i;
            for (int i = 0; i < buttons.Count; i++) buttons[i].image.color = i == hover ? new Color(.35f, .8f, 1f, .75f) : new Color(.16f, .55f, .78f, .35f);
            if (hover >= 0 && JourneyAim.TriggerDown()) { Sfx.Play("click", buttons[hover].box.transform.position, .6f); chosen = buttons[hover].next; }
        }
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

    void LateUpdate()
    {
        float time = Time.time;
        bool animated = clip != null && clip.isPlaying;
        // 숨쉬기: 움직임 파일이 없는 모형만, 척추가 아주 조금 앞뒤로
        if (!animated)
            for (int i = 0; i < spineRest.Length; i++) if (spine[i] != null) spine[i].localRotation = spineRest[i] * Quaternion.Euler(Mathf.Sin(time * 1.6f + i) * .8f, 0, 0);
        // 고개: 가까이 있거나 대화 중이면 탐험가 쪽으로 돌린다 (뼈 축과 상관없이, 몸 정면 → 탐험가 방향만큼 더 돌림)
        var e = Eye();
        if (head == null || e == null) return;
        bool near = Vector3.Distance(e.position, head.position) < talkRange;
        look = Mathf.MoveTowards(look, near || IsTalking ? 1 : 0, Time.deltaTime * 1.5f);
        var basePose = (head.parent != null ? head.parent.rotation : Quaternion.identity) * (animated ? head.localRotation : headRest);
        var delta = Quaternion.FromToRotation(transform.forward, (e.position - head.position).normalized);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180) angle -= 360;
        head.rotation = Quaternion.AngleAxis(Mathf.Clamp(angle, -55f, 55f) * look, axis) * basePose;
        // 말하는 동안 살짝 끄덕인다
        if (IsTalking && voice != null && voice.isPlaying) head.rotation = Quaternion.AngleAxis(Mathf.Sin(time * 7f) * 2.5f, transform.right) * head.rotation;
        if (panel != null && panel.activeSelf)
        {
            var flat = Flat(panel.transform.position - e.position);
            if (flat.sqrMagnitude > .01f) panel.transform.rotation = Quaternion.LookRotation(flat);
        }
    }

    /// <summary>대화 시작 (다른 스크립트에서 불러도 된다).</summary>
    public void Talk() { if (!IsTalking && nodes.Length > 0) StartCoroutine(Run(startNode)); }
    public void TalkFrom(int node) { if (!IsTalking && node >= 0 && node < nodes.Length) StartCoroutine(Run(node)); }

    IEnumerator Run(int index)
    {
        IsTalking = true;
        if (prompt != null) prompt.SetActive(false);
        if (GuideNarrator.Instance != null && GuideNarrator.Instance.IsSpeaking) GuideNarrator.Instance.Stop(true);
        if (panel != null) panel.SetActive(true);
        if (nameText != null) nameText.text = npcName;
        while (index >= 0 && index < nodes.Length)
        {
            var node = nodes[index];
            ClearChoices();
            if (lineText != null) lineText.text = node.text;
            node.onEnter?.Invoke();
            float wait = 1.5f + node.text.Length * .07f;
            if (voice != null && node.voice != null) { voice.clip = node.voice; voice.Play(); wait = node.voice.length + .3f; }
            if (node.choices != null && node.choices.Length > 0)
            {
                // 말이 끝나기 전에도 고를 수 있다
                for (int i = 0; i < node.choices.Length; i++) AddChoice(i, node.choices[i]);
                chosen = int.MinValue;
                while (chosen == int.MinValue) yield return null;
                if (voice != null) voice.Stop();
                index = chosen;
            }
            else
            {
                float until = Time.time + wait;
                yield return null;
                while (Time.time < until && !JourneyAim.TriggerDown()) yield return null;
                index = node.next;
            }
        }
        ClearChoices();
        if (panel != null) panel.SetActive(false);
        IsTalking = false;
        if (repeatNode >= 0) startNode = repeatNode;
        onConversationEnd?.Invoke();
    }

    /// <summary>다음에 말을 걸면 시작할 대사를 바꾼다 (진찰이 끝나면 잠수정 안내로 등).</summary>
    public void SetStart(int node) { startNode = node; repeatNode = -1; }

    /// <summary>대화와 상관없이 한 줄 말한다 (진찰 결과 등). 다른 말이 끝날 때까지 기다렸다가 말한다.</summary>
    public IEnumerator SayRoutine(NarrationLine line)
    {
        if (line == null) yield break;
        while (IsTalking) yield return null;
        IsTalking = true;
        if (panel != null) panel.SetActive(true);
        if (nameText != null) nameText.text = npcName;
        if (lineText != null) lineText.text = line.text;
        float wait = line.voice != null ? line.voice.length + .3f : Mathf.Max(line.holdSeconds, 1.5f + line.text.Length * .07f);
        if (voice != null && line.voice != null) { voice.clip = line.voice; voice.Play(); }
        yield return new WaitForSeconds(wait);
        if (panel != null) panel.SetActive(false);
        IsTalking = false;
    }

    void AddChoice(int i, DialogueChoice choice)
    {
        if (choiceTemplate == null || choiceRoot == null) return;
        var go = Instantiate(choiceTemplate, choiceRoot);
        go.SetActive(true);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = new Vector2(0, -i * 64);
        go.GetComponentInChildren<TextMeshProUGUI>().text = choice.label;
        var box = go.GetComponent<BoxCollider>();
        box.size = new Vector3(rect.sizeDelta.x, rect.sizeDelta.y, 10);
        buttons.Add((box, go.GetComponent<Image>(), choice.next));
    }

    void ClearChoices()
    {
        foreach (var b in buttons) if (b.box != null) Destroy(b.box.gameObject);
        buttons.Clear();
    }
}
