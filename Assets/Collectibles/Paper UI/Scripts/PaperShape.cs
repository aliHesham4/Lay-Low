using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One flat piece of paper for the UI, drawn as a mesh so it stays crisp at any screen size and can be recoloured:
// a torn polygon (the card / alert / toast outlines from the design boards) or a rounded rectangle, pill or circle.
// Like the CSS in the boards it can add a cream "cut" edge around itself and a hard drop shadow under that.
[RequireComponent(typeof(CanvasRenderer))]
public class PaperShape : MaskableGraphic
{
    public enum Kind { RoundRect, Polygon }

    public Kind kind = Kind.RoundRect;
    [Tooltip("RoundRect: corner radius in UI units. Half the shorter side gives a pill or a circle.")]
    public float radius = 10f;
    [Tooltip("Polygon: the corners as fractions of the rect, CSS style (0,0 = top-left, 1,1 = bottom-right).")]
    public Vector2[] points;

    [Header("Cut edge and shadow")]
    [Tooltip("Width of the cut edge drawn around the shape (0 = none).")]
    public float edge = 0f;
    public Color edgeColor = new Color32(241, 228, 198, 255);
    public bool shadow = false;
    public Vector2 shadowOffset = new Vector2(5f, -8f);
    public Color shadowColor = new Color(6f / 255f, 2f / 255f, 10f / 255f, 0.5f);
    [Tooltip("How far the shadow grows past the shape (CSS spread).")]
    public float shadowSpread = 0f;

    [Header("Fill")]
    [Tooltip("Draw the shape itself (off = only its edge and shadow, used for tape that sits on top of a card).")]
    public bool drawFill = true;
    [Tooltip("Only draw a ring this thick, inside the outline, instead of filling the shape. 0 = filled.")]
    public float ring = 0f;
    [Tooltip("Circles: how much of the disc to fill, clockwise from 12 o'clock (the clock's red slice).")]
    [Range(0f, 1f)] public float pie = 1f;

    const float AA = 0.8f; // soft fringe so the edges don't look jagged

    public void Refresh() { SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        if (r.width <= 0f || r.height <= 0f) return;

        List<Vector2> outline = Outline(r);
        if (outline.Count < 3) return;
        float a = color.a;

        if (shadow)
        {
            List<Vector2> s = Offset(outline, edge + shadowSpread);
            for (int i = 0; i < s.Count; i++) s[i] += shadowOffset;
            Color sc = shadowColor; sc.a *= a;
            Solid(vh, s, Centre(s), sc);
        }
        if (edge > 0f)
        {
            Color ec = edgeColor; ec.a *= a;
            Solid(vh, Offset(outline, edge), r.center, ec);
        }
        if (!drawFill) return;

        if (ring > 0f) Ring(vh, outline, ring, color);
        else if (pie < 0.999f) Pie(vh, r, color);
        else Solid(vh, outline, r.center, color);
    }

