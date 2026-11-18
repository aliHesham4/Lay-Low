using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Draws a pulsing yellow outline that follows the exact silhouette of an object (TV, vacuum...)
// while it is active. It is drawn over everything, so it shows through walls and from far away.
public class DistractionOutline : MonoBehaviour
{
    [Tooltip("Every mesh under this transform is outlined. Defaults to this object.")]
    public Transform boundsRoot;
    public Color color = new Color(1f, 0.9f, 0.1f, 1f);
    [Tooltip("Outline thickness in pixels on a 1080p screen.")]
    public float thickness = 4.5f;
    [Range(0f, 1f)] public float fillAlpha = 0.12f;
    public Material maskMaterial;      // Custom/DistractionMask
    public Material outlineMaterial;   // Custom/DistractionOutlineUI
    [Tooltip("Outlines on different layers are drawn separately, each with its own thickness / fill / opacity.")]
    public string layerName = DistractionOutlineSystem.LayerName;
    [Range(0f, 1f)] public float opacity = 1f;
    [Range(0f, 1f)] [Tooltip("Per-object strength on top of the group opacity (used to fade a single outline).")]
    public float fade = 1f;

    [Tooltip("Extra meshes to outline that are not under Bounds Root (e.g. a fridge whose model sits elsewhere).")]
    public List<MeshRenderer> extraRenderers = new List<MeshRenderer>();

    readonly List<GameObject> masks = new List<GameObject>();
    bool visible;

    void Start()
    {
        if (boundsRoot == null) boundsRoot = transform;
    }

    public void SetVisible(bool on)
    {
        if (on == visible) return;
        visible = on;
        if (on)
        {
            BuildMasks();
            DistractionOutlineSystem.Get(this).Add(this);
        }
        else
        {
            ClearMasks();
            DistractionOutlineSystem s = DistractionOutlineSystem.Find(layerName);
            if (s != null) s.Remove(this);
        }
    }

    void BuildMasks()
    {
        ClearMasks();
        if (boundsRoot == null) boundsRoot = transform;
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0 || maskMaterial == null) return;

