
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MonsterHuntUIController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject notificationPanel;
    public GameObject monsterIntroPanel;
    public GameObject warningPanel;
    public GameObject distancePanel;

    [Tooltip("Assign GameHUD > CaughtMonsterPanel here.")]
    public GameObject caughtMonsterPanel;

    [Header("Monster Notification")]
    [Tooltip("Assign the text displaying 'Bu Daryah has spawned!'")]
    public TMP_Text monsterSpawnText;

    [Header("Monster Intro - Title and Description")]
    [Tooltip("Assign MonsterIntroPanel > MonsterNameText.")]
    public TMP_Text monsterIntroText;
    [Tooltip("Assign MonsterIntroPanel > MonsterDescriptionText.")]
    public TMP_Text monsterIntroDescriptionText;
    [TextArea(2, 5)]
    [SerializeField]
    private string omElSaafDescription =
        "Collect five palm fronds to reveal Om El-Saaf!";

    [Header("Monster Intro - Rotating Preview")]
    [Tooltip("Scene preview model for Bu Daryah, NOT the gameplay monster.")]
    [SerializeField] private GameObject buDaryaPreview;
    [Tooltip("Scene preview model for Om El-Saaf, NOT the gameplay monster.")]
    [SerializeField] private GameObject omElSaafPreview;
    [Tooltip("Optional existing MonsterPreviewFramer component, if your preview setup uses one. Leave empty otherwise.")]
    [SerializeField] private MonsterPreviewFramer monsterPreviewFramer;

    [Header("Monster Intro - Dynamic Picture")]
    [Tooltip("Assign the UI Image to change on MonsterIntroPanel. Use a dedicated child Image if the panel background should remain visible.")]
    [SerializeField] private Image monsterIntroPicture;
    [Tooltip("Picture shown when the player accepts Bu Daryah's hunt.")]
    [SerializeField] private Sprite buDaryahIntroPicture;
    [Tooltip("Picture shown when the player accepts Om El-Saaf's hunt.")]
    [SerializeField] private Sprite omElsaafIntroPicture;

    private string originalIntroTemplate;
    private string originalBuDaryaDescription;

    [SerializeField]
    private string currentMonsterName = "Bu Daryah";

    [Header("Caught Panel UI")]
    public TMP_Text caughtTitleText;
    public TMP_Text caughtRewardsText;
    public Slider crystalBar;
    public TMP_Text crystalBarCountText;

    [Header("Caught Panel - Rotating Reward Item")]
    [Tooltip("Drag oudPreview from MonsterPreviewStage (scene object, not FBX asset).")]
    [SerializeField] private GameObject buDaryahRewardItem;
    [Tooltip("Drag saafPreview from MonsterPreviewStage (scene object, not FBX asset).")]
    [SerializeField] private GameObject omElsaafRewardItem;

    [Header("Gameplay")]
    public GameObject gameplayRoot;

    [Header("Hunt Theme Switching")]
    [Tooltip("Drag ThemeCanvas here (the object with MasARThemeController).")]
    [SerializeField] private MasARThemeController themeController;

    [Header("Timing")]
    public float introDuration = 5f;

    [Header("Wrong Direction Warning")]
    public float warningDuration = 5f;
    public bool flashWarning = false;
    public float flashInterval = 0.35f;

    [Header("Crystal Animation")]
    public float crystalFillDuration = 3f;

    private Coroutine huntSequence;
    private Coroutine warningRoutine;
    private Coroutine crystalFillRoutine;
    private static readonly string[] Directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
    private HuntRewardActions huntRewards;
    private bool followingOffer;
    private bool offerAccepted;
    private float nextOfferUpdate;

    void Awake()
    {
        // Preserve the existing intro wording and only replace the monster name.
        if (monsterIntroText != null)
            originalIntroTemplate = monsterIntroText.text;
        if (monsterIntroDescriptionText != null)
            originalBuDaryaDescription = monsterIntroDescriptionText.text;

        // All four models already share the same existing MonsterPreviewTexture.
        // Never change camera targets or RenderTexture assignments here.

        // Nothing is visible in the preview before the player starts a hunt.
        ClearIntroPicture();
        HideAllPreviewModels();
    }

    void Start()
    {
        // Clean starting state without exit animations.
        SetVisibleImmediately(notificationPanel, false);
        SetVisibleImmediately(monsterIntroPanel, false);
        SetVisibleImmediately(warningPanel, false);
        SetVisibleImmediately(distancePanel, false);
        SetVisibleImmediately(caughtMonsterPanel, false);

        if (gameplayRoot != null)
            gameplayRoot.SetActive(false);

        ClearIntroPicture();
        HideAllPreviewModels();
        UpdateMonsterNotificationText();
        UpdateMonsterIntroContent();

        huntRewards = GetComponent<HuntRewardActions>();
        if (EmbeddedHunt.Active)
            ShowEmbeddedOffer();
        else
        {
            if (huntRewards != null)
                SetMonsterSpawnName(huntRewards.SelectMonster(Random.Range(0, 2) == 0 ? "bu_daryah" : "om_el_saaf"));
            ShowMonsterNotification();
        }
    }

    public void ShowEmbeddedOffer()
    {
        offerAccepted = false;
        followingOffer = true;
        ShowMonsterNotification();
        if (monsterSpawnText != null)
            monsterSpawnText.enableAutoSizing = true;
        RefreshEmbeddedOffer();
    }

    private void Update()
    {
        if (followingOffer && Time.unscaledTime >= nextOfferUpdate)
            RefreshEmbeddedOffer();
    }

    private void RefreshEmbeddedOffer()
    {
        nextOfferUpdate = Time.unscaledTime + 1f;
        EmbeddedHunt.State state = EmbeddedHunt.ReadState();

        if (state.status == "arrived" && offerAccepted && huntRewards != null)
        {
            followingOffer = false;
            SetMonsterSpawnName(huntRewards.SelectMonster(state.monsterId));
            ApplyThemeForCurrentMonster();
            StopCurrentSequence();
            huntSequence = StartCoroutine(BeginHuntSequence());
            return;
        }

        if (monsterSpawnText == null) return;
        string name = state.monsterId == "om_el_saaf" ? "Om El-Saaf" : "Bu Daryah";
        string direction = Directions[Mathf.RoundToInt(Mathf.Repeat(state.bearingDegrees, 360f) / 45f) % 8];
        monsterSpawnText.text = state.status switch
        {
            "arrived" => name + " has spawned nearby!\nBegin the hunt?",
            "en_route" => name + " • " + state.siteName + "\n" +
                (state.accuracyLimited && state.distanceMeters < 30
                    ? "Waiting for a more precise GPS fix…"
                    : state.distanceMeters + " m " + direction),
            "locating" => "Finding your location…",
            "gps_off" => "Turn on GPS to find the hunt.",
            "no_sites" => "No hunt sites are configured.",
            "no_nearby_sites" => "No hunt sites nearby. Move closer to a site.",
            "no_permission" => "Enable precise location to find the hunt.",
            _ => "Hunt location is unavailable."
        };
    }

    // =====================================================
    // DYNAMIC MONSTER NOTIFICATION
    // =====================================================

    public void SetMonsterSpawnName(string monsterName)
    {
        if (string.IsNullOrWhiteSpace(monsterName))
        {
            Debug.LogWarning(
                "Monster name is empty. Keeping previous name."
            );
            return;
        }

        currentMonsterName = monsterName.Trim();

        // A new spawn alone does not select the intro picture.
        ClearIntroPicture();
        UpdateMonsterNotificationText();
        UpdateMonsterIntroContent();

        Debug.Log(
            "MONSTER NOTIFICATION UPDATED: " +
            currentMonsterName
        );
    }

    private void UpdateMonsterNotificationText()
    {
        if (monsterSpawnText != null)
        {
            monsterSpawnText.text =
                currentMonsterName + " has spawned!";
        }
    }

    private void UpdateMonsterIntroContent()
    {
        // Use the same monster selection for every part of the intro.
        bool isOmElSaaf =
            currentMonsterName.Equals("Om El-Saaf", System.StringComparison.OrdinalIgnoreCase) ||
            currentMonsterName.Equals("Om Elsaaf", System.StringComparison.OrdinalIgnoreCase);

        // 1. TITLE - keep the existing Bu Daryah title format.
        if (monsterIntroText != null)
        {
            if (string.IsNullOrEmpty(originalIntroTemplate))
                originalIntroTemplate = monsterIntroText.text;

            string title = originalIntroTemplate
                .Replace("Bu Daryah", currentMonsterName)
                .Replace("Bu Darya", currentMonsterName);

            if (title == originalIntroTemplate)
                title = currentMonsterName;

            monsterIntroText.text = title;
        }

        // 2. DESCRIPTION - preserve the existing Bu Daryah description.
        if (monsterIntroDescriptionText != null)
        {
            if (originalBuDaryaDescription == null)
                originalBuDaryaDescription = monsterIntroDescriptionText.text;

            monsterIntroDescriptionText.text = isOmElSaaf
                ? omElSaafDescription
                : originalBuDaryaDescription;
        }

        // 3D preview selection is deliberately NOT performed here.
        // This method also runs when a monster spawns, before Hunt is chosen.
        // ShowIntroPreviewForCurrentMonster() activates the model on Hunt.
    }

    // This affects only the picture Image; it does not change the rotating
    // 3D preview, monster intro wording, theme controller, or hunt mechanics.
    private void SetIntroPictureForCurrentMonster()
    {
        if (monsterIntroPicture == null)
            return;

        string key = (currentMonsterName ?? string.Empty)
            .Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);

        Sprite selected = null;
        switch (key)
        {
            case "budarya":
            case "budaryah":
                selected = buDaryahIntroPicture;
                break;

            case "omelsaaf":
            case "umelsaaf":
                selected = omElsaafIntroPicture;
                break;
        }

        // Unsupported monsters and unassigned pictures remain blank.
        monsterIntroPicture.sprite = selected;
        monsterIntroPicture.enabled = selected != null;
    }

    private void ClearIntroPicture()
    {
        if (monsterIntroPicture == null)
            return;

        monsterIntroPicture.sprite = null;
        monsterIntroPicture.enabled = false;
    }

    public void ShowMonsterNotification()
    {
        StopCurrentSequence();
        StopWarning();
        StopCrystalFill();
        HideAllPreviewModels();

        // A monster notification is not yet an accepted hunt.
        if (themeController != null)
            themeController.ShowNoTheme();

        ClearIntroPicture();

        // Refresh the text before showing the panel.
        UpdateMonsterNotificationText();

        SetVisible(notificationPanel, true);
        SetVisible(monsterIntroPanel, false);
        SetVisible(warningPanel, false);
        SetVisible(distancePanel, false);
        SetVisible(caughtMonsterPanel, false);
        SetVisible(gameplayRoot, false);
    }

    public void AcceptHunt()
    {
        if (followingOffer)
        {
            offerAccepted = true;
            RefreshEmbeddedOffer();
            return;
        }
        StopCurrentSequence();

        // The player has explicitly chosen Hunt; switch to that monster's theme.
        ApplyThemeForCurrentMonster();

        huntSequence = StartCoroutine(
            BeginHuntSequence()
        );
    }

    public void IgnoreHunt()
    {
        if (followingOffer && huntRewards != null)
        {
            huntRewards.ReturnToApp();
            return;
        }
        StopCurrentSequence();
        StopWarning();
        StopCrystalFill();
        HideAllPreviewModels();

        if (themeController != null)
            themeController.IgnoreMonster();

        ClearIntroPicture();

        SetVisible(notificationPanel, false);
        SetVisible(monsterIntroPanel, false);
        SetVisible(warningPanel, false);
        SetVisible(distancePanel, false);
        SetVisible(caughtMonsterPanel, false);
        SetVisible(gameplayRoot, false);
    }

    // Uses the same monster name already provided by the existing spawn flow.
    // Only one theme is displayed by MasARThemeController at any time.
    private void ApplyThemeForCurrentMonster()
    {
        if (themeController == null)
        {
            Debug.LogWarning("MonsterHuntUIController: Assign ThemeCanvas in Hunt Theme Switching.", this);
            return;
        }

        string name = (currentMonsterName ?? string.Empty)
            .Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);

        switch (name)
        {
            case "budarya":
            case "budaryah":
                themeController.BeginBuDaryahHunt();
                break;

            case "omelsaaf":
            case "umelsaaf":
                themeController.BeginOmElsaafHunt();
                break;

            case "tantal":
                themeController.BeginTantalHunt();
                break;

            default:
                themeController.ShowNoTheme();
                Debug.LogWarning("MonsterHuntUIController: No theme configured for '" +
                                 currentMonsterName + "'.", this);
                break;
        }
    }

    private IEnumerator BeginHuntSequence()
    {
        UpdateMonsterIntroContent();
        SetIntroPictureForCurrentMonster();
        ShowIntroPreviewForCurrentMonster();
        SetVisible(notificationPanel, false);
        SetVisible(monsterIntroPanel, true);
        SetVisible(warningPanel, false);
        SetVisible(caughtMonsterPanel, false);

        yield return new WaitForSeconds(introDuration);

        SetVisible(monsterIntroPanel, false);
        SetVisible(gameplayRoot, true);
        SetVisible(distancePanel, true);

        huntSequence = null;
    }

    public void ShowWrongDirectionWarning()
    {
        if (warningRoutine != null)
            return;

        warningRoutine = StartCoroutine(
            WrongDirectionWarningRoutine()
        );
    }

    public bool IsWarningShowing()
    {
        return warningRoutine != null;
    }

    private IEnumerator WrongDirectionWarningRoutine()
    {
        if (!flashWarning)
        {
            SetVisible(warningPanel, true);

            yield return new WaitForSeconds(
                warningDuration
            );
        }
        else
        {
            float elapsed = 0f;

            while (elapsed < warningDuration)
            {
                if (warningPanel != null)
                {
                    SetVisible(
                        warningPanel,
                        !warningPanel.activeSelf
                    );
                }

                yield return new WaitForSeconds(
                    flashInterval
                );

                elapsed += flashInterval;
            }
        }

        SetVisible(warningPanel, false);

        warningRoutine = null;
    }

    // =====================================================
    // SHARED 3D PREVIEW: 2 MONSTERS + 2 REWARD ITEMS
    // All four objects are siblings under MonsterPreviewStage.
    // They are rendered by the PRE-EXISTING camera and its single
    // MonsterPreviewTexture. This controller only switches model visibility.
    // =====================================================

    private static string MonsterKey(string name)
    {
        return (name ?? string.Empty)
            .Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);
    }

    private void HideAllPreviewModels()
    {
        if (buDaryaPreview != null) buDaryaPreview.SetActive(false);
        if (omElSaafPreview != null) omElSaafPreview.SetActive(false);
        if (buDaryahRewardItem != null) buDaryahRewardItem.SetActive(false);
        if (omElsaafRewardItem != null) omElsaafRewardItem.SetActive(false);
    }

    private void ActivatePreviewModel(GameObject selected)
    {
        HideAllPreviewModels();

        if (selected == null)
        {
            Debug.LogWarning("MonsterHuntUIController: Preview model not assigned.", this);
            return;
        }

        selected.SetActive(true);

        // Keep the existing auto-framer, just point it to the selected object.
        if (monsterPreviewFramer != null)
            monsterPreviewFramer.monsterRoot = selected.transform;
    }

    private void ShowIntroPreviewForCurrentMonster()
    {
        GameObject selected = null;
        switch (MonsterKey(currentMonsterName))
        {
            case "budarya":
            case "budaryah":
                selected = buDaryaPreview;
                break;
            case "omelsaaf":
            case "umelsaaf":
                selected = omElSaafPreview;
                break;
        }

        ActivatePreviewModel(selected);
    }

    private void ShowRewardItemForMonster(string capturedMonsterName)
    {
        GameObject selected = null;
        switch (MonsterKey(capturedMonsterName))
        {
            case "budarya":
            case "budaryah":
                selected = buDaryahRewardItem;
                break;
            case "omelsaaf":
            case "umelsaaf":
                selected = omElsaafRewardItem;
                break;
        }

        // We use the CAPTURED monster name, not the next spawned monster.
        ActivatePreviewModel(selected);
    }

    // Preserve existing Bu Daryah callers while supporting Om El-Saaf.
    public void ShowCaughtBuDaryah(
        int crystalsEarned,
        int previousCrystals,
        int newCrystals)
    {
        ShowCaughtMonster("Bu Darya", crystalsEarned, previousCrystals, newCrystals);
    }

    public void ShowCaughtMonster(
        string monsterName,
        int crystalsEarned,
        int previousCrystals,
        int newCrystals)
    {
        StopCurrentSequence();
        StopWarning();
        StopCrystalFill();

        // The hunt has ended; return to the neutral HUD theme.
        if (themeController != null)
            themeController.EndHunt();

        ClearIntroPicture();

        SetVisible(notificationPanel, false);
        SetVisible(monsterIntroPanel, false);
        SetVisible(warningPanel, false);
        SetVisible(distancePanel, false);

        if (caughtMonsterPanel == null)
        {
            Debug.LogError(
                "CaughtMonsterPanel is NOT assigned!"
            );

            return;
        }

        // Select the 3D reward item BEFORE the panel becomes visible.
        // Use the captured monster name passed by the hunt system, not the
        // name that might already belong to the next spawning monster.
        ShowRewardItemForMonster(monsterName);

        // Preserve the existing draw order.
        caughtMonsterPanel.transform.SetAsLastSibling();

        // Show with upward slide animation.
        SetVisible(caughtMonsterPanel, true);

        if (caughtTitleText != null)
        {
            caughtTitleText.text =
                "Hooray! You caught " + monsterName + "!";
        }

        if (caughtRewardsText != null)
        {
            caughtRewardsText.text =
                "Rewards: +" +
                crystalsEarned +
                " Crystals";
        }

        if (crystalBar != null)
        {
            crystalBar.minValue = 0;
            crystalBar.maxValue = Mathf.Max(100, newCrystals);
            crystalBar.value = previousCrystals;
        }

        if (crystalBarCountText != null)
        {
            crystalBarCountText.text = previousCrystals.ToString();
        }

        // Preserve the existing crystal animation.
        crystalFillRoutine = StartCoroutine(
            AnimateCrystalBar(
                previousCrystals,
                newCrystals
            )
        );
    }

    void SetupCrystalBar(int crystalsEarned)
    {
        if (crystalBar != null)
        {
            crystalBar.minValue = 0f;
            crystalBar.maxValue = crystalsEarned;
            crystalBar.value = 0f;
        }

        if (crystalBarCountText != null)
        {
            crystalBarCountText.text =
                "0 / " + crystalsEarned;
        }
    }

    private IEnumerator AnimateCrystalBar(
        int startCrystals,
        int endCrystals)
    {
        if (crystalBar == null)
            yield break;

        float elapsed = 0f;

        while (elapsed < crystalFillDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / crystalFillDuration
            );

            float currentValue = Mathf.Lerp(
                startCrystals,
                endCrystals,
                progress
            );

            crystalBar.value = currentValue;

            if (crystalBarCountText != null)
            {
                crystalBarCountText.text =
                    Mathf.RoundToInt(currentValue).ToString();
            }

            yield return null;
        }

        crystalBar.value = endCrystals;

        if (crystalBarCountText != null)
        {
            crystalBarCountText.text =
                endCrystals.ToString();
        }

        crystalFillRoutine = null;
    }

    void StopCrystalFill()
    {
        if (crystalFillRoutine != null)
        {
            StopCoroutine(crystalFillRoutine);
            crystalFillRoutine = null;
        }
    }

    private void StopWarning()
    {
        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
            warningRoutine = null;
        }

        SetVisible(warningPanel, false);
    }

    private void StopCurrentSequence()
    {
        if (huntSequence != null)
        {
            StopCoroutine(huntSequence);
            huntSequence = null;
        }
    }

    // =====================================================
    // ANIMATED PANEL VISIBILITY
    // =====================================================

    private void SetVisible(GameObject target, bool visible)
    {
        if (target == null)
            return;

        UIPanelAnimator animator =
            target.GetComponent<UIPanelAnimator>();

        if (animator != null)
        {
            if (visible)
                animator.Show();
            else
                animator.Hide();

            return;
        }

        // Objects without an animator keep old behavior.
        target.SetActive(visible);
    }

    private void SetVisibleImmediately(
        GameObject target,
        bool visible)
    {
        if (target == null)
            return;

        UIPanelAnimator animator =
            target.GetComponent<UIPanelAnimator>();

        if (!visible && animator != null)
        {
            animator.HideImmediately();
        }
        else
        {
            target.SetActive(visible);
        }
    }
}
