using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Draws the moving water and foam used in Bu Daryah's HUD theme.
/// Each wave is shaped from UI vertices and fades toward the screen edges.
/// The wave layers use the tide controller to approach and retreat together.
/// </summary>
public abstract class BuDaryahWaveLayerBase : MaskableGraphic
{
    [Header("Dishdasha-inspired sea colors")]
    [SerializeField] protected Color seaColor = new Color32(133, 172, 187, 255);
    [SerializeField] protected Color reflectionColor = new Color32(218, 232, 235, 255);

    [Header("Water appearance")]
    [Range(0f, 1f)][SerializeField] protected float visibility = 0.4f;
    [Range(20f, 250f)][SerializeField] protected float waterHeight = 125f;
    [Range(5f, 110f)][SerializeField] protected float frontRestingHeight = 65f;
    [Range(0f, 30f)][SerializeField] protected float curvedSurface = 7f;
    [Range(0f, 1f)][SerializeField] protected float crestVisibility = 0.25f;
    [Range(1f, 12f)][SerializeField] protected float crestWidth = 3f;
    [Range(0.2f, 1f)][SerializeField] protected float centerStrength = 0.75f;

    [Header("White foam grows near the shore")]
    [Tooltip("Extra width added to the white foam at the peak of an incoming wave. Set 0 to disable on a layer.")]
    [Range(0f, 35f)][SerializeField] protected float foamExtraWidthAtShore = 0f;
    [Tooltip("How much brighter the white foam gets as the wave reaches the shore.")]
    [Range(0f, 2f)][SerializeField] protected float foamOpacityBoostAtShore = 0.6f;
    [Tooltip("At what point in the incoming tide (0-1) the foam starts to spread.")]
    [Range(0f, 0.95f)][SerializeField] protected float foamStartsAtTide = 0.55f;

    [Header("Incoming tide: toward the bottom shore, then away")]
    [Range(2f, 14f)][SerializeField] protected float secondsPerCycle = 6f;
    [Range(0f, 70f)][SerializeField] protected float approachDistance = 38f;
    [Range(-1f, 1f)][SerializeField] protected float phaseOffset = 0f;

    [Header("Organic outline")]
    [Range(0.5f, 5f)][SerializeField] protected float waveCycles = 1.5f;
    [Range(24, 128)][SerializeField] protected int segments = 80;

    [Header("Soft horizontal screen edges")]
    [Tooltip("Keep the extreme left/right fully transparent; the extra inset also hides the wave RectTransform overhang outside ThemeCanvas.")]
    [Range(0f, 0.12f)][SerializeField] protected float clearEdgeFraction = 0.05f;
    [Tooltip("How gradually the waves appear after the transparent edge (fraction of layer width, each side). Increase for a softer blend.")]
    [Range(0.02f, 0.35f)][SerializeField] protected float edgeFadeFraction = 0.14f;

    [Header("Optional shared controller (found on parent automatically)")]
    [SerializeField] private BuDaryahTideController tideController;

    private const float TwoPi = Mathf.PI * 2f;
    private const int BodyRows = 13;
    private const int CrestRows = 9;
    private float nextRedrawTime;

    /// <summary>
    /// Prepares the wave graphic and finds the shared tide controller.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        FindController();
    }

    /// <summary>
    /// Reconnects the tide controller and redraws the wave when enabled.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        FindController();
        SetVerticesDirty();
    }

#if UNITY_EDITOR

    /// <summary>
    /// Refreshes the wave preview when its Inspector values change.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        raycastTarget = false;
        SetVerticesDirty();
    }