        List<MeshRenderer> meshes = new List<MeshRenderer>(boundsRoot.GetComponentsInChildren<MeshRenderer>(false));
        foreach (MeshRenderer er in extraRenderers) if (er != null && !meshes.Contains(er)) meshes.Add(er);
        foreach (MeshRenderer r in meshes)
        {
            if (!r.enabled || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
            MeshFilter mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            GameObject g = new GameObject("OutlineMask");
            g.layer = layer;
            g.transform.SetParent(r.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            MeshRenderer mr = g.AddComponent<MeshRenderer>();
            Material[] mats = new Material[mf.sharedMesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = maskMaterial;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Tint(mr);
            masks.Add(g);
        }

        // animated (skinned) meshes, e.g. the alarm clock: a skinned copy that shares the same bones
        foreach (SkinnedMeshRenderer r in boundsRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (!r.enabled || r.sharedMesh == null || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;

            GameObject g = new GameObject("OutlineMask");
            g.layer = layer;
            g.transform.SetParent(r.transform, false);
            SkinnedMeshRenderer sr = g.AddComponent<SkinnedMeshRenderer>();
            sr.sharedMesh = r.sharedMesh;
            sr.bones = r.bones;
            sr.rootBone = r.rootBone;
            sr.localBounds = r.localBounds;
            sr.updateWhenOffscreen = true;
            Material[] mats = new Material[r.sharedMesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = maskMaterial;
            sr.sharedMaterials = mats;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            Tint(sr);
            masks.Add(g);
        }
    }

    // each object's mask carries its own outline colour, so several colours can show at once
    void Tint(Renderer r)
    {
        var block = new MaterialPropertyBlock();
        Color c = color; c.a = fade;
        block.SetColor("_Color", c);
        r.SetPropertyBlock(block);
    }

    // fade just this outline (0 = gone, 1 = full) while it is showing
    public void SetFade(float f)
    {
        f = Mathf.Clamp01(f);
        if (Mathf.Abs(f - fade) < 0.01f) return;
        fade = f;
        foreach (GameObject g in masks) if (g != null) { Renderer r = g.GetComponent<Renderer>(); if (r != null) Tint(r); }
    }

    // change the colour while it is showing
    public void SetColor(Color c)
    {
        color = c;
        foreach (GameObject g in masks) if (g != null) { Renderer r = g.GetComponent<Renderer>(); if (r != null) Tint(r); }
    }

    void ClearMasks()
    {
        foreach (GameObject g in masks) if (g != null) Destroy(g);
        masks.Clear();
    }

    void OnDestroy()
    {
        ClearMasks();
        DistractionOutlineSystem s = DistractionOutlineSystem.Find(layerName);
        if (s != null) s.Remove(this);
    }
}

// One shared camera renders only the mask copies into a texture; a full-screen image turns that
// texture into the yellow outline.
public class DistractionOutlineSystem : MonoBehaviour
{
    public const string LayerName = "DistractionOutline";
    static readonly Dictionary<string, DistractionOutlineSystem> systems = new Dictionary<string, DistractionOutlineSystem>();
    public static DistractionOutlineSystem Instance { get { return Find(LayerName); } }
    public static DistractionOutlineSystem Find(string layerName)
    {
        DistractionOutlineSystem s;
        return layerName != null && systems.TryGetValue(layerName, out s) && s != null ? s : null;
    }
    string groupLayer;

    readonly List<DistractionOutline> targets = new List<DistractionOutline>();
    Camera cam;
    RenderTexture rt;
    RawImage image;
    Material uiMat;
    int layer;
    Transform parentCam;

    public static DistractionOutlineSystem Get(DistractionOutline from)
    {
        DistractionOutlineSystem s = Find(from.layerName);
        if (s != null) return s;
        GameObject go = new GameObject("DistractionOutlineSystem (" + from.layerName + ")");
        s = go.AddComponent<DistractionOutlineSystem>();
        systems[from.layerName] = s;
        s.Init(from);
        return s;
    }

    void Init(DistractionOutline from)
    {
        groupLayer = from.layerName;
        layer = LayerMask.NameToLayer(from.layerName);

        GameObject cg = new GameObject("OutlineMaskCamera");
        cg.transform.SetParent(transform, false);
        cam = cg.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << layer;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.depth = -50;
        cam.enabled = false;

        HeartbeatMonitor hm = FindFirstObjectByType<HeartbeatMonitor>();
        Canvas canvas = (hm != null && hm.heartIcon != null) ? hm.heartIcon.canvas.rootCanvas : FindFirstObjectByType<Canvas>();
        GameObject ig = new GameObject("DistractionOutlineImage", typeof(RectTransform), typeof(RawImage));
        ig.transform.SetParent(canvas.transform, false);
        ig.transform.SetAsFirstSibling(); // behind the HUD cards
        RectTransform r = ig.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        image = ig.GetComponent<RawImage>();
        image.raycastTarget = false;
        uiMat = new Material(from.outlineMaterial);
        image.material = uiMat;
        image.enabled = false;
    }

    public void Add(DistractionOutline o) { if (!targets.Contains(o)) targets.Add(o); }
    public void Remove(DistractionOutline o) { targets.Remove(o); }

    void LateUpdate()
    {
        bool any = targets.Count > 0;
        if (cam == null || image == null) return;
        if (!any)
        {
            if (cam.enabled) { cam.enabled = false; image.enabled = false; }
            return;
        }

        Camera main = Camera.main;
        if (main == null) return;

        // keep the real cameras from drawing the mask copies
        int bit = 1 << layer;
        foreach (Camera c in Camera.allCameras)
            if (c != cam && (c.cullingMask & bit) != 0) c.cullingMask &= ~bit;

        // follow the player's view exactly
        if (parentCam != main.transform)
        {
            parentCam = main.transform;
            cam.transform.SetParent(parentCam, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.transform.localScale = Vector3.one;
        }
        cam.fieldOfView = main.fieldOfView;
        cam.nearClipPlane = main.nearClipPlane;
        cam.farClipPlane = main.farClipPlane;
        cam.orthographic = main.orthographic;
        cam.orthographicSize = main.orthographicSize;

        if (rt == null || rt.width != Screen.width || rt.height != Screen.height)
        {
            if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(Mathf.Max(16, Screen.width), Mathf.Max(16, Screen.height), 16, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;
            cam.targetTexture = rt;
            image.texture = rt;
        }

        DistractionOutline first = targets[0];
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
        Color col = first.color; col.a = first.opacity;
        uiMat.SetColor("_Color", col);
        uiMat.SetFloat("_Width", first.thickness * (Screen.height / 1080f) * pulse + 1f);
        uiMat.SetFloat("_Fill", first.fillAlpha * pulse);

        cam.enabled = true;
        image.enabled = true;
    }

    void OnDestroy()
    {
        if (rt != null) { rt.Release(); Destroy(rt); }
        if (groupLayer != null && Find(groupLayer) == this) systems.Remove(groupLayer);
    }
}
