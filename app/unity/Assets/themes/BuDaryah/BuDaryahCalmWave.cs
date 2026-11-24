using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Draws a soft water effect that approaches and retreats at the bottom of the HUD.
/// The water is built from UI vertices with pale colors and fading edges,
/// so it does not need a separate wave image or texture.
/// </summary>
[AddComponentMenu("MasAR+/HUD/Bu Daryah Calm Wave")]
public class BuDaryahCalmWave : MaskableGraphic
{
    [Header("Disshdasha-inspired pale sea colors")]
    [SerializeField] private Color mainWaveColor = new Color32(133, 172, 187, 255);
    [SerializeField] private Color lightWaveColor = new Color32(199, 218, 223, 255);

    [Header("Appearance (quiet, shallow water)")]
    [SerializeField, Range(0f, 1f)] private float visibility = 0.50f;
    [SerializeField, Range(2f, 16f)] private float waveHeight = 6f;
    [SerializeField, Range(5f, 40f)] private float shoreDepth = 23f;
    [SerializeField, Range(0.5f, 4f)] private float crestThickness = 1.2f;
    [SerializeField, Range(0f, 1f)] private float centerVisibility = 0.65f;

    [Header("Soft horizontal screen edges")]
    [Range(0f, 0.12f)][SerializeField] private float clearEdgeFraction = 0.05f;
    [Range(0.02f, 0.35f)][SerializeField] private float edgeFadeFraction = 0.14f;

    [Header("Movement: toward player then away (not sideways)")]
    [Tooltip("Seconds for a complete approach-and-retreat cycle")]
    [SerializeField, Range(2f, 12f)] private float cycleSeconds = 5.5f;
    [SerializeField, Range(2f, 28f)] private float advanceDistance = 18f;
    [SerializeField, Range(0f, 2f)] private float surfaceDrift = 0.6f;

    [Header("Shape")]
    [SerializeField, Range(0.5f, 4f)] private float waveCycles = 1.8f;
    [SerializeField, Range(24, 180)] private int segments = 100;

    private float elapsed;
    private float redrawElapsed;

    /// <summary>
    /// Stops the wave graphic from blocking HUD interactions.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    /// <summary>
    /// Advances the wave cycle and refreshes its shape at about 30 frames per second.
    /// </summary>
    private void Update()
    {
        if (!Application.isPlaying) return;
        elapsed += Time.unscaledDeltaTime;
        redrawElapsed += Time.unscaledDeltaTime;
        if (redrawElapsed >= 1f / 30f)
        {
            redrawElapsed = 0f;
            SetVerticesDirty();
        }
    }

    /// <summary>
    /// Builds the two water layers for the current approach and retreat.
    /// </summary>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 0 || rect.height <= 0 || visibility <= 0) return;


        // Turn elapsed time into one repeating approach-and-retreat cycle.
        float phase = elapsed * Mathf.PI * 2f / Mathf.Max(0.2f, cycleSeconds);
        float approach = (1f - Mathf.Cos(phase)) * 0.5f;
        approach = approach * approach * (3f - 2f * approach);


        // Draw the lighter water behind the main wave.
        DrawWater(vh, rect, approach, 1.8f, 0.60f, 0.60f, lightWaveColor);
        DrawWater(vh, rect, approach, 0.1f, 1.00f, 1.00f, mainWaveColor);
    }

    /// <summary>
    /// Draws one layer of water with a soft body and a thin surface highlight.
    /// </summary>
    private void DrawWater(VertexHelper vh, Rect r, float approach,
        float phaseOffset, float heightScale, float opacityScale, Color tint)
    {
        int count = Mathf.Max(24, segments);
        float[] crest = new float[count + 1];
        float[] strength = new float[count + 1];
        float maxHeight = r.height - 3f;

        for (int i = 0; i <= count; i++)
        {
            float u = i / (float)count;

            // Mix a few gentle curves to make the surface look natural.
            float smallRipples =
                Mathf.Sin(u * waveCycles * Mathf.PI * 2f + phaseOffset) * waveHeight * 0.60f +
                Mathf.Sin(u * waveCycles * Mathf.PI * 4f + 0.8f + phaseOffset) * waveHeight * 0.20f +
                Mathf.Sin(u * Mathf.PI * 0.9f - 0.4f) * waveHeight * 0.33f;

            float shimmer = Mathf.Sin(u * 17f + elapsed * 0.40f + phaseOffset) * surfaceDrift;
            float y = shoreDepth * heightScale + advanceDistance * approach * heightScale
                      + smallRipples * heightScale + shimmer;
            crest[i] = Mathf.Clamp(y, 1.5f, maxHeight);
            float edgeFactor = Mathf.Pow(Mathf.Abs(2f * u - 1f), 1.5f);
            // Fade the water out at both horizontal edges.
            float distanceFromSide = Mathf.Min(u, 1f - u);
            float horizontalFade = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((distanceFromSide - clearEdgeFraction) /
                              Mathf.Max(0.001f, edgeFadeFraction)));
            strength[i] = Mathf.Lerp(centerVisibility, 1f, edgeFactor) *
                          visibility * opacityScale * color.a * horizontalFade;
        }


        // Use a few vertical points for the soft fill and thin crest.
        int first = vh.currentVertCount;
        for (int i = 0; i <= count; i++)
        {
            float u = i / (float)count;
            float x = Mathf.Lerp(r.xMin, r.xMax, u);
            float y = r.yMin + crest[i];
            float a = strength[i];

            AddVert(vh, x, r.yMin, tint, a * 0.045f);
            AddVert(vh, x, Mathf.Lerp(r.yMin, y, 0.70f), tint, a * 0.14f);
            AddVert(vh, x, y - crestThickness, tint, a * 0.33f);
            AddVert(vh, x, y, lightWaveColor, a * 0.47f);
            if (i == 0) continue;
            int p = first + (i - 1) * 4;
            int c = first + i * 4;
            // Connect consecutive samples into a continuous water surface.
            for (int j = 0; j < 3; j++)
            {
                vh.AddTriangle(p + j, p + j + 1, c + j + 1);
                vh.AddTriangle(p + j, c + j + 1, c + j);
            }
        }
    }

    /// <summary>
    /// Adds a UI vertex with the requested position, color, and transparency.
    /// </summary>
    private static void AddVert(VertexHelper vh, float x, float y, Color baseColor, float alpha)
    {
        UIVertex v = UIVertex.simpleVert;
        baseColor.a *= Mathf.Clamp01(alpha);
        v.color = baseColor;
        v.position = new Vector3(x, y, 0f);
        v.uv0 = Vector2.zero;
        vh.AddVert(v);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Updates the preview when wave settings change in the Inspector.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        if (isActiveAndEnabled) SetVerticesDirty();
    }
#endif
}