    // ---------------- outlines ----------------
    List<Vector2> Outline(Rect r)
    {
        List<Vector2> pts = new List<Vector2>();
        if (kind == Kind.Polygon && points != null && points.Length >= 3)
        {
            foreach (Vector2 p in points) pts.Add(new Vector2(r.xMin + p.x * r.width, r.yMax - p.y * r.height));
            return pts;
        }
        float rad = Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * 0.5f);
        if (rad < 0.01f)
        {
            pts.Add(new Vector2(r.xMin, r.yMax)); pts.Add(new Vector2(r.xMax, r.yMax));
            pts.Add(new Vector2(r.xMax, r.yMin)); pts.Add(new Vector2(r.xMin, r.yMin));
            return pts;
        }
        int seg = Mathf.Clamp(Mathf.CeilToInt(rad * 0.8f), 3, 16);
        // clockwise from the top-right corner
        Corner(pts, new Vector2(r.xMax - rad, r.yMax - rad), rad, 90f, 0f, seg);
        Corner(pts, new Vector2(r.xMax - rad, r.yMin + rad), rad, 0f, -90f, seg);
        Corner(pts, new Vector2(r.xMin + rad, r.yMin + rad), rad, -90f, -180f, seg);
        Corner(pts, new Vector2(r.xMin + rad, r.yMax - rad), rad, 180f, 90f, seg);
        // drop points that sit on top of each other (pills and circles)
        for (int i = pts.Count - 1; i > 0; i--) if ((pts[i] - pts[i - 1]).sqrMagnitude < 0.0001f) pts.RemoveAt(i);
        if (pts.Count > 1 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude < 0.0001f) pts.RemoveAt(pts.Count - 1);
        return pts;
    }

    static void Corner(List<Vector2> pts, Vector2 c, float rad, float from, float to, int seg)
    {
        for (int i = 0; i <= seg; i++)
        {
            float ang = Mathf.Lerp(from, to, i / (float)seg) * Mathf.Deg2Rad;
            pts.Add(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad);
        }
    }

    // Grows (d > 0) or shrinks (d < 0) a closed outline by d, keeping the corners sharp (mitred)
    static List<Vector2> Offset(List<Vector2> pts, float d)
    {
        int n = pts.Count;
        List<Vector2> o = new List<Vector2>(n);
        if (Mathf.Abs(d) < 0.0001f) { o.AddRange(pts); return o; }
        float area = 0f;
        for (int i = 0; i < n; i++) { Vector2 p = pts[i], q = pts[(i + 1) % n]; area += p.x * q.y - q.x * p.y; }
        float side = area > 0f ? 1f : -1f; // makes the normals point outwards whatever the winding
        for (int i = 0; i < n; i++)
        {
            Vector2 prev = pts[(i - 1 + n) % n], cur = pts[i], next = pts[(i + 1) % n];
            Vector2 n1 = Normal(prev, cur, side), n2 = Normal(cur, next, side);
            Vector2 m = (n1 + n2);
            if (m.sqrMagnitude < 0.0001f) m = n1; else m.Normalize();
            float k = Mathf.Max(0.35f, Vector2.Dot(m, n1));
            o.Add(cur + m * (d / k));
        }
        return o;
    }

    static Vector2 Normal(Vector2 a, Vector2 b, float side)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 0.000001f) return Vector2.zero;
        dir.Normalize();
        return new Vector2(dir.y, -dir.x) * side;
    }

    static Vector2 Centre(List<Vector2> pts)
    {
        Vector2 c = Vector2.zero;
        foreach (Vector2 p in pts) c += p;
        return c / Mathf.Max(1, pts.Count);
    }

    // ---------------- mesh ----------------
    static void Vert(VertexHelper vh, Vector2 p, Color c)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = p; v.color = c;
        vh.AddVert(v);
    }

    // a filled outline (fan from 'centre') with a soft fringe all round
    static void Solid(VertexHelper vh, List<Vector2> pts, Vector2 centre, Color c)
    {
        int n = pts.Count;
        List<Vector2> outer = Offset(pts, AA);
        Color clear = c; clear.a = 0f;
        int start = vh.currentVertCount;
        Vert(vh, centre, c);
        for (int i = 0; i < n; i++) Vert(vh, pts[i], c);
        for (int i = 0; i < n; i++) Vert(vh, outer[i], clear);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            int ii = start + 1 + i, ij = start + 1 + j, oi = start + 1 + n + i, oj = start + 1 + n + j;
            vh.AddTriangle(start, ii, ij);
            vh.AddTriangle(ii, oi, oj);
            vh.AddTriangle(ii, oj, ij);
        }
    }

    // only a band of 'width' just inside the outline (stamp borders, highlight rings)
    static void Ring(VertexHelper vh, List<Vector2> pts, float width, Color c)
    {
        int n = pts.Count;
        List<Vector2> inner = Offset(pts, -width);
        List<Vector2> outerAA = Offset(pts, AA);
        List<Vector2> innerAA = Offset(pts, -width - AA);
        Color clear = c; clear.a = 0f;
        int s = vh.currentVertCount;
        for (int i = 0; i < n; i++) Vert(vh, pts[i], c);
        for (int i = 0; i < n; i++) Vert(vh, inner[i], c);
        for (int i = 0; i < n; i++) Vert(vh, outerAA[i], clear);
        for (int i = 0; i < n; i++) Vert(vh, innerAA[i], clear);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Quad(vh, s + i, s + j, s + n + j, s + n + i);                 // the band
            Quad(vh, s + 2 * n + i, s + 2 * n + j, s + j, s + i);         // soft outside
            Quad(vh, s + n + i, s + n + j, s + 3 * n + j, s + 3 * n + i); // soft inside
        }
    }

    static void Quad(VertexHelper vh, int a, int b, int c, int d)
    {
        vh.AddTriangle(a, b, c);
        vh.AddTriangle(a, c, d);
    }

    // part of a disc, clockwise from 12 o'clock
    void Pie(VertexHelper vh, Rect r, Color c)
    {
        if (pie <= 0.001f) return;
        float rad = Mathf.Min(r.width, r.height) * 0.5f;
        int seg = Mathf.Max(2, Mathf.CeilToInt(64 * pie));
        List<Vector2> pts = new List<Vector2>();
        pts.Add(r.center);
        for (int i = 0; i <= seg; i++)
        {
            float ang = (90f - 360f * pie * i / seg) * Mathf.Deg2Rad;
            pts.Add(r.center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad);
        }
        // fan from the centre, soft fringe on the curved side only
        Color clear = c; clear.a = 0f;
        int s = vh.currentVertCount;
        Vert(vh, r.center, c);
        for (int i = 1; i < pts.Count; i++) Vert(vh, pts[i], c);
        for (int i = 1; i < pts.Count; i++) Vert(vh, r.center + (pts[i] - r.center).normalized * (rad + AA), clear);
        int m = pts.Count - 1;
        for (int i = 0; i < m - 1; i++)
        {
            vh.AddTriangle(s, s + 1 + i, s + 2 + i);
            Quad(vh, s + 1 + i, s + 1 + m + i, s + 2 + m + i, s + 2 + i);
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate() { base.OnValidate(); SetVerticesDirty(); }
#endif
}
