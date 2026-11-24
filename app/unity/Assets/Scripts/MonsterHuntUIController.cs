using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the monster hunt interface in MasAR+.
///
/// A monster first appears through a spawn notification. The player can
/// accept the hunt to see its introduction, picture, and 3D preview,
/// or ignore it. After the introduction, the gameplay objects are enabled.
///
/// This controller also chooses monsters for local hunts, makes sure
/// the same monster is not selected twice in a row, and shows the
/// correct HUD theme for Bu Daryah, Om El-Saaf, or Tantal.
///
/// When a monster is captured, it displays the reward panel, switches
/// to that monster's reward item, and animates the crystal counter.
/// It also supports hunts selected by the mobile app.
/// </summary>
public class MonsterHuntUIController : MonoBehaviour
{
    // References to the panels shown at different points of the hunt.
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

    // Each monster has its own instructions, shown after the player accepts the hunt.
    [Header("Monster Intro - Title and Description")]
    [Tooltip("Assign MonsterIntroPanel > MonsterNameText.")]
    public TMP_Text monsterIntroText;
    [Tooltip("Assign MonsterIntroPanel > MonsterDescriptionText.")]
    public TMP_Text monsterIntroDescriptionText;
    [TextArea(2, 5)]
    [SerializeField]
    private string omElSaafDescription =
        "Collect five palm fronds to reveal Om El-Saaf!";

    [TextArea(2, 5)]
    [SerializeField]
    private string tantalDescription =
        "Follow the shadow trails. Only one path is real! Find Tantal before he disappears.";

    // These are the 3D models shown in the intro, separate from the gameplay models.
    [Header("Monster Intro - Rotating Preview")]
    [Tooltip("Scene preview model for Bu Daryah, NOT the gameplay monster.")]
    [SerializeField] private GameObject buDaryaPreview;
    [Tooltip("Scene preview model for Om El-Saaf, NOT the gameplay monster.")]
    [SerializeField] private GameObject omElSaafPreview;
    [Tooltip("Scene preview model for Tantal, NOT the gameplay monster.")]
    [SerializeField] private GameObject tantalPreview;
    [Tooltip("Optional existing MonsterPreviewFramer component, if your preview setup uses one. Leave empty otherwise.")]
    [SerializeField] private MonsterPreviewFramer monsterPreviewFramer;

    // Background pictures used for each monster introduction.
    [Header("Monster Intro - Dynamic Picture")]
    [Tooltip("Assign the UI Image to change on MonsterIntroPanel. Use a dedicated child Image if the panel background should remain visible.")]
    [SerializeField] private Image monsterIntroPicture;
    [Tooltip("Picture shown when the player accepts Bu Daryah's hunt.")]
    [SerializeField] private Sprite buDaryahIntroPicture;
    [Tooltip("Picture shown when the player accepts Om El-Saaf's hunt.")]
    [SerializeField] private Sprite omElsaafIntroPicture;
    [Tooltip("Picture shown when the player accepts Tantal's hunt.")]
    [SerializeField] private Sprite tantalIntroPicture;
    [SerializeField] private HamaretHuntController hamaret;
    [SerializeField] private Sprite hamaretPanelArt;

    // Keep the starting UI text so it can be reused with different monster names.
    private string originalIntroTemplate;
    private string originalBuDaryaDescription;

    [SerializeField]
    private string currentMonsterName = "Bu Daryah";

    [Header("Caught Panel UI")]
    public TMP_Text caughtTitleText;
    public TMP_Text caughtRewardsText;
    public Slider crystalBar;
    public TMP_Text crystalBarCountText;

    // The reward panel shows an item related to the monster that was captured.
    [Header("Caught Panel - Rotating Reward Item")]
    [Tooltip("Drag oudPreview from MonsterPreviewStage (scene object, not FBX asset).")]
    [SerializeField] private GameObject buDaryahRewardItem;
    [Tooltip("Drag saafPreview from MonsterPreviewStage (scene object, not FBX asset).")]
    [SerializeField] private GameObject omElsaafRewardItem;
    [Tooltip("Drag Tantal's robe/reward preview from MonsterPreviewStage when it is ready.")]
    [SerializeField] private GameObject tantalRewardItem;

    // Tantal can be included in local random hunts from the Inspector.
    [Header("Monster Selection")]
    [Tooltip("Enable only after HuntRewardActions and the actual Tantal hunt flow support the 'tantal' monster ID. When off, keep the existing 2-monster random selection.")]
    [SerializeField] private bool includeTantalInRandomHunts = false;

