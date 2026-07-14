using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EndZoneTrigger : MonoBehaviour
{
    Collider2D zoneCollider;

    void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();
    }

    // Fresh bounds check every call — avoids stale state from OnTriggerEnter/Exit
    // (which Unity doesn't reliably fire when a frog's collider gets disabled, e.g.
    // while swallowed, rather than physically leaving the zone) and avoids relying
    // on IsTouching's simulated-contact state, which needs a physics step to update
    // and won't reflect a just-teleported/just-spawned frog's true position yet.
    public bool ContainsAllFrogs(List<FrogController> frogs)
    {
        if (frogs.Count == 0) return false;

        Bounds zoneBounds = zoneCollider.bounds;

        foreach (var frog in frogs)
        {
            if (frog == null) return false;

            var col = frog.GetComponent<Collider2D>();
            if (col == null || !col.enabled) return false;
            if (!zoneBounds.Intersects(col.bounds)) return false;
        }

        return true;
    }
}
