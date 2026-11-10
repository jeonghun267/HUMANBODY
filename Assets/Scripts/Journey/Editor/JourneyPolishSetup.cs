#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class JourneyPolishSetup
{
    public const string Folder="Assets/Data/Generated/Characters";
    const string Effects="Assets/Data/Generated/Effects";
    public static readonly string[] Models={"Char_ChickDoctor","Char_Enzyme","Char_Pepsin","Char_Probiotic","Char_HarmfulBacteria","Char_Virus","Char_RedBloodCell"};
    public static GameObject AnimatedPrefab(string name) => Models.Contains(name) ? AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+name+".prefab") : null;
    [MenuItem("인체탐험/6. 캐릭터 동작과 소화 표현 보완",priority=15)]
    public static void Apply()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var previous=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Directory.CreateDirectory(Folder);Directory.CreateDirectory(Effects);Directory.CreateDirectory("Tools/polish_captures");AssetDatabase.Refresh();
            foreach(var name in Models) BuildRig(name);
            const string persistent="Assets/Data/Generated/JourneyPersistent.prefab";
            var root=PrefabUtility.LoadPrefabContents(persistent);
            try { foreach(var guide in root.GetComponentsInChildren<GuideNarrator>(true)) Attach(guide.gameObject,"Char_ChickDoctor"); PrefabUtility.SaveAsPrefabAsset(root,persistent); }
            finally {PrefabUtility.UnloadPrefabContents(root);}
            var report=new List<string>();
            foreach(string id in new[]{"00_Lobby","01_Mouth","02_Esophagus","03_Stomach","04_SmallIntestine","05_LargeIntestine","06_Theater"})
            {
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/Journey/"+id+".unity");
                ConfigureScene(id.Substring(3));EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                JourneyEnvironmentRepair.Capture(id);
                report.AddRange(JourneyEnvironmentRepair.Verify(id));
                report.Add("연결 완료: "+id);
            }
            AssetDatabase.SaveAssets();
            foreach(string n in new[]{"인체탐험/둥근 액체 입자","인체탐험/소화되는 음식 표면","인체탐험/기관 내부 벽","인체탐험/차분한 기관 조직","인체탐험/환경 배경"})
            {var shader=Shader.Find(n);if(shader==null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))throw new Exception("셰이더 오류: "+n);report.Add("셰이더 검사 통과: "+n);}
            CaptureCharacters(report);
            CaptureEffects();
            File.WriteAllLines("Tools/polish_apply.txt",report.Concat(new[]{"실패 수: 0"}));
            if(Application.isBatchMode)EditorApplication.Exit(0); else EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
        catch(Exception ex) {Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
    static float Ramp(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
    static void BuildRig(string name)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Meshy/"+name+".glb");
        if(source==null)throw new Exception("캐릭터 원본 없음: "+name);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
        var mf=instance.GetComponentInChildren<MeshFilter>();
        var original=mf.sharedMesh;
        var mesh=Object.Instantiate(original);mesh.name=name+"_관절메시";
        Matrix4x4 matrix=instance.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix;
        mesh.vertices=original.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
        mesh.normals=original.normals.Select(n=>matrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();
        var oldTangents=original.tangents;
        mesh.tangents=oldTangents.Select(t=>{var v=matrix.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized;return new Vector4(v.x,v.y,v.z,t.w*(matrix.determinant<0?-1:1));}).ToArray();
        if(matrix.determinant<0) for(int s=0;s<mesh.subMeshCount;s++){var triangles=mesh.GetTriangles(s);for(int i=0;i<triangles.Length;i+=3)(triangles[i+1],triangles[i+2])=(triangles[i+2],triangles[i+1]);mesh.SetTriangles(triangles,s);}
        mesh.RecalculateBounds();var bounds=mesh.bounds;
        var go=new GameObject(name);var skin=go.AddComponent<SkinnedMeshRenderer>();
        skin.sharedMaterials=mf.GetComponent<MeshRenderer>().sharedMaterials;skin.shadowCastingMode=ShadowCastingMode.Off;skin.quality=SkinQuality.Bone4;
        var motion=go.AddComponent<JourneyCharacterMotion>();motion.characterRole=(JourneyCharacterMotion.CharacterRole)Array.IndexOf(Models,name);
        var positions=new[]{new Vector3(0,.42f,0),new Vector3(0,.63f,0),new Vector3(0,.765f,.31f),new Vector3(.28f,.57f,.04f),new Vector3(-.28f,.57f,.04f),new Vector3(.21f,.57f,-.13f),new Vector3(-.21f,.57f,-.13f),new Vector3(0,.24f,0)};
        if(name=="Char_Virus" || name=="Char_RedBloodCell")positions=new[]{new Vector3(0,.5f,0),new Vector3(0,.72f,0),new Vector3(0,.5f,.25f),new Vector3(.2f,.5f,0),new Vector3(-.2f,.5f,0),new Vector3(0,.5f,.2f),new Vector3(0,.5f,-.2f),new Vector3(0,.28f,0)};
        string[] labels={"몸통","머리","아랫부리","왼팔","오른팔","왼날개","오른날개","하체"};
        var bones=new Transform[8];
        for(int i=0;i<8;i++)
        {
            bones[i]=new GameObject(labels[i]).transform;bones[i].SetParent(i==0?go.transform:i==2?bones[1]:bones[0],false);
            var p=positions[i];bones[i].position=go.transform.TransformPoint(new Vector3(bounds.center.x+p.x*bounds.size.x,bounds.min.y+p.y*bounds.size.y,bounds.center.z+p.z*bounds.size.z));
        }
        var weights=new BoneWeight[mesh.vertexCount];var vertices=mesh.vertices;
        for(int i=0;i<vertices.Length;i++)
        {
            var v=vertices[i];float x=(v.x-bounds.center.x)/bounds.size.x,y=(v.y-bounds.min.y)/bounds.size.y,z=(v.z-bounds.center.z)/bounds.size.z;
            var w=new float[8];w[0]=1;
            if(name=="Char_ChickDoctor")
            {
                float side=Ramp(.23f,y>.65f?.39f:.36f,Mathf.Abs(x));
                float arm=side*(1-Ramp(.79f,.85f,y))*Ramp(.24f,.34f,y)*Ramp(-.2f,0,z);
                float wing=side*(1-Ramp(.65f,.72f,y))*Ramp(.45f,.53f,y)*(1-Ramp(-.17f,-.04f,z));
                float head=Ramp(.61f,.67f,y)*(1-arm)*(1-wing);
                float beak=(1-Ramp(.075f,.13f,Mathf.Abs(x)))*Ramp(.72f,.745f,y)*(1-Ramp(.766f,.777f,y))*Ramp(.23f,.34f,z);
                w[1]=head*(1-beak);w[2]=head*beak;w[x>0?3:4]=arm;w[x>0?5:6]=wing;
                w[7]=1-Ramp(.18f,.24f,y);
            }
            else if(name=="Char_Virus" || name=="Char_RedBloodCell")
            {
                w[x>0?3:4]=Ramp(.08f,.42f,Mathf.Abs(x))*.9f;
                w[z>0?5:6]=Ramp(.18f,.44f,Mathf.Abs(z))*.6f;
                w[1]=Ramp(.65f,.9f,y)*.7f;w[7]=(1-Ramp(.1f,.35f,y))*.7f;
            }
            else
            {
                float arms=Ramp(.23f,.4f,Mathf.Abs(x))*Ramp(.28f,.4f,y)*(1-Ramp(.66f,.77f,y));
                w[x>0?3:4]=arms;w[1]=Ramp(.58f,.8f,y)*(1-arms);w[7]=1-Ramp(.12f,.32f,y);
            }
            w[0]=Mathf.Max(0,1-w.Skip(1).Sum());
            var top=Enumerable.Range(0,8).OrderByDescending(j=>w[j]).Take(4).ToArray();float sum=top.Sum(j=>w[j]);
            weights[i]=new BoneWeight{boneIndex0=top[0],weight0=w[top[0]]/sum,boneIndex1=top[1],weight1=w[top[1]]/sum,boneIndex2=top[2],weight2=w[top[2]]/sum,boneIndex3=top[3],weight3=w[top[3]]/sum};
        }
        mesh.boneWeights=weights;mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*go.transform.localToWorldMatrix).ToArray();
        string meshPath=Folder+"/"+name+".asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(existing!=null){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,meshPath);
        skin.sharedMesh=mesh;skin.bones=bones;skin.rootBone=bones[0];var cull=bounds;cull.Expand(.18f);skin.localBounds=cull;skin.updateWhenOffscreen=false;motion.joints=bones;
        PrefabUtility.SaveAsPrefabAsset(go,Folder+"/"+name+".prefab");Object.DestroyImmediate(go);Object.DestroyImmediate(instance);
    }
    static void Attach(GameObject target,string name)
    {
        if(target.GetComponentInChildren<JourneyCharacterMotion>(true)!=null)return;
        var prefab=AnimatedPrefab(name);if(prefab==null)return;
        // 원본의 충돌 영역, 이름표, 음성, 이동 연결은 유지한다.
        foreach(var filter in target.GetComponentsInChildren<MeshFilter>(true))
            if(filter.GetComponent<MeshRenderer>() is MeshRenderer renderer)renderer.enabled=false;
        var child=(GameObject)PrefabUtility.InstantiatePrefab(prefab,target.transform);child.name="관절모형";
        child.transform.localPosition=Vector3.zero;child.transform.localRotation=Quaternion.identity;child.transform.localScale=Vector3.one;
    }
    public static void ConfigureScene(string id)
    {
        JourneyEnvironmentRepair.Configure(id);
        foreach(var swarm in Object.FindObjectsByType<FloatingSwarm>(FindObjectsSortMode.None))swarm.prefabs=Replace(swarm.prefabs);
        foreach(var flow in Object.FindObjectsByType<SplineFlow>(FindObjectsSortMode.None))flow.prefabs=Replace(flow.prefabs);
        foreach(var narrator in Object.FindObjectsByType<GuideNarrator>(FindObjectsSortMode.None))Attach(narrator.gameObject,"Char_ChickDoctor");
        var cut=Object.FindFirstObjectByType<EnzymeCutSequence>();if(cut!=null && cut.enzyme!=null)Attach(cut.enzyme.gameObject,"Char_Pepsin");
        var particles=Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Where(p=>p.name.StartsWith("Saliva_")||p.name.StartsWith("위산_분비_")||p.name.StartsWith("GlandSecretion_")||p.name=="BileFlow"||p.name=="PancreaticJuice").ToArray();
        ParticleSystem splash=null;
        if(particles.Length>0)
        {
            splash=Burst("액체_접촉_물방울",new Color(.8f,.95f,1,.8f),192,.04f);
            foreach(var ps in particles)
            {
                ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=LiquidMaterial();
                var collision=ps.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;collision.mode=ParticleSystemCollisionMode.Collision3D;
                collision.quality=ParticleSystemCollisionQuality.Medium;collision.collidesWith=~0;collision.enableDynamicColliders=false;collision.maxCollisionShapes=32;collision.sendCollisionMessages=true;collision.lifetimeLoss=1;collision.radiusScale=.7f;
                var impact=ps.GetComponent<LiquidContactSplash>();if(impact==null)impact=ps.gameObject.AddComponent<LiquidContactSplash>();impact.contactSplash=splash;
                EditorUtility.SetDirty(ps);
            }
        }
        var chew=Object.FindFirstObjectByType<ChewingSequence>();
        if(chew!=null)
        {
            foreach(var renderer in chew.foodBolus.GetComponentsInChildren<Renderer>())
                if(renderer is MeshRenderer)renderer.sharedMaterials=renderer.sharedMaterials.Select(DigestMaterial).ToArray();
            chew.salivaEmitters=particles.Where(p=>p.name.StartsWith("Saliva_")).OrderBy(p=>p.name).ToArray();
            foreach(var p in chew.salivaEmitters){var main=p.main;main.playOnAwake=false;}
            var zone=GameObject.Find("ChewZone")?.GetComponent<NarrationZone>();
            if(zone!=null)for(int i=zone.onFinished.GetPersistentEventCount()-1;i>=0;i--)if(zone.onFinished.GetPersistentTarget(i) is ParticleSystem p && chew.salivaEmitters.Contains(p))UnityEventTools.RemovePersistentListener(zone.onFinished,i);
        }
        var churn=Object.FindFirstObjectByType<StomachChurn>();
        if(churn!=null)foreach(var food in churn.foodBoluses)
        {
            if(food==null)continue;
            var dissolve=food.GetComponent<JourneyDissolve>();if(dissolve==null)dissolve=food.AddComponent<JourneyDissolve>();
            dissolve.dissolveRenderers=food.GetComponentsInChildren<Renderer>();
            foreach(var renderer in dissolve.dissolveRenderers)renderer.sharedMaterials=renderer.sharedMaterials.Select(DigestMaterial).ToArray();
            foreach(var mf in food.GetComponentsInChildren<MeshFilter>())if(mf.GetComponent<Collider>()==null)mf.gameObject.AddComponent<MeshCollider>();
        }
        if(cut!=null)
        {
            cut.cutVfx=Burst("효소_절단_조각",new Color(1,.77f,.24f,1),160,.075f);
            var r=cut.cutVfx.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Effects+"/소화_빛입자.mat")??LiquidMaterial();
            var main=cut.cutVfx.main;main.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.55f);main.startSpeed=new ParticleSystem.MinMaxCurve(.5f,1.1f);main.gravityModifier=.07f;
        }
        // 자세한 기관 모형은 원래 표면 그림을 유지하며 젖은 반사 정도만 조절한다.
        foreach(var label in Object.FindObjectsByType<PartLabel>(FindObjectsSortMode.None))
        {
            if(!new[]{"Villi","Haustra","Mucus"}.Contains(label.partId))continue;
            foreach(var r in label.GetComponentsInChildren<MeshRenderer>())
            {
                var materials=r.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var mat=materials[i];if(mat==null || mat.shader==null || mat.shader.name.Contains("TextMesh"))continue;
                    string path=Effects+"/점막_"+label.partId+"_"+i+".mat";
                    var wet=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(wet==null){wet=new Material(mat);wet.name="촉촉한_"+label.partId;AssetDatabase.CreateAsset(wet,path);}
                    if(wet.HasProperty("roughnessFactor"))wet.SetFloat("roughnessFactor",.48f);
                    if(wet.HasProperty("metallicFactor"))wet.SetFloat("metallicFactor",0);
                    if(wet.HasProperty("_Smoothness"))wet.SetFloat("_Smoothness",.72f);
                    materials[i]=wet;
                }
                r.sharedMaterials=materials;
            }
        }
    }
    static GameObject[] Replace(GameObject[] prefabs)=>prefabs?.Select(p=>p==null?null:AnimatedPrefab(p.name)??p).ToArray();
    static Material LiquidMaterial()
    {
        string path=Effects+"/둥근_액체.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("인체탐험/둥근 액체 입자"));AssetDatabase.CreateAsset(mat,path);}return mat;
    }
    static Material DigestMaterial(Material source)
    {
        if(source.shader.name=="인체탐험/소화되는 음식 표면")return source;
        string path=Effects+"/음식_분해_"+source.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat!=null)return mat;
        mat=new Material(Shader.Find("인체탐험/소화되는 음식 표면"));
        if(source.HasProperty("baseColorTexture"))mat.SetTexture("_BaseMap",source.GetTexture("baseColorTexture"));
        else if(source.HasProperty("_BaseMap"))mat.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));
        if(source.HasProperty("normalTexture"))mat.SetTexture("_BumpMap",source.GetTexture("normalTexture"));
        if(source.HasProperty("baseColorFactor"))mat.SetColor("_BaseColor",source.GetColor("baseColorFactor"));
        AssetDatabase.CreateAsset(mat,path);return mat;
    }
    static ParticleSystem Burst(string name,Color color,int max,float size)
    {
        var root=GameObject.Find("JourneyEffects")??new GameObject("JourneyEffects");
        var t=root.transform.Find(name);var go=t!=null?t.gameObject:new GameObject(name);go.transform.SetParent(root.transform,false);
        var ps=go.GetComponent<ParticleSystem>();if(ps==null)ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=1;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=max;main.startLifetime=new ParticleSystem.MinMaxCurve(.2f,.45f);main.startSize=new ParticleSystem.MinMaxCurve(size*.6f,size*1.3f);main.startColor=color;main.startSpeed=.4f;main.gravityModifier=.15f;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
        var em=ps.emission;em.enabled=true;em.rateOverTime=0;em.rateOverDistance=0;em.SetBursts(new ParticleSystem.Burst[0]);
        var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.025f;
        var over=ps.colorOverLifetime;over.enabled=true;var grad=new Gradient();grad.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});over.color=grad;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=LiquidMaterial();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=.15f;
        return ps;
    }
    static void CaptureEffects()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
        var light=new GameObject("효과 확인 조명").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(40,140,0);
        var food=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Meshy/Prop_FoodBolus.glb"));
        foreach(var r in food.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(DigestMaterial).ToArray();
        var dissolve=food.AddComponent<JourneyDissolve>();dissolve.dissolveRenderers=food.GetComponentsInChildren<Renderer>();
        foreach(float p in new[]{0f,.45f,.8f,1f}) {dissolve.SetProgress(p);JourneyPolishInspect.Shoot("음식분해_"+Mathf.RoundToInt(p*100),new Vector3(0,.3f,4),Vector3.zero);}
        Object.DestroyImmediate(food);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Sphere);wall.transform.localScale=Vector3.one*2;
        wall.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Data/Environments/위_위벽주름_가까운벽.mat");
        JourneyPolishInspect.Shoot("점막_표면",new Vector3(0,.3f,4),Vector3.zero);Object.DestroyImmediate(wall);
        var liquid=Burst("검사용_액체",new Color(.65f,.85f,1,.9f),40,.25f);
        liquid.Play();for(int i=0;i<9;i++)liquid.Emit(new ParticleSystem.EmitParams{position=new Vector3((i%3-1)*.5f,(i/3-1)*.5f,0),velocity=Vector3.zero,startLifetime=5,startSize=.24f},1);
        JourneyPolishInspect.Shoot("액체_물방울",new Vector3(0,0,4),Vector3.zero);
    }
    static void CapturePose(string name,SkinnedMeshRenderer skin,Mesh baked)
    {
        var preview=new GameObject("자세 확인용 모형");preview.AddComponent<MeshFilter>().sharedMesh=baked;
        preview.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
        JourneyPolishInspect.Shoot(name,new Vector3(0,.12f,4),Vector3.zero);
        skin.enabled=true;Object.DestroyImmediate(preview);
    }
    static void CaptureCharacters(List<string> report)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.63f,.7f);
        var light=new GameObject("모형 확인 조명").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(40,140,0);
        foreach(var name in Models)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AnimatedPrefab(name));var motion=go.GetComponent<JourneyCharacterMotion>();var skin=go.GetComponent<SkinnedMeshRenderer>();
            var baked=new Mesh();motion.Pose(0,0,0,0,0);skin.BakeMesh(baked);var original=baked.vertices;
            CapturePose(name+"_대기",skin,baked);
            motion.Pose(1,1,1,1,12);skin.BakeMesh(baked);var moved=baked.vertices;
            if(name=="Char_ChickDoctor")
                {
                    var weights=skin.sharedMesh.boneWeights;
                    int beak=weights.Count(w=>(w.boneIndex0==2?w.weight0:0)+(w.boneIndex1==2?w.weight1:0)+(w.boneIndex2==2?w.weight2:0)+(w.boneIndex3==2?w.weight3:0)>.05f);
                    if(beak<10)throw new Exception("안내자 부리의 관절 연결 부족");
                    report.Add("안내자 부리 연결 점 수: "+beak);
                }
            float max=original.Zip(moved,(a,b)=>Vector3.Distance(a,b)).Max();
            if(max<.003f || max>.7f)throw new Exception(name+" 관절 변형 확인 필요 "+max);
            if(skin.sharedMesh.boneWeights.Any(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)>.001f))throw new Exception(name+" 관절 가중치 오류");
            CapturePose(name+"_동작",skin,baked);
            report.Add("관절 검증: "+name+" / 모형 내부 최대 변화 "+max.ToString("F3")+" / 관절 가중치 정상");
            Object.DestroyImmediate(baked);Object.DestroyImmediate(go);
        }
    }
}
#endif
