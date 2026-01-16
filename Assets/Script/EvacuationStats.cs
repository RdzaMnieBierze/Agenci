using UnityEngine;
using TMPro;

public class EvacuationStats : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI evacuatedText; // UI text dla ewakuowanych
    [SerializeField] private TextMeshProUGUI removedText; // UI text dla usuniętych z zagęszczenia
    [SerializeField] private TextMeshProUGUI totalAgentsText; // UI text dla ogółu agentów
    [SerializeField] private TextMeshProUGUI timeText; // UI text dla czasu
    [SerializeField] private TextMeshProUGUI deathsText; // UI text dla liczby zgonów
    
    [SerializeField] private Transform exitPoint; // Punkt wyjścia
    [SerializeField] private float exitDetectionRadius = 2f; // Promień detekcji wyjścia
    [SerializeField] private TileDensityManager tileDensityManager;
    [SerializeField] private AgentSpawner agentSpawner;
    
    private int evacuatedCount = 0;
    private int removedCount = 0;
    private int totalAgentsSpawned = 0;
    private float elapsedTime = 0f;
    private bool isSimulationRunning = false;
    private int deathsCount = 0;

    private void Update()
    {
        if (isSimulationRunning)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimeDisplay();
        }
        removedCount = tileDensityManager != null ? tileDensityManager.removedAgentsCount : 0;
        totalAgentsSpawned = agentSpawner != null ? agentSpawner.spawnedAgents : 0;
        CheckForEvacuatedAgents();
    }

    private void CheckForEvacuatedAgents()
    {
        if (exitPoint == null) return;

        // Znajdź wszystkich NavMeshAgents
        UnityEngine.AI.NavMeshAgent[] allAgents = FindObjectsByType<UnityEngine.AI.NavMeshAgent>(FindObjectsSortMode.None);
        if (allAgents == null || allAgents.Length == 0) return;


        foreach (UnityEngine.AI.NavMeshAgent agent in allAgents)
        {
            float distanceToExit = Vector3.Distance(agent.transform.position, exitPoint.position);

            if (distanceToExit <= exitDetectionRadius)
            {
                evacuatedCount++;
                Debug.Log($"Agent ewakuowany! Razem: {evacuatedCount}");
                
                Destroy(agent.gameObject);
                UpdateStatsDisplay();
            }
        }
    }

    public void AddRemovedAgent()
    {
        removedCount++;
        UpdateStatsDisplay();
    }

    public void SetTotalAgents(int count)
    {
        totalAgentsSpawned = count;
        UpdateStatsDisplay();
    }

    public void AddDeathInFire()
    {
        deathsCount++;
        UpdateStatsDisplay();
    }

    public void StartSimulation()
    {
        isSimulationRunning = true;
        elapsedTime = 0f;
        evacuatedCount = 0;
        removedCount = 0;
        deathsCount = 0;
        Debug.Log("Symulacja ewakuacji rozpoczęta!");
        UpdateStatsDisplay();
    }

    public void StopSimulation()
    {
        isSimulationRunning = false;
        Debug.Log($"Symulacja zakończona! Ewakuowani: {evacuatedCount}, Usunięci: {removedCount}");
    }

    private void UpdateStatsDisplay()
    {
        if (evacuatedText != null)
            evacuatedText.text = $"Ewakuowani: {evacuatedCount}";

        if (removedText != null)
            removedText.text = $"Usunięci (zabici): {removedCount}";

        if (deathsText != null)
            deathsText.text = $"Zgony w pożarze: {deathsCount}";

        if (totalAgentsText != null)
            totalAgentsText.text = $"Razem agentów: {totalAgentsSpawned}";
    }

    private void UpdateTimeDisplay()
    {
        if (timeText != null)
        {
            int minutes = (int)(elapsedTime / 60f);
            int seconds = (int)(elapsedTime % 60f);
            timeText.text = $"Czas: {minutes:00}:{seconds:00}";
        }
    }

    // Wizualizacja promienia detekcji wyjścia
    private void OnDrawGizmosSelected()
    {
        if (exitPoint == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(exitPoint.position, exitDetectionRadius);
    }
}