    // Remember the previous spawn between scene loads using PlayerPrefs.
    private const string LastSpawnedMonsterPref = "MasAR_LastSpawnedMonster";
    // Available monsters when Tantal is disabled or enabled.
    private static readonly string[] TwoMonsterIds = { "bu_daryah", "om_el_saaf" };
    private static readonly string[] ThreeMonsterIds = { "bu_daryah", "om_el_saaf", "tantal", "hamaret_al_qayla" };

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

    // Keep references to running animations so they can be stopped safely.
    private Coroutine huntSequence;
    private Coroutine warningRoutine;
    private Coroutine crystalFillRoutine;
    private HuntRewardActions huntRewards;
    // Tracks whether the player is following an offer from the mobile app.
    private bool followingOffer;
    private bool acceptingHunt;
    private float locationLoadingStarted;
    private HuntLoadingScreen loadingScreen;
    private float nextOfferUpdate;

    /// <summary>
    /// Saves the original introduction text and starts with all previews hidden.
    /// </summary>
    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (notificationPanel != null)
        {
            foreach (Button button in notificationPanel.GetComponentsInChildren<Button>(true))
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (button.onClick.GetPersistentTarget(i) == this &&
                        button.onClick.GetPersistentMethodName(i) == nameof(AcceptHunt))
                        button.gameObject.AddComponent<XrealHuntPressHandler>().huntUI = this;
                }
            }
        }