#endif

    /// <summary>
    /// Forces the wave graphic to update after its settings are changed.
    /// </summary>
    protected void RefreshWave()
    {
        raycastTarget = false;
        SetVerticesDirty();
    }

    /// <summary>
    /// Looks for the tide controller on the parent theme object.
    /// </summary>
    private void FindController()
    {
        if (tideController == null)
            tideController = GetComponentInParent<BuDaryahTideController>();
    }

    /// <summary>
    /// Refreshes the wave shape at about 30 frames per second.
    /// </summary>
    private void Update()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (Time.unscaledTime < nextRedrawTime) return;
        nextRedrawTime = Time.unscaledTime + 1f / 30f;
        SetVerticesDirty();
    }

    /// <summary>
    /// Calculates the water position for this frame and draws the body and foam.
    /// </summary>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f) return;

        int columns = Mathf.Clamp(segments, 24, 128);
        float time = Application.isPlaying ? Time.time : 0f;
        // Calculate this layer's own wave movement if no tide controller is active.
        float angle = time * TwoPi / Mathf.Max(0.2f, secondsPerCycle) + phaseOffset;
        float tide = 0.5f - 0.5f * Mathf.Cos(angle);
        float reach = 1f;

        // Use the shared tide timing when it is available.
        if (Application.isPlaying && tideController != null && tideController.isActiveAndEnabled)
        {
            float delay = Mathf.Clamp(0.14f - phaseOffset * 0.30f, 0.02f, 0.32f);
            tide = tideController.GetAdvance01(delay);
            reach = tideController.ReachMultiplier;
        }


        // Move the water front down toward the shore as the tide advances.
        float frontY = Mathf.Clamp(
            frontRestingHeight - approachDistance * tide * reach,
            3f, r.height - 5f);

        float depthScale = Mathf.Clamp(1f + (reach - 1f) * 0.12f * tide, 0.85f, 1.18f);


        // Increase foam near the peak of an incoming wave.
        float foamProgress = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(foamStartsAtTide, 1f, Mathf.Clamp01(tide)));
        float foamExtraWidth = foamExtraWidthAtShore * foamProgress * Mathf.Clamp(reach, 0.65f, 1.4f);
        float foamBrightness = 1f + foamOpacityBoostAtShore * foamProgress;

        // Draw the water body and its pale crest separately.
        DrawLayer(vh, r, columns, BodyRows, frontY, waterHeight * depthScale, angle, false, 0f, 1f);
        if (crestVisibility > 0.001f)
            DrawLayer(vh, r, columns, CrestRows, frontY, waterHeight, angle, true,
                foamExtraWidth, foamBrightness);
    }

    /// <summary>
    /// Draws a curved water layer using color and alpha at each vertex.
    /// </summary>
    private void DrawLayer(VertexHelper vh, Rect r, int columns, int rows,
        float frontY, float depth, float angle, bool crest, float extraFoamWidth, float foamBrightness)
    {
        int start = vh.currentVertCount;
        Color tint = color;

        for (int ix = 0; ix <= columns; ix++)
        {
            float u = ix / (float)columns;
            float x = Mathf.Lerp(r.xMin, r.xMax, u);

            // Blend two gentle curves for an uneven wave edge.
            float shape = Mathf.Sin(TwoPi * (u * waveCycles + 0.11f));
            shape += 0.31f * Mathf.Sin(TwoPi * (u * waveCycles * 2.1f + 0.32f));
            float yFront = Mathf.Clamp(r.yMin + frontY + curvedSurface * shape,
                r.yMin + 2f, r.yMax - 2f);

            float center = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2f);
            float edgeStrength = Mathf.Lerp(1f, centerStrength, center);


            // Fade the waves before their left and right boundaries.
            float distanceFromSide = Mathf.Min(u, 1f - u);
            float horizontalFade = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((distanceFromSide - clearEdgeFraction) /
                              Mathf.Max(0.001f, edgeFadeFraction)));
            float shimmer = 0.96f + 0.04f * Mathf.Sin(TwoPi * u * 1.65f + angle * 0.38f);

            for (int iy = 0; iy < rows; iy++)
            {
                float v = iy / (float)(rows - 1);
                float y;
                float alpha;
                Color tone;

                // Draw the water body with transparency fading upward.
                if (!crest)
                {

                    y = Mathf.Min(r.yMax, yFront + depth * v);
                    float fadeUpward = 1f - Mathf.SmoothStep(0f, 1f, v);
                    alpha = visibility * fadeUpward * edgeStrength * shimmer;
                    tone = Color.Lerp(seaColor, reflectionColor, v * 0.20f);
                }
                else
                {


                    // Spread the foam around the water front when the tide reaches shore.
                    float shoreward = crestWidth + extraFoamWidth * 0.28f;
                    float seaward = crestWidth + extraFoamWidth * 0.72f;
                    y = Mathf.Clamp(yFront + Mathf.Lerp(-shoreward, seaward, v), r.yMin, r.yMax);
                    float feather = Mathf.Pow(Mathf.Sin(Mathf.PI * v), 1.5f);
                    alpha = visibility * crestVisibility * foamBrightness * feather * edgeStrength * shimmer;
                    tone = reflectionColor;
                }

                Color vertexColor = new Color(
                    tone.r * tint.r, tone.g * tint.g, tone.b * tint.b,
                    Mathf.Clamp01(alpha * horizontalFade * tone.a * tint.a));
                vh.AddVert(new Vector3(x, y, 0f), (Color32)vertexColor, new Vector2(u, v));
            }
        }

        // Connect adjacent vertices to create the water surface.
        for (int ix = 0; ix < columns; ix++)
        {
            for (int iy = 0; iy < rows - 1; iy++)
            {
                int a = start + ix * rows + iy;
                int b = a + rows;
                vh.AddTriangle(a, a + 1, b);
                vh.AddTriangle(b, a + 1, b + 1);
            }
        }
    }
}
