using System.Collections.Generic;
using UnityEngine;

// Spins the item model inside a collectible's glass sphere a full 360° around its own centre.
// The sphere itself and the light stay still.
public class CollectibleSpin : MonoBehaviour
{
    [Tooltip("Degrees per second around the vertical axis.")]
    public float degreesPerSecond = 90f;

    Transform pivot;

    void Start()
    {
        // Every direct child that has a visible mesh under it is part of the item model.
        var parts = new List<Transform>();
        Bounds b = new Bounds();
        bool has = false;
        foreach (Transform child in transform)
        {
            if (child.GetComponent<Light>() != null) continue;
            var rs = child.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) continue;
            parts.Add(child);
            foreach (var r in rs)
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue; // hidden parts would throw the centre off
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
        }
        if (!has) { enabled = false; return; }

        pivot = new GameObject("Spin Pivot").transform;
        pivot.SetParent(transform, false);
        pivot.position = b.center;
        foreach (var p in parts) p.SetParent(pivot, true);
        pivot.Rotate(0f, Random.Range(0f, 360f), 0f, Space.Self);
    }

    void Update()
    {
        if (pivot != null) pivot.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