#endif

        if (monsterIntroText != null)
            originalIntroTemplate = monsterIntroText.text;
        if (monsterIntroDescriptionText != null)
            originalBuDaryaDescription = monsterIntroDescriptionText.text;

        ClearIntroPicture();
        HideAllPreviewModels();
    }

    /// <summary>
    /// Prepares the UI and starts either a local monster spawn or a mobile-app hunt.
    /// </summary>
    void Start()
    {

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
        // Hunts sent by the mobile app use the monster assigned to their location.
        if (EmbeddedHunt.Active)
            ShowEmbeddedOffer();
        else
        {

            SpawnNextRandomMonster();
        }
    }

    /// <summary>
    /// Selects a random monster for a local hunt, excluding the previous spawn.
    /// The mobile app handles its own monster selections.
    /// </summary>
    public void SpawnNextRandomMonster()
    {
        if (huntRewards == null)
            huntRewards = GetComponent<HuntRewardActions>();

        if (huntRewards == null)
        {
            Debug.LogError("MonsterHuntUIController: HuntRewardActions is missing; cannot select a random monster.", this);
            return;
        }

        // Hunts sent by the mobile app use the monster assigned to their location.
        if (EmbeddedHunt.Active)
        {
            Debug.LogWarning("MonsterHuntUIController: This is an embedded location hunt. The external app chooses its monster; local random respawning is disabled.", this);
            return;
        }

        // Pick the monster first, then activate its corresponding gameplay object.
        string nextId = ChooseRandomMonsterExceptPrevious();
        string selectedName = huntRewards.SelectMonster(nextId);
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            Debug.LogWarning("MonsterHuntUIController: HuntRewardActions returned no monster for " + nextId, this);
            return;
        }

        followingOffer = false;
        SetMonsterSpawnName(selectedName);
        ShowMonsterNotification();
    }

    /// <summary>
    /// Picks a monster from the enabled list without repeating the last one.
    /// </summary>
    private string ChooseRandomMonsterExceptPrevious()
    {
        string[] available = includeTantalInRandomHunts ? ThreeMonsterIds : TwoMonsterIds;
        // Read the last spawned monster, even if Unity has been restarted.
        string previous = PlayerPrefs.GetString(LastSpawnedMonsterPref, string.Empty);

        // Count the choices that are not the previous monster.
        int validCount = 0;
        foreach (string id in available)
            if (available.Length == 1 || id != previous) validCount++;

        // Pick one of the remaining choices with equal probability.
        int chosenIndex = Random.Range(0, validCount);
        foreach (string id in available)
        {
            if (available.Length > 1 && id == previous) continue;
            if (chosenIndex-- == 0) return id;
        }

        return available[0];
    }

    /// <summary>
    /// Converts the monster's display name to the ID used by the hunt system.
    /// </summary>
    private static string CanonicalMonsterId(string name)
    {
        switch (MonsterKey(name))
        {
            case "budarya":
            case "budaryah": return "bu_daryah";
            case "omelsaaf":
            case "umelsaaf": return "om_el_saaf";
            case "tantal": return "tantal";
            case "hamaretalqayla": return HamaretHuntController.MonsterId;
            default: return string.Empty;
        }
    }

    /// <summary>
    /// Opens the hunt notification for a location-based hunt from the mobile app.
    /// </summary>
    public void ShowEmbeddedOffer()
    {
        acceptingHunt = false;
        followingOffer = true;
        locationLoadingStarted = Time.unscaledTime;
        StopCurrentSequence();
        StopWarning();
        StopCrystalFill();
        HideAllPreviewModels();
        themeController?.ShowNoTheme();
        foreach (GameObject panel in new[] { notificationPanel, monsterIntroPanel, warningPanel,
            distancePanel, caughtMonsterPanel, gameplayRoot }) SetVisibleImmediately(panel, false);
        if (loadingScreen == null) loadingScreen = gameObject.AddComponent<HuntLoadingScreen>();
        loadingScreen.Show();
        RefreshEmbeddedOffer();
    }

    /// <summary>
    /// Refreshes the mobile-app hunt information while the offer is active.
    /// </summary>
    private void Update()
    {
        if (followingOffer && Time.unscaledTime >= nextOfferUpdate)
            RefreshEmbeddedOffer();
    }

    /// <summary>
    /// Reads the current hunt location and updates the notification.
    /// The normal hunt offer appears once a destination has been selected.
    /// </summary>
    private void RefreshEmbeddedOffer()
    {
        // Check the mobile-app hunt state once per second.
        nextOfferUpdate = Time.unscaledTime + 1f;
        EmbeddedHunt.State state = EmbeddedHunt.ReadState();

        if ((state.status == "arrived" || state.status == "en_route") && huntRewards != null)
        {
            HuntLocationGuide.Begin(Camera.main != null ? Camera.main.transform : transform, state);
            string selectedName = huntRewards.SelectMonster(state.monsterId);
            if (string.IsNullOrWhiteSpace(selectedName))
            {
                followingOffer = false;
                loadingScreen.Hide();
                EmbeddedHunt.ShowLocationError("The selected monster could not start. Please try again.");
                huntRewards.ReturnToApp();
                return;
            }
            followingOffer = false;
            loadingScreen.Hide();
            SetMonsterSpawnName(selectedName);
            ShowMonsterNotification();
            return;
        }

        string error = state.status switch
        {
            "gps_off" => "Turn on your phone's location and try again.",
            "no_sites" => "No monster locations are configured yet.",
            "no_nearby_sites" => "No monster locations are within the configured walking range.",
            "no_permission" => "Enable precise location for MasAR+ and try again.",
            "catalog_error" => "The hunt locations could not be loaded. Please try again.",
            _ => null
        };
        if (error == null && Time.unscaledTime - locationLoadingStarted >= 45f)
            error = "Could not get an accurate location. Move outdoors and try again.";
        if (error != null)
        {
            followingOffer = false;
            loadingScreen.Hide();
            EmbeddedHunt.ShowLocationError(error);
            huntRewards?.ReturnToApp();
        }
    }

    /// <summary>
    /// Stores the spawned monster's name and updates the notification and intro text.
    /// Also saves its ID so the next random spawn can avoid repeating it.
    /// </summary>
    public void SetMonsterSpawnName(string monsterName)
    {
        if (string.IsNullOrWhiteSpace(monsterName))
        {
            Debug.LogWarning(
                "Monster name is empty. Keeping previous name."
            );
            return;
        }

        // Save the selected monster and its ID for the next random spawn.
        currentMonsterName = monsterName.Trim();

        string spawnedId = CanonicalMonsterId(currentMonsterName);
        if (!string.IsNullOrEmpty(spawnedId))
        {
            PlayerPrefs.SetString(LastSpawnedMonsterPref, spawnedId);
            PlayerPrefs.Save();
        }

        ClearIntroPicture();
        UpdateMonsterNotificationText();
        UpdateMonsterIntroContent();

        Debug.Log(
            "MONSTER NOTIFICATION UPDATED: " +
            currentMonsterName
        );
    }

    /// <summary>
    /// Shows the selected monster's name in the spawn notification.
    /// </summary>
    private void UpdateMonsterNotificationText()
    {
        if (monsterSpawnText != null)
        {
            monsterSpawnText.text =
                currentMonsterName + " has spawned!";
        }
    }

    /// <summary>
    /// Updates the introduction title and description for the selected monster.
    /// </summary>
    private void UpdateMonsterIntroContent()
    {

        // Use one consistent key when choosing the intro text.
        string key = MonsterKey(currentMonsterName);
        bool isOmElSaaf = key == "omelsaaf" || key == "umelsaaf";
        bool isTantal = key == "tantal";

        if (monsterIntroText != null)
        {
            if (string.IsNullOrEmpty(originalIntroTemplate))
                originalIntroTemplate = monsterIntroText.text;

            // Replace the default monster name while keeping the title layout.
            string title = originalIntroTemplate
                .Replace("Bu Daryah", currentMonsterName)
                .Replace("Bu Darya", currentMonsterName);

            if (title == originalIntroTemplate)
                title = currentMonsterName;

            monsterIntroText.text = title;
        }

        if (monsterIntroDescriptionText != null)
        {
            if (originalBuDaryaDescription == null)
                originalBuDaryaDescription = monsterIntroDescriptionText.text;

            // Pick the instructions that belong to this monster.
            var omHunt = isOmElSaaf && HuntLocationGuide.Active
                ? FindFirstObjectByType<OmElSaafHuntController>(FindObjectsInactive.Include)
                : null;
            monsterIntroDescriptionText.text = key == "hamaretalqayla"
                ? "Walk to claim territory and take back the Freej!"
                : isTantal
                ? tantalDescription
                : isOmElSaaf
                    ? (omHunt != null
                        ? $"Collect {omHunt.RequiredLeafCount} palm fronds to reveal Om El-Saaf!"
                        : omElSaafDescription)
                    : originalBuDaryaDescription;
        }

    }

    /// <summary>
    /// Selects the introduction background picture for the current monster.
    /// </summary>
    private void SetIntroPictureForCurrentMonster()
    {
        if (monsterIntroPicture == null)
            return;

        string key = (currentMonsterName ?? string.Empty)
            .Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);

        // Choose a picture only when the monster has one assigned.
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

            case "tantal":
                selected = tantalIntroPicture;
                break;
            case "hamaretalqayla":
                selected = hamaretPanelArt;
                break;
        }

        monsterIntroPicture.sprite = selected;
        // Hide the Image component if no picture is available.
        monsterIntroPicture.enabled = selected != null;
    }

    /// <summary>
    /// Clears the introduction picture when no hunt is being introduced.
    /// </summary>
    private void ClearIntroPicture()
    {
        if (monsterIntroPicture == null)
            return;

        monsterIntroPicture.sprite = null;
        monsterIntroPicture.enabled = false;
    }

    /// <summary>
    /// Shows a new monster offer while hiding the intro, gameplay, and rewards.
    /// </summary>
    public void ShowMonsterNotification()
    {
        acceptingHunt = false;
        StopCurrentSequence();
        StopWarning();
        StopCrystalFill();
        HideAllPreviewModels();

        if (themeController != null)
            themeController.ShowNoTheme();

        ClearIntroPicture();

        UpdateMonsterNotificationText();

        SetVisible(notificationPanel, true);
        SetVisible(monsterIntroPanel, false);
        SetVisible(warningPanel, false);
        SetVisible(distancePanel, false);
        SetVisible(caughtMonsterPanel, false);
        SetVisible(gameplayRoot, false);
    }

    /// <summary>
    /// Handles the Hunt button and starts the selected monster's introduction.
    /// </summary>
    public void AcceptHunt()
    {
        if (followingOffer || acceptingHunt)
        {
            return;
        }
        acceptingHunt = true;
        Debug.Log("MonsterHuntUIController: Hunt accepted for " + currentMonsterName);
        StopCurrentSequence();

        // Set the matching HUD theme for the selected monster.
        ApplyThemeForCurrentMonster();

        huntSequence = StartCoroutine(
            BeginHuntSequence()
        );
    }

    /// <summary>
    /// Handles the Ignore button and clears the current hunt UI.
    /// </summary>
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

    /// <summary>
    /// Activates the selected monster's HUD theme.
    /// </summary>
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
            case "hamaretalqayla":
                themeController.BeginHamaretHunt();
                break;

            default:
                themeController.ShowNoTheme();
                Debug.LogWarning("MonsterHuntUIController: No theme configured for '" +
                                 currentMonsterName + "'.", this);
                break;
        }
    }

    /// <summary>
    /// Shows the monster introduction for a few seconds, then starts gameplay.
    /// </summary>
    private IEnumerator BeginHuntSequence()
    {
        UpdateMonsterIntroContent();
        SetIntroPictureForCurrentMonster();
        ShowIntroPreviewForCurrentMonster();
        SetVisible(notificationPanel, false);
        SetVisible(monsterIntroPanel, true);
        SetVisible(warningPanel, false);
        SetVisible(caughtMonsterPanel, false);

        // Keep the intro on screen before activating the gameplay objects.
        yield return new WaitForSeconds(introDuration);

        SetVisible(monsterIntroPanel, false);
        SetVisible(gameplayRoot, true);
        SetVisible(distancePanel, true);

        huntSequence = null;
    }

    /// <summary>
    /// Starts the wrong-direction warning if one is not already showing.
    /// </summary>
    public void ShowWrongDirectionWarning()
    {
        // Avoid starting the same warning twice.
        if (warningRoutine != null)
            return;

        warningRoutine = StartCoroutine(
            WrongDirectionWarningRoutine()
        );
    }

    /// <summary>
    /// Checks whether the wrong-direction warning is currently active.
    /// </summary>
    public bool IsWarningShowing()
    {
        return warningRoutine != null;
    }

    /// <summary>
    /// Displays the wrong-direction warning for the selected duration.
    /// It can remain visible or flash, depending on the Inspector setting.
    /// </summary>
    private IEnumerator WrongDirectionWarningRoutine()
    {
        // Show a steady warning or a flashing one depending on the settings.
        if (!flashWarning)
        {
            SetVisible(warningPanel, true);

            yield return new WaitForSeconds(
                warningDuration
            );
        }
        else
        {
            // Track how long the warning or animation has been running.
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

    /// <summary>
    /// Standardizes a monster name so different spacing and spellings can be checked.
    /// </summary>
    private static string MonsterKey(string name)
    {
        return (name ?? string.Empty)
            .Trim().ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty);
    }

    /// <summary>
    /// Returns the name shown to the player for a monster ID.
    /// </summary>
    private static string DisplayNameFromMonsterId(string id)
    {
        switch (MonsterKey(id))
        {
            case "tantal": return "Tantal";
            case "hamaretalqayla": return HamaretHuntController.LegendName;
            case "omelsaaf":
            case "umelsaaf": return "Om El-Saaf";
            default: return "Bu Daryah";
        }
    }

    /// <summary>
    /// Hides all monster and reward models in the shared preview area.
    /// </summary>
    private void HideAllPreviewModels()
    {
        if (buDaryaPreview != null) buDaryaPreview.SetActive(false);
        if (omElSaafPreview != null) omElSaafPreview.SetActive(false);
        if (tantalPreview != null) tantalPreview.SetActive(false);
        if (buDaryahRewardItem != null) buDaryahRewardItem.SetActive(false);
        if (omElsaafRewardItem != null) omElsaafRewardItem.SetActive(false);
        if (tantalRewardItem != null) tantalRewardItem.SetActive(false);
        if (hamaret != null) hamaret.HidePanelPreviews();
    }

    /// <summary>
    /// Displays one model and tells the preview camera which object to frame.
    /// </summary>
    private void ActivatePreviewModel(GameObject selected)
    {
        HideAllPreviewModels();

        if (selected == null)
        {
            Debug.LogWarning("MonsterHuntUIController: Preview model not assigned.", this);
            return;
        }

        // Only one preview model should be visible at a time.
        selected.SetActive(true);

        if (monsterPreviewFramer != null)
            // Frame the active model using the shared preview camera.
            monsterPreviewFramer.monsterRoot = selected.transform;
    }

    /// <summary>
    /// Selects the 3D monster model used in the introduction panel.
    /// </summary>
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
            case "tantal":
                selected = tantalPreview;
                break;
            case "hamaretalqayla":
                selected = hamaret != null ? hamaret.GetPanelPreview(false) : null;
                break;
        }

        ActivatePreviewModel(selected);
    }

    /// <summary>
    /// Selects the 3D reward item belonging to the captured monster.
    /// </summary>
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
            case "tantal":
                selected = tantalRewardItem;
                break;
            case "hamaretalqayla":
                selected = hamaret != null ? hamaret.GetPanelPreview(true) : null;
                break;
        }

        ActivatePreviewModel(selected);
    }

    /// <summary>
    /// Opens the capture panel for Bu Daryah.
    /// </summary>
    public void ShowCaughtBuDaryah(
        int crystalsEarned,
        int previousCrystals,
        int newCrystals)
    {
        ShowCaughtMonster("Bu Darya", crystalsEarned, previousCrystals, newCrystals);
    }

    /// <summary>
    /// Opens the capture panel for Tantal.
    /// </summary>
    public void ShowCaughtTantal(int crystalsEarned, int previousCrystals, int newCrystals)
    {
        ShowCaughtMonster("Tantal", crystalsEarned, previousCrystals, newCrystals);
    }

    internal void ShowHamaretDefeat(bool tied = false)
    {
        int crystals = CrystalManager.Instance != null ? CrystalManager.Instance.CurrentCrystals : 0;
        ShowCaughtMonster(HamaretHuntController.LegendName, 0, crystals, crystals);
        if (caughtTitleText != null) caughtTitleText.text = tied ? "It’s a draw!" : HamaretHuntController.LegendName + " wins";
        if (caughtRewardsText != null) caughtRewardsText.text = "Try a wider loop.";
        hamaret?.HidePanelPreviews();
    }

    /// <summary>
    /// Displays the captured monster, its reward item, and the earned crystals.
    /// </summary>
    public void ShowCaughtMonster(
        string monsterName,
        int crystalsEarned,
        int previousCrystals,
        int newCrystals)
    {
        StopCurrentSequence();
        StopWarning();
        StopCrystalFill();

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

        // Select the item from the captured monster, not a newly spawned one.
        ShowRewardItemForMonster(monsterName);

        // Bring the reward panel to the front of the UI.
        caughtMonsterPanel.transform.SetAsLastSibling();

        SetVisible(caughtMonsterPanel, true);

        if (caughtTitleText != null)
        {
            caughtTitleText.text =
                monsterName == HamaretHuntController.LegendName ? "Congrats! The Freej is yours!" : "Hooray! You caught " + monsterName + "!";
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
            // Start the animation at the crystal total before this reward.
            crystalBar.value = previousCrystals;
        }

        if (crystalBarCountText != null)
        {
            crystalBarCountText.text = previousCrystals.ToString();
        }

        crystalFillRoutine = StartCoroutine(
            AnimateCrystalBar(
                previousCrystals,
                newCrystals
            )
        );
    }

    /// <summary>
    /// Sets the crystal bar's initial values.
    /// </summary>
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

    /// <summary>
    /// Gradually fills the crystal bar from the previous total to the new total.
    /// </summary>
    private IEnumerator AnimateCrystalBar(
        int startCrystals,
        int endCrystals)
    {
        if (crystalBar == null)
            yield break;

        // Track how long the warning or animation has been running.
        float elapsed = 0f;

        while (elapsed < crystalFillDuration)
        {
            elapsed += Time.deltaTime;

            // Calculate the percentage of the crystal animation that has completed.
            float progress = Mathf.Clamp01(
                elapsed / crystalFillDuration
            );

            // Gradually increase the visible crystal total.
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

        // Mark the animation as finished.
        crystalFillRoutine = null;
    }

    /// <summary>
    /// Stops an active crystal-bar animation.
    /// </summary>
    void StopCrystalFill()
    {
        if (crystalFillRoutine != null)
        {
            StopCoroutine(crystalFillRoutine);
            // Mark the animation as finished.
            crystalFillRoutine = null;
        }
    }

    /// <summary>
    /// Stops the warning routine and hides its panel.
    /// </summary>
    private void StopWarning()
    {
        // Avoid starting the same warning twice.
        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
            warningRoutine = null;
        }

        SetVisible(warningPanel, false);
    }

    /// <summary>
    /// Stops the current introduction or hunt-start sequence.
    /// </summary>
    private void StopCurrentSequence()
    {
        if (huntSequence != null)
        {
            StopCoroutine(huntSequence);
            huntSequence = null;
        }
    }

    /// <summary>
    /// Shows or hides a panel using its animation when one is available.
    /// </summary>
    private void SetVisible(GameObject target, bool visible)
    {
        if (target == null)
            return;

        // Use the panel animator when the object has one.
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

        target.SetActive(visible);
    }

    /// <summary>
    /// Changes panel visibility without waiting for a hide animation.
    /// </summary>
    private void SetVisibleImmediately(
        GameObject target,
        bool visible)
    {
        if (target == null)
            return;

        // Use the panel animator when the object has one.
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
