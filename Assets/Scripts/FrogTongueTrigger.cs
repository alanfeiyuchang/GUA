using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FrogTongueTrigger : MonoBehaviour
{
    FrogController owner;

    void Awake()
    {
        owner = GetComponentInParent<FrogController>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (owner == null) return;

        FrogController target = other.GetComponent<FrogController>();
        if (target == null || target == owner) return;

        owner.TryAbsorb(target);
    }
}
