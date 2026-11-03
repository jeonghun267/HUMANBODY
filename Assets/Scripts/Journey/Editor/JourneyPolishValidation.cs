#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class JourneyPolishValidation
{
    static Action<bool,string> check;
    const string Prefix="_보완검사_";
    public static void PrepareScenes()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("검사용 소리 수신").AddComponent<AudioListener>();
        foreach(var id in new[]{"01_Mouth","03_Stomach"})
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Journey/"+id+".unity",OpenSceneMode.Additive);
            var roots=scene.GetRootGameObjects();var group=new GameObject(Prefix+id);SceneManager.MoveGameObjectToScene(group,scene);
            foreach(var root in roots)
            {
                if(root.GetComponent<PersistentBootstrap>()!=null)root.SetActive(false);
                foreach(var zone in root.GetComponentsInChildren<NarrationZone>(true))zone.enabled=false;
                foreach(var portal in root.GetComponentsInChildren<StageExitPortal>(true))portal.enabled=false;
                foreach(var ride in root.GetComponentsInChildren<CapsuleRide>(true))ride.autoBoard=false;
                root.transform.SetParent(group.transform,true);
            }
            group.SetActive(id=="01_Mouth");
        }
    }
    static GameObject Group(string name)=>Resources.FindObjectsOfTypeAll<GameObject>().Single(g=>g.name==Prefix+name && g.scene.IsValid());
    static IEnumerator Delay(float seconds) { double end=EditorApplication.timeSinceStartup+seconds;while(EditorApplication.timeSinceStartup<end)yield return null; }
    static IEnumerator Until(Func<bool> condition,string label,float seconds=8)
    {double end=EditorApplication.timeSinceStartup+seconds;while(!condition()){if(EditorApplication.timeSinceStartup>end)throw new Exception("보완 검사 시간 초과: "+label);yield return null;}}
    public static IEnumerator RunChecks(Action<bool,string> record)
    {
        check=record;var mouth=Group("01_Mouth");var stomach=Group("03_Stomach");
        SceneManager.SetActiveScene(mouth.scene);yield return Delay(.15f);
        var chew=mouth.GetComponentInChildren<ChewingSequence>();
        var upper=mouth.GetComponentsInChildren<Transform>().Single(x=>x.name=="Part_UpperTeethArch");
        var lower=mouth.GetComponentsInChildren<Transform>().Single(x=>x.name=="Part_LowerTeethArch");
        // 아랫니 활은 씹는 면을 윗니와 평행하게 맞추려 조금 기울고(모형 차이), 평소에는 턱이 살짝 벌어져 있다.
        check(Vector3.Dot(upper.forward,lower.forward)>.9f && Vector3.Dot(upper.up,lower.up)<-.9f,"위아래 치열의 앞뒤 방향 일치, 치관이 서로 마주봄");
        var eye=mouth.GetComponentsInChildren<Transform>().Single(x=>x.name=="SpawnPoint").position+Vector3.up*1.6f;
        var tongue=mouth.GetComponentsInChildren<Transform>().Single(x=>x.name=="Part_Tongue").GetComponentInChildren<Renderer>();
        check(tongue.bounds.max.y<eye.y-.8f,"혀 표면이 탐험자의 눈높이를 가리지 않음");
        check(chew.salivaEmitters.Length==6 && chew.salivaEmitters.Distinct().Count()==6,"실제 입 장면의 침샘 여섯 개를 한 연출에 연결");
        check(chew.salivaEmitters.All(p=>!p.isEmitting && p.particleCount==0),"씹기 전 침샘 자동 재생 없음");
        var zone=mouth.GetComponentsInChildren<NarrationZone>(true).Single(z=>z.name=="ChewZone");
        check(Enumerable.Range(0,zone.onFinished.GetPersistentEventCount()).All(i=>!(zone.onFinished.GetPersistentTarget(i) is ParticleSystem)),"침샘의 개별 중복 시작 연결 제거");
        chew.onChewingFinished=new UnityEvent();int finished=0;chew.onChewingFinished.AddListener(()=>finished++);
        chew.chewCount=2;chew.chewsPerSecond=6;var initialJaw=chew.upperJaw.localRotation;
        chew.Begin();chew.Begin();
        check(chew.salivaEmitters.All(p=>p.isEmitting),"씹기 시작 시 여섯 침샘이 함께 분비");
        yield return Until(()=>finished==1,"씹기 정상 종료");
        check(finished==1 && chew.salivaEmitters.All(p=>!p.isEmitting),"중복 시작에도 완료 한 번, 모든 침샘 방출 종료");
        check(Quaternion.Angle(initialJaw,chew.upperJaw.localRotation)<.01f,"정상 종료 시 턱을 닫힌 자세로 복원");
        yield return Until(()=>chew.salivaEmitters.All(p=>p.particleCount==0),"남은 침 입자 자연 소멸",6);
        check(true,"종료 후 남은 침 입자는 수명에 따라 소멸");
        var originalScale=chew.foodBolus.localScale;
        chew.chewCount=20;chew.Begin();yield return Delay(.1f);chew.enabled=false;yield return null;
        check(chew.salivaEmitters.All(p=>!p.isEmitting&&p.particleCount==0),"중도 비활성화 시 모든 침샘 즉시 정리");
        check(finished==1 && Vector3.Distance(originalScale,chew.foodBolus.localScale)<.001f && Quaternion.Angle(initialJaw,chew.upperJaw.localRotation)<.01f,"중단은 완료를 알리지 않고 음식 크기와 턱 자세 복원");
        chew.enabled=true;chew.chewCount=1;chew.Begin();yield return Until(()=>finished==2,"중단 후 다시 시작");
        check(true,"침 연출 중단 후 다시 시작하고 정상 종료");
        var source=chew.salivaEmitters[0];var impact=source.GetComponent<LiquidContactSplash>();
        check(chew.salivaEmitters.All(p=>p.collision.enabled&&p.collision.sendCollisionMessages&&p.GetComponent<LiquidContactSplash>().contactSplash!=null),"모든 침샘에 접촉 물방울과 충돌 알림 연결");
        var splash=impact.contactSplash;splash.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        for(int i=0;i<30;i++)impact.Splash(Vector3.zero,Vector3.up);
        check(splash.particleCount<=impact.contactLimitPerFrame*impact.dropletsPerContact && splash.particleCount>0,"한 화면 갱신에 과도한 접촉 입자가 쌓이지 않도록 제한");
        // 실제 충돌 콜백까지 검사한다. 관찰용 별도 바닥으로 원래 혀 충돌 모형과 분리한다.
        source.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="검사용 접촉면";floor.transform.position=new Vector3(100,0,0);floor.transform.localScale=new Vector3(5,.2f,5);
        source.transform.SetParent(null,true);source.transform.SetPositionAndRotation(new Vector3(100,1.4f,0),Quaternion.Euler(90,0,0));source.transform.localScale=Vector3.one;
        var main=source.main;main.startSpeed=3;main.startLifetime=2;main.gravityModifier=0;var em=source.emission;em.rateOverTime=40;
        Physics.SyncTransforms();int contacts=impact.EmittedContacts;source.Play();
        yield return Until(()=>impact.EmittedContacts>contacts,"액체의 실제 표면 충돌",8);
        check(true,"액체 입자가 표면에 충돌해 물방울을 방출");source.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);Object.Destroy(source.gameObject);Object.Destroy(floor);
        mouth.SetActive(false);stomach.SetActive(true);SceneManager.SetActiveScene(stomach.scene);yield return Delay(.1f);
        var churn=stomach.GetComponentInChildren<StomachChurn>();churn.onDigested=new UnityEvent();churn.digestDuration=2;
        var food=churn.foodBoluses[0];var dissolve=food.GetComponent<JourneyDissolve>();var foodScale=food.transform.localScale;
        check(churn.foodBoluses.All(f=>f.GetComponent<JourneyDissolve>()!=null),"실제 위 장면의 음식 전체에 표면 분해 연결");
        churn.Begin();yield return Until(()=>dissolve.Progress>.2f,"표면 분해 진행");
        check(dissolve.Progress<1 && food.activeSelf && food.transform.localScale.magnitude>foodScale.magnitude*.7f,"음식 형태를 유지하며 표면 분해 진행");
        churn.enabled=false;yield return null;
        check(dissolve.Progress==0 && food.activeSelf && Vector3.Distance(food.transform.localScale,foodScale)<.001f,"분해 중단 시 음식 표면과 크기 복원");
        churn.enabled=true;churn.digestDuration=.4f;bool digested=false;churn.onDigested.AddListener(()=>digested=true);churn.Begin();yield return Until(()=>digested,"분해 완료");
        check(churn.foodBoluses.All(f=>!f.activeSelf) && churn.chymeBlobs.All(f=>f.activeSelf),"분해 완료 후 음식이 사라지고 미즙 표시");
        var gate=stomach.GetComponentInChildren<SphincterGate>();
        gate.openSeconds=.2f;gate.Open();yield return Until(()=>gate.IsOpen,"괄약근 열기");
        check(gate.openingSurface!=null && gate.openingSurface.GetBlendShapeWeight(0)>99 && !gate.blocker.enabled,"괄약근 중앙 구멍이 실제로 열리고 통과 가능");
        gate.Close();yield return Until(()=>!gate.IsOpen,"괄약근 닫기");
        check(gate.openingSurface.GetBlendShapeWeight(0)<1 && gate.blocker.enabled,"괄약근 닫힘과 통과 차단 복원");
        var cut=stomach.GetComponentInChildren<EnzymeCutSequence>();var motion=cut.enzyme.GetComponentInChildren<JourneyCharacterMotion>();
        cut.approachSeconds=.05f;cut.cutSeconds=.8f;cut.betweenChains=.1f;cut.fragmentLife=.1f;bool cutDone=false;cut.onAllCut=new UnityEvent();cut.onAllCut.AddListener(()=>cutDone=true);
        check(motion!=null && cut.cutVfx!=null,"펩신 관절 동작과 절단 순간 조각 효과 연결");
        cut.Begin();yield return Until(()=>motion.CutAmount>.2f,"펩신 절단 자세");
        check(true,"절단 타이밍에 펩신 팔 관절 동작 실행");
        yield return Until(()=>cut.cutVfx.particleCount>0,"절단 조각 방출");check(true,"단백질 절단 순간 조각 입자 방출");
        yield return Until(()=>cutDone,"단백질 절단 완료");check(motion.CutAmount==0,"절단 후 펩신 기본 동작으로 복귀");
        cut.enabled=false;check(cut.cutVfx.particleCount==0,"효소 연출 중단 시 잔여 효과 정리");
        stomach.SetActive(false);
        var guideObject=new GameObject("검사용 안내자");var model=Object.Instantiate(JourneyPolishSetup.AnimatedPrefab("Char_ChickDoctor"),guideObject.transform);
        var narrator=guideObject.AddComponent<GuideNarrator>();narrator.holdAfterLast=0;narrator.gapBetweenLines=0;
        var guideMotion=model.GetComponent<JourneyCharacterMotion>();narrator.Play(new NarrationLine{text="관절 동작 검사",holdSeconds=5});
        check(guideMotion.speaking,"안내 시작에 부리와 머리 말하기 동작 연결");
        narrator.Skip();check(!guideMotion.speaking,"안내 건너뛰기에도 말하기 동작 종료");
        var target=new GameObject("설명 대상").AddComponent<PartLabel>();target.line=new NarrationLine{text="여기를 관찰해요",holdSeconds=5};target.OnGazeEnter();
        check(guideMotion.FocusTarget==target.transform && guideMotion.speaking,"부위 설명에 안내자의 시선과 손짓 연결");
        narrator.Skip();Object.Destroy(target.gameObject);Object.Destroy(guideObject);
        foreach(var name in JourneyPolishSetup.Models)
        {
            var prefab=JourneyPolishSetup.AnimatedPrefab(name);var skin=prefab.GetComponent<SkinnedMeshRenderer>();
            check(skin!=null&&skin.bones.Length==8&&prefab.GetComponent<JourneyCharacterMotion>()!=null,"관절 모형 준비: "+name);
        }
    }
}
#endif
