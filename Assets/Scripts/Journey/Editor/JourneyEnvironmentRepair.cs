#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;
using Object=UnityEngine.Object;

/// <summary>기관 내부를 실제 연속 벽으로 구성하고, 통로와 시야를 가리는 잘못된 배치를 정리한다.</summary>
public static class JourneyEnvironmentRepair
{
    const string Folder="Assets/Data/Generated/EnvironmentRepair";
    static Bounds Bounds(GameObject go)
    {
        var rs=go.GetComponentsInChildren<Renderer>().Where(r=>r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
        if(rs.Length==0)return new Bounds(go.transform.position,Vector3.zero);
        var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
    }
    static void Center(GameObject go,Vector3 center) {go.transform.position+=center-Bounds(go).center;}
    static void Bottom(GameObject go,float y) {go.transform.position+=Vector3.up*(y-Bounds(go).min.y);}
    public static Material Tissue(string id)
    {
        if(JourneyRealism.IsBody(id))return JourneyRealism.Mucosa(id);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();string path=Folder+"/"+id+"_조직.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("인체탐험/차분한 기관 조직"));AssetDatabase.CreateAsset(mat,path);}
        var p=JourneyEnvironments.GetPreset(id);ColorUtility.TryParseHtmlString(p.baseHex,out var color);ColorUtility.TryParseHtmlString(p.lightHex,out var light);
        mat.SetColor("_BaseColor",color);mat.SetColor("_LightColor",Color.Lerp(color,light,.5f));mat.SetFloat("_Detail",.3f);mat.SetFloat("_Wetness",.18f);EditorUtility.SetDirty(mat);return mat;
    }
    public static void Configure(string id)
    {
        if(id=="Lobby" || id=="Theater")return;
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var shell=GameObject.Find(id=="Mouth"?"MouthDome":"StomachShell");
        if(shell!=null)shell.GetComponent<Renderer>().sharedMaterial=Tissue(id);
        if(id=="Mouth")Mouth();
        if(id=="Stomach")StomachFolds();
        foreach(string part in new[]{"Part_Tongue","Part_Uvula","Part_Epiglottis","Part_Palate"})
        {
            var organ=GameObject.Find(part);if(organ==null)continue;
            foreach(var renderer in organ.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>Tissue(id)).ToArray();
        }
        foreach(var ride in Object.FindObjectsByType<CapsuleRide>(FindObjectsSortMode.None))
        {
            // 유에프오처럼 튀어나온 외피 대신 기존 발판·난간과 좌석 기준으로 이동한다.
            foreach(var filter in ride.GetComponentsInChildren<MeshFilter>(true))
            {
                string asset=AssetDatabase.GetAssetPath(filter.sharedMesh);
                if(asset.EndsWith("Prop_ExplorerCapsule.glb") && filter.GetComponent<Renderer>() is Renderer renderer)renderer.enabled=false;
            }
            var rail=ride.transform.Find("Railing");
            if(rail!=null && rail.Find("관찰_발판")==null)
            {
                var platform=GameObject.CreatePrimitive(PrimitiveType.Cylinder);platform.name="관찰_발판";platform.transform.SetParent(rail,false);
                platform.transform.localPosition=new Vector3(0,-.1f,0);platform.transform.localScale=new Vector3(2.1f,.06f,2.1f);
                Object.DestroyImmediate(platform.GetComponent<Collider>());
                var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.22f,.32f,.36f));mat.SetFloat("_Smoothness",.2f);
                string path=Folder+"/관찰_발판.mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing==null){AssetDatabase.CreateAsset(mat,path);existing=mat;}else Object.DestroyImmediate(mat);
                platform.GetComponent<Renderer>().sharedMaterial=existing;
            }
        }
        if(id=="Esophagus" || id=="SmallIntestine" || id=="LargeIntestine")
        {
            var ride=Object.FindFirstObjectByType<CapsuleRide>();
            if(ride!=null && ride.cart!=null && ride.cart.Spline!=null)Tube(id,ride.cart.Spline);
            var old=GameObject.Find("Tunnel");if(old!=null)old.SetActive(false);
            foreach(var wave in Object.FindObjectsByType<PeristalsisWave>(FindObjectsSortMode.None))
            {
                wave.ringPrefab=ContractionRing(id);wave.ringDiameter=id=="Esophagus"?6.7f:6.2f;wave.ringCount=2;wave.squeezeScale=.97f;
                wave.ringEuler=id=="Esophagus"?Vector3.zero:new Vector3(90,0,0);
            }
        }
        // 괄약근 모형의 구멍 축은 원본의 세로 축이다. 통로 축과 일치시킨다.
        foreach(var gate in Object.FindObjectsByType<SphincterGate>(FindObjectsSortMode.None))
        {
            var center=Bounds(gate.gameObject).center;
            gate.transform.rotation=id=="Esophagus"?Quaternion.identity:Quaternion.Euler(90,0,0);
            Center(gate.gameObject,center);
            GateSurface(gate,id);
            if(gate.blocker is BoxCollider box)
            {
                // 방향 수정으로 논리 차단 상자도 함께 뒤집히지 않게 통로 단면에 다시 맞춘다.
                Vector3 worldSize=id=="Esophagus"?new Vector3(4,.4f,4):new Vector3(4,4,.4f);
                var local=new Vector3(4,.4f,4);var s=gate.transform.lossyScale;
                box.size=new Vector3(local.x/Mathf.Abs(s.x),local.y/Mathf.Abs(s.y),local.z/Mathf.Abs(s.z));
            }
        }
        var cap=GameObject.Find("ThroatCap");if(cap!=null)cap.GetComponent<Renderer>().sharedMaterial=Tissue(id);
    }
    static void StomachFolds()
    {
        var mucus=GameObject.Find("MucusLayer");if(mucus!=null)foreach(var r in mucus.GetComponentsInChildren<Renderer>())r.enabled=false;
        var old=GameObject.Find("위벽에_붙은_주름");if(old!=null)Object.DestroyImmediate(old);
        var shell=GameObject.Find("StomachShell");if(shell==null)return;
        const int around=96,vertical=48;var vertices=new Vector3[(around+1)*(vertical+1)];var triangles=new List<int>();
        for(int j=0;j<=vertical;j++)for(int i=0;i<=around;i++)
        {
            float y=(float)j/vertical*Mathf.PI,a=(float)i/around*Mathf.PI*2;
            float sin=Mathf.Sin(y),fold=.02f*Mathf.Sin(a*10+Mathf.Sin(y*3))*sin*sin;
            int index=j*(around+1)+i;
            vertices[index]=new Vector3(Mathf.Cos(a)*sin*(.5f+fold),Mathf.Cos(y)*.5f,Mathf.Sin(a)*sin*(.5f+fold));
            if(j<vertical&&i<around){int next=index+around+1;triangles.AddRange(new[]{index,next,index+1,index+1,next,next+1});}
        }
        var mesh=new Mesh{name="주름이 이어진 위 내부벽"};mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();SaveMesh(Folder+"/Stomach_내부벽.asset",ref mesh);
        shell.GetComponent<MeshFilter>().sharedMesh=mesh;
        var walls=GameObject.Find("Walls");if(walls==null)return;
        foreach(var panel in walls.GetComponentsInChildren<Transform>(true).Where(x=>x.name=="Part_RugaeWallPanel"))
        {
            foreach(var r in panel.GetComponentsInChildren<MeshRenderer>())r.enabled=false;
            foreach(var c in panel.GetComponentsInChildren<Collider>())c.enabled=false;
            var label=panel.GetComponent<PartLabel>();if(label==null)continue;
            panel.position=new Vector3(6.2f,3.3f,9.3f);
            var anchor=panel.GetComponent<SphereCollider>();if(anchor==null)anchor=panel.gameObject.AddComponent<SphereCollider>();anchor.enabled=true;anchor.isTrigger=true;anchor.center=Vector3.zero;anchor.radius=.65f/Mathf.Max(.01f,panel.lossyScale.x);
            if(label.labelRoot!=null)label.labelRoot.transform.position=panel.position+Vector3.up*.5f;
        }
    }
    public static void GateSurface(SphincterGate gate,string id)
    {
        foreach(var filter in gate.GetComponentsInChildren<MeshFilter>())
            if(AssetDatabase.GetAssetPath(filter.sharedMesh).EndsWith("Part_Sphincter.glb"))filter.GetComponent<Renderer>().enabled=false;
        var child=gate.transform.Find("열리는_점막");
        if(child==null){child=new GameObject("열리는_점막").transform;child.SetParent(gate.transform,false);}
        var scale=gate.transform.lossyScale;child.localScale=new Vector3(1/Mathf.Abs(scale.x),1/Mathf.Abs(scale.y),1/Mathf.Abs(scale.z));
        // 원본 중심과 같게 놓되 고정된 구형 마개를 사용하지 않는다.
        var center=Bounds(gate.gameObject).center;child.position=center;
        const int around=64,radial=8;var v=new Vector3[(around+1)*(radial+1)];var delta=new Vector3[v.Length];var triangles=new List<int>();
        for(int a=0;a<=around;a++)for(int b=0;b<=radial;b++)
        {
            float angle=a*2*Mathf.PI/around,u=(float)b/radial;int index=a*(radial+1)+b;
            float closed=Mathf.Lerp(.12f,2.35f,u),opened=Mathf.Lerp(1.65f,2.35f,u);
            var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            // 닫힌 괄약근은 가운데로 모이는 주름 10개가 오므라든 모양이다.
            float pucker=Mathf.Pow(.5f+.5f*Mathf.Cos(angle*10),3)*(1-u)*.25f;
            float lip=Mathf.Sin(u*Mathf.PI)*.17f+pucker;closed*=1-.08f*Mathf.Cos(angle*10)*(1-u);
            // 식도 쪽에서 내려다보는 분문은 위(탐험가 쪽)로 부풀지 않고 아래(위 속)로 오목하게 들어간다.
            v[index]=direction*closed+Vector3.up*lip*(id=="Esophagus"?-1:1);delta[index]=direction*(opened-closed);
            if(a<around&&b<radial){int next=index+radial+1;triangles.AddRange(new[]{index,next,index+1,index+1,next,next+1});}
        }
        var mesh=new Mesh{name="괄약근의 열리는 점막"};mesh.vertices=v;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.AddBlendShapeFrame("열림",100,delta,new Vector3[v.Length],new Vector3[v.Length]);
        SaveMesh(Folder+"/"+id+"_괄약근.asset",ref mesh);
        var skin=child.GetComponent<SkinnedMeshRenderer>();if(skin==null)skin=child.gameObject.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterial=Tissue(id);skin.localBounds=mesh.bounds;skin.updateWhenOffscreen=true;skin.shadowCastingMode=ShadowCastingMode.Off;gate.openingSurface=skin;
    }
    // 예전에는 이·혀·입천장만 옮겨 턱 회전축·목젖·침샘·음식이 옛 자리에 남았다. 이제 입 전체를 한 곳에서 실제 비율로 배치한다.
    static void Mouth() => JourneyMouthAnatomy.Configure();
    static void SaveMesh(string path,ref Mesh mesh)
    {
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing!=null){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
    }
    public static void Tube(string id,SplineContainer path)
    {
        // 기관별 실제 주름 모양(식도 세로 주름, 소장 돌림 주름, 대장 팽기)은 JourneyRealism.TubeRadius 가 정한다.
        float pathLength=path.CalculateLength();
        float step=id=="SmallIntestine"?.1f:.15f; // 돌림 주름은 얇아서 길이 방향으로 더 촘촘히 나눈다
        int around=96,length=Mathf.Clamp(Mathf.CeilToInt(pathLength*1.24f/step),120,640);float radius=id=="Esophagus"?3.4f:3.15f;
        var v=new Vector3[(around+1)*(length+1)];var uv=new Vector2[v.Length];var triangles=new List<int>(around*length*6);
        Vector3 start=path.EvaluatePosition(0),end=path.EvaluatePosition(1);
        Vector3 first=((Vector3)path.EvaluatePosition(.01f)-start).normalized,last=(end-(Vector3)path.EvaluatePosition(.99f)).normalized;
        for(int j=0;j<=length;j++)
        {
            float u=(float)j/length;float extended=Mathf.Lerp(-.12f,1.12f,u);
            Vector3 center,tangent;
            if(extended<0){center=start+first*(extended/.12f*6);tangent=first;}
            else if(extended>1){center=end+last*((extended-1)/.12f*6);tangent=last;}
            else {center=path.EvaluatePosition(extended);tangent=((Vector3)path.EvaluateTangent(extended)).normalized;}
            // 가로 방향 단면을 실제 이동 경로에 직각으로 놓는다.
            // 벽이 이동 경로 주변에서 뒤집히거나 꼬이지 않도록 기관별 단면 방향을 고정한다.
            var a=Vector3.right;var b=id=="Esophagus"?Vector3.forward:Vector3.up;
            for(int i=0;i<=around;i++)
            {
                float angle=(float)i/around*Mathf.PI*2;
                float wall=JourneyRealism.TubeRadius(id,radius,angle,extended*pathLength);
                int index=j*(around+1)+i;v[index]=center+(a*Mathf.Cos(angle)+b*Mathf.Sin(angle))*wall;uv[index]=new Vector2((float)i/around,u*12);
                if(j<length&&i<around){int next=index+around+1;triangles.AddRange(new[]{index,next,index+1,index+1,next,next+1});}
            }
        }
        var mesh=new Mesh{name=id+"_연속_내부벽"};mesh.vertices=v;mesh.uv=uv;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();SaveMesh(Folder+"/"+id+"_통로.asset",ref mesh);
        string name="연속_기관벽";var go=GameObject.Find(name)??new GameObject(name);go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=Vector3.one;
        var mf=go.GetComponent<MeshFilter>();if(mf==null)mf=go.AddComponent<MeshFilter>();mf.sharedMesh=mesh;
        var renderer=go.GetComponent<MeshRenderer>();if(renderer==null)renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=Tissue(id);renderer.shadowCastingMode=ShadowCastingMode.Off;
        var collider=go.GetComponent<MeshCollider>();if(collider==null)collider=go.AddComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;
        string capName="통로_끝_벽";var cap=GameObject.Find(capName);
        if(cap==null){cap=GameObject.CreatePrimitive(PrimitiveType.Sphere);cap.name=capName;Object.DestroyImmediate(cap.GetComponent<Collider>());}
        cap.transform.position=end+last*7;cap.transform.localScale=Vector3.one*(radius*2.7f);cap.GetComponent<Renderer>().sharedMaterial=Tissue(id);cap.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        if(id=="LargeIntestine")
        {
            var lining=GameObject.Find("HaustraLining");
            if(lining!=null)
            {
                foreach(var panelRenderer in lining.GetComponentsInChildren<Renderer>())if(panelRenderer is MeshRenderer)panelRenderer.enabled=false;
                foreach(var c in lining.GetComponentsInChildren<Collider>())c.enabled=false;
                var label=lining.GetComponentInChildren<PartLabel>();
                if(label!=null)
                {
                    Vector3 point=path.EvaluatePosition(.32f);label.transform.position=point+Vector3.right*3.0f;
                    var anchor=label.GetComponent<SphereCollider>();if(anchor==null)anchor=label.gameObject.AddComponent<SphereCollider>();anchor.enabled=true;anchor.isTrigger=true;anchor.center=Vector3.zero;anchor.radius=.6f/Mathf.Max(.01f,label.transform.lossyScale.x);
                    if(label.labelRoot!=null)label.labelRoot.transform.position=label.transform.position+Vector3.up*.5f;
                }
            }
        }

    }
    static GameObject ContractionRing(string id)
    {
        string path=Folder+"/"+id+"_벽수축.prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
        const int around=64,section=8;float radius=3.25f,thick=.16f;
        var vertices=new Vector3[(around+1)*(section+1)];var triangles=new List<int>();
        for(int a=0;a<=around;a++)for(int b=0;b<=section;b++)
        {
            float angle=a*2*Mathf.PI/around,s=b*2*Mathf.PI/section;int k=a*(section+1)+b;
            vertices[k]=new Vector3(Mathf.Cos(angle)*(radius+Mathf.Cos(s)*thick),Mathf.Sin(s)*.11f,Mathf.Sin(angle)*(radius+Mathf.Cos(s)*thick));
            if(a<around&&b<section){int next=k+section+1;triangles.AddRange(new[]{k,k+1,next,k+1,next+1,next});}
        }
        var mesh=new Mesh{name="통로를 막지 않는 벽 수축띠"};mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();SaveMesh(Folder+"/"+id+"_벽수축.asset",ref mesh);
        var go=new GameObject("벽_수축띠");go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Tissue(id);
        existing=PrefabUtility.SaveAsPrefabAsset(go,path);Object.DestroyImmediate(go);return existing;
    }
    public static IEnumerable<string> Verify(string scene)
    {
        var result=new List<string>();
        string shared=File.ReadAllText("Assets/Shaders/JourneyEnvironmentShared.hlsl");
        if(shared.Contains("atan2") || shared.Contains("asin") || shared.Contains("_Time"))throw new Exception("배경의 방사형 또는 시간 변화 무늬가 남아 있음");
        result.Add("환경 확인: "+scene+" 배경에 방사형 무늬와 시간 변화 없음");
        var tube=GameObject.Find("연속_기관벽");var ride=Object.FindFirstObjectByType<CapsuleRide>();
        if(tube!=null&&ride!=null)
        {
            Physics.SyncTransforms();var collider=tube.GetComponent<MeshCollider>();var path=ride.cart.Spline;
            for(int i=0;i<=8;i++)
            {
                float u=i/8f;Vector3 center=path.EvaluatePosition(u);var tangent=((Vector3)path.EvaluateTangent(u)).normalized;
                var reference=Mathf.Abs(Vector3.Dot(tangent,Vector3.up))>.9f?Vector3.forward:Vector3.up;
                var a=Vector3.Cross(tangent,reference).normalized;var b=Vector3.Cross(tangent,a).normalized;
                foreach(var direction in new[]{a,-a,b,-b})
                {
                    if(!collider.Raycast(new Ray(center,direction),out var hit,5))throw new Exception(scene+" 통로 벽이 이어지지 않음: "+u);
                    if(hit.distance<2.6f)throw new Exception(scene+" 이동 경로와 벽의 간격 부족: "+hit.distance);
                }
            }
            result.Add("환경 확인: "+scene+" 이동 경로 아홉 지점의 사방 벽 연결과 여유 공간 정상");
        }
        foreach(var gate in Object.FindObjectsByType<SphincterGate>(FindObjectsSortMode.None))
            if(gate.openingSurface==null || gate.openingSurface.sharedMesh.blendShapeCount!=1)throw new Exception("괄약근의 구멍 변형 연결 없음");
        return result;
    }
    public static void Capture(string scene)
    {
        if(scene.Contains("Lobby")||scene.Contains("Theater"))return;
        Directory.CreateDirectory("Tools/polish_captures");var spawn=GameObject.Find("SpawnPoint");
        if(spawn!=null)
        {
            Vector3 eye=spawn.transform.position+Vector3.up*1.6f;
            JourneyPolishInspect.Shoot(scene+"_탐험자_앞",eye,eye+spawn.transform.forward*8,75);
            JourneyPolishInspect.Shoot(scene+"_탐험자_옆",eye,eye+Vector3.right*8,75);
        }
        if(scene.Contains("Mouth"))
        {
            var lower=GameObject.Find("Part_LowerTeethArch");var upper=GameObject.Find("Part_UpperTeethArch");var b=Bounds(lower);b.Encapsulate(Bounds(upper));
            var dome=GameObject.Find("MouthDome").GetComponent<Renderer>();dome.enabled=false;
            var tongue=GameObject.Find("Part_Tongue");var palate=GameObject.Find("Part_Palate");
            tongue.SetActive(false);palate.SetActive(false);
            JourneyPolishInspect.Shoot("입_치열_측면",b.center+new Vector3(17,4,8),b.center,65);
            JourneyPolishInspect.Shoot("입_치열_정면",b.center+new Vector3(0,0,-20),b.center,65);
            tongue.SetActive(true);palate.SetActive(true);dome.enabled=true;
        }
        var ride=Object.FindFirstObjectByType<CapsuleRide>();
        if(ride!=null&&ride.cart!=null&&ride.cart.Spline!=null)
        {
            var path=ride.cart.Spline;Vector3 eye=(Vector3)path.EvaluatePosition(.45f)+Vector3.up*1.6f;
            JourneyPolishInspect.Shoot(scene+"_통로_중간",eye,eye+Vector3.forward*8,75);
            if(scene.Contains("Esophagus"))JourneyPolishInspect.Shoot(scene+"_아래쪽_통로",eye,eye+Vector3.down*8+Vector3.forward,75);
        }
    }
}
#endif

