using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuDaryahWaterDrops : MonoBehaviour
{
    [Header("Water Drop Sprite")]
    public Sprite waterDropSprite;

    [Header("Character Visibility")]
    [Tooltip("Assign Bu Daryah's actual Mesh Renderer (Bu_Darya).")]
    public Renderer characterRenderer;

    [Header("Spawn Points")]
    public Transform[] dropPoints;

    [Header("Spawn Timing")]
    public float minSpawnDelay = 2f;
    public float maxSpawnDelay = 4f;

    [Header("Drop Appearance")]
    public float dropSize = 0.025f;
    [Range(0f, 1f)] public float dropOpacity = 0.7f;

    [Header("Falling Motion")]
    public float fallSpeed = 0.2f;
    public float fallDistance = 0.4f;
    public float horizontalDrift = 0.01f;

    [Header("Fade")]
    [Range(0f, 1f)] public float fadeStart = 0.5f;

    private Coroutine spawnRoutine;
    private readonly List<GameObject> activeDrops = new List<GameObject>();

    private bool CharacterIsVisible()
    {
        // Do not spawn drops if the reference hasn't been assigned.
        return characterRenderer != null &&
               characterRenderer.enabled &&
               characterRenderer.gameObject.activeInHierarchy;
    }

    void Update()
    {
        if (CharacterIsVisible())
        {
            if (spawnRoutine == null)
                spawnRoutine = StartCoroutine(SpawnDrops());
        }
        else
        {
            StopAndClearDrops();
        }
    }

    void OnDisable()
    {
        StopAndClearDrops();
    }

    private void StopAndClearDrops()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        // Drops spawn independently in world space, so clean them up too.
        // Disabling immediately hides them, even before Destroy completes.
        for (int i = activeDrops.Count - 1; i >= 0; i--)
        {
            if (activeDrops[i] != null)
            {
                activeDrops[i].SetActive(false);
                Destroy(activeDrops[i]);
            }
        }
        activeDrops.Clear();
    }

    IEnumerator SpawnDrops()
    {
        while (CharacterIsVisible())
        {
            float minDelay = Mathf.Max(0.1f, minSpawnDelay);
            float maxDelay = Mathf.Max(minDelay, maxSpawnDelay);
            yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));

            // Visibility might have changed during the delay.
            if (!CharacterIsVisible())
                break;

            if (waterDropSprite == null || dropPoints == null || dropPoints.Length == 0)
                continue;

            Transform spawnPoint = dropPoints[Random.Range(0, dropPoints.Length)];
            if (spawnPoint != null)
                CreateWaterDrop(spawnPoint.position);
        }

        spawnRoutine = null;
    }

    void CreateWaterDrop(Vector3 position)
    {
        GameObject drop = new GameObject("BuDaryah_WaterDrop");
        drop.transform.position = position;

        SpriteRenderer sr = drop.AddComponent<SpriteRenderer>();
        sr.sprite = waterDropSprite;
        sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(dropOpacity));

        float spriteHeight = waterDropSprite.bounds.size.y;
        float scale = dropSize / Mathf.Max(0.0001f, spriteHeight);
        drop.transform.localScale = Vector3.one * scale;

        activeDrops.Add(drop);
        StartCoroutine(AnimateDrop(drop, sr));
    }

    IEnumerator AnimateDrop(GameObject drop, SpriteRenderer sr)
    {
        Vector3 start = drop.transform.position;
        Vector3 drift = new Vector3(
            Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)
        ).normalized;

        float distance = Mathf.Max(0.01f, fallDistance);
        float speed = Mathf.Max(0.01f, fallSpeed);
        float traveled = 0f;

        while (drop != null && traveled < distance && CharacterIsVisible())
        {
            traveled = Mathf.Min(traveled + speed * Time.deltaTime, distance);
            float progress = traveled / distance;

            drop.transform.position = start + Vector3.down * traveled +
                                      drift * horizontalDrift * progress;

            Camera cam = Camera.main;
            if (cam != null)
                drop.transform.rotation = cam.transform.rotation;

            float alpha = 1f;
            if (progress >= fadeStart)
                alpha = 1f - Mathf.InverseLerp(Mathf.Clamp01(fadeStart), 1f, progress);

            Color color = sr.color;
            color.a = Mathf.Clamp01(dropOpacity) * alpha;
            sr.color = color;

            yield return null;
        }

        activeDrops.Remove(drop);
        if (drop != null)
            Destroy(drop);
    }
}
