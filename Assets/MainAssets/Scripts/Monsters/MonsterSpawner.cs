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
    [Tooltip("Rotate spawned monsters 180° on Y so they face the opposing side. Enable on the Enemy spawner.")]
    [SerializeField] private bool flipFacing = false;

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
                Quaternion spawnRot   = flipFacing ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
                GameObject newMonster = Instantiate(data.monsterPrefab, worldPos, spawnRot);

                // 2. Register with the Data Layer
                targetTile.SetOccupation(Tile.OccupationType.Monster, newMonster);

                // 3. Attach a world-space HP bar
                //    InitHPBarNextFrame waits one frame so Monster.Start() runs first and
                //    sets currentHP before the bar reads it.
                Monster monsterComp = newMonster.GetComponentInChildren<Monster>();
                if (monsterComp != null)
                {
                    monsterComp.CurrentTile = targetTile;
                    MonsterHPBar hpBar = newMonster.AddComponent<MonsterHPBar>();
                    StartCoroutine(InitHPBarNextFrame(hpBar, monsterComp));
                }

                // Add a CapsuleCollider on the root so the monster can be clicked
                // directly by the player (monster-layer raycast in InputManager).
                // The collider is sized to approximate a standing creature; it can
                // be tuned per-monster later if needed.
                if (newMonster.GetComponent<Collider>() == null)
                {
                    var cap = newMonster.AddComponent<CapsuleCollider>();
                    cap.center = new Vector3(0f, 0.75f, 0f);
                    cap.radius = 0.4f;
                    cap.height = 1.5f;
                }
                newMonster.layer = LayerMask.NameToLayer("Monster");

                // Silhouette — shows a solid coloured fill when the monster is visually
                // hidden behind terrain, obstructions, or other units (QuickOutline package).
                // SilhouetteOnly: renders only when the object is occluded by geometry.
                // [DisallowMultipleComponent] on Outline means we must not add a second one.
                var outline = newMonster.GetComponent<Outline>();
                if (outline == null) outline = newMonster.AddComponent<Outline>();
                outline.OutlineMode  = Outline.Mode.SilhouetteOnly;
                outline.OutlineColor = (monsterComp != null && monsterComp.IsEnemy)
                    ? new Color(1f, 0.25f, 0.25f, 1f)   // red  — enemy
                    : new Color(0.25f, 0.85f, 1f,  1f); // cyan — player
                outline.OutlineWidth = 8f;

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