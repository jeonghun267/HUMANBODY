using UnityEngine;
using System.Collections.Generic;

[AddComponentMenu("인체탐험/미생물 무리 제어") ]
public class VirusGroupController : MonoBehaviour
{
    [System.Serializable]
    public class VirusData
    {
        public Transform transform;
        [HideInInspector] public Vector3 initialLocalPos;
        [HideInInspector] public Vector3 randomAxis;
        [HideInInspector] public float randomOffset;
        [HideInInspector] public float individualSpeed;
        [HideInInspector] public float individualRange;
        [HideInInspector] public float individualRotateSpeed; // 개별 회전 속도 추가
    }

    [Header("바이러스 리스트")]
    public List<VirusData> virusModels = new List<VirusData>();

    [Header("전체적인 기준 설정")]
    public float baseMoveRange = 0.15f;
    public float baseMoveSpeed = 0.8f;
    public float baseRotateSpeed = 20.0f; // 기준 회전 속도

    void Start()
    {
        foreach (var virus in virusModels)
        {
            if (virus.transform != null)
            {
                virus.initialLocalPos = virus.transform.localPosition;

                // 1. 타이밍 랜덤화
                virus.randomOffset = Random.Range(0f, 10f);

                // 2. 회전축을 완전히 랜덤하게 (어떤 방향으로 돌지 결정)
                virus.randomAxis = Random.insideUnitSphere.normalized;

                // 3. 이동 속도와 폭 랜덤화
                virus.individualSpeed = Random.Range(0.7f, 1.3f);
                virus.individualRange = Random.Range(0.7f, 1.3f);

                // 4. [핵심] 회전 속도 랜덤화 (음수 값을 섞어서 반대 방향 회전 추가)
                // -1.5배 ~ 1.5배 사이로 설정하여 속도와 방향을 모두 다르게 함
                virus.individualRotateSpeed = Random.Range(-1.5f, 1.5f);
            }
        }
    }

    void Update()
    {
        float time = Time.time;

        foreach (var virus in virusModels)
        {
            if (virus.transform == null) continue;

            float offsetTime = (time * baseMoveSpeed * virus.individualSpeed) + virus.randomOffset;
            float currentRange = baseMoveRange * virus.individualRange;

            float x = Mathf.Sin(offsetTime) * currentRange;
            float y = Mathf.Cos(offsetTime * 0.85f) * currentRange;
            float z = Mathf.Sin(offsetTime * 1.15f) * (currentRange * 0.5f);

            virus.transform.localPosition = virus.initialLocalPos + new Vector3(x, y, z);

            // 5. [핵심] 개별 회전 속도와 개별 축을 사용하여 회전
            // 이제 어떤 놈은 시계방향, 어떤 놈은 반시계방향으로 각자 돕니다.
            virus.transform.Rotate(virus.randomAxis * (baseRotateSpeed * virus.individualRotateSpeed) * Time.deltaTime);
        }
    }
}