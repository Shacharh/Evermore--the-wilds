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

                // 3. Attach a world-space HP bar
                //    InitHPBarNextFrame waits one frame so Monster.Start() runs first and
                //    sets currentHP before the bar reads it.
                Monster monsterComp = newMonster.GetComponent<Monster>();
                if (monsterComp != null)
                {
                    MonsterHPBar hpBar = newMonster.AddComponent<MonsterHPBar>();
                    StartCoroutine(InitHPBarNextFrame(hpBar, monsterComp));
                }

                Debug.Log($"Spawned {newMonster.name} at {data.gridPosition}");
            }
        }
    }

    /// <summary>
    /// Waits one frame so Monster.Start() has run and set currentHP before
    /// the HP bar initialises.
    /// </summary>
    private System.Collections.IEnumerator InitHPBarNextFrame(MonsterHPBar bar, Monster monster)
    {
        yield return null; // let Monster.Start() execute
        if (bar != null && monster != null)
            bar.Initialize(monster);
    }
}