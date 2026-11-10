using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 텔레포트로 내려설 수 있는 바닥 표시. 장면에 하나라도 있으면 표시된 바닥에만 내려선다
/// (입 장면에서 이·목젖·음식 위로 올라서지 않도록). 없는 장면은 기존처럼 평평한 곳 어디든 가능.
/// </summary>
[AddComponentMenu("인체탐험/이동 가능 바닥")]
public class TeleportArea : MonoBehaviour
{
    static readonly HashSet<TeleportArea> active = new HashSet<TeleportArea>();
    public static bool AnyActive => active.Count > 0;

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    public static bool Allows(Collider collider) => !AnyActive || (collider != null && collider.GetComponentInParent<TeleportArea>() != null);
}
