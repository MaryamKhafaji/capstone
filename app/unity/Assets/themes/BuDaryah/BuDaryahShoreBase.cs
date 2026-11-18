using UnityEngine;
using UnityEngine.UI;

// MasAR+ / Bu Daryah: a STILL shoreline with tiny, gently fading reflections.
// Attach to a UI object under BuDaryahTheme. No Image or PNG is required.
// This component draws its own UI geometry. No wave motion or sideways scrolling.
[AddComponentMenu("MasAR+/Bu Daryah/Still Shore Base")]
public class BuDaryahShoreBase : MaskableGraphic
{
    [Header("Shore palette (muted moonlit sand)")]
    [SerializeField] private Color sandColor = new Color32(160, 177, 179, 255);
    [SerializeField] private Color upperSandColor = new Color32(206, 211, 201, 255);
    [SerializeField] private Color glintColor = new Color32(232, 239, 232, 255);

    [Header("Still shoreline")]
    [Range(0f, 1f)][SerializeField] private float shoreVisibility = 0.55f;
    [Range(6f, 90f)][SerializeField] private float shoreHeight = 45f;
    [Range(0f, 12f)][SerializeField] private float edgeCurvature = 4.0f;
    [Range(0f, 1f)][SerializeField] private float centerVisibility = 0.83f;
    [Range(16, 100)][SerializeField] private int horizontalSegments = 64;

    [Header("Sand grain (static)")]
    [Range(0, 70)][SerializeField] private int tinyGrainCount = 30;
    [Range(0f, 1f)][SerializeField] private float grainVisibility = 0.18f;

    [Header("Tiny stationary glints (fade only)")]
    [Range(0, 12)][SerializeField] private int glintCount = 4;
    [Range(0f, 1f)][SerializeField] private float glintVisibility = 0.30f;
    [Range(1f, 7f)][SerializeField] private float glintSize = 2.5f;
    [Range(3f, 14f)][SerializeField] private float glintCycleSeconds = 8f;

    private float nextRefresh;
    private const int Rows = 7;
    private const float TwoPi = Mathf.PI * 2f;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        raycastTarget = false;
        SetVerticesDirty();
    }

