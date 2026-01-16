using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class AgentMovement : MonoBehaviour
{
    public float UpdateSpeed = 0.5f;
    private NavMeshAgent Agent;
    
    private EvacuationBeacon currentBeacon;
    private bool destinationSet = false;
    
    [Header("Odległość, na której agent uznaje beacon za osiągnięty")]
    public float arrivalDistance = 2f;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        // Znajdź najbliższy beacon
        EvacuationBeacon nearest = FindNearestBeacon();
        if (nearest == null)
        {
            Debug.LogWarning($"{name}: Brak beaconów!");
            return;
        }

        currentBeacon = nearest;
        Debug.Log($"{name}: Idę do najbliższego beacona: {currentBeacon.name}");

        StartCoroutine(FollowBeacons());
    }

    private IEnumerator FollowBeacons()
    {
        WaitForSeconds wait = new WaitForSeconds(UpdateSpeed);

        while (enabled && currentBeacon != null)
        {
            // Ustaw destination tylko RAZ dla każdego beacona
            if (!destinationSet)
            {
                Agent.SetDestination(currentBeacon.Position);
                destinationSet = true;
                Debug.Log($"{name}: Ustawiam cel na beacon: {currentBeacon.name}");
            }

            // Sprawdź czy dotarł
            if (!Agent.pathPending)
            {
                float distance = Vector3.Distance(transform.position, currentBeacon.Position);
                
                if (distance <= arrivalDistance)
                {
                    OnReachedBeacon();
                    destinationSet = false;
                }
            }

            yield return wait;
        }
        
        Debug.Log($"{name}: Zakończył trasę (brak więcej beaconów)");
    }

    private EvacuationBeacon FindNearestBeacon()
    {
        EvacuationBeacon[] beacons = FindObjectsOfType<EvacuationBeacon>();

        if (beacons.Length == 0)
        {
            Debug.LogError("Brak obiektów EvacuationBeacon w scenie!");
            return null;
        }

        EvacuationBeacon nearest = null;
        float bestDistance = Mathf.Infinity;

        foreach (var beacon in beacons)
        {
            if (!beacon.IsActive) continue;

            float dist = Vector3.Distance(transform.position, beacon.Position);
            
            // Opcjonalnie: sprawdź czy beacon jest w zasięgu widoczności
            if (dist <= beacon.VisibilityRange && dist < bestDistance)
            {
                bestDistance = dist;
                nearest = beacon;
            }
        }

        if (nearest == null)
        {
            // Jeśli żaden beacon nie jest w zasięgu, weź po prostu najbliższy
            foreach (var beacon in beacons)
            {
                if (!beacon.IsActive) continue;
                
                float dist = Vector3.Distance(transform.position, beacon.Position);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    nearest = beacon;
                }
            }
        }

        return nearest;
    }

    private void OnReachedBeacon()
    {
        Debug.Log($"{name}: Dotarł do beacona: {currentBeacon.name}");

        // Sprawdź czy to finalny beacon (wyjście)
        if (currentBeacon.IsFinalExit)
        {
            Debug.Log($"{name}: Dotarł do wyjścia!");
            currentBeacon = null;
            return;
        }

        // Idź do następnego beacona
        if (currentBeacon.NextBeacon != null)
        {
            currentBeacon = currentBeacon.NextBeacon;
            Debug.Log($"{name}: Następny beacon: {currentBeacon.name}");
        }
        else
        {
            Debug.Log($"{name}: Brak kolejnego beacona - koniec trasy");
            currentBeacon = null;
        }
    }
}