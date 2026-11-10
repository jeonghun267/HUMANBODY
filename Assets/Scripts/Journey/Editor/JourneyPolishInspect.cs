#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class JourneyPolishInspect
{
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Journey/01_Mouth.unity");
            var chew = Object.FindFirstObjectByType<ChewingSequence>();
            chew.salivaEmitters = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Where(p => p.name.StartsWith("Saliva_")).OrderBy(p => p.name).ToArray();
            if (chew.salivaEmitters.Length != 6) throw new Exception("침샘 방출기 수 확인 필요");
            var zone = GameObject.Find("ChewZone").GetComponent<NarrationZone>();
            for (int i = zone.onFinished.GetPersistentEventCount()-1; i >= 0; i--)
                if (zone.onFinished.GetPersistentTarget(i) is ParticleSystem p && chew.salivaEmitters.Contains(p))
                    UnityEventTools.RemovePersistentListener(zone.onFinished, i);
            foreach (var p in chew.salivaEmitters) { var main = p.main; main.playOnAwake = false; EditorUtility.SetDirty(p); }
            EditorSceneManager.MarkSceneDirty(chew.gameObject.scene);
            EditorSceneManager.SaveScene(chew.gameObject.scene);
            Directory.CreateDirectory("Tools/polish_captures");
            var report = new StringBuilder("침샘 여섯 개 통합 연결과 개별 중복 시작 연결 제거 완료\n");
            foreach (string name in new[]{"Char_ChickDoctor","Char_Enzyme","Char_Pepsin","Char_Probiotic","Char_HarmfulBacteria","Char_Virus","Char_RedBloodCell","cute_chick"})
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                string path = name == "cute_chick" ? "Assets/Character/cute_chick.glb" : "Assets/Models/Meshy/"+name+".glb";
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                report.AppendLine("\n"+name+" / 내부 동작 수: "+AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Count());
                foreach(var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = mf.sharedMesh;
                    report.AppendLine("메시: "+mf.name+" / 점 수 "+mesh.vertexCount+" / 읽기 "+mesh.isReadable+" / 중심 "+mesh.bounds.center+" / 크기 "+mesh.bounds.size);
                }
                foreach(var r in go.GetComponentsInChildren<Renderer>())
                    foreach(var m in r.sharedMaterials)
                    {
                        report.AppendLine("재질: "+m.name+" / "+m.shader.name);
                        foreach(var prop in m.GetTexturePropertyNames()) report.AppendLine("텍스처 "+prop+": "+(m.GetTexture(prop)?.name ?? "없음"));
                    }
                var rs = go.GetComponentsInChildren<Renderer>();
                Bounds b = rs[0].bounds; foreach(var r in rs) b.Encapsulate(r.bounds);
                float scale = 2f/Mathf.Max(.001f,b.size.y);
                go.transform.localScale *= scale;
                b = rs[0].bounds; foreach(var r in rs) b.Encapsulate(r.bounds);
                go.transform.position -= b.center;
                b.center = Vector3.zero;
                var key = new GameObject("촬영 조명").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.2f; key.transform.rotation=Quaternion.Euler(35,-30,0);
                RenderSettings.ambientLight = new Color(.6f,.63f,.7f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                Shoot(name+"_front",new Vector3(0,.12f,-4),Vector3.zero);
                if(name=="Char_ChickDoctor" || name=="Char_Pepsin")
                {
                    Shoot(name+"_back",new Vector3(0,.12f,4),Vector3.zero);
                    Shoot(name+"_side",new Vector3(4,.12f,0),Vector3.zero);
                }
                Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText("Tools/polish_asset_inspection.txt",report.ToString(),new UTF8Encoding(false));
            EditorApplication.Exit(0);
        }
        catch(Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    public static void Shoot(string name,Vector3 eye,Vector3 target,float fieldOfView=40)
    {
        // 편집기의 같은 프레임에서 피부 변형 버퍼가 갱신되기 전 촬영하면 삼각형 잔상이 생길 수 있다.
        // 검사용으로 현재 변형을 굳힌 임시 모형을 그린 뒤 즉시 원래 렌더러를 복원한다.
        var frozen=new List<(SkinnedMeshRenderer original,GameObject preview,Mesh mesh)>();
        foreach(var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
        {
            if(!skin.enabled || !skin.gameObject.activeInHierarchy)continue;
            var mesh=new Mesh();skin.BakeMesh(mesh);
            var preview=new GameObject("촬영용 현재 자세");preview.transform.SetParent(skin.transform,false);
            preview.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=preview.AddComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;
            var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);renderer.SetPropertyBlock(block);
            frozen.Add((skin,preview,mesh));skin.enabled=false;
        }
        var go = new GameObject("촬영 카메라"); var cam=go.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye)); cam.fieldOfView=fieldOfView;
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.11f,.15f,.2f); cam.nearClipPlane=.02f;
        var rt=new RenderTexture(800,800,24); var tex=new Texture2D(800,800,TextureFormat.RGB24,false); var old=RenderTexture.active;
        try {cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,800,800),0,0);tex.Apply();File.WriteAllBytes("Tools/polish_captures/"+name+".png",tex.EncodeToPNG());}
        finally {RenderTexture.active=old;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);Object.DestroyImmediate(go);foreach(var item in frozen){item.original.enabled=true;Object.DestroyImmediate(item.preview);Object.DestroyImmediate(item.mesh);}}
    }
}
#endif
