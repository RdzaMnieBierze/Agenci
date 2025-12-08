using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class AgentSpawner : MonoBehaviour
{
    [SerializeField] private GameObject agentPrefab;
    [SerializeField] private Transform spawnArea;
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(50f, 1f, 50f);
    [SerializeField] private int defaultAgentCount = 5;
    
    // Ustawienia Tilemap
    [SerializeField] private float tileSize = 1f; 
    [SerializeField] private float agentsPerSquareMeter = 2f; 
    
    // UI elementy
    [SerializeField] private TMP_InputField agentCountInput;
    [SerializeField] private Button spawnButton;
    [SerializeField] private TextMeshProUGUI statusText;
    
    private int spawnedAgents = 0;

    // lista checkpointów 
    public List<Transform> globalPath;
    private void Start()
    {
        if (spawnButton != null)
            spawnButton.onClick.AddListener(SpawnAgents);
        
        if (agentCountInput != null)
            agentCountInput.text = defaultAgentCount.ToString();
    }

    
    public void SpawnAgents()
    {
        DestroyPreviousAgents();
        
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

        for (int i = 0; i < agentCount; i++)
        {
            Vector3 randomPos = GetRandomSpawnPosition();
            
            if (IsPositionOnNavMesh(randomPos))
            {
                GameObject newAgent = Instantiate(agentPrefab, randomPos, Quaternion.identity, spawnArea);
                newAgent.name = "Agent_" + (i + 1);
                newAgent.GetComponent<EnemyMovement>().orderedPath = new List<Transform>(globalPath);
                spawnedAgents++;
            }
            else
            {
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
        
        Vector3 randomPos = new Vector3(randomX, spawnAreaSize.y, randomZ);
        
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