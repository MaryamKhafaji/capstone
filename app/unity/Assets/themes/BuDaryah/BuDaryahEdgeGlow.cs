using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("MasAR+/Bu Daryah/Edge Aura")]
public class BuDaryahEdgeAura : MaskableGraphic
{
    [Header("Colors")]
    [SerializeField] private Color auraColor = new Color(0.53f, 0.73f, 0.88f, 0.20f);
    [SerializeField] private Color cornerColor = new Color(0.75f, 0.88f, 1f, 0.34f);

    [Header("Edge softness")]
    [Range(10f, 120f)][SerializeField] private float edgeWidth = 38f;
    [Range(20f, 180f)][SerializeField] private float cornerRadius = 95f;

    [Header("Visibility")]
    [Range(0f, 1f)][SerializeField] private float edgeVisibility = 0.45f;
    [Range(0f, 1f)][SerializeField] private float cornerVisibility = 0.70f;

    [Header("Breathing shimmer")]
    [Range(0f, 0.5f)][SerializeField] private float shimmerAmount = 0.08f;
    [Range(1f, 12f)][SerializeField] private float shimmerSeconds = 5.5f;

    private float nextRedrawTime;
    private const int CornerSegments = 18;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private void Update()
    {
        if (!isActiveAndEnabled) return;
        if (Time.unscaledTime < nextRedrawTime) return;

        nextRedrawTime = Time.unscaledTime + (1f / 30f);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f) return;

        float t = Application.isPlaying
            ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime / Mathf.Max(0.1f, shimmerSeconds) * Mathf.PI * 2f)
            : 0.5f;

        float shimmer = 1f + ((t - 0.5f) * 2f) * shimmerAmount;

        Color tint = color;

        Color edgeCol = Multiply(auraColor, tint, edgeVisibility * shimmer);
        Color cornerCol = Multiply(cornerColor, tint, cornerVisibility * shimmer);

        // Soft edges
        DrawTopEdge(vh, r, edgeWidth, edgeCol);
        DrawBottomEdge(vh, r, edgeWidth, edgeCol);
        DrawLeftEdge(vh, r, edgeWidth, edgeCol);
        DrawRightEdge(vh, r, edgeWidth, edgeCol);

        // Soft corner blooms
        DrawCornerGlow(vh, new Vector2(r.xMin, r.yMin), cornerRadius, 180f, 270f, cornerCol); // BL
        DrawCornerGlow(vh, new Vector2(r.xMax, r.yMin), cornerRadius, 270f, 360f, cornerCol); // BR
        DrawCornerGlow(vh, new Vector2(r.xMax, r.yMax), cornerRadius, 0f, 90f, cornerCol); // TR
        DrawCornerGlow(vh, new Vector2(r.xMin, r.yMax), cornerRadius, 90f, 180f, cornerCol); // TL
    }

    private Color Multiply(Color a, Color b, float extraAlpha)
    {
        return new Color(
            a.r * b.r,
            a.g * b.g,
            a.b * b.b,
            a.a * b.a * Mathf.Clamp01(extraAlpha)
        );
    }

    private void DrawTopEdge(VertexHelper vh, Rect r, float width, Color col)
    {
        AddVerticalFadeQuad(vh, r.xMin, r.xMax, r.yMax - width, r.yMax, false, col);
    }

    private void DrawBottomEdge(VertexHelper vh, Rect r, float width, Color col)
    {
        AddVerticalFadeQuad(vh, r.xMin, r.xMax, r.yMin, r.yMin + width, true, col);
    }

    private void DrawLeftEdge(VertexHelper vh, Rect r, float width, Color col)
    {
        AddHorizontalFadeQuad(vh, r.xMin, r.xMin + width, r.yMin, r.yMax, true, col);
    }

    private void DrawRightEdge(VertexHelper vh, Rect r, float width, Color col)
    {
        AddHorizontalFadeQuad(vh, r.xMax - width, r.xMax, r.yMin, r.yMax, false, col);
    }

    private void AddVerticalFadeQuad(VertexHelper vh, float xMin, float xMax, float yA, float yB, bool fadeUp, Color col)
    {
        Color clear = new Color(col.r, col.g, col.b, 0f);
        int i = vh.currentVertCount;

        if (fadeUp)
        {
            vh.AddVert(new Vector3(xMin, yA), col, Vector2.zero);
            vh.AddVert(new Vector3(xMax, yA), col, Vector2.right);
            vh.AddVert(new Vector3(xMax, yB), clear, Vector2.one);
            vh.AddVert(new Vector3(xMin, yB), clear, Vector2.up);
        }
        else
        {
            vh.AddVert(new Vector3(xMin, yA), clear, Vector2.zero);
            vh.AddVert(new Vector3(xMax, yA), clear, Vector2.right);
            vh.AddVert(new Vector3(xMax, yB), col, Vector2.one);
            vh.AddVert(new Vector3(xMin, yB), col, Vector2.up);
        }

        vh.AddTriangle(i + 0, i + 1, i + 2);
        vh.AddTriangle(i + 0, i + 2, i + 3);
    }

    private void AddHorizontalFadeQuad(VertexHelper vh, float xA, float xB, float yMin, float yMax, bool fadeRight, Color col)
    {
        Color clear = new Color(col.r, col.g, col.b, 0f);
        int i = vh.currentVertCount;

        if (fadeRight)
        {
            vh.AddVert(new Vector3(xA, yMin), col, Vector2.zero);
            vh.AddVert(new Vector3(xB, yMin), clear, Vector2.right);
            vh.AddVert(new Vector3(xB, yMax), clear, Vector2.one);
            vh.AddVert(new Vector3(xA, yMax), col, Vector2.up);
        }
        else
        {
            vh.AddVert(new Vector3(xA, yMin), clear, Vector2.zero);
            vh.AddVert(new Vector3(xB, yMin), col, Vector2.right);
            vh.AddVert(new Vector3(xB, yMax), col, Vector2.one);
            vh.AddVert(new Vector3(xA, yMax), clear, Vector2.up);
        }

        vh.AddTriangle(i + 0, i + 1, i + 2);
        vh.AddTriangle(i + 0, i + 2, i + 3);
    }

    private void DrawCornerGlow(VertexHelper vh, Vector2 center, float radius, float startDeg, float endDeg, Color col)
    {
        Color clear = new Color(col.r, col.g, col.b, 0f);

        int startIndex = vh.currentVertCount;
        vh.AddVert(center, (Color32)col, Vector2.zero);

        for (int s = 0; s <= CornerSegments; s++)
        {
            float t = (float)s / CornerSegments;
            float deg = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
            Vector2 p = center + new Vector2(Mathf.Cos(deg), Mathf.Sin(deg)) * radius;
            vh.AddVert(p, (Color32)clear, Vector2.zero);
        }

        for (int s = 0; s < CornerSegments; s++)
        {
            vh.AddTriangle(startIndex, startIndex + s + 1, startIndex + s + 2);
        }
    }
}