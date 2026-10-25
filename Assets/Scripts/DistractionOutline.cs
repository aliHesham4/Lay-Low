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
            if (DistractionOutlineSystem.Instance != null) DistractionOutlineSystem.Instance.Remove(this);
        }
    }

    void BuildMasks()
    {
        ClearMasks();
        if (boundsRoot == null) boundsRoot = transform;
        int layer = LayerMask.NameToLayer(DistractionOutlineSystem.LayerName);
        if (layer < 0 || maskMaterial == null) return;

        foreach (MeshRenderer r in boundsRoot.GetComponentsInChildren<MeshRenderer>(false))
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
            masks.Add(g);
        }
    }

    void ClearMasks()
    {
        foreach (GameObject g in masks) if (g != null) Destroy(g);
        masks.Clear();
    }

    void OnDestroy()
    {
        ClearMasks();
        if (DistractionOutlineSystem.Instance != null) DistractionOutlineSystem.Instance.Remove(this);
    }
}

// One shared camera renders only the mask copies into a texture; a full-screen image turns that
// texture into the yellow outline.
public class DistractionOutlineSystem : MonoBehaviour
{
    public const string LayerName = "DistractionOutline";
    public static DistractionOutlineSystem Instance;

    readonly List<DistractionOutline> targets = new List<DistractionOutline>();
    Camera cam;
    RenderTexture rt;
    RawImage image;
    Material uiMat;
    int layer;
    Transform parentCam;

    public static DistractionOutlineSystem Get(DistractionOutline from)
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("DistractionOutlineSystem");
        Instance = go.AddComponent<DistractionOutlineSystem>();
        Instance.Init(from);
        return Instance;
    }

    void Init(DistractionOutline from)
    {
        layer = LayerMask.NameToLayer(LayerName);

        GameObject cg = new GameObject("OutlineMaskCamera");
        cg.transform.SetParent(transform, false);
        cam = cg.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
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
        uiMat.SetColor("_Color", first.color);
        uiMat.SetFloat("_Width", first.thickness * (Screen.height / 1080f) * pulse + 1f);
        uiMat.SetFloat("_Fill", first.fillAlpha * pulse);

        cam.enabled = true;
        image.enabled = true;
    }

    void OnDestroy()
    {
        if (rt != null) { rt.Release(); Destroy(rt); }
        if (Instance == this) Instance = null;
    }
}
