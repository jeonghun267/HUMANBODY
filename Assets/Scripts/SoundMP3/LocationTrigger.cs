using UnityEngine;
using System.Collections;
using TMPro;

[AddComponentMenu("인체탐험/장소별 안내") ]
public class LocationTrigger : MonoBehaviour
{
    // ==========================================================
    // 인스펙터에서 연결할 오브젝트
    // ==========================================================

    // (필수) 이 스크립트가 켜고 끌 NPC_Canvas (전체 말풍선) 오브젝트
    public GameObject canvasRoot;

    // NPC_Canvas 아래에 있는 순서대로 나타날 Text (TMP) 오브젝트들을 연결합니다.
    public GameObject[] dialogueTexts;

    // 각 텍스트가 화면에 표시될 시간 (초 단위)
    public float timePerText = 3f;

    // 플레이어가 콜라이더 영역에 진입했을 때
    private void OnTriggerEnter(Collider other)
    {
        // [수정됨] 이제 "VRCamera"가 아니라 통일된 태그인 "Player"를 감지함
        if (other.CompareTag("Player"))
        {
            // 이전에 실행 중이던 코루틴이 있다면 중지 (중복 실행 방지)
            StopAllCoroutines();

            //  1. 캔버스 루트 활성화 (텍스트가 보이게 하는 핵심)
            if (canvasRoot != null)
            {
                canvasRoot.SetActive(true);
            }

            // 모든 텍스트 일단 비활성화 (초기화)
            DeactivateAllTexts();

            // 순차 출력 코루틴 시작
            StartCoroutine(ShowDialogueSequence());
        }
    }

    // 대사를 순서대로 출력하는 코루틴
    IEnumerator ShowDialogueSequence()
    {
        for (int i = 0; i < dialogueTexts.Length; i++)
        {
            // 1. 현재 텍스트 활성화
            if (dialogueTexts[i] != null)
            {
                dialogueTexts[i].SetActive(true);
            }

            // 3. timePerText 시간만큼 대기
            yield return new WaitForSeconds(timePerText);

            // 4. 다음 텍스트를 위해 현재 텍스트 비활성화
            if (dialogueTexts[i] != null)
            {
                dialogueTexts[i].SetActive(false);
            }
        }

        // 텍스트 순서가 모두 끝난 후, 3초 대기 후 캔버스 전체 비활성화
        yield return new WaitForSeconds(3f);
        if (canvasRoot != null)
        {
            canvasRoot.SetActive(false);
        }
    }

    // 모든 텍스트를 비활성화하는 헬퍼 함수
    private void DeactivateAllTexts()
    {
        foreach (GameObject textObj in dialogueTexts)
        {
            if (textObj != null)
            {
                textObj.SetActive(false);
            }
        }
    }

    // 플레이어가 영역을 벗어났을 때 코루틴 중지 및 모든 텍스트 비활성화
    private void OnTriggerExit(Collider other)
    {
        // [수정됨] "VRCamera" -> "Player"
        if (other.CompareTag("Player"))
        {
            // 영역을 벗어나면 코루틴 중지
            StopAllCoroutines();

            // 모든 텍스트 비활성화
            DeactivateAllTexts();

            //  캔버스 루트 비활성화 (화면에서 말풍선 전체를 숨김)
            if (canvasRoot != null)
            {
                canvasRoot.SetActive(false);
            }
        }
    }
}