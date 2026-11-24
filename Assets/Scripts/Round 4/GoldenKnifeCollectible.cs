using UnityEngine;

// The golden collectible lying on the map. The first free player (or AI) who walks up to it takes the knife.
// A tall golden beam shows everyone where it is.
public class GoldenKnifeCollectible : MonoBehaviour
{
    [Tooltip("How close a player must get to grab it (metres, flat distance).")]
    public float pickupRadius = 0.9f;

    [Header("Beam (so everyone can see where it is)")]
    [Tooltip("A 'Sprites/Default' material. The beam colour comes from Beam Color.")]
    public Material beamMaterial;
    public Color beamColor = new Color(1f, 0.82f, 0.25f, 0.55f);
    public float beamHeight = 7f;
    public float beamRadius = 0.32f;

    Hider[] hiders;
    float nextCheck;
    Material beamInstance;

    void Start()
    {
        BuildBeam();
    }

    void BuildBeam()
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Golden Beam";
        Destroy(beam.GetComponent<Collider>());
        beam.transform.SetParent(transform, false);
        // the root is scaled (the glass bubble), so size the beam in world units
        float s = Mathf.Max(0.0001f, transform.lossyScale.x);
        beam.transform.localScale = new Vector3(beamRadius * 2f / s, beamHeight * 0.5f / s, beamRadius * 2f / s);
        beam.transform.position = transform.position + Vector3.up * (beamHeight * 0.5f - 0.15f);

        Shader sh = beamMaterial != null ? beamMaterial.shader : Shader.Find("Sprites/Default");
        beamInstance = beamMaterial != null ? new Material(beamMaterial) : new Material(sh);
        // strong at the bottom, fading away at the top
        Texture2D tex = new Texture2D(2, 64, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
        {
            float v = y / 63f; // cylinder UV: v runs bottom to top
            float a = Mathf.Pow(1f - v, 1.6f) * Mathf.SmoothStep(0f, 1f, v * 12f);
            for (int x = 0; x < 2; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        beamInstance.mainTexture = tex;
        beamInstance.color = beamColor;

        MeshRenderer mr = beam.GetComponent<MeshRenderer>();
        mr.sharedMaterial = beamInstance;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void Update()
    {
        if (beamInstance != null)
        {
            float k = (Mathf.Sin(Time.time * 2.4f) + 1f) * 0.5f;
            Color c = beamColor; c.a = beamColor.a * (0.75f + 0.25f * k);
            beamInstance.color = c;
        }

        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.1f;
        if (KnifeHolder.Current != null || FinalHuntRound.Instance == null) return;
        if (hiders == null || hiders.Length == 0) hiders = FindObjectsByType<Hider>(FindObjectsSortMode.None);

        Hider best = null;
        float bestD = pickupRadius;
        foreach (Hider h in hiders)
        {
            if (h == null || h.IsCaught || h.IsHidden) continue;
            Vector3 a = h.transform.position, b = transform.position;
            a.y = 0f; b.y = 0f;
            float d = Vector3.Distance(a, b);
            if (d < bestD) { bestD = d; best = h; }
        }
        if (best != null) FinalHuntRound.Instance.Grant(best, this);
    }

    void OnDestroy()
    {
        if (beamInstance != null) Destroy(beamInstance.mainTexture);
        if (beamInstance != null) Destroy(beamInstance);
    }
}
