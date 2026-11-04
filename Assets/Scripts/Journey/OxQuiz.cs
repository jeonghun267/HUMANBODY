using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 마무리 OX 퀴즈. 큰 O·X 판을 손으로 가리키고 방아쇠를 당겨 답한다. 정답이면 맑은 소리, 틀리면 낮은 소리와 함께 해설을 들려준다.
/// 결과는 치료 기록(JourneyScore)의 "OX 퀴즈" 로 남는다.
/// </summary>
[AddComponentMenu("인체탐험/OX 퀴즈")]
public class OxQuiz : MonoBehaviour
{
    [Serializable]
    public class Question
    {
        [TextArea(2, 3)] public string text;
        public bool answer;
        [TextArea(2, 3)] public string explain;
        public AudioClip voice, explainVoice;
    }

    public Question[] questions = new Question[0];
    public GameObject panel;
    public TextMeshProUGUI questionText, progressText, feedbackText;
    public BoxCollider oButton, xButton;
    public Image oImage, xImage;
    public AudioSource voice;
    public UnityEvent onFinished = new UnityEvent();

    public int Correct { get; private set; }
    public bool IsDone { get; private set; }
    public bool IsRunning { get; private set; }
    int answer = -1;
    DepthButton oDepth, xDepth;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        oDepth = oButton != null ? oButton.GetComponent<DepthButton>() : null;
        xDepth = xButton != null ? xButton.GetComponent<DepthButton>() : null;
    }

    public void Begin() { if (!IsRunning && !IsDone) StartCoroutine(Run()); }

    /// <summary>모든 문제를 맞힌 것으로 끝낸다 (자동 검사·건너뛰기).</summary>
    public void FinishNow() { StopAllCoroutines(); Correct = questions.Length; Done(); }

    void Update()
    {
        int hover = -1;
        if (IsRunning && answer < 0 && Physics.Raycast(JourneyAim.Ray(), out var hit, 10f, ~0, QueryTriggerInteraction.Collide))
            hover = hit.collider == oButton ? 1 : hit.collider == xButton ? 0 : -1;
        // 입체 버튼이면 가리킬 때 밝아지고 둘레 고리가 켜진다 (해설을 듣는 동안은 꺼 둔다)
        if (oDepth != null) oDepth.SetHover(hover == 1); else if (oImage != null) oImage.color = hover == 1 ? new Color(.45f, .85f, 1f, .9f) : new Color(.16f, .55f, .78f, .55f);
        if (xDepth != null) xDepth.SetHover(hover == 0); else if (xImage != null) xImage.color = hover == 0 ? new Color(1f, .6f, .5f, .9f) : new Color(.78f, .35f, .3f, .55f);
        if (hover >= 0 && JourneyAim.TriggerDown())
        {
            answer = hover;
            var pressed = hover == 1 ? oDepth : xDepth; if (pressed != null) pressed.Press();
        }
    }

    IEnumerator Run()
    {
        IsRunning = true; Correct = 0;
        if (panel != null) panel.SetActive(true);
        for (int i = 0; i < questions.Length; i++)
        {
            var q = questions[i];
            if (progressText != null) progressText.text = $"문제 {i + 1} / {questions.Length}";
            if (questionText != null) questionText.text = q.text;
            if (feedbackText != null) feedbackText.text = "";
            if (voice != null && q.voice != null) { voice.clip = q.voice; voice.Play(); }
            answer = -1;
            while (answer < 0) yield return null;
            bool right = (answer == 1) == q.answer;
            if (right) Correct++;
            Sfx.Play(right ? "correct" : "incorrect", panel != null ? panel.transform.position : transform.position, .8f);
            Haptics.Pulse(right ? .5f : .25f, .12f);
            if (feedbackText != null) feedbackText.text = (right ? "<color=#7FE3FF>정답이에요!</color> " : "<color=#FFB36B>아쉬워요!</color> ") + q.explain;
            float wait = 2.5f + q.explain.Length * .05f;
            if (voice != null && q.explainVoice != null) { voice.clip = q.explainVoice; voice.Play(); wait = q.explainVoice.length + .6f; }
            yield return new WaitForSeconds(wait);
        }
        Done();
    }

    void Done()
    {
        IsRunning = false; IsDone = true;
        if (questionText != null) questionText.text = $"{questions.Length}문제 중 {Correct}개 정답!";
        if (progressText != null) progressText.text = "퀴즈 끝";
        if (feedbackText != null) feedbackText.text = "";
        JourneyScore.Record("quiz", "OX 퀴즈", Correct, questions.Length);
        Sfx.Play("success", transform.position, .8f);
        onFinished?.Invoke();
    }
}
