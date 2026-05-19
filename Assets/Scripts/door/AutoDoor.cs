using UnityEngine;

public class AutoDoor : MonoBehaviour
{
    private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // "VRCamera" -> "Player"로 변경
        if (other.CompareTag("Player"))
        {
            // isOpen 대신 character_nearby 사용!
            anim.SetBool("character_nearby", true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // "VRCamera" -> "Player"로 변경
        if (other.CompareTag("Player"))
        {
            // 나가면 false로 꺼줌
            anim.SetBool("character_nearby", false);
        }
    }
}