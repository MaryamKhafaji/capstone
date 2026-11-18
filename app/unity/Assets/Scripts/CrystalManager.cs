using UnityEngine;

public class CrystalManager : MonoBehaviour
{
    public static CrystalManager Instance;

    [Header("Crystal Settings")]
    public int maxCrystals = 100;

    public int CurrentCrystals { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentCrystals = EmbeddedHunt.Active ? EmbeddedHunt.InitialCrystals() : 0;
    }

    public int AddCrystals(int amount)
    {
        CurrentCrystals = Mathf.Max(0, CurrentCrystals + amount);

        return CurrentCrystals;
    }

    public void ResetCrystals()
    {
        CurrentCrystals = 0;
    }
}
