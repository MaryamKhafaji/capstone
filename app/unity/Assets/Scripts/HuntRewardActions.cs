
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

public class HuntRewardActions : MonoBehaviour
{
    [Header("Monster Hunt Roots")]
    [SerializeField] private GameObject buDaryaHunt;
    [SerializeField] private GameObject omElSaafHunt;
    [SerializeField] private GameObject tantalHunt;
    [SerializeField] private GameObject hamaretHunt;

    [Serializable]
    private class HuntRecord
    {
        public string huntId;
        public string monsterId;
        public int huntSteps;
        public int crystalsEarned;
        public int energyEarned;
        public string legend;
        public string startTime;
        public string endTime;
    }

    [Serializable]
    private class SessionRecord
    {
        public string sessionId;
        public string startTime;
        public string endTime;
        public int totalSteps;
        public int crystalsEarned;
        public int totalEnergyEarned;
        public float currentEnergy;
        public List<HuntRecord> hunts = new List<HuntRecord>();
    }

    private static HuntRewardActions instance;
    private SessionRecord activeSession;
    private string activeFilePath;

    // =================================================
    // SESSION MANAGEMENT
    // =================================================

    // Saved in Unity's persistentDataPath;
    // a stable ID survives retries.
    private void BeginSession()
    {
        activeSession = new SessionRecord
        {
            sessionId = Guid.NewGuid().ToString("N"),
            startTime = DateTime.UtcNow.ToString("o")
        };

        activeFilePath = Path.Combine(
            Application.persistentDataPath,
            "masar_active_session.json"
        );

        // If the previous AR launch crashed,
        // preserve its unsent data.
        if (File.Exists(activeFilePath))
        {
            string backup = Path.Combine(
                Application.persistentDataPath,
                "masar_recovered_" +
                Guid.NewGuid().ToString("N") +
                ".json"
            );

            File.Move(activeFilePath, backup);

            Debug.LogWarning(
                "Previous AR session retained for future automatic recovery: " +
                backup
            );
        }

        SaveActiveSession();
    }

    private FootwearStepManager GetSteps()
    {
        return FindFirstObjectByType<FootwearStepManager>();
    }

    private void SnapshotCounters()
    {
        if (activeSession == null)
            return;

        FootwearStepManager steps = GetSteps();

        if (steps != null)
        {
            activeSession.totalSteps = steps.SessionSteps;

            if (steps.energySystem != null)
            {
                activeSession.currentEnergy =
                    steps.energySystem.currentEnergy;

                float energyRate = Mathf.Max(
                    0.01f,
                    steps.energySystem.stepsPerEnergy
                );

                activeSession.totalEnergyEarned =
                    Mathf.FloorToInt(
                        activeSession.totalSteps / energyRate
                    );
            }
        }

        int earned = 0;

        foreach (HuntRecord hunt in activeSession.hunts)
        {
            earned += hunt.crystalsEarned;
        }

        activeSession.crystalsEarned = earned;
    }

    private void SaveActiveSession()
    {
        if (activeSession == null)
            return;

        SnapshotCounters();

        try
        {
            string json = JsonUtility.ToJson(
                activeSession,
                true
            );

            string tmp = activeFilePath + ".tmp";

            File.WriteAllText(tmp, json);

            // Replace the active record after
            // a fully completed write.
            if (File.Exists(activeFilePath))
            {
                File.Delete(activeFilePath);
            }

            File.Move(tmp, activeFilePath);

            Debug.Log(
                "AR SESSION SAVED: " +
                activeSession.sessionId +
                " | steps=" +
                activeSession.totalSteps +
                " | hunts=" +
                activeSession.hunts.Count
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "AR SESSION SAVE FAILED: " + e
            );
        }
    }

    public static void RecordCompletedHunt(
        string huntId,
        string monsterId,
        int huntSteps,
        int crystalsEarned,
        int energyEarned,
        string legend,
        DateTime startTime,
        DateTime endTime)
    {
        if (instance == null ||
            instance.activeSession == null)
        {
            Debug.LogError(
                "HUNT NOT SAVED: HuntRewardActions session manager missing."
            );
            return;
        }

        foreach (HuntRecord entry in instance.activeSession.hunts)
        {
            if (entry.huntId == huntId)
                return;
        }

        // Idempotent within Unity.
        instance.activeSession.hunts.Add(
            new HuntRecord
            {
                huntId = huntId,
                monsterId = monsterId,
                huntSteps = Mathf.Max(0, huntSteps),
                crystalsEarned = Mathf.Max(0, crystalsEarned),
                energyEarned = Mathf.Max(0, energyEarned),
                legend = legend,
                startTime = startTime.ToString("o"),
                endTime = endTime.ToString("o")
            }
        );

        instance.SaveActiveSession();
        instance.TryQueueResult(instance.activeSession.hunts[instance.activeSession.hunts.Count - 1]);
    }

    private readonly HashSet<string> queuedHunts = new HashSet<string>();

    private bool TryQueueResult(HuntRecord hunt)
    {
        if (!EmbeddedHunt.Active || queuedHunts.Contains(hunt.huntId))
            return true;

        try
        {
            EmbeddedHunt.QueueResult(JsonUtility.ToJson(hunt));
            queuedHunts.Add(hunt.huntId);
            return true;
        }
        catch (AndroidJavaException exception)
        {
            Debug.LogError("Could not queue hunt result: " + exception.Message);
            return false;
        }
    }

    private void Awake()
    {
        instance = this;
        BeginSession();
    }

    public string SelectMonster(string monsterId)
    {
        // Used by the current MonsterHuntUIController and by embedded Flutter hunts.
        // Recognize all three IDs without accidentally treating Tantal as Bu Daryah.
        string id = (monsterId ?? string.Empty).Trim().ToLowerInvariant();
        GameObject selectedRoot;
        string displayName;

        switch (id)
        {
            case "bu_daryah":
                selectedRoot = buDaryaHunt;
                displayName = "Bu Daryah";
                break;
            case "om_el_saaf":
                selectedRoot = omElSaafHunt;
                displayName = "Om El-Saaf";
                break;
            case "tantal":
                selectedRoot = tantalHunt;
                displayName = "Tantal";
                break;
            case HamaretHuntController.MonsterId:
                selectedRoot = hamaretHunt;
                displayName = HamaretHuntController.LegendName;
                break;
            default:
                Debug.LogError("HuntRewardActions: Unknown monster ID '" + monsterId + "'.", this);
                return null;
        }

        if (selectedRoot == null)
        {
            Debug.LogError("HuntRewardActions: Assign the hunt root for '" + id + "' in the Inspector.", this);
            return null;
        }

        // Deactivate every hunt controller before enabling the chosen one.
        // This preserves the original start/reset behavior for each hunt.
        if (buDaryaHunt != null) buDaryaHunt.SetActive(false);
        if (omElSaafHunt != null) omElSaafHunt.SetActive(false);
        if (tantalHunt != null) tantalHunt.SetActive(false);
        if (hamaretHunt != null) hamaretHunt.SetActive(false);

        selectedRoot.SetActive(true);
        return displayName;
    }

    public void ReturnToApp()
    {
        if (activeSession == null)
            return;

        activeSession.endTime = DateTime.UtcNow.ToString("o");
        SaveActiveSession();

        if (!EmbeddedHunt.Active)
        {
            Debug.Log("AR session saved at " + activeFilePath);
            return;
        }

        foreach (HuntRecord hunt in activeSession.hunts)
        {
            if (!TryQueueResult(hunt))
                return;
        }

        FindFirstObjectByType<FootwearStepManager>()?.ReleaseForReturn();
        EmbeddedHunt.ReturnToApp();
    }

    public void HuntAgain()
    {
        MonsterHuntUIController huntUI = GetComponent<MonsterHuntUIController>();
        if (huntUI == null)
            return;

        if (EmbeddedHunt.Active)
        {
            EmbeddedHunt.RequestNextHunt();
            huntUI.ShowEmbeddedOffer();
            return;
        }

        // Reuse the UI controller's existing non-repeating random selection.
        // It also respects "Include Tantal In Random Hunts" on MonsterFlowManager.
        // Calling this avoids a second random picker that might repeat a monster.
        huntUI.SpawnNextRandomMonster();
    }
}
