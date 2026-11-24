using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Creates the falling palm leaves used in Om Elsaaf's HUD theme.
/// Leaves appear near the top, drift gently as they fall, and fade away.
/// A small pool of UI images is reused to keep the effect lightweight.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class OmElsaafFallingLeaves : MonoBehaviour
{
    [Header("Three leaf sprites (Sprite 2D and UI)")]
    [SerializeField] private Sprite[] leafSprites = new Sprite[3];

    [Header("Keep it subtle")]
    [Range(1, 10)][SerializeField] private int maximumVisible = 3;
    [Min(0.2f)][SerializeField] private float spawnIntervalMin = 2.0f;
    [Min(0.2f)][SerializeField] private float spawnIntervalMax = 3.5f;
    [Range(0.01f, 1f)][SerializeField] private float leafOpacity = 0.55f;
    [SerializeField] private Color leafTint = Color.white;

    [Header("Leaf sizes in UI units (1280 x 720 HUD)")]
    [Min(2f)][SerializeField] private float sizeMin = 14f;
    [Min(2f)][SerializeField] private float sizeMax = 30f;

    [Header("Fall from top to bottom")]
    [Min(0.2f)][SerializeField] private float lifetimeMin = 8f;
    [Min(0.2f)][SerializeField] private float lifetimeMax = 12f;
    [Min(0f)][SerializeField] private float topPadding = 20f;
    [Min(0f)][SerializeField] private float bottomPadding = 20f;
    [Min(0f)][SerializeField] private float edgePadding = 32f;

    [Header("Gentle natural movement")]
    [Range(0f, 80f)][SerializeField] private float sidewaysDrift = 24f;
    [Range(0f, 30f)][SerializeField] private float swayAmount = 8f;
    [Range(0f, 90f)][SerializeField] private float rotationAmount = 35f;

    // Stores the position, timing, and movement of one falling leaf.
    private class Leaf
    {
        public RectTransform rect;
        public Image image;
        public bool active;
        public float age;
        public float life;
        public float startX;
        public float startY;
        public float endY;
        public float drift;
        public float sway;
        public float phase;
        public float startAngle;
        public float spin;
    }

    // Reuse the same leaf images instead of creating new objects repeatedly.
    private readonly List<Leaf> pool = new List<Leaf>();
    private RectTransform area;
    private float untilNextSpawn;
    private bool warnedMissingSprites;

    /// <summary>
    /// Finds the UI area and prepares the reusable leaf images.
    /// </summary>
    private void Awake()
    {
        area = GetComponent<RectTransform>();
        EnsurePool();
    }

    /// <summary>
    /// Schedules the first leaf shortly after the theme appears.
    /// </summary>
    private void OnEnable()
    {
        untilNextSpawn = 0.4f;
    }

    /// <summary>
    /// Hides the existing leaves when the theme is turned off.
    /// </summary>
    private void OnDisable()
    {
        foreach (Leaf leaf in pool)
        {
            leaf.active = false;
            if (leaf.image != null) leaf.image.enabled = false;
        }
    }

    /// <summary>
    /// Spawns leaves when needed and updates their position, rotation, and fading.
    /// </summary>
    private void Update()
    {
        if (!Application.isPlaying || area == null) return;
        if (area.rect.width <= 1f || area.rect.height <= 1f) return;
        // Do not try to spawn leaves until at least one sprite is assigned.
        if (!HasAnySprite())
        {
            if (!warnedMissingSprites)
            {
                Debug.LogWarning("OmElsaafFallingLeaves: Assign at least one leaf Sprite in the Inspector.", this);
                warnedMissingSprites = true;
            }
            return;
        }

        EnsurePool();
        float dt = Time.deltaTime;
        untilNextSpawn -= dt;
        if (untilNextSpawn <= 0f)
        {
            SpawnOne();
            untilNextSpawn = Random.Range(
                Mathf.Max(0.2f, spawnIntervalMin),
                Mathf.Max(spawnIntervalMin, spawnIntervalMax));
        }

        foreach (Leaf leaf in pool)
        {
            if (!leaf.active) continue;

            leaf.age += dt;
            float t = Mathf.Clamp01(leaf.age / leaf.life);
            if (t >= 1f)
            {
                leaf.active = false;
                leaf.image.enabled = false;
                continue;
            }


            // Move downward with a small sideways sway.
            float x = leaf.startX + leaf.drift * t
                      + Mathf.Sin(t * Mathf.PI * 2f + leaf.phase) * leaf.sway;
            float y = Mathf.Lerp(leaf.startY, leaf.endY, t);
            leaf.rect.anchoredPosition = new Vector2(x, y);


            // Slowly rotate each leaf as it falls.
            float angle = leaf.startAngle + leaf.spin * t
                          + Mathf.Sin(t * Mathf.PI * 2f + leaf.phase) * 7f;
            leaf.rect.localRotation = Quaternion.Euler(0f, 0f, angle);


            // Fade leaves in and out instead of making them suddenly appear or disappear.
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.12f));
            float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / 0.22f));
            Color color = leafTint;
            color.a = leafOpacity * leafTint.a * Mathf.Min(fadeIn, fadeOut);
            leaf.image.color = color;
        }
    }

    /// <summary>
    /// Creates enough reusable leaf images without making a new one every frame.
    /// </summary>
    private void EnsurePool()
    {
        // Only prepare as many leaf images as the maximum visible count needs.
        int wanted = Mathf.Max(1, maximumVisible);
        while (pool.Count < wanted)
        {
            int id = pool.Count + 1;
            GameObject go = new GameObject("OmElsaaf_Leaf_" + id,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);

            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.maskable = false;
            img.preserveAspect = true;
            img.enabled = false;

            pool.Add(new Leaf { rect = rt, image = img });
        }


        for (int i = wanted; i < pool.Count; i++)
        {
            pool[i].active = false;
            if (pool[i].image != null) pool[i].image.enabled = false;
        }
    }

    /// <summary>
    /// Starts one leaf at a random position above the HUD.
    /// </summary>
    private void SpawnOne()
    {
        // Find an unused leaf image from the pool.
        Leaf chosen = null;
        int count = Mathf.Min(maximumVisible, pool.Count);
        for (int i = 0; i < count; i++)
        {
            if (!pool[i].active)
            {
                chosen = pool[i];
                break;
            }
        }
        if (chosen == null) return;

        Sprite sprite = GetRandomSprite();
        if (sprite == null) return;

        float w = area.rect.width;
        float h = area.rect.height;
        float margin = Mathf.Min(edgePadding, w * 0.45f);
        // Start above the screen at a random horizontal position.
        chosen.startX = Random.Range(margin, Mathf.Max(margin, w - margin));
        chosen.startY = h + topPadding;
        chosen.endY = -bottomPadding;

        float size = Random.Range(Mathf.Max(2f, sizeMin), Mathf.Max(sizeMin, sizeMax));
        chosen.rect.sizeDelta = new Vector2(size, size);
        chosen.rect.anchoredPosition = new Vector2(chosen.startX, chosen.startY);
        chosen.age = 0f;
        chosen.life = Random.Range(Mathf.Max(0.2f, lifetimeMin),
                                   Mathf.Max(lifetimeMin, lifetimeMax));
        // Give each leaf slightly different motion and rotation.
        chosen.drift = Random.Range(-sidewaysDrift, sidewaysDrift);
        chosen.sway = Random.Range(0.5f * swayAmount, swayAmount);
        chosen.phase = Random.Range(0f, Mathf.PI * 2f);
        chosen.startAngle = Random.Range(-40f, 40f);
        chosen.spin = Random.Range(-rotationAmount, rotationAmount);
        chosen.rect.localRotation = Quaternion.Euler(0f, 0f, chosen.startAngle);
        chosen.image.sprite = sprite;
        chosen.image.color = new Color(leafTint.r, leafTint.g, leafTint.b, 0f);
        chosen.image.enabled = true;
        chosen.active = true;
    }

    /// <summary>
    /// Checks that at least one leaf image has been assigned.
    /// </summary>
    private bool HasAnySprite()
    {
        if (leafSprites == null) return false;
        foreach (Sprite s in leafSprites)
            if (s != null) return true;
        return false;
    }

    /// <summary>
    /// Chooses randomly from the leaf images assigned in the Inspector.
    /// </summary>
    private Sprite GetRandomSprite()
    {
        if (leafSprites == null || leafSprites.Length == 0) return null;
        int validCount = 0;
        foreach (Sprite s in leafSprites)
            if (s != null) validCount++;
        if (validCount == 0) return null;

        int index = Random.Range(0, validCount);
        foreach (Sprite s in leafSprites)
        {
            if (s == null) continue;
            if (index-- == 0) return s;
        }
        return null;
    }
}
