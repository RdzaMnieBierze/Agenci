using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Tilemaps;

public class SimulationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FireManager fireManager;
    [SerializeField] private AgentSpawner agentSpawner;
    [SerializeField] private Button startFireButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private EvacuationStats evacuationStats;
    [SerializeField] private TileDensityManager tileDensityManager;

    private bool fireStarted = false;

    private void Start()
    {
        if (startFireButton != null)
        {
            startFireButton.onClick.AddListener(StartFire);
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartSimulation);
        }

        if (fireManager == null)
        {
            fireManager = FindFirstObjectByType<FireManager>();
        }

        if (agentSpawner == null)
        {
            agentSpawner = FindFirstObjectByType<AgentSpawner>();
        }

        if (evacuationStats == null)
        {
            evacuationStats = FindFirstObjectByType<EvacuationStats>();
        }
        if (tileDensityManager == null)
        {
            tileDensityManager = FindFirstObjectByType<TileDensityManager>();
        }

        UpdateStatus("Symulacja gotowa. Kliknij 'Wywołaj Pożar'", Color.green);
    }

    public void StartFire()
    {
        if (fireStarted)
        {
            UpdateStatus("Pożar już się rozpoczął!", Color.yellow);
            return;
        }

        if (fireManager == null)
        {
            UpdateStatus("Błąd: Brak FireManager!", Color.red);
            Debug.LogError("SimulationController: FireManager not found!");
            return;
        }
        if(evacuationStats != null)
        {
            evacuationStats.StartSimulation();
        }else return;

        fireManager.StartFire();
        fireStarted = true;
        UpdateStatus("⚠️ POŻAR! Agenci ewakuują się!", Color.red);

        // Disable button after use
        if (startFireButton != null)
        {
            startFireButton.interactable = false;
        }
    }

    private void Update()
    {
        if (fireStarted)
        {
            AgentController[] agents = FindObjectsByType<AgentController>(FindObjectsSortMode.None);
            if (agents == null || agents.Length == 0)
            {
                UpdateStatus("✅ Wszyscy agenci ewakuowani lub usunięci. Symulacja zakończona.", Color.green);
                fireStarted = false;
                evacuationStats.StopSimulation();
                fireManager.fireStarted = false;
                return;
            }
        }
    }
    private void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        Debug.Log($"[SimulationController] {message}");
    }

    // Full simulation restart
    public void RestartSimulation()
    {
        UpdateStatus("Resetowanie symulacji...", Color.yellow);

        // 1. Destroy all fire objects
        FireSource[] fires = FindObjectsByType<FireSource>(FindObjectsSortMode.None);
        foreach (var fire in fires)
        {
            Destroy(fire.gameObject);
        }

        // 2. Destroy all agents
        AgentController[] agents = FindObjectsByType<AgentController>(FindObjectsSortMode.None);
        foreach (var agent in agents)
        {
            Destroy(agent.gameObject);
        }

        // 3. Reset AlarmSystem
        if (AlarmSystem.Instance != null)
        {
            AlarmSystem.Instance.ResetAlarm();
        }

        // 4. Reset FireManager state
        if (fireManager != null)
        {
            fireManager.ResetFire();
        }

        // 5. Respawn agents
        if (agentSpawner != null)
        {
            agentSpawner.SpawnAgents();
        }

        // 6. Reset UI
        fireStarted = false;
        if (startFireButton != null)
        {
            startFireButton.interactable = true;
        }
        if (evacuationStats != null)
        {
            evacuationStats.StartSimulation();
        }

        UpdateStatus("✅ Symulacja zresetowana. Gotowa do ponownego uruchomienia.", Color.green);
    }
}