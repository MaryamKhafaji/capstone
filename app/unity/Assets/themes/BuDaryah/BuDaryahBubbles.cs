using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Creates the small floating bubbles used in Bu Daryah's HUD theme.
/// Bubbles rise slowly from the lower corners and fade in and out.
/// The script reuses UI images and limits how many can appear at once.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BuDaryahBubbles : MonoBehaviour
{
    [Header("Asset (set Texture Type to Sprite 2D and UI)")]
    [SerializeField] private Sprite bubbleSprite;

    [Header("Keep it minimal")]
    [Range(1, 10)][SerializeField] private int maximumVisible = 4;
    [Min(0.2f)][SerializeField] private float spawnIntervalMin = 2.2f;
    [Min(0.2f)][SerializeField] private float spawnIntervalMax = 4.2f;
    [Range(0.01f, 1f)][SerializeField] private float bubbleOpacity = 0.46f;
    [SerializeField] private Color bubbleTint = new Color(0.82f, 0.94f, 0.98f, 1f);

    [Header("Bubble sizes in UI units (1280 x 720 HUD)")]
    [Min(2f)][SerializeField] private float sizeMin = 12f;
    [Min(2f)][SerializeField] private float sizeMax = 25f;

    [Header("Float slowly upward")]
    [Min(0.2f)][SerializeField] private float lifetimeMin = 7f;
    [Min(0.2f)][SerializeField] private float lifetimeMax = 11f;
    [Min(1f)][SerializeField] private float riseDistanceMin = 85f;
    [Min(1f)][SerializeField] private float riseDistanceMax = 155f;
    [Range(0f, 20f)][SerializeField] private float sideDrift = 7f;

    [Header("Placement: bottom left & right only")]
    [Range(0.02f, 0.4f)][SerializeField] private float sideAreaFraction = 0.13f;
    [Min(0f)][SerializeField] private float edgePadding = 12f;
    [Min(0f)][SerializeField] private float spawnAboveBottomMin = 17f;
    [Min(0f)][SerializeField] private float spawnAboveBottomMax = 35f;

    // Stores the movement and lifetime of one bubble.
    private class Bubble
    {
        public RectTransform rect;
        public Image image;
        public bool active;
        public float age;
        public float life;
        public float rise;
        public float drift;
        public float phase;
        public Vector2 origin;
    }

    // Reuse bubble images to avoid creating objects during every update.
    private readonly List<Bubble> pool = new List<Bubble>();
    private RectTransform area;
    private float untilNextSpawn;
    private bool warnedMissingSprite;

    /// <summary>
    /// Finds the UI area and prepares reusable bubble images.
    /// </summary>
    private void Awake()
    {
        area = (RectTransform)transform;

        EnsurePool();
    }

    /// <summary>
    /// Schedules the first bubble when the theme becomes active.
    /// </summary>
    private void OnEnable()
    {
        untilNextSpawn = 0.5f;
    }

    /// <summary>
    /// Hides any active bubbles when the theme is disabled.
    /// </summary>
    private void OnDisable()
    {
        foreach (Bubble b in pool)
        {
            if (b.image != null) b.image.enabled = false;
            b.active = false;
        }
    }

    /// <summary>
    /// Spawns bubbles and moves the visible ones upward with a soft fade.
    /// </summary>
    private void Update()
    {
        if (!Application.isPlaying) return;
        if (area == null || area.rect.width <= 1f || area.rect.height <= 1f) return;
        // Skip spawning if the bubble image has not been assigned.
        if (bubbleSprite == null)
        {
            if (!warnedMissingSprite)
            {
                Debug.LogWarning("BuDaryahBubbles: Assign Bubble_DisdashaSoft sprite in the Inspector.", this);
                warnedMissingSprite = true;
            }
            return;
        }

        EnsurePool();
        float dt = Time.deltaTime;
        untilNextSpawn -= dt;
        if (untilNextSpawn <= 0f)
        {
            SpawnOne();
            untilNextSpawn = Random.Range(Mathf.Max(0.2f, spawnIntervalMin),
                                          Mathf.Max(spawnIntervalMin, spawnIntervalMax));
        }

        for (int i = 0; i < pool.Count; i++)
        {
            Bubble b = pool[i];
            if (!b.active) continue;
            b.age += dt;
            float t = Mathf.Clamp01(b.age / b.life);
            if (t >= 1f)
            {
                b.active = false;
                b.image.enabled = false;
                continue;
            }


            // Move upward with only a small sideways sway.
            float x = b.origin.x + Mathf.Sin(t * Mathf.PI * 1.3f + b.phase) * b.drift * t;
            float y = b.origin.y + b.rise * t;
            b.rect.anchoredPosition = new Vector2(x, y);


            // Use a soft fade at the start and end of each bubble's lifetime.
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.18f));
            float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / 0.32f));
            float a = bubbleOpacity * Mathf.Min(fadeIn, fadeOut) * bubbleTint.a;
            Color c = bubbleTint;
            c.a = a;
            b.image.color = c;
        }
    }

    /// <summary>
    /// Keeps a reusable set of bubble images ready for spawning.
    /// </summary>
    private void EnsurePool()
    {
        if (area == null) area = GetComponent<RectTransform>();
        // Create enough bubble images for the chosen visibility limit.
        int wanted = Mathf.Max(1, maximumVisible);
        while (pool.Count < wanted)
        {
            int id = pool.Count + 1;
            GameObject go = new GameObject("BuDaryah_Bubble_" + id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.maskable = false;
            img.sprite = bubbleSprite;
            img.preserveAspect = true;
            img.enabled = false;
            pool.Add(new Bubble { rect = rt, image = img });
        }
        for (int i = wanted; i < pool.Count; i++)
        {
            pool[i].active = false;
            if (pool[i].image != null) pool[i].image.enabled = false;
        }
    }

    /// <summary>
    /// Places one bubble near a lower corner with random size and movement.
    /// </summary>
    private void SpawnOne()
    {
        int count = Mathf.Min(maximumVisible, pool.Count);
        // Find a free bubble image to reuse.
        Bubble chosen = null;
        for (int i = 0; i < count; i++)
        {
            if (!pool[i].active) { chosen = pool[i]; break; }
        }
        if (chosen == null) return;

        float w = area.rect.width;
        float sideWidth = Mathf.Max(1f, w * sideAreaFraction - edgePadding);
        // Spawn bubbles near the lower-left or lower-right corner.
        bool left = Random.value < 0.5f;
        float x = left
            ? edgePadding + Random.Range(0f, sideWidth)
            : w - edgePadding - Random.Range(0f, sideWidth);
        float y = Random.Range(spawnAboveBottomMin,
                               Mathf.Max(spawnAboveBottomMin, spawnAboveBottomMax));

        float size = Random.Range(Mathf.Max(2f, sizeMin), Mathf.Max(sizeMin, sizeMax));
        chosen.rect.sizeDelta = new Vector2(size, size);
        chosen.origin = new Vector2(x, y);
        chosen.rect.anchoredPosition = chosen.origin;
        chosen.age = 0f;
        chosen.life = Random.Range(Mathf.Max(0.2f, lifetimeMin), Mathf.Max(lifetimeMin, lifetimeMax));
        // Give each bubble a different speed, height, and drift.
        chosen.rise = Random.Range(Mathf.Max(1f, riseDistanceMin), Mathf.Max(riseDistanceMin, riseDistanceMax));
        chosen.drift = Random.Range(-sideDrift, sideDrift);
        chosen.phase = Random.Range(0f, Mathf.PI * 2f);
        chosen.image.sprite = bubbleSprite;
        chosen.image.color = new Color(bubbleTint.r, bubbleTint.g, bubbleTint.b, 0f);
        chosen.image.enabled = true;
        chosen.active = true;
    }
}
