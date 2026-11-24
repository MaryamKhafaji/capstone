using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Draws the moving gray-black fog used in Tantal's HUD theme.
/// The fog is built from soft UI shapes, moves slowly across the screen,
/// and fades toward its edges so it blends into the AR view.
/// The back, middle, and front fog layers each provide their own settings.
/// </summary>
public abstract class TantalFogLayerBase : MaskableGraphic
{
    [Header("Shadow fog colors")]
    [SerializeField] protected Color darkFog = new Color(0.11f, 0.12f, 0.14f, 1f);
    [SerializeField] protected Color mistFog = new Color(0.35f, 0.36f, 0.39f, 1f);

    [Header("Soft fog appearance")]
    [Range(0f, 0.6f)][SerializeField] protected float visibility = 0.16f;
    [Range(20f, 220f)][SerializeField] protected float fogThickness = 115f;
    [Range(0f, 350f)][SerializeField] protected float heightAboveBottom = 80f;
    [Range(0f, 80f)][SerializeField] protected float shapeVariation = 20f;
    [Range(0.1f, 1f)][SerializeField] protected float centerStrength = 0.50f;
    [Range(0.05f, 0.40f)][SerializeField] protected float edgeFeather = 0.18f;
    [Range(1, 4)][SerializeField] protected int fogBands = 2;
    [Range(0f, 80f)][SerializeField] protected float spacingBetweenBands = 28f;

    [Header("Make the shadow fog visible")]
    [Tooltip("Multiplies the opacity of the existing fog. Increase this before changing your original Visibility values.")]
    [Range(1f, 5f)][SerializeField] protected float densityMultiplier = 2.8f;
    [Tooltip("Keeps a faint fog between drifting clouds so the effect never completely vanishes.")]
    [Range(0f, 0.8f)][SerializeField] protected float minimumCoverage = 0.38f;
    [Tooltip("Moves the fog upward in HUD units. Helps it show against the brighter sky/horizon instead of blending into the dark ground.")]
    [Range(-80f, 280f)][SerializeField] protected float heightLift = 120f;

    [Header("Slow shadow drift: left to right")]
    [Range(8f, 60f)][SerializeField] protected float secondsAcrossScreen = 28f;
    [Range(0.15f, 0.65f)][SerializeField] protected float cloudWidth = 0.34f;
    [Range(0f, 1f)][SerializeField] protected float phaseOffset = 0.40f;
    [Range(0.4f, 3f)][SerializeField] protected float curveCycles = 1.2f;
    [Range(24, 120)][SerializeField] protected int segments = 64;

    private const float TwoPi = Mathf.PI * 2f;
    // More vertical rows allow a softer fade through the fog.
    private const int VerticalRows = 13;
    private float enabledAt;
    private float nextRedraw;

