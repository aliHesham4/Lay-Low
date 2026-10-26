using UnityEngine;

// Makes the fireplace look alive:
// - tells the flame shader where the fire starts and how tall it is (from the flame cards' bounds),
//   so the flames stay correct wherever the fireplace is placed or however it is scaled
// - flickers the fire light
[ExecuteAlways]
public class FireplaceFX : MonoBehaviour
{
    [Tooltip("Renderers of the flame cards (use the LayLow/ProceduralFire material).")]
    public Renderer[] flameCards;
    [Tooltip("Other renderers that use the flame material (e.g. the glowing back of the firebox). They don't affect the flame height.")]
    public Renderer[] alsoBurning;
    [Tooltip("Optional light inside the fireplace that flickers.")]
    public Light fireLight;

    [Header("Flames")]
    [Tooltip("Flame height as a multiple of the cards' own height.")]
    public float heightScale = 1.0f;

    [Header("Light flicker")]
    public float baseIntensity = 1.2f;
    public float flickerAmount = 0.45f;
    public float flickerSpeed = 7f;

    MaterialPropertyBlock block;
    float seed;

    void OnEnable()
    {
        seed = Random.value * 100f;
        ApplyFlameBounds();
    }

    void OnValidate() { ApplyFlameBounds(); }

    public void ApplyFlameBounds()
    {
        if (flameCards == null || flameCards.Length == 0) return;
        Bounds b = new Bounds();
        bool first = true;
        foreach (Renderer r in flameCards)
        {
            if (r == null) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        if (first) return;

        if (block == null) block = new MaterialPropertyBlock();
        System.Collections.Generic.List<Renderer> all = new System.Collections.Generic.List<Renderer>(flameCards);
        if (alsoBurning != null) all.AddRange(alsoBurning);
        foreach (Renderer r in all)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetFloat("_FireBottom", b.min.y);
            block.SetFloat("_FireHeight", b.size.y * heightScale);
            r.SetPropertyBlock(block);
        }
    }

    void Update()
    {
        if (fireLight == null) return;
        float t = (Application.isPlaying ? Time.time : (float)UnityEditor_Time()) * flickerSpeed + seed;
        float n = Mathf.PerlinNoise(t, 0.37f) * 0.7f + Mathf.PerlinNoise(t * 2.3f, 5.1f) * 0.3f; // 0..1
        fireLight.intensity = baseIntensity * (1f - flickerAmount * 0.5f + flickerAmount * n);
    }

    static double UnityEditor_Time()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorApplication.timeSinceStartup;
#else
        return Time.time;
#endif
    }
}
