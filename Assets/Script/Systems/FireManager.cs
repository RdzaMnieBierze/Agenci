using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FireManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameObject firePrefab;
    [SerializeField] private List<BoxCollider> hazardZones;
    [SerializeField] private float spreadInterval = 2f;
    private float initialSpreadInterval;
    [SerializeField] private float spreadDecrement = 0.02f;
    [SerializeField] private int maxFireNodes = 400;
    
    [Header("Spread Settings")]
    [SerializeField] private float minDistanceBetweenFires = 0.5f; // Min odległość między ogniskami
    [SerializeField] private float spreadRadius = 1.5f; // Promień rozprzestrzeniania
    [SerializeField] private int maxSpreadAttempts = 5; // Max prób znalezienia dobrej pozycji
    [SerializeField] private bool blockNavMesh = true; // Czy ogień ma blokować NavMesh
    [SerializeField] private bool allowVerticalSpread = true; // Czy pozwolić na rozprzestrzenianie w pionie
    [SerializeField] private float floorHeight = 2.2f; // Wys
    [SerializeField] private float chanceToSpreadUp = 0.075f; // Szansa na rozprzestrzenianie w górę
    [SerializeField] private float chanceToSpreadDown = 0.05f; // Szansa na rozprzestrzenianie w dół
    
    [Header("Agent Death Settings")]
    [SerializeField] private float fireKillRadius = 0.5f; // Promień w którym agent umiera
    [SerializeField] private float checkInterval = 0.2f; // Jak często sprawdzać agentów

    private List<GameObject> activeFires = new List<GameObject>();
    private bool fireStarted = false;

    public void StartFire()
    {
        if (fireStarted)
        {
            Debug.LogWarning("FireManager: Fire already started!");
            return;
        }
        
        fireStarted = true;
        Debug.Log("FireManager: Starting fire...");

        if (firePrefab == null)
        {
            Debug.LogError("FireManager: Fire Prefab is not assigned!");
            return;
        }

        if (hazardZones.Count == 0)
        {
            GameObject[] taggedZones = GameObject.FindGameObjectsWithTag("HazardZone");
            foreach (var obj in taggedZones)
            {
                BoxCollider col = obj.GetComponent<BoxCollider>();
                if (col != null) hazardZones.Add(col);
            }

            if (hazardZones.Count == 0)
            {
                BoxCollider[] foundColliders = GetComponentsInChildren<BoxCollider>();
                if (foundColliders.Length > 0)
                {
                    hazardZones.AddRange(foundColliders);
                }
                else
                {
                    Debug.LogError("FireManager: No Hazard Zones found!");
                    return;
                }
            }
        }

        BoxCollider zone = hazardZones[Random.Range(0, hazardZones.Count)];
        Vector3 spawnPos = GetRandomPointInCollider(zone);

        SpawnFireNode(spawnPos);
        
        if (AlarmSystem.Instance != null)
        {
            AlarmSystem.Instance.TriggerAlarm(spawnPos);
        }

        StartCoroutine(SpreadFireRoutine());
        StartCoroutine(CheckAgentsInFireRoutine());
    }

    private void SpawnFireNode(Vector3 position)
    {
        if (activeFires.Count >= maxFireNodes) return;

        if (firePrefab == null)
        {
            Debug.LogError("FireManager: Cannot spawn fire - prefab is null!");
            return;
        }

        GameObject fire = Instantiate(firePrefab, position, Quaternion.identity);
        activeFires.Add(fire);
        
        
        if (blockNavMesh)
        {
            var obstacle = fire.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
            obstacle.radius = 0.5f;
            obstacle.height = 1f;
        }
        
        Debug.Log($"FireManager: Fire spawned at {position}. Total: {activeFires.Count}");
    }

    private IEnumerator SpreadFireRoutine()
    {
        while (fireStarted)
        {
            yield return new WaitForSeconds(spreadInterval);

            if (activeFires.Count < maxFireNodes && activeFires.Count > 0)
            {
                // Wybierz losowe ognisko jako źródło
                GameObject source = activeFires[Random.Range(0, activeFires.Count)];
                
                // Zdecyduj czy rozprzestrzeniać poziomo czy pionowo
                bool tryVerticalSpread = allowVerticalSpread && ShouldSpreadVertically();
                
                Vector3? spreadPos = null;
                
                if (tryVerticalSpread)
                {
                    // Spróbuj rozprzestrzenić w pionie (góra/dół)
                    spreadPos = TryVerticalSpread(source.transform.position);
                }
                
                // Jeśli vertical spread się nie udał lub nie został wybrany, spróbuj poziomo
                if (!spreadPos.HasValue)
                {
                    spreadPos = FindValidSpreadPosition(source.transform.position);
                }
                
                if (spreadPos.HasValue)
                {
                    SpawnFireNode(spreadPos.Value);
                    if (spreadInterval > spreadDecrement * 2f) 
                        spreadInterval -= spreadDecrement;
                }
                else
                {
                    Debug.LogWarning("FireManager: Could not find valid spread position");
                }
            }
        }
    }

    private bool ShouldSpreadVertically()
    {
        float randomValue = Random.value;
        
        // Sprawdź czy iść w dół
        if (randomValue < chanceToSpreadDown)
        {
            return true;
        }
        
        // Sprawdź czy iść w górę
        if (randomValue < chanceToSpreadDown + chanceToSpreadUp)
        {
            return true;
        }
        
        return false;
    }

    private Vector3? TryVerticalSpread(Vector3 sourcePos)
    {
        float randomValue = Random.value;
        float totalChance = chanceToSpreadDown + chanceToSpreadUp;
        
        // Określ kierunek (dół ma większy priorytet)
        bool spreadDown = randomValue < (chanceToSpreadDown / totalChance);
        
        float verticalOffset = spreadDown ? -floorHeight : floorHeight;
        
        // Spróbuj kilka pozycji na innym piętrze
        for (int attempt = 0; attempt < maxSpreadAttempts; attempt++)
        {
            // Dodaj małe losowe przesunięcie poziome (ogień nie pada dokładnie w dół)
            Vector2 randomOffset = Random.insideUnitCircle * spreadRadius * 0.5f;
            
            Vector3 candidatePos = sourcePos + new Vector3(
                randomOffset.x,
                verticalOffset,
                randomOffset.y
            );
            
            // Sprawdź czy na tym piętrze jest NavMesh
            if (!IsPositionValid(candidatePos)) continue;
            
            // Sprawdź czy nie jest za blisko innych ogni
            if (IsTooCloseToOtherFires(candidatePos)) continue;
            
            // Sprawdź czy faktycznie jest na innym piętrze
            float heightDiff = Mathf.Abs(candidatePos.y - sourcePos.y);
            if (heightDiff < floorHeight * 0.8f) continue; // Minimum 80% wysokości piętra
            
            Debug.Log($"FireManager: Fire spreading {(spreadDown ? "DOWN" : "UP")} to another floor!");
            return candidatePos;
        }
        
        return null;
    }

    private IEnumerator CheckAgentsInFireRoutine()
    {
        while (fireStarted)
        {
            yield return new WaitForSeconds(checkInterval);

            // Znajdź wszystkich agentów
            AgentController[] agents = FindObjectsByType<AgentController>(FindObjectsSortMode.None);

            foreach (var agent in agents)
            {
                if (agent == null || !agent.gameObject.activeInHierarchy) continue;

                // Sprawdź czy agent jest w zasięgu jakiegoś ognia
                if (IsAgentInFire(agent.transform.position))
                {
                    KillAgent(agent);
                }
            }
        }
    }

    private bool IsAgentInFire(Vector3 agentPos)
    {
        foreach (var fire in activeFires)
        {
            if (fire == null) continue;

            float distance = Vector3.Distance(agentPos, fire.transform.position);
            if (distance <= fireKillRadius)
            {
                return true;
            }
        }

        return false;
    }

    private void KillAgent(AgentController agent)
    {
        Debug.LogWarning($"FireManager: Agent {agent.name} died in fire!");

        // Powiadom EvacuationStats
        EvacuationStats stats = FindAnyObjectByType<EvacuationStats>();
        if (stats != null)
        {
            stats.AddDeathInFire();
        }

        // Usuń agenta
        Destroy(agent.gameObject);
    }

    private Vector3? FindValidSpreadPosition(Vector3 sourcePos)
    {
        for (int attempt = 0; attempt < maxSpreadAttempts; attempt++)
        {
            // Losowy kierunek w płaszczyźnie XZ
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomDist = Random.Range(minDistanceBetweenFires, spreadRadius);
            
            Vector3 candidatePos = sourcePos + new Vector3(
                randomDir.x * randomDist,
                0,
                randomDir.y * randomDist
            );
            
            candidatePos.y = sourcePos.y; // Zachowaj wysokość źródła

            // Sprawdź czy pozycja jest na NavMesh
            if (!IsPositionValid(candidatePos)) continue;
            
            // Sprawdź czy nie jest za blisko innych ogni
            if (IsTooCloseToOtherFires(candidatePos)) continue;
            
            // Znaleziono dobrą pozycję!
            return candidatePos;
        }
        
        return null; // Nie znaleziono dobrej pozycji po maxSpreadAttempts próbach
    }

    private bool IsTooCloseToOtherFires(Vector3 position)
    {
        foreach (var fire in activeFires)
        {
            if (fire == null) continue;
            
            float distance = Vector3.Distance(position, fire.transform.position);
            if (distance < minDistanceBetweenFires)
            {
                return true;
            }
        }
        
        return false;
    }

    private Vector3 GetRandomPointInCollider(BoxCollider collider)
    {
        Vector3 min = collider.bounds.min;
        Vector3 max = collider.bounds.max;

        return new Vector3(
            Random.Range(min.x, max.x),
            collider.bounds.center.y,
            Random.Range(min.z, max.z)
        );
    }

    private bool IsPositionValid(Vector3 pos)
    {
        UnityEngine.AI.NavMeshHit hit;
        return UnityEngine.AI.NavMesh.SamplePosition(pos, out hit, 1f, UnityEngine.AI.NavMesh.AllAreas);
    }

    public void ResetFire()
    {
        StopAllCoroutines();
        
        // Usuń wszystkie ogniska
        foreach (var fire in activeFires)
        {
            if (fire != null)
                Destroy(fire);
        }
        
        activeFires.Clear();
        fireStarted = false;
        spreadInterval = initialSpreadInterval;
        
        Debug.Log("FireManager: Reset complete.");
    }
    
    // Debug visualization
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Czerwone kółka - min distance between fires
        Gizmos.color = Color.red;
        foreach (var fire in activeFires)
        {
            if (fire != null)
            {
                Gizmos.DrawWireSphere(fire.transform.position, minDistanceBetweenFires);
            }
        }
        
        // Żółte kółka - kill radius
        Gizmos.color = Color.yellow;
        foreach (var fire in activeFires)
        {
            if (fire != null)
            {
                Gizmos.DrawWireSphere(fire.transform.position, fireKillRadius);
            }
        }
        
        // Niebieskie linie pokazujące wysokość pięter
        if (allowVerticalSpread && activeFires.Count > 0)
        {
            Gizmos.color = new Color(0, 0.5f, 1f, 0.3f); // Przezroczysty niebieski
            
            foreach (var fire in activeFires)
            {
                if (fire != null)
                {
                    // Linia w górę
                    Vector3 upperFloor = fire.transform.position + Vector3.up * floorHeight;
                    Gizmos.DrawLine(fire.transform.position, upperFloor);
                    Gizmos.DrawWireSphere(upperFloor, 0.3f);
                    
                    // Linia w dół
                    Vector3 lowerFloor = fire.transform.position + Vector3.down * floorHeight;
                    Gizmos.DrawLine(fire.transform.position, lowerFloor);
                    Gizmos.DrawWireSphere(lowerFloor, 0.3f);
                }
            }
        }
    }
}