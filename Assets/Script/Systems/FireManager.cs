using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FireManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameObject firePrefab;
    [SerializeField] private List<BoxCollider> hazardZones; // Zones where fire can start
    [SerializeField] private float spreadInterval = 5f;
    [SerializeField] private int maxFireNodes = 50;

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
            // 1. Try to find objects with tag "HazardZone"
            GameObject[] taggedZones = GameObject.FindGameObjectsWithTag("HazardZone");
            foreach (var obj in taggedZones)
            {
                BoxCollider col = obj.GetComponent<BoxCollider>();
                if (col != null) hazardZones.Add(col);
            }

            if (hazardZones.Count > 0)
            {
                Debug.Log($"FireManager: Found {hazardZones.Count} zones via 'HazardZone' tag.");
            }
            else
            {
                // 2. Fallback: Try to find colliders on this object or children
                BoxCollider[] foundColliders = GetComponentsInChildren<BoxCollider>();
                if (foundColliders.Length > 0)
                {
                    hazardZones.AddRange(foundColliders);
                    Debug.LogWarning($"FireManager: Found {foundColliders.Length} colliders on self/children.");
                }
                else
                {
                    Debug.LogError("FireManager: No Hazard Zones found! Tag objects with 'HazardZone' and add BoxCollider.");
                    return;
                }
            }
        }

        // Pick random zone
        BoxCollider zone = hazardZones[Random.Range(0, hazardZones.Count)];
        Vector3 spawnPos = GetRandomPointInCollider(zone);
        Debug.Log($"FireManager: Spawning fire at {spawnPos} in zone {zone.name}");

        SpawnFireNode(spawnPos);
        
        // IMMEDIATE GLOBAL ALARM - All agents evacuate immediately
        if (AlarmSystem.Instance != null)
        {
            Debug.Log("FireManager: Triggering global alarm immediately!");
            AlarmSystem.Instance.TriggerAlarm(spawnPos);
        }
        else
        {
            Debug.LogError("FireManager: AlarmSystem not found! Agents won't react.");
        }

        StartCoroutine(SpreadFireRoutine());
    }

    private void SpawnFireNode(Vector3 position)
    {
        if (activeFires.Count >= maxFireNodes) return;

        if (firePrefab == null)
        {
            Debug.LogError("FireManager: Cannot spawn fire - prefab is null!");
            return;
        }

        // Add offset to ensure fire is visible above ground
        //Vector3 spawnPos = position + Vector3.up * 0.5f;
        Vector3 spawnPos = position;

        GameObject fire = Instantiate(firePrefab, spawnPos, Quaternion.identity);
        activeFires.Add(fire);
        Debug.Log($"FireManager: Fire spawned at {spawnPos}. Total fires: {activeFires.Count}");
    }

    private IEnumerator SpreadFireRoutine()
    {
        while (fireStarted)
        {
            yield return new WaitForSeconds(spreadInterval);

            if (activeFires.Count < maxFireNodes && activeFires.Count > 0)
            {
                // Pick a random existing fire node and try to spread
                GameObject source = activeFires[Random.Range(0, activeFires.Count)];
                Vector3 spreadPos = source.transform.position + Random.insideUnitSphere * 2f;
                spreadPos.y = source.transform.position.y;

                // Check if valid position (NavMesh)
                /*if (IsPositionValid(spreadPos))
                {
                    SpawnFireNode(spreadPos);
                }*/
                SpawnFireNode(spreadPos);
            }
        }
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
        // Stop spreading
        StopAllCoroutines();
        
        // Clear fire list
        activeFires.Clear();
        
        // Reset state
        fireStarted = false;
        
        Debug.Log("FireManager: Reset complete.");
    }
}