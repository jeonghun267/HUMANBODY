#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 소리 배선: 영속 프리팹의 플레이어에 걷기 발소리(Footsteps), 진행 관리 쪽에 배경 음악(JourneyMusic)을 붙이고,
/// 긴 배경 음악은 Quest 메모리를 아끼게 압축한 채로 들고 있게(CompressedInMemory·Vorbis) 가져오기 설정을 맞춘다.
/// 소리 파일은 Tools/make_sfx.py 가 Assets/Resources/Sfx 에 만든다.
/// </summary>
public static partial class JourneyRealism
{
    static readonly string[] MusicTracks = { "bgm_clinic", "bgm_body" };

    static void AudioPass()
    {
        const string path = "Assets/Data/Generated/JourneyPersistent.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var player = root.GetComponentInChildren<OVRPlayerController>(true);
            if (player != null && player.GetComponent<Footsteps>() == null) player.gameObject.AddComponent<Footsteps>();
            var manager = root.GetComponentInChildren<JourneyManager>(true);
            if (manager != null && manager.GetComponent<JourneyMusic>() == null) manager.gameObject.AddComponent<JourneyMusic>();
            if (player == null || manager == null) Debug.LogWarning($"[소리] 영속 프리팹에 플레이어({player != null}) 또는 진행 관리({manager != null})가 없음");
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        foreach (var track in MusicTracks)
        {
            var importer = AssetImporter.GetAtPath($"Assets/Resources/Sfx/{track}.wav") as AudioImporter;
            if (importer == null) { Debug.LogWarning($"[소리] 배경 음악 파일 없음: {track} (python Tools/make_sfx.py bgm)"); continue; }
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .45f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
        }
        Debug.Log("[소리] 발소리·배경 음악 배선 완료");
    }

    /// <summary>배치모드: 소리 배선만 (장면은 건드리지 않음).</summary>
    public static void AudioSetup()
    {
        AssetDatabase.Refresh();
        AudioPass();
        AssetDatabase.SaveAssets();
    }
}
#endif
