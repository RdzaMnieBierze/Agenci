using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class AgentSpawner : MonoBehaviour
{
    [Header("Factory")]
    [SerializeField] private AgentFactory agentFactory;

    [Header("Spawn Settings")]
    [SerializeField] private Transform spawnArea;
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(50f, 1f, 50f);
    [SerializeField] private int defaultAgentCount = 5;
    
    // UI elementy
    [SerializeField] private TMP_InputField agentCountInput;
    [SerializeField] private Button spawnButton;
    [SerializeField] private TextMeshProUGUI statusText;
    
    public int spawnedAgents = 0;

    // lista checkpointów 
    //public List<Transform> globalPath;

    private void Start()
    {
        // Setup UI
        if (spawnButton != null)
            spawnButton.onClick.AddListener(SpawnAgents);
        
        if (agentCountInput != null)
            agentCountInput.text = defaultAgentCount.ToString();

        // Find AgentFactory if not assigned
        if (agentFactory == null)
        {
            agentFactory = FindFirstObjectByType<AgentFactory>();
            if (agentFactory == null)
            {
                Debug.LogError("AgentSpawner: Brak AgentFactory! Dodaj GameObject z komponentem AgentFactory.");
            }
            else
            {
                Debug.Log("AgentSpawner: AgentFactory found.");
            }
        }
    }

    public void SpawnAgents()
    {
        Debug.Log("SpawnAgents called");
        
        DestroyPreviousAgents();
        
        if (agentFactory == null)
        {
            UpdateStatus("Błąd: Brak AgentFactory!", Color.red);
            Debug.LogError("AgentFactory is null!");
            return;
        }
        
        if (!int.TryParse(agentCountInput.text, out int agentCount))
        {
            UpdateStatus("Błąd: Wpisz liczbę agentów!", Color.red);
            return;
        }

        if (agentCount <= 0)
        {
            UpdateStatus("Błąd: Liczba musi być > 0", Color.red);
            return;
        }

        spawnedAgents = 0;
        int attempts = 0;
        int maxAttempts = agentCount * 20; // Safety limit to prevent freeze

        for (int i = 0; i < agentCount; i++)
        {
            attempts++;
            if (attempts > maxAttempts)
            {
                Debug.LogError($"AgentSpawner: Too many failed spawn attempts ({attempts})! Check SpawnArea size and NavMesh.");
                UpdateStatus("Błąd: Nie można znaleźć miejsca na NavMesh!", Color.red);
                break;
            }

            Vector3 randomPos = GetRandomSpawnPosition();
            
            if (IsPositionOnNavMesh(randomPos))
            {
                // Randomize Agent Type
                AgentType randomType;
                do
                {
                    randomType = (AgentType)Random.Range(0, System.Enum.GetValues(typeof(AgentType)).Length);                   
                }while(randomType == AgentType.Blind); // Exclude Blind for now

                GameObject newAgent = agentFactory.CreateAgent(randomType, randomPos);
                if (newAgent != null)
                {
                    newAgent.transform.SetParent(spawnArea);
                    
                    // Set wander bounds to keep agent within spawn area
                    AgentController controller = newAgent.GetComponent<AgentController>();
                    if (controller != null)
                    {
                        Bounds bounds = new Bounds(spawnArea.position, spawnAreaSize);
                        controller.SetWanderBounds(bounds);
                    }
                    
                    spawnedAgents++;
                }
            }
            else
            {
                // Retry this index
                i--; 
            }
        }

        Debug.Log($"Spawned {spawnedAgents} agents");
        UpdateStatus($"Spawned {spawnedAgents} agents", Color.green);
    }

    private Vector3 GetRandomSpawnPosition()
    {
        float randomX = Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f);
        float randomZ = Random.Range(-spawnAreaSize.z / 2f, spawnAreaSize.z / 2f);
        float randomY = Random.Range(-spawnAreaSize.y, spawnAreaSize.y);
        
        Vector3 randomPos = new Vector3(randomX, randomY, randomZ);
        
        if (spawnArea != null)
            randomPos += spawnArea.position;
        
        return randomPos;
    }

    private bool IsPositionOnNavMesh(Vector3 position)
    {
        UnityEngine.AI.NavMeshHit hit;
        return UnityEngine.AI.NavMesh.SamplePosition(position, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas);
    }

    private void DestroyPreviousAgents()
    {
        if (spawnArea == null) return;
        
        foreach (Transform child in spawnArea)
        {
            Destroy(child.gameObject);
        }
    }

    private void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
    }
}