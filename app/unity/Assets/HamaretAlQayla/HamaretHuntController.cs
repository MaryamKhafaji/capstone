using System;
using System.Collections;
using NRKernal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HamaretHuntController : MonoBehaviour
{
    public const string MonsterId = "hamaret_al_qayla";
    public const string LegendName = "Hamaret Al Qayla";
    public const string ThankYouArabic = "شكراً لك. لقد أعدت النور إلى الأرض.";

    [Header("Shared MasAR+ systems")]
    [SerializeField] private Transform trackedCenterCamera;
    [SerializeField] private MonsterHuntUIController huntUI;
    [SerializeField] private FootwearStepManager footwear;
    [SerializeField] private BoostPromptController sharedBoost;
    [SerializeField] private TMP_Text statusText;

    [Header("Pre-cleared level courtyard (meters)")]
    [SerializeField, Min(0.5f)] private float headsetHeightFromFloor = 1.6f;
    [SerializeField, Min(0.15f)] private float maximumHeadHeightChange = 0.38f;

    [Header("Steal the Courtyard")]
    [SerializeField, Range(1.1f, 15f)] private float cellSize = 4.8f;
    [SerializeField, Range(0.12f, 0.35f)] private float maximumPoseSample = 0.28f;
    [SerializeField, Min(0)] private int crystalReward = 2;
    [SerializeField] private bool editorMovementTest = true;

    [Header("Presentation")]
    [SerializeField] private Shader visualShader;
    [SerializeField] private Texture2D arabicThankYou;
    [SerializeField] private AudioClip hoofStrikeClip;
    [SerializeField] private AudioClip thankYouVoice;
    [SerializeField, Min(2f)] private float thankYouSeconds = 4f;
    [SerializeField] private Transform introPreviewAnchor;

    private HamaretHuntVisuals visuals;
    private HamaretGroundTraversal traversal;
    private Vector3 origin;
    private Vector3 forward;
    private Vector3 right;
    private float startHeight;
    private bool inArea;
    private bool stepsStarted;
    private bool completed;
    private DateTime startedAt;
    private string huntId;
    private string previousStatus;
    private float previousFontMin, previousFontMax;
    private bool previousAutosize;
    private TextWrappingModes previousStatusWrapping;
    private AudioSource audioSource;
    private AudioClip impactClip;
    private AudioClip crackClip;
    private const int LoopSegments = 42;
    private const float RoundSeconds = 55f;
    private const float RivalMetersPerSecond = 1.30f;
    private readonly Vector2[] rivalRoute = new Vector2[LoopSegments + 1];
    private int rivalWaypoint, bankedSections;
    private float protectedUntil, bankNoticeUntil, rivalNoticeUntil, cutNoticeUntil;
    private byte lastCutSide;
    private RawImage arabicImage;
    private GameObject introPreview;
    private HamaretHuntVisuals introVisuals;
    private EnergySystem previousBoostEnergy;
    private string previousBoostName;
    private bool boostConnected;

    private void OnEnable()
    {
        completed = false;
        bankedSections = 0;
        protectedUntil = 0f;
        bankNoticeUntil = rivalNoticeUntil = cutNoticeUntil = 0f;
        lastCutSide = 0;
        stepsStarted = false;
        inArea = false;
        if (statusText != null)
        {
            previousStatus = statusText.text;
            previousAutosize = statusText.enableAutoSizing;
            previousStatusWrapping = statusText.textWrappingMode;
            statusText.textWrappingMode = TextWrappingModes.NoWrap;
            previousFontMin = statusText.fontSizeMin;
            previousFontMax = statusText.fontSizeMax;
            statusText.fontSizeMin = statusText.fontSize * .50f;
            statusText.fontSizeMax = statusText.fontSize * .84f;
            statusText.enableAutoSizing = true;
        }

        if (trackedCenterCamera == null || huntUI == null || visualShader == null ||
            statusText == null || arabicThankYou == null)
        {
            Debug.LogError("Hamaret hunt is missing its tracked camera, HUD or visual assets.", this);
            return;
        }

#if UNITY_EDITOR
        if (!editorMovementTest)
        {
            SetStatus("Enable Editor Movement Test to play without glasses.");
            return;
        }
#endif
        StartCoroutine(RunHunt());
    }

    private void LateUpdate() => visuals?.Animate();

    private IEnumerator RunHunt()
    {
        while (HuntLocationGuide.Active && !HuntLocationGuide.AtDestination)
            yield return new WaitForSeconds(0.5f);
        SetStatus("Face into the cleared play area.");
        yield return new WaitForSeconds(.32f);
        while (!PoseReady() || Vector3.ProjectOnPlane(trackedCenterCamera.forward, Vector3.up).sqrMagnitude < 0.25f)
            yield return null;

        startHeight = trackedCenterCamera.position.y;
        forward = Vector3.ProjectOnPlane(trackedCenterCamera.forward, Vector3.up).normalized;
        right = Vector3.Cross(Vector3.up, forward);
        origin = trackedCenterCamera.position - Vector3.up * headsetHeightFromFloor - forward * cellSize * 0.5f;
        startedAt = DateTime.UtcNow;
        huntId = Guid.NewGuid().ToString("N");
        if (footwear != null)
        {
            footwear.BeginHunt();
            stepsStarted = true;
        }
        if (sharedBoost != null)
        {
            previousBoostEnergy = sharedBoost.energySystem;
            previousBoostName = sharedBoost.boostName;
            if (sharedBoost.energySystem == null && footwear != null)
                sharedBoost.energySystem = footwear.energySystem;
            sharedBoost.boostName = "SLOWDOWN";
            sharedBoost.onBoostActivated.AddListener(IlluminateNextCheckpoint);
            boostConnected = true;
        }

        traversal = new HamaretGroundTraversal();
        visuals = new HamaretHuntVisuals(transform);
        visuals.CreateCourtyard(origin, forward, cellSize, visualShader);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        impactClip = hoofStrikeClip == null ? MakeImpact("Hoof on stone", 103f, 0.28f) : null;
        crackClip = MakeImpact("Courtyard reclaimed", 320f, 0.31f);
        Vector3 rival = WorldPoint(new Vector2(UnityEngine.Random.value < 0.5f ? .17f : .83f, .72f));
        traversal.Seed(BoardPoint(trackedCenterCamera.position), 1);
        traversal.Seed(BoardPoint(rival), 2);
        traversal.Advance(BoardPoint(rival), 2);
        PlanRivalLoop(BoardPoint(rival), BoardPoint(trackedCenterCamera.position));
        visuals.PlaceFigure(rival, WorldPoint(rivalRoute[0]));
        visuals.UpdateCourtyard(traversal);
        inArea = true;

        float roundTime = 0f;
        int majority = 0;
        bool tied = false;

        Vector2 previous = default;
        bool hasPrevious = false;
        while (inArea)
        {
            Vector3 offset = trackedCenterCamera.position - origin;
            var point = new Vector2(Vector3.Dot(offset, right), Vector3.Dot(offset, forward));
            if (!CanWalk())
            {
                traversal.Interrupt();
                hasPrevious = false;
                visuals.SetWalking(false);
                visuals.UpdateCourtyard(traversal);
                yield return null;
                continue;
            }

            float allowedStep = maximumPoseSample + 3f * Time.deltaTime;
            if (hasPrevious && (point - previous).sqrMagnitude > allowedStep * allowedStep)
            {
                traversal.Interrupt();
                hasPrevious = false;
                SetStatus("Tracking shifted. Stand still.");
                visuals.SetWalking(false);
                yield return null;
                continue;
            }

            roundTime += Time.deltaTime;
            Vector2 courtPoint = BoardPoint(trackedCenterCamera.position);
            if (GetCell(point) < 0)
                traversal.Interrupt();
            else
                traversal.Advance(courtPoint, 1);
            if (GetCell(point) >= 0 && traversal.LastBank > 0)
            {
                bankedSections += traversal.LastBank;
                bankNoticeUntil = Time.time + 1.65f;
                PlayClip(crackClip);
            }
            previous = point;
            hasPrevious = true;

            if (traversal.Winner == 0) MoveRival(ref rival, courtPoint);
            visuals.UpdateCourtyard(traversal);
            byte cut = traversal.TakeCutNotice();
            if (cut != 0)
            {
                lastCutSide = cut;
                cutNoticeUntil = Time.time + 1.65f;
            }
            string hint = Time.time < cutNoticeUntil
                ? (lastCutSide == 1 ? "Trail lost. Keep walking." : "Violet trail erased.")
                : Time.time < bankNoticeUntil ? "Ground claimed!"
                : Time.time < rivalNoticeUntil ? "Ground taken."
                : GetCell(point) < 0 ? "Return to the Freej."
                : traversal.Pending > 0 ? "Return to your light."
                : "Leave your light.";
            int lead = traversal.LightShare.CompareTo(traversal.DarkShare);
            string score = lead > 0 ? "You're ahead"
                : lead < 0 ? LegendName + " leads" : "Even ground";
            SetStatus(score + "\n" + Mathf.CeilToInt(Mathf.Max(0f, RoundSeconds - roundTime)) + "s · " + hint);
            if (traversal.Winner != 0)
            {
                majority = traversal.Winner == 1 ? 1 : -1;
                break;
            }
            if (roundTime >= RoundSeconds)
            {
                tied = lead == 0;
                majority = lead > 0 ? 1 : -1;
                break;
            }
            yield return null;
        }

        inArea = false;
        visuals.SetWalking(false);
        if (majority < 0)
        {
            if (stepsStarted)
            {
                footwear.CancelHunt();
                stepsStarted = false;
            }
            ReleasePresentation();
            huntUI.ShowHamaretDefeat(tied);
            yield break;
        }
        SetStatus("The courtyard is free. Stay where you are.");
        visuals.PlaceFigure(rival, rival + forward);
        SetStatus("Congrats! The Freej is yours!");
        PlayClip(thankYouVoice);
        for (float time = 0f; time < 2.45f;)
        {
            if (PoseReady())
            {
                float reveal = Mathf.Clamp01((time - .48f) / 1.55f);
                visuals.FadeFigure(reveal);
                visuals.UpdateCourtyard(traversal);
                time += Time.deltaTime;
            }
            yield return null;
        }
        visuals.FadeFigure(1f);
        CompleteHunt();
    }

    private int GetCell(Vector2 point)
    {
        int column = Mathf.FloorToInt(point.x / cellSize + 1.5f);
        int row = Mathf.FloorToInt(point.y / cellSize);
        return column >= 0 && column < 3 && row >= 0 && row < 3 ? row * 3 + column : -1;
    }

    private Vector2 BoardPoint(Vector3 world)
    {
        Vector3 delta = world - origin;
        return new Vector2(Vector3.Dot(delta, right) / (3f * cellSize) + .5f,
            Vector3.Dot(delta, forward) / (3f * cellSize));
    }

    private Vector3 WorldPoint(Vector2 point) =>
        origin + (right * (point.x - .5f) + forward * point.y) * (3f * cellSize);

    private void PlanRivalLoop(Vector2 position, Vector2 player)
    {
        rivalWaypoint = 0;
        Vector2 home = traversal.NearestOwned(position, 2);
        const float radius = .15f;
        float best = float.NegativeInfinity;
        Vector2 center = home;
        float phase = 0f;
        for (int option = 0; option < 16; option++)
        {
            float angle = option * Mathf.PI * 2f / 16f;
            Vector2 candidate = home - new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (candidate.x < radius || candidate.x > 1f - radius ||
                candidate.y < radius || candidate.y > 1f - radius) continue;

            float score = UnityEngine.Random.value * .35f +
                Mathf.Min(.4f, Vector2.Distance(candidate, player)) * 2f;
            for (int probe = 0; probe < 8; probe++)
            {
                float at = probe * Mathf.PI * 2f / 8f;
                byte owner = traversal.OwnerAt(candidate + new Vector2(Mathf.Cos(at), Mathf.Sin(at)) * radius * .85f);
                score += owner == 1 ? 1.15f : owner == 0 ? .65f : -.55f;
            }
            if (score <= best) continue;
            best = score;
            center = candidate;
            phase = angle;
        }
        float direction = UnityEngine.Random.value < .5f ? -1f : 1f;
        for (int i = 1; i < LoopSegments; i++)
        {
            float angle = phase + direction * Mathf.PI * 2f * i / LoopSegments;
            rivalRoute[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            rivalRoute[i] = new Vector2(Mathf.Clamp01(rivalRoute[i].x), Mathf.Clamp01(rivalRoute[i].y));
        }
        rivalRoute[0] = rivalRoute[LoopSegments] = home;
    }

    private void MoveRival(ref Vector3 position, Vector2 player)
    {
        float remaining = Time.deltaTime * RivalMetersPerSecond * (Time.time < protectedUntil ? .62f : 1f);
        Vector3 facing = position + forward;
        bool walking = false;
        visuals.SetWalking(remaining > 0f);
        while (remaining > .0001f && traversal.Winner == 0)
        {
            Vector3 target = WorldPoint(rivalRoute[rivalWaypoint]);
            Vector3 step = Vector3.MoveTowards(position, target, remaining);
            Vector3 delta = step - position;
            position = step;
            remaining -= delta.magnitude;
            if (delta.sqrMagnitude > 0f)
            {
                facing = step + delta.normalized;
                walking = true;
                if (traversal.Advance(BoardPoint(position), 2) > 0)
                {
                    rivalNoticeUntil = Time.time + 1.5f;
                    PlayClip(hoofStrikeClip != null ? hoofStrikeClip : impactClip);
                }
            }
            if (position != target) break;
            if (rivalWaypoint == LoopSegments)
                PlanRivalLoop(BoardPoint(position), player);
            else
                rivalWaypoint++;
        }
        if (walking) visuals.PlaceFigure(position, facing);
        else visuals.SetWalking(false);
    }

    private static AudioClip MakeImpact(string name, float pitch, float volume)
    {
        const int rate = 24000;
        var samples = new float[9600];
        uint noise = 3251;
        for (int index = 0; index < samples.Length; index++)
        {
            float time = index / (float)rate;
            noise = unchecked(noise * 1664525u + 1013904223u);
            float grain = (noise >> 8) / 16777215f * 2f - 1f;
            float thud = Mathf.Sin(2f * Mathf.PI * time * pitch * (1f - 1.25f * time));
            samples[index] = volume * Mathf.Min(1f, time * 900f) *
                (0.65f * thud * Mathf.Exp(-time * 20f) + 0.35f * grain * Mathf.Exp(-time * 56f));
        }
        var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    private bool PoseReady()
    {
#if UNITY_EDITOR
        return editorMovementTest;
#elif UNITY_ANDROID
        return NRFrame.SessionStatus == SessionState.Running &&
            NRFrame.LostTrackingReason == LostTrackingReason.NONE;
#else
        return false;
#endif
    }

    private bool CanWalk()
    {
        if (!PoseReady())
        {
            SetStatus("Tracking paused. Stop walking.");
            return false;
        }

        Vector3 head = trackedCenterCamera.position;
        if (Mathf.Abs(head.y - startHeight) > maximumHeadHeightChange)
        {
            SetStatus("Stop. Stay on level ground.");
            return false;
        }

        return true;
    }

    private void CompleteHunt()
    {
        if (completed)
            return;

        completed = true;
        int steps = stepsStarted ? footwear.CompleteHunt() : 0;
        EnergySystem energy = footwear != null ? footwear.energySystem : null;
        int earnedEnergy = energy != null ? Mathf.FloorToInt(steps / Mathf.Max(0.01f, energy.stepsPerEnergy)) : 0;
        CrystalManager crystals = CrystalManager.Instance;
        int before = crystals != null ? crystals.CurrentCrystals : 0;
        int after = crystals != null ? crystals.AddCrystals(crystalReward) : before;
        HuntRewardActions.RecordCompletedHunt(huntId, MonsterId, steps, after - before, earnedEnergy,
            LegendName, startedAt, DateTime.UtcNow);

        ReleasePresentation();
        huntUI.ShowCaughtMonster(LegendName, after - before, before, after);
    }

    private void SetStatus(string value)
    {
        if (statusText != null && statusText.text != value)
            statusText.text = value;
    }

    public void IlluminateNextCheckpoint()
    {
        if (isActiveAndEnabled && inArea)
            protectedUntil = Time.time + 4f;
    }

    public GameObject GetPanelPreview(bool reward)
    {
        if (reward)
            return null;
        if (introPreview == null)
            introPreview = CreatePanelPreview(introPreviewAnchor, out introVisuals);
        return introPreview;
    }

    public void HidePanelPreviews()
    {
        if (introPreview != null) introPreview.SetActive(false);
    }

    private GameObject CreatePanelPreview(Transform anchor, out HamaretHuntVisuals model)
    {
        model = null;
        if (anchor == null || visualShader == null)
            return null;

        var item = new GameObject("Hamaret Character Preview");
        item.SetActive(false);
        item.transform.SetParent(anchor.parent, false);
        item.transform.SetPositionAndRotation(anchor.position, Quaternion.identity);
        model = new HamaretHuntVisuals(item.transform);
        item.AddComponent<HamaretPreviewPerformance>().Initialize(model);
        foreach (var child in item.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = anchor.gameObject.layer;
        return item;
    }

    private void ShowThankYou()
    {
        GameObject message = new GameObject("Arabic thank-you", typeof(RectTransform), typeof(RawImage));
        message.layer = statusText.gameObject.layer;
        arabicImage = message.GetComponent<RawImage>();
        RectTransform frame = arabicImage.rectTransform;
        frame.SetParent(statusText.transform, false);
        frame.anchorMin = new Vector2(0f, 0.5f);
        frame.anchorMax = new Vector2(1f, 0.5f);
        frame.sizeDelta = new Vector2(0f, statusText.rectTransform.rect.width * arabicThankYou.height / arabicThankYou.width);
        frame.anchoredPosition = Vector2.zero;
        arabicImage.texture = arabicThankYou;
        arabicImage.color = new Color(.24f,.10f,.27f);
        arabicImage.raycastTarget = false;
        SetStatus(" ");
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void ReleasePresentation()
    {
        if (boostConnected && sharedBoost != null)
        {
            sharedBoost.onBoostActivated.RemoveListener(IlluminateNextCheckpoint);
            sharedBoost.DeclineBoost();
            sharedBoost.energySystem = previousBoostEnergy;
            sharedBoost.boostName = previousBoostName;
        }
        boostConnected = false;

        if (visuals != null)
        {
            visuals.Dispose();
            visuals = null;
        }

        if (arabicImage != null)
        {
            arabicImage.gameObject.SetActive(false);
            Destroy(arabicImage.gameObject);
            arabicImage = null;
        }
        SetStatus(previousStatus);
        if (statusText != null)
        {
            statusText.enableAutoSizing = previousAutosize;
            statusText.textWrappingMode = previousStatusWrapping;
            statusText.fontSizeMin = previousFontMin;
            statusText.fontSizeMax = previousFontMax;
        }
        if (impactClip != null)
            Destroy(impactClip);
        if (crackClip != null)
            Destroy(crackClip);
        impactClip = null;
        crackClip = null;
        if (audioSource != null)
        {
            audioSource.Stop();
            Destroy(audioSource);
            audioSource = null;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && inArea)
            traversal.Interrupt();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        inArea = false;
        if (!completed && stepsStarted && footwear != null)
            footwear.CancelHunt();

        stepsStarted = false;
        ReleasePresentation();
    }

    private void OnDestroy()
    {
        introVisuals?.Dispose();
        if (introPreview != null) Destroy(introPreview);
    }
}
