using UnityEngine;

public class HuntManager : MonoBehaviour
{
    [SerializeField] private GameObject homeCanvas;
    [SerializeField] private GameObject gameplayRoot;
    [SerializeField] private GameObject[] monsterPrefabs = new GameObject[5];

    public int SelectedMonsterIndex { get; private set; } = -1;

    public GameObject SelectedMonsterPrefab
    {
        get
        {
            if (SelectedMonsterIndex < 0 ||
                SelectedMonsterIndex >= monsterPrefabs.Length)
            {
                return null;
            }

            return monsterPrefabs[SelectedMonsterIndex];
        }
    }

    public void SelectMonster(int monsterIndex)
    {
        if (monsterIndex < 0 || monsterIndex >= monsterPrefabs.Length)
        {
            Debug.LogWarning("Invalid monster number.");
            return;
        }

        SelectedMonsterIndex = monsterIndex;

        homeCanvas.SetActive(false);
        gameplayRoot.SetActive(true);

        Debug.Log("Selected monster: " + (monsterIndex + 1));
    }
}