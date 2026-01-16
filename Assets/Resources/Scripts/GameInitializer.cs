using System.Reflection;
using UnityEngine;
public class GameInitializer : MonoBehaviour
{
    public static GameInitializer Instance { get; private set; }

    [Header("Databases")]
    public AttackDatabase attackDatabase;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        attackDatabase.Init();
    }
}
