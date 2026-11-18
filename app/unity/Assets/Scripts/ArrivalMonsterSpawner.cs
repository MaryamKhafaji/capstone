using UnityEngine;

public class ArrivalMonsterSpawner : MonoBehaviour
{
    public HuntManager huntManager;
    public NavigationSystem navigationSystem;
    public Transform playerCamera;

    private GameObject spawnedMonster;
    private bool hasSpawned;

    void Update()
    {
        if (hasSpawned || navigationSystem == null || !navigationSystem.hasArrived)
            return;

        SpawnSelectedMonster();
    }

    void SpawnSelectedMonster()
    {
        GameObject monsterPrefab = huntManager.SelectedMonsterPrefab;

        if (monsterPrefab == null)
        {
            Debug.LogWarning("No monster prefab was selected.");
            return;
        }

        Vector3 spawnPosition = navigationSystem.fixedDestination;
        Quaternion spawnRotation = Quaternion.identity;

        if (playerCamera != null)
        {
            Vector3 directionToPlayer =
                playerCamera.position - spawnPosition;

            directionToPlayer.y = 0f;

            if (directionToPlayer.sqrMagnitude > 0.001f)
                spawnRotation = Quaternion.LookRotation(directionToPlayer);
        }

        spawnedMonster = Instantiate(
            monsterPrefab,
            spawnPosition,
            spawnRotation
        );

        hasSpawned = true;
        Debug.Log("Monster spawned at destination.");
    }
}