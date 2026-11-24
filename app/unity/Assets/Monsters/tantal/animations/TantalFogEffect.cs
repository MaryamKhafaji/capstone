
using UnityEngine;

public class TantalFogEffect : MonoBehaviour
{
    [Header("Tantal Visibility")]
    public Renderer characterRenderer;
    public Camera playerCamera;

    [Header("Fog Spawn Points")]
    public Transform[] fogPoints;

    [Header("Fog Appearance")]
    public Color fogColor =
        new Color(0.38f, 0.40f, 0.44f, 1f);

    [Range(0f, 1f)]
    public float opacity = 0.55f;

    [Range(10, 100)]
    public int fogDensity = 65;

    [Header("Fog Size")]
    public float minCloudSize = 0.4f;
    public float maxCloudSize = 1.2f;
    public float fogHeight = 0.45f;

    [Header("Outward Spread")]
    public float initialRadius = 0.3f;
    public float maxRadius = 4f;
    public float spreadDuration = 8f;

    [Header("Gentle Movement")]
    public float driftAmount = 0.15f;
    public float driftSpeed = 0.4f;
    public float verticalMovement = 0.04f;

    private class FogCloud
    {
        public GameObject obj;
        public SpriteRenderer renderer;

        public int anchorIndex;
        public float angle;
        public float distance;
        public float height;
        public float size;
        public float phase;
        public float speed;
    }

    private FogCloud[] clouds;
    private Texture2D fogTexture;
    private Sprite fogSprite;

    private float elapsedTime;
    private bool wasVisible;
    private bool initialized;

