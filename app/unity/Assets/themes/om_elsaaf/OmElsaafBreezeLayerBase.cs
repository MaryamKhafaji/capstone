using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Draws Om Elsaaf's soft sandy breeze on the HUD.
/// The breeze moves gently from left to right using curved UI shapes.
/// Its edges fade out so the animation blends into the player's view.
/// The back, middle, and front scripts set the look of each layer.
/// </summary>
public abstract class OmElsaafBreezeLayerBase : MaskableGraphic
{
    [Header("Sandy breeze palette")]
    [SerializeField] protected Color sandColor = new Color(0.79f, 0.68f, 0.51f, 1f);
    [SerializeField] protected Color lightColor = new Color(0.95f, 0.87f, 0.73f, 1f);

    [Header("Appearance (start very subtle)")]
    [Range(0f, 1f)][SerializeField] protected float visibility = 0.18f;
    [Range(8f, 130f)][SerializeField] protected float ribbonThickness = 42f;
    [Range(0f, 500f)][SerializeField] protected float heightAboveBottom = 95f;
    [Range(0f, 35f)][SerializeField] protected float curveAmplitude = 10f;
    [Range(0f, 1f)][SerializeField] protected float centerStrength = 0.65f;
    [Range(0.04f, 0.30f)][SerializeField] protected float edgeFeather = 0.12f;
    [Range(0f, 1f)][SerializeField] protected float paleStreakStrength = 0.27f;

    [Header("Soft gust moving from LEFT to RIGHT")]
    [Range(6f, 35f)][SerializeField] protected float secondsAcrossScreen = 16f;
    [Range(0.08f, 0.60f)][SerializeField] protected float gustWidth = 0.28f;
    [Range(0f, 1.6f)][SerializeField] protected float phaseOffset = 0f;
    [Range(0.3f, 3f)][SerializeField] protected float curveCycles = 1.3f;
    [Range(24, 100)][SerializeField] protected int segments = 60;


    // Several vertical points create a smooth fade through each ribbon.
    private const int VerticalRows = 11;
    private const float TwoPi = 2f * Mathf.PI;
    private float nextRedrawAt;
    private float activationTime;

    /// <summary>
    /// Prevents the breeze from blocking HUD interactions.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    /// <summary>
    /// Starts the breeze movement from the beginning when enabled.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        activationTime = Application.isPlaying ? Time.unscaledTime : 0f;
        nextRedrawAt = 0f;
        SetVerticesDirty();
    }

    /// <summary>
    /// Redraws the moving breeze at about 30 frames per second.
    /// </summary>
    private void Update()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (Time.unscaledTime < nextRedrawAt) return;


        nextRedrawAt = Time.unscaledTime + (1f / 30f);
        SetVerticesDirty();
    }

    /// <summary>
    /// Builds the sandy ribbon with curved movement and soft transparency.
    /// </summary>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 1f || r.height <= 1f) return;

        int columns = Mathf.Clamp(segments, 24, 100);
        float seconds = Mathf.Max(6f, secondsAcrossScreen);
        float elapsed = Application.isPlaying ? Time.unscaledTime - activationTime : 0f;


        // Move the gust across the HUD, starting outside the left edge.
        float gustCenter = Mathf.Repeat(elapsed / seconds + phaseOffset, 1.6f) - 0.30f;
        float animatedPhase = elapsed / seconds * TwoPi;
        float thickness = Mathf.Max(1f, ribbonThickness);
        float width = Mathf.Max(0.08f, gustWidth);
        int firstVertex = vh.currentVertCount;

        for (int ix = 0; ix <= columns; ix++)
        {
            float u = (float)ix / columns;
            float x = Mathf.Lerp(r.xMin, r.xMax, u);


            // Layer gentle curves so the breeze does not look like a straight line.
            float wave = Mathf.Sin(TwoPi * (curveCycles * u - elapsed / seconds) + phaseOffset * TwoPi);
            wave += 0.33f * Mathf.Sin(TwoPi * (u * curveCycles * 2.1f - elapsed / seconds * 0.72f));
            float yMid = r.yMin + heightAboveBottom + curveAmplitude * wave;


            // Make the breeze strongest around the moving gust.
            float z = (u - gustCenter) / width;
            float movingGust = Mathf.Exp(-0.5f * z * z);
            float gustOpacity = 0.035f + 0.965f * movingGust;


            // Fade the breeze out before it reaches the visible side edges.
            float leftEdge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / edgeFeather));
            float rightEdge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - u) / edgeFeather));
            float outerFade = leftEdge * rightEdge;


            // Keep the middle of the view lighter for gameplay.
            float middle = Mathf.Sin(Mathf.PI * u);
            float quietCenter = Mathf.Lerp(1f, centerStrength, middle * middle);

            for (int iy = 0; iy < VerticalRows; iy++)
            {
                float v = (float)iy / (VerticalRows - 1);
                float y = yMid + (v - 0.5f) * thickness;


                // Soften the upper and lower edges of the sandy ribbon.
                float verticalFade = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * v)), 1.5f);
                float streak = Mathf.Exp(-Mathf.Pow((v - 0.70f) / 0.12f, 2f));
                float tintAmount = Mathf.Clamp01(0.23f + v * 0.50f + paleStreakStrength * streak * 0.27f);
                Color baseTone = Color.Lerp(sandColor, lightColor, tintAmount);

                // Combine the movement and fade values into final transparency.
                float alpha = visibility * gustOpacity * verticalFade * outerFade * quietCenter;
                alpha *= 1f + paleStreakStrength * streak * 0.28f;

                Color c = new Color(
                    baseTone.r * color.r,
                    baseTone.g * color.g,
                    baseTone.b * color.b,
                    Mathf.Clamp01(alpha * baseTone.a * color.a));
                vh.AddVert(new Vector3(x, y, 0f), (Color32)c, new Vector2(u, v));
            }
        }

        // Join adjacent points to form the visible UI mesh.
        for (int ix = 0; ix < columns; ix++)
        {
            for (int iy = 0; iy < VerticalRows - 1; iy++)
            {
                int a = firstVertex + ix * VerticalRows + iy;
                int b = a + VerticalRows;
                vh.AddTriangle(a, a + 1, b);
                vh.AddTriangle(b, a + 1, b + 1);
            }
        }
    }
}
