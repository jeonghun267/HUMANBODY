using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(ParticleSystem))]
[AddComponentMenu("인체탐험/액체 접촉 물방울")]
public class LiquidContactSplash : MonoBehaviour
{
    public ParticleSystem contactSplash;
    [Range(1,12)] public int dropletsPerContact=3;
    [Range(1,32)] public int contactLimitPerFrame=6;
    public int EmittedContacts { get; private set; }
    ParticleSystem source;
    readonly List<ParticleCollisionEvent> contacts=new List<ParticleCollisionEvent>(32);
    int frame=-1,used;
    void Awake() { source=GetComponent<ParticleSystem>(); }
    void OnParticleCollision(GameObject other)
    {
        if(source==null || contactSplash==null) return;
        int count=source.GetCollisionEvents(other,contacts);
        for(int i=0;i<count;i++) Splash(contacts[i].intersection,contacts[i].normal);
    }
    public void Splash(Vector3 position,Vector3 normal)
    {
        if(contactSplash==null) return;
        if(frame!=Time.frameCount) {frame=Time.frameCount;used=0;}
        if(used++>=contactLimitPerFrame) return;
        EmittedContacts++;
        if (!contactSplash.isPlaying) contactSplash.Play(false);
        normal=normal.sqrMagnitude<.001f?Vector3.up:normal.normalized;
        for(int j=0;j<dropletsPerContact;j++)
        {
            var direction=(normal+Random.insideUnitSphere*.6f).normalized;
            var emit=new ParticleSystem.EmitParams {position=position+normal*.018f,velocity=direction*Random.Range(.25f,.65f)};
            contactSplash.Emit(emit,1);
        }
    }
}
