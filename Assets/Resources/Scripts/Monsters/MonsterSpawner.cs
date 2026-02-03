using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnData
    {
        public GameObject monsterPrefab;
        public Vector2Int gridPosition;
    }

    [SerializeField] private GridManager gridManager;
    [SerializeField] private SpawnData[] monstersToSpawn;

    void Start()
    {
        if (gridManager == null) gridManager = FindFirstObjectByType<GridManager>();

        // Wait a tiny bit for GridManager to finish GenerateGrid()
        Invoke(nameof(SpawnAll), 0.2f);
    }

    void SpawnAll()
    {
        foreach (var data in monstersToSpawn)
        {
            Tile targetTile = gridManager.GetTile(data.gridPosition.x, data.gridPosition.y);

            if (targetTile != null && targetTile.IsWalkable())
            {
                // 1. Physically Spawn
                Vector3 worldPos = targetTile.transform.position;
                GameObject newMonster = Instantiate(data.monsterPrefab, worldPos, Quaternion.identity);

                // 2. Register with the Data Layer
                targetTile.SetOccupation(Tile.OccupationType.Monster, newMonster);

                Debug.Log($"Spawned {newMonster.name} at {data.gridPosition}");
            }
        }
    }
}