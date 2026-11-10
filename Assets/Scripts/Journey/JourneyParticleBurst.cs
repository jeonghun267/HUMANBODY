using UnityEngine;

/// <summary>연속 흡수에서도 앞선 입자의 위치가 유지되는 공간 기준 방출.</summary>
public static class JourneyParticleBurst
{
    public static void Emit(ParticleSystem system, Vector3 position, int count, Vector3? surfaceNormal = null)
    {
        if (system == null || !system.gameObject.activeInHierarchy || count <= 0) return;
        var emission = new ParticleSystem.EmitParams
        {
            position = position,
            applyShapeToPosition = !surfaceNormal.HasValue
        };
        if (surfaceNormal.HasValue)
        {
            emission.rotation3D = Quaternion.LookRotation(surfaceNormal.Value.normalized).eulerAngles;
            emission.velocity = Vector3.zero;
        }
        if (!system.isPlaying) system.Play(false);
        system.Emit(emission, count);
    }
}

