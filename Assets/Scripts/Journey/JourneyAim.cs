using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 미니게임 공통 조준·방아쇠. 오른손(없으면 왼손) 컨트롤러가 가리키는 방향, 컨트롤러가 없으면 시선.
/// 컴퓨터에서 시험할 때는 마우스 왼쪽 버튼이나 스페이스바가 방아쇠다.
/// </summary>
public static class JourneyAim
{
    static Transform right, left;
    /// <summary>왼손잡이 설정 (체험관 설정 파일): 왼손 컨트롤러를 먼저 쓴다.</summary>
    public static bool LeftHanded;

    public static Ray Ray()
    {
        if (right == null) { var r = GameObject.Find("RightHandAnchor"); if (r != null) right = r.transform; }
        if (left == null) { var l = GameObject.Find("LeftHandAnchor"); if (l != null) left = l.transform; }
        if (LeftHanded && left != null && OVRInput.IsControllerConnected(OVRInput.Controller.LTouch)) return new Ray(left.position, left.forward);
        if (right != null && OVRInput.IsControllerConnected(OVRInput.Controller.RTouch)) return new Ray(right.position, right.forward);
        if (left != null && OVRInput.IsControllerConnected(OVRInput.Controller.LTouch)) return new Ray(left.position, left.forward);
        var eye = JourneyEye.Get();
        return eye != null ? new Ray(eye.position, eye.forward) : new Ray(Vector3.zero, Vector3.forward);
    }

    public static bool TriggerDown() =>
        OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.All) ||
        OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger, OVRInput.Controller.All) ||
        (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
        (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);

    public static bool TriggerHeld() =>
        OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.All) ||
        OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger, OVRInput.Controller.All) ||
        (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
        (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);

    /// <summary>광선에서 점까지의 수직 거리와 광선 위 거리. 앞쪽이 아니면 false.</summary>
    public static bool Near(Ray ray, Vector3 point, float radius, float range, out float along)
    {
        var to = point - ray.origin;
        along = Vector3.Dot(to, ray.direction);
        if (along < 0 || along > range) return false;
        return (to - ray.direction * along).sqrMagnitude <= radius * radius;
    }

    /// <summary>광선 그리기 (잠깐 보였다 사라진다).</summary>
    public static void Beam(LineRenderer line, Vector3 from, Vector3 to)
    {
        if (line == null) return;
        line.enabled = true;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
    }
}
