
using UnityEngine;

public class OmElsaafOrnamentGlow : MonoBehaviour
{
    [Header("Ornament Positions")]
    public Transform[] ornaments;

    [Header("Character Visibility")]
    public Renderer characterRenderer;

    [Header("Glow Appearance")]
    public Color glowColor = new Color(1f, 0.75f, 0.35f, 1f);
    public float glowSize = 0.12f;

    [Range(0f, 1f)]
    public float minOpacity = 0.05f;

    [Range(0f, 1f)]
    public float maxOpacity = 0.35f;

    [Header("Pulsing")]
    public float pulseDuration = 4f;

    [Tooltip("Set to 0 for synchronized glowing.")]
    public float phaseOffset = 0.7f;

    private SpriteRenderer[] glowRenderers;
    private Sprite glowSprite;
    private Texture2D glowTexture;

    void Start()
    {
        CreateGlowTexture();
        CreateGlowObjects();
    }

    void CreateGlowTexture()
    {
        int size = 128;

        glowTexture = new Texture2D(
            size, size, TextureFormat.RGBA32, false
        );

        glowTexture.filterMode = FilterMode.Bilinear;
        glowTexture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - size / 2f) / (size / 2f);
                float dy = (y - size / 2f) / (size / 2f);

                float distance = Mathf.Sqrt(
                    dx * dx + dy * dy
                );

                float alpha = Mathf.Pow(
                    Mathf.Clamp01(1f - distance), 2f
                );

                pixels[y * size + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        glowTexture.SetPixels(pixels);
        glowTexture.Apply();

        glowSprite = Sprite.Create(
            glowTexture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
    }

    void CreateGlowObjects()
    {
        if (ornaments == null)
            return;

        glowRenderers = new SpriteRenderer[ornaments.Length];

        for (int i = 0; i < ornaments.Length; i++)
        {
            if (ornaments[i] == null)
                continue;

            GameObject glow = new GameObject(
                "OrnamentGlow_" + (i + 1)
            );

            glow.transform.SetParent(ornaments[i], false);
            glow.transform.localPosition = Vector3.zero;
            glow.transform.localRotation = Quaternion.identity;

            SpriteRenderer sr =
                glow.AddComponent<SpriteRenderer>();

            sr.sprite = glowSprite;
            sr.color = glowColor;
            // Never show glow until character visibility is confirmed.
            sr.enabled = CharacterIsVisible();

            glowRenderers[i] = sr;
        }
    }

    bool CharacterIsVisible()
    {
        // Missing reference = hidden, not accidentally visible.
        return characterRenderer != null &&
               characterRenderer.gameObject.activeInHierarchy &&
               characterRenderer.enabled;
    }

    void OnDisable()
    {
        SetGlowsVisible(false);
    }

    void SetGlowsVisible(bool visible)
    {
        if (glowRenderers == null) return;
        foreach (SpriteRenderer sr in glowRenderers)
        {
            if (sr != null) sr.enabled = visible;
        }
    }

    void Update()
    {
        if (glowRenderers == null)
            return;

        // The glow follows Om Elsaaf's Mesh Renderer visibility.
        bool visible = CharacterIsVisible();
        SetGlowsVisible(visible);
        if (!visible) return;

        Camera cam = Camera.main;

        for (int i = 0; i < glowRenderers.Length; i++)
        {
            SpriteRenderer sr = glowRenderers[i];

            if (sr == null)
                continue;

            Transform glow = sr.transform;

            // Keep glow size consistent in world units.
            float spriteWidth = glowSprite.bounds.size.x;

            float targetScale = glowSize /
                Mathf.Max(0.0001f, spriteWidth);

            Vector3 parentScale = glow.parent.lossyScale;

            glow.localScale = new Vector3(
                targetScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                targetScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                targetScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z))
            );

            // Face the player's camera.
            if (cam != null)
                glow.rotation = cam.transform.rotation;

            float phase = (
                Time.time / Mathf.Max(0.1f, pulseDuration)
                + i * phaseOffset
            ) * Mathf.PI * 2f;

            float pulse = (Mathf.Sin(phase) + 1f) * 0.5f;

            float opacity = Mathf.Lerp(
                minOpacity,
                maxOpacity,
                pulse
            );

            Color color = glowColor;
            color.a = opacity;
            sr.color = color;
        }
    }

    void OnDestroy()
    {
        if (glowSprite != null)
            Destroy(glowSprite);

        if (glowTexture != null)
            Destroy(glowTexture);
    }
}