    void Start()
    {
        if (characterRenderer == null ||
            fogPoints == null ||
            fogPoints.Length == 0)
        {
            Debug.LogError(
                "TantalFogEffect: Assign the character " +
                "renderer and fog points."
            );

            enabled = false;
            return;
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        CreateFogSprite();
        CreateClouds();

        initialized = true;

        // The fog starts hidden.
        SetFogVisible(false);
    }

    void LateUpdate()
    {
        if (!initialized)
            return;

        bool visible =
            characterRenderer != null &&
            characterRenderer.enabled &&
            characterRenderer.gameObject.activeInHierarchy;

        // Hide fog immediately when Tantal disappears.
        if (!visible)
        {
            if (wasVisible)
                SetFogVisible(false);

            wasVisible = false;
            return;
        }

        // Restart spreading when Tantal appears.
        if (!wasVisible)
        {
            elapsedTime = 0f;
            SetFogVisible(true);
        }

        wasVisible = true;
        elapsedTime += Time.deltaTime;

        UpdateFog();
    }

    void CreateFogSprite()
    {
        int size = 128;

        fogTexture = new Texture2D(
            size, size,
            TextureFormat.RGBA32,
            false
        );

        fogTexture.filterMode = FilterMode.Bilinear;
        fogTexture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - size * 0.5f) /
                           (size * 0.5f);

                float ny = (y - size * 0.5f) /
                           (size * 0.5f);

                float distance =
                    Mathf.Sqrt(nx * nx + ny * ny);

                // Soft irregular edges.
                float noise = Mathf.PerlinNoise(
                    x * 0.045f,
                    y * 0.045f
                );

                float edge =
                    distance +
                    (noise - 0.5f) * 0.18f;

                float alpha =
                    Mathf.Clamp01(1f - edge);

                alpha = Mathf.SmoothStep(
                    0f, 1f, alpha
                );

                pixels[y * size + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        fogTexture.SetPixels(pixels);
        fogTexture.Apply();

        fogSprite = Sprite.Create(
            fogTexture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
    }

    void CreateClouds()
    {
        clouds = new FogCloud[fogDensity];

        // Independent random generator.
        System.Random random =
            new System.Random(427);

        for (int i = 0; i < clouds.Length; i++)
        {
            GameObject obj = new GameObject(
                "Tantal_FogCloud_" + i
            );

            obj.transform.SetParent(transform, false);

            SpriteRenderer sr =
                obj.AddComponent<SpriteRenderer>();

            sr.sprite = fogSprite;

            FogCloud cloud = new FogCloud();

            cloud.obj = obj;
            cloud.renderer = sr;

            cloud.anchorIndex =
                random.Next(fogPoints.Length);

            cloud.angle =
                (float)random.NextDouble() *
                Mathf.PI * 2f;

            // More clouds near the center.
            cloud.distance = Mathf.Pow(
                (float)random.NextDouble(),
                1.5f
            );

            cloud.height =
                (float)random.NextDouble();

            cloud.size = Mathf.Lerp(
                minCloudSize,
                maxCloudSize,
                (float)random.NextDouble()
            );

            cloud.phase =
                (float)random.NextDouble() *
                Mathf.PI * 2f;

            cloud.speed = Mathf.Lerp(
                0.6f, 1.4f,
                (float)random.NextDouble()
            );

            clouds[i] = cloud;
        }
    }

    void UpdateFog()
    {
        float progress = Mathf.Clamp01(
            elapsedTime /
            Mathf.Max(0.1f, spreadDuration)
        );

        float spread = Mathf.SmoothStep(
            initialRadius,
            maxRadius,
            progress
        );

        // Gentle fade-in when Tantal appears.
        float appearance = Mathf.SmoothStep(
            0f, 1f,
            Mathf.Clamp01(elapsedTime / 1.5f)
        );

        Camera cam = playerCamera != null
            ? playerCamera
            : Camera.main;

        foreach (FogCloud cloud in clouds)
        {
            if (cloud == null || cloud.obj == null)
                continue;

            Transform anchor =
                fogPoints[cloud.anchorIndex];

            if (anchor == null)
                continue;

            float time = elapsedTime;

            float radius =
                cloud.distance * spread;

            // Fog expands in 3D around each anchor.
            float x =
                Mathf.Cos(cloud.angle) * radius;

            float z =
                Mathf.Sin(cloud.angle) * radius;

            // Independent, subtle drifting.
            x += Mathf.Sin(
                time * driftSpeed *
                cloud.speed + cloud.phase
            ) * driftAmount;

            z += Mathf.Cos(
                time * driftSpeed * 0.7f *
                cloud.speed + cloud.phase
            ) * driftAmount;

            float y =
                cloud.height * fogHeight;

            y += Mathf.Sin(
                time * 0.5f + cloud.phase
            ) * verticalMovement;

            // World-space placement.
            cloud.obj.transform.position =
                anchor.position +
                new Vector3(x, y, z);

            // Face camera, while occupying
            // different actual 3D positions.
            if (cam != null)
            {
                cloud.obj.transform.rotation =
                    cam.transform.rotation;
            }

            // Very gentle expansion/contraction.
            float sizePulse =
                1f + Mathf.Sin(
                    time * 0.45f + cloud.phase
                ) * 0.12f;

            float worldSize =
                cloud.size * sizePulse;

            float spriteWidth =
                Mathf.Max(
                    0.0001f,
                    fogSprite.bounds.size.x
                );

            // Compensate for parent scaling.
            Vector3 parentScale =
                cloud.obj.transform.parent.lossyScale;

            float scale = worldSize / spriteWidth;

            cloud.obj.transform.localScale =
                new Vector3(
                    scale / Mathf.Max(
                        0.0001f,
                        Mathf.Abs(parentScale.x)
                    ),
                    scale / Mathf.Max(
                        0.0001f,
                        Mathf.Abs(parentScale.y)
                    ),
                    scale / Mathf.Max(
                        0.0001f,
                        Mathf.Abs(parentScale.z)
                    )
                );

            // Soft opacity fluctuations.
            float pulse =
                0.75f +
                0.25f * Mathf.Sin(
                    time * 0.6f + cloud.phase
                );

            // Lower density toward the edges.
            float edgeFade = Mathf.Lerp(
                1f, 0.45f,
                cloud.distance
            );

            Color c = fogColor;

            c.a = Mathf.Clamp01(
                opacity *
                Mathf.Max(0.25f, pulse) *
                edgeFade *
                appearance
            );

            cloud.renderer.color = c;
        }
    }

    void SetFogVisible(bool visible)
    {
        if (clouds == null)
            return;

        foreach (FogCloud cloud in clouds)
        {
            if (cloud != null &&
                cloud.renderer != null)
            {
                cloud.renderer.enabled = visible;
            }
        }
    }

    void OnDisable()
    {
        SetFogVisible(false);
        wasVisible = false;
    }

    void OnDestroy()
    {
        if (fogSprite != null)
            Destroy(fogSprite);

        if (fogTexture != null)
            Destroy(fogTexture);
    }
}
