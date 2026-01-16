using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

public class TileDensityManager : MonoBehaviour
{
    private EvacuationStats evacuationStats;
    [SerializeField] private float tileSize = 1f; // Rozmiar jednego tile'a
    [SerializeField] private int maxAgentsPerTile = 3; // Max agentów na tile'u
    [SerializeField] private float penaltyDuration = 2f; // Jak długo mogą być (w sekundach)
    [SerializeField] private float slowdownSpeed = 2f; // Zmniejszona prędkość po karze
    
    [SerializeField] private PenaltyType penaltyType = PenaltyType.Remove; // Typ kary
    [SerializeField] public int removedAgentsCount = 0; // Licznik usuniętych agentów
    
    [SerializeField] private float floorHeight = 2.2f; // Wysokość jednego piętra (dla 3D)
    [SerializeField] private bool visualizeCrowding = true; // Pokazuj kolor dla przepełnionych tile'ów
    [SerializeField] private Color crowdedColor = Color.red; // Kolor dla przepełnionego tile'a
    [SerializeField] private Color normalColor = Color.yellow; // Normalny kolor
    
    public enum PenaltyType
    {
        Slowdown, // Spowolnienie
        Remove    // Usunięcie
    }
    
    private Dictionary<Vector3Int, TileData> tiles = new Dictionary<Vector3Int, TileData>();
    private Dictionary<NavMeshAgent, float> agentPenaltyTimer = new Dictionary<NavMeshAgent, float>();
    private Dictionary<NavMeshAgent, float> originalSpeeds = new Dictionary<NavMeshAgent, float>();

    private class TileData
    {
        public List<NavMeshAgent> agents = new List<NavMeshAgent>();
        public float crowdedTime = 0f; // Jak długo tile jest przepełniony
    }

    private void Update()
    {
        UpdateAgentPositions();
        CheckCrowding();
        UpdatePenalties();
    }

    private void UpdateAgentPositions()
    {
        // Wyczyść stare dane
        foreach (var tile in tiles.Values)
            tile.agents.Clear();

        // Znajdź wszystkich agentów i przypisz do tile'ów
        NavMeshAgent[] allAgents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
        
        foreach (NavMeshAgent agent in allAgents)
        {
            Vector3Int tilePos = GetTilePosition3D(agent.transform.position);
            
            if (!tiles.ContainsKey(tilePos))
                tiles[tilePos] = new TileData();
            
            tiles[tilePos].agents.Add(agent);
        }
    }

    private void CheckCrowding()
    {
        foreach (var tileEntry in tiles)
        {
            Vector3Int tilePos = tileEntry.Key;
            TileData tileData = tileEntry.Value;
            int agentCount = tileData.agents.Count;

            if (agentCount > maxAgentsPerTile)
            {
                // Tile jest przepełniony
                tileData.crowdedTime += Time.deltaTime;

                if (tileData.crowdedTime >= penaltyDuration)
                {
                    // Czas na karę!
                    PunishAgentsOnTile(tileData.agents);
                    tileData.crowdedTime = 0f;
                }

                // Wizualizacja (opcjonalnie)
                //Debug.Log($"Tile {tilePos}: {agentCount} agentów (przepełniony {tileData.crowdedTime:F1}s)");
            }
            else
            {
                // Reset licznika jeśli już nie ma zagęszczenia
                tileData.crowdedTime = 0f;
            }
        }
    }

    private void PunishAgentsOnTile(List<NavMeshAgent> agents)
    {
        // Wybierz losowego agenta do kary
        if (agents.Count == 0) return;

        NavMeshAgent victim = agents[Random.Range(0, agents.Count)];
        
        if (penaltyType == PenaltyType.Remove)
        {
            Debug.LogWarning($"Agent usunięty! (Zagęszczenie na tile'u)");
            removedAgentsCount++;
            
            if (evacuationStats != null)
                evacuationStats.AddRemovedAgent();
            
            Destroy(victim.gameObject);
        }
        else if (penaltyType == PenaltyType.Slowdown)
        {
            Debug.LogWarning($"Agent ukarany! Zmniejszam prędkość na stałe.");
            
            // Zapisz oryginalną prędkość jeśli jeszcze nie karana
            if (!originalSpeeds.ContainsKey(victim))
                originalSpeeds[victim] = victim.speed;
            
            // Zmniejsz prędkość na stałe
            victim.speed = slowdownSpeed;
        }
    }

    private void UpdatePenalties()
    {
        // Opcjonalny timer dla kar (jeśli byś chciał czasowe kary)
        List<NavMeshAgent> toRemove = new List<NavMeshAgent>();
        
        foreach (var entry in agentPenaltyTimer)
        {
            agentPenaltyTimer[entry.Key] -= Time.deltaTime;
            
            if (agentPenaltyTimer[entry.Key] <= 0)
                toRemove.Add(entry.Key);
        }
        
        foreach (var agent in toRemove)
            agentPenaltyTimer.Remove(agent);
    }

    private Vector3Int GetTilePosition3D(Vector3 worldPos)
    {
        // Konwertuj pozycję świata na koordynaty tile'a (3D)
        int tileX = Mathf.FloorToInt(worldPos.x / tileSize);
        int tileZ = Mathf.FloorToInt(worldPos.z / tileSize);
        int tileY = Mathf.FloorToInt(worldPos.y / floorHeight); // Piętro
        
        return new Vector3Int(tileX, tileY, tileZ);
    }

    // Metoda do wizualizacji grid'a (Gizmos) - 3D
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        foreach (var tileEntry in tiles)
        {
            Vector3Int tilePos = tileEntry.Key;
            TileData tileData = tileEntry.Value;
            
            // Wybierz kolor na podstawie zagęszczenia
            if (visualizeCrowding && tileData.agents.Count > maxAgentsPerTile)
            {
                Gizmos.color = crowdedColor;
            }
            else
            {
                Gizmos.color = normalColor;
            }

            // Oblicz centrum tile'a w przestrzeni 3D
            Vector3 tileCenter = new Vector3(
                (tilePos.x + 0.5f) * tileSize,
                tilePos.y * floorHeight + 2.2f, // Wysokość piętra
                (tilePos.z + 0.5f) * tileSize
            );
            DrawTileGizmo(tileCenter);
        }
    }

    private void DrawTileGizmo(Vector3 center)
    {
        float halfSize = tileSize * 0.5f;
        
        Vector3 topLeft = center + new Vector3(-halfSize, 0, halfSize);
        Vector3 topRight = center + new Vector3(halfSize, 0, halfSize);
        Vector3 botLeft = center + new Vector3(-halfSize, 0, -halfSize);
        Vector3 botRight = center + new Vector3(halfSize, 0, -halfSize);

        // Rysuj linie
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, botRight);
        Gizmos.DrawLine(botRight, botLeft);
        Gizmos.DrawLine(botLeft, topLeft);
        
        // Rysuj wypełniony kwadrat (tło)
        DrawFilledSquare(topLeft, topRight, botRight, botLeft);
    }

    private void DrawFilledSquare(Vector3 topLeft, Vector3 topRight, Vector3 botRight, Vector3 botLeft)
    {
        // Rysuj linie diagonalne żeby wizualizować wypełnienie
        Gizmos.DrawLine(topLeft, botRight);
        Gizmos.DrawLine(topRight, botLeft);
    }
}