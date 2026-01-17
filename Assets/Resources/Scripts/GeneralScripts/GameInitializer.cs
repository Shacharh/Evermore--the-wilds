using System.Reflection;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UnityConsent;
using Unity.Services.Analytics;

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

    private async void Start() 
    {
        // Initialize Services
        await UnityServices.InitializeAsync();

        // Set Consent -- Analytics Intent must be granted for data to be collected
        EndUserConsent.SetConsentState(new ConsentState
        {
            AnalyticsIntent = ConsentStatus.Granted,
            AdsIntent = ConsentStatus.Denied // optional
        });

        Debug.Log("UGS Analytics Initialized");
    }
}