    /// <summary>
    /// Prepares the UI graphic without blocking HUD interactions.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    /// <summary>
    /// Restarts the fog movement whenever this theme becomes active.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        enabledAt = Time.unscaledTime;
        nextRedraw = 0f;
        raycastTarget = false;
        SetVerticesDirty();
    }

    /// <summary>
    /// Refreshes the fog mesh at about 30 frames per second.
    /// </summary>
    private void Update()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (Time.unscaledTime < nextRedraw) return;

        nextRedraw = Time.unscaledTime + 1f / 30f;
        SetVerticesDirty();
    }

    /// <summary>
    /// Builds the curved fog bands and gives each vertex its color and transparency.
    /// </summary>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        if (rect.width <= 1f || rect.height <= 1f || visibility <= 0f) return;

        // Use unscaled time so the fog can move independently of gameplay speed.
        float elapsed = Application.isPlaying ? Mathf.Max(0f, Time.unscaledTime - enabledAt) : 0f;
        float appearance = Application.isPlaying ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.2f)) : 1f;
        float seconds = Mathf.Max(8f, secondsAcrossScreen);
        float width = Mathf.Max(0.15f, cloudWidth);
        int columns = Mathf.Clamp(segments, 24, 120);
        int bandCount = Mathf.Clamp(fogBands, 1, 4);
        float edge = Mathf.Max(0.05f, edgeFeather);

        for (int band = 0; band < bandCount; band++)
        {


            // Give each fog band a different starting position.
            float bandPhase = Mathf.Repeat(phaseOffset + band * 0.34f, 1f);
            float travel = Mathf.Repeat(elapsed / seconds + bandPhase, 1f);
            float movingCenter = -3.4f * width + travel * (1f + 6.8f * width);
            int start = vh.currentVertCount;

            for (int ix = 0; ix <= columns; ix++)
            {
                float u = ix / (float)columns;
                float x = Mathf.Lerp(rect.xMin, rect.xMax, u);

                // Fade out at both sides of the screen.
                float edgeL = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / edge));
                float edgeR = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - u) / edge));
                float fadeAtSides = edgeL * edgeR;


                // Keep the center clearer for the player.
                float center = Mathf.Sin(Mathf.PI * u);
                float quietCenter = Mathf.Lerp(1f, centerStrength, center * center);

                float z = (u - movingCenter) / width;
                float movingCloud = Mathf.Exp(-0.5f * z * z);


                // A faint base remains visible between the moving darker patches.
                float coverage = Mathf.Lerp(minimumCoverage, 1f, movingCloud);


                // Use noise and slow curves to avoid perfectly straight fog shapes.
                float noise = Mathf.PerlinNoise(u * 5.1f + band * 7.3f - elapsed * 0.017f,
                                                band * 2.9f + elapsed * 0.028f);
                float wobble = Mathf.Sin(TwoPi * (u * curveCycles - elapsed / seconds * 0.58f)
                                         + band * 1.71f);
                float wobble2 = Mathf.Sin(TwoPi * (u * curveCycles * 1.75f - elapsed / seconds * 0.32f)
                                          + band * 2.17f);
                float yCenter = rect.yMin + heightAboveBottom + heightLift + band * spacingBetweenBands
                                + shapeVariation * (wobble * 0.65f + wobble2 * 0.25f);
                float bandThickness = fogThickness * (0.85f + 0.25f * noise);
                float columnDensity = Mathf.Lerp(0.62f, 1.12f, noise);

                for (int iy = 0; iy < VerticalRows; iy++)
                {
                    float v = iy / (float)(VerticalRows - 1);
                    float y = yCenter + (v - 0.5f) * bandThickness;
                    // Make the top and bottom of each fog band transparent.
                    float verticalFeather = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * v)), 1.55f);


                    float lighterAmount = Mathf.Clamp01(0.22f + v * 0.37f + (1f - noise) * 0.16f);
                    Color fogColor = Color.Lerp(darkFog, mistFog, lighterAmount);
                    // Combine density, movement, edge fading, and the current layer color.
                    float alpha = visibility * densityMultiplier * appearance * coverage * fadeAtSides
                                  * verticalFeather * quietCenter * columnDensity;
                    Color vertex = new Color(fogColor.r * color.r,
                                             fogColor.g * color.g,
                                             fogColor.b * color.b,
                                             Mathf.Clamp01(alpha * fogColor.a * color.a));
                    vh.AddVert(new Vector3(x, y, 0f), (Color32)vertex, new Vector2(u, v));
                }
            }

            // Connect the generated vertices into triangles.
            for (int ix = 0; ix < columns; ix++)
            {
                for (int iy = 0; iy < VerticalRows - 1; iy++)
                {
                    int a = start + ix * VerticalRows + iy;
                    int b = a + VerticalRows;
                    vh.AddTriangle(a, a + 1, b);
                    vh.AddTriangle(b, a + 1, b + 1);
                }
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Refreshes the fog when its settings change in the Inspector.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        raycastTarget = false;
        if (isActiveAndEnabled) SetVerticesDirty();
    }
#endif
}