#if UNITY_EDITOR
    // UI Graphic.OnValidate is only available in Editor builds of Unity UI.
    // Guard the override so Android player builds compile successfully.
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif

    private void Update()
    {
        // The shore NEVER moves. We only refresh the occasional fading glint.
        if (!Application.isPlaying || !isActiveAndEnabled || glintCount == 0) return;
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.10f; // 10 fps is plenty for tiny shines
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        DrawStillShore(vh, rect);
        DrawSandGrains(vh, rect);
        DrawFadingGlints(vh, rect);
    }

    private float ShorelineAt(float u)
    {
        // Fixed, broad irregularity: an organic shore edge without cartoon peaks.
        float a = Mathf.Sin(TwoPi * (1.12f * u + 0.12f));
        float b = 0.32f * Mathf.Sin(TwoPi * (2.65f * u + 0.39f));
        return shoreHeight + edgeCurvature * (a + b);
    }

    private float EdgeMultiplier(float u)
    {
        float middle = Mathf.Sin(Mathf.PI * u);
        return Mathf.Lerp(1f, centerVisibility, middle * middle);
    }

    private void DrawStillShore(VertexHelper vh, Rect rect)
    {
        int sections = Mathf.Clamp(horizontalSegments, 16, 100);
        int first = vh.currentVertCount;

        for (int x = 0; x <= sections; x++)
        {
            float u = (float)x / sections;
            float localX = Mathf.Lerp(rect.xMin, rect.xMax, u);
            float top = Mathf.Clamp(ShorelineAt(u), 3f, rect.height - 1f);
            float edgeFade = EdgeMultiplier(u);

            for (int row = 0; row < Rows; row++)
            {
                float v = (float)row / (Rows - 1); // 0 bottom; 1 upper shore edge
                float localY = rect.yMin + Mathf.Lerp(0f, top, v);

                // Solid-ish at the base; very soft fade into the scene at its upper edge.
                float feather = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.40f, 1f, v));
                float alpha = shoreVisibility * edgeFade * feather;
                Color sand = Color.Lerp(sandColor, upperSandColor, v * 0.75f);
                AddVertex(vh, localX, localY, sand, alpha, new Vector2(u, v));
            }
        }

        for (int x = 0; x < sections; x++)
        {
            for (int row = 0; row < Rows - 1; row++)
            {
                int a = first + x * Rows + row;
                int b = a + Rows;
                vh.AddTriangle(a, a + 1, b);
                vh.AddTriangle(b, a + 1, b + 1);
            }
        }
    }

    private void DrawSandGrains(VertexHelper vh, Rect rect)
    {
        if (grainVisibility <= 0f) return;
        for (int i = 0; i < tinyGrainCount; i++)
        {
            float u = 0.015f + 0.97f * Hash01(i * 2 + 11);
            float v = 0.20f + 0.54f * Hash01(i * 2 + 12);
            float x = Mathf.Lerp(rect.xMin, rect.xMax, u);
            float top = Mathf.Min(ShorelineAt(u), rect.height - 1f);
            float y = rect.yMin + top * v;
            float size = Mathf.Lerp(0.4f, 1.1f, Hash01(i + 803));
            float strength = shoreVisibility * grainVisibility * EdgeMultiplier(u) * 0.5f;
            AddSoftDiamond(vh, x, y, size, upperSandColor, strength);
        }
    }

    private void DrawFadingGlints(VertexHelper vh, Rect rect)
    {
        if (glintCount <= 0 || glintVisibility <= 0f) return;
        float time = Application.isPlaying ? Time.unscaledTime : 0f;
        float cycle = Mathf.Max(0.1f, glintCycleSeconds);

        for (int i = 0; i < glintCount; i++)
        {
            // The position is deterministic and does NOT move across the shore.
            float u = 0.06f + 0.88f * Hash01(i * 7 + 151);
            float v = 0.45f + 0.28f * Hash01(i * 7 + 152);
            float x = Mathf.Lerp(rect.xMin, rect.xMax, u);
            float top = Mathf.Min(ShorelineAt(u), rect.height - 1f);
            float y = rect.yMin + top * v;

            // Each shine slowly appears, then disappears, at different moments.
            float phase = (time / cycle + (float)i / Mathf.Max(1, glintCount));
            float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(TwoPi * phase)), 3f);
            float strength = glintVisibility * pulse * EdgeMultiplier(u);
            if (strength < 0.003f) continue;
            float size = glintSize * Mathf.Lerp(0.7f, 1.3f, Hash01(i + 330));
            AddSoftDiamond(vh, x, y, size, glintColor, strength);
        }
    }

    private void AddSoftDiamond(VertexHelper vh, float x, float y, float radius, Color tint, float alpha)
    {
        // Center is bright; tips are transparent, so the shine is feathered, not a hard star.
        int p = vh.currentVertCount;
        AddVertex(vh, x, y, tint, alpha, Vector2.zero);
        AddVertex(vh, x - radius, y, tint, 0f, Vector2.zero);
        AddVertex(vh, x, y + radius * 0.75f, tint, 0f, Vector2.zero);
        AddVertex(vh, x + radius, y, tint, 0f, Vector2.zero);
        AddVertex(vh, x, y - radius * 0.75f, tint, 0f, Vector2.zero);
        vh.AddTriangle(p, p + 1, p + 2);
        vh.AddTriangle(p, p + 2, p + 3);
        vh.AddTriangle(p, p + 3, p + 4);
        vh.AddTriangle(p, p + 4, p + 1);
    }

    private void AddVertex(VertexHelper vh, float x, float y, Color tint, float alpha, Vector2 uv)
    {
        Color graphicTint = color; // Allows normal UI Color tint and alpha controls.
        Color final = new Color(tint.r * graphicTint.r,
                                tint.g * graphicTint.g,
                                tint.b * graphicTint.b,
                                Mathf.Clamp01(tint.a * graphicTint.a * alpha));
        vh.AddVert(new Vector3(x, y, 0f), (Color32)final, uv);
    }

    private static float Hash01(int n)
    {
        return Mathf.Repeat(Mathf.Sin(n * 12.9898f + 78.233f) * 43758.5453f, 1f);
    }
}
