using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class AgentController : MonoBehaviour, IAlarmObserver
{
    #region Ustawienia Agenta
    [Header("Agent Settings")]
    [SerializeField] private AgentType agentType;
    [SerializeField] private AgentTraits traits;
    private NavMeshAgent navAgent;
    private AgentState currentState = AgentState.Wandering;
    
    public enum AgentState
    {
        Wandering,
        Evacuating
    }

    private EvacuationBeacon currentTargetBeacon = null;
    private float wanderTimer = 0f;
    private float wanderInterval = 5f;
    private Bounds? wanderBounds = null;
    private float timeSinceLastBeaconSeen = 0f;
    private PanicSystem panicSystem = new PanicSystem();
    
    [Header("Room/Wandering System")]
    [SerializeField] private float minTimeInRoom = 5f;
    [SerializeField] private float maxTimeInRoom = 20f;
    [SerializeField] private float chanceToChangeRoom = 0.4f;
    
    private BoxCollider currentRoom = null;
    private float timeInCurrentRoom = 0f;
    private float targetTimeInRoom;
    #endregion

    #region Publiczne properties
    public AgentType Type => agentType;
    public AgentTraits Traits => traits;
    public AgentState CurrentState => currentState;
    public float TimeSinceLastBeaconSeen => timeSinceLastBeaconSeen;
    public NavMeshAgent NavAgent => navAgent;
    public bool HasTargetBeacon => currentTargetBeacon != null;
    #endregion

    #region Unity Methods (Start, OnDestroy, Initialize)
    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (AlarmSystem.Instance != null)
        {
            AlarmSystem.Instance.RegisterObserver(this);
        }

        wanderTimer = Random.Range(1f, wanderInterval);
        
        // Znajdź najbliższy pokój i zacznij w nim
        currentRoom = FindNearestRoom();
        if (currentRoom != null)
        {
            targetTimeInRoom = Random.Range(minTimeInRoom, maxTimeInRoom);
            WanderInCurrentRoom();
        }
        else
        {
            // Fallback - zwykłe wandering
            WanderAround();
        }
    }

    private void OnDestroy()
    {
        if (AlarmSystem.Instance != null)
        {
            AlarmSystem.Instance.UnregisterObserver(this);
        }
    }

    public void Initialize(AgentType type, AgentTraits assignedTraits)
    {
        agentType = type;
        traits = assignedTraits;

        if (navAgent != null)
        {
            navAgent.speed = traits.MoveSpeed;
            navAgent.acceleration = 8f;
            navAgent.angularSpeed = 120f;
            
            if (agentType == AgentType.Elderly || agentType == AgentType.Disabled)
            {
                navAgent.avoidancePriority = 40;
            }
            else
            {
                navAgent.avoidancePriority = 50 + Random.Range(0, 10);
            }

            navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            navAgent.autoBraking = false;
        }
    }
    #endregion

    public void OnAlarmTriggered(Vector3 alarmPosition)
    {
        if (currentState == AgentState.Evacuating) return;
        StartCoroutine(ReactToAlarm(traits.ReactionTime));
    }

    private IEnumerator ReactToAlarm(float delay)
    {
        yield return new WaitForSeconds(delay);
        currentState = AgentState.Evacuating;
    }

    private void Update()
    {
        if (currentState == AgentState.Wandering)
        {
            UpdateWandering();
        }
        else if (currentState == AgentState.Evacuating)
        {
            UpdateEvacuating();
        }
    }

    private void UpdateWandering()
    {
        wanderTimer += Time.deltaTime;
        timeInCurrentRoom += Time.deltaTime;
        
        bool needsNewDestination = !navAgent.hasPath || 
                                   navAgent.remainingDistance < 1.5f ||
                                   wanderTimer >= wanderInterval;
        
        if (needsNewDestination)
        {
            wanderTimer = 0f;
            
            // Sprawdź czy czas zmienić pokój
            if (timeInCurrentRoom >= targetTimeInRoom)
            {
                // Losuj czy zmienić pokój (25% szans)
                if (Random.value < chanceToChangeRoom)
                {
                    Debug.Log($"{name} changing room");
                    MoveToNewRoom();
                }
                else
                {
                    // Zostań w pokoju, resetuj czas
                    Debug.Log($"{name} staying in room");
                    timeInCurrentRoom = 0f;
                    targetTimeInRoom = Random.Range(minTimeInRoom, maxTimeInRoom);
                    WanderInCurrentRoom();
                }
            }
            else
            {
                // Wciąż w pokoju - pokreć się trochę
                WanderInCurrentRoom();
            }
        }
    }

    private void UpdateEvacuating()
    {
        timeSinceLastBeaconSeen += Time.deltaTime;       
        panicSystem.UpdatePanicLevel(this, Time.deltaTime);

        if (IsNearExit(transform.position, 3f))
        {
            Debug.Log($"{name} reached Exit - evacuation complete!");
            gameObject.SetActive(false);
            return;
        }

        if (currentTargetBeacon != null)
        {
            bool beaconReached = navAgent.hasPath && navAgent.remainingDistance < 1.5f && !navAgent.pathPending;
            
            if (beaconReached)
            {
                timeSinceLastBeaconSeen = 0f;
                Debug.Log($"{name} reached beacon {currentTargetBeacon.name}");
                
                if (currentTargetBeacon.NextBeacon != null)
                {
                    currentTargetBeacon = currentTargetBeacon.NextBeacon;
                    navAgent.SetDestination(currentTargetBeacon.Position);
                    Debug.Log($"{name} moving to next beacon: {currentTargetBeacon.name}");
                    return;
                }
                else
                {
                    Debug.Log($"{name} reached final beacon, looking for exit");
                    currentTargetBeacon = null;
                    DecideEvacuationTarget();
                    return;
                }
            }
        }

        if (!navAgent.hasPath || navAgent.pathPending)
        {
            DecideEvacuationTarget();
        }
    }

    private void DecideEvacuationTarget()
    {
        if (currentTargetBeacon != null && currentTargetBeacon.IsActive)
        {
            float pathDist = GetNavMeshPathDistance(transform.position, currentTargetBeacon.Position);
            if (pathDist != Mathf.Infinity && navAgent.hasPath && navAgent.remainingDistance > 1.5f)
            {
                return;
            }
        }
        
        EvacuationBeacon visibleBeacon = FindNearestVisibleBeacon();
        if (visibleBeacon != null)
        {
            Debug.Log($"{name} sees beacon: {visibleBeacon.name}");
            currentTargetBeacon = visibleBeacon;
            timeSinceLastBeaconSeen = 0f;
            navAgent.SetDestination(visibleBeacon.Position);
            return;
        }

        Vector3? corridorPoint = FindNearestCorridorPoint();
        if (corridorPoint.HasValue && Vector3.Distance(transform.position, corridorPoint.Value) > 0.2f)
        {
            Debug.Log($"{name} heading to corridor to find beacon");
            navAgent.SetDestination(corridorPoint.Value);
            return;
        }

        Debug.LogWarning($"{name} wandering on corridor looking for beacon");
        WanderAroundOnCorridor();
    }

    private EvacuationBeacon FindNearestVisibleBeacon()
    {
        EvacuationBeacon[] beacons = FindObjectsByType<EvacuationBeacon>(FindObjectsSortMode.None);
        EvacuationBeacon best = null;
        float minDist = Mathf.Infinity;

        float extendedVisionRange = traits.VisionRange * 1.5f;

        foreach (var beacon in beacons)
        {
            if (!beacon.IsActive) continue;
            
            if (beacon.Position.y > transform.position.y + 2.2f) continue;

            float pathDist = GetNavMeshPathDistance(transform.position, beacon.Position);          
            if (pathDist == Mathf.Infinity) continue;
            
            if (!HasLineOfSight(beacon.Position)) continue;
            
            if (pathDist <= extendedVisionRange && pathDist < minDist)
            {
                minDist = pathDist;
                best = beacon;
            }
        }
        
        return best;
    }

    private float GetNavMeshPathDistance(Vector3 start, Vector3 end)
    {
        NavMeshPath path = new NavMeshPath();
        
        NavMeshHit startHit, endHit;
        if (!NavMesh.SamplePosition(start, out startHit, 2f, NavMesh.AllAreas))
            return Mathf.Infinity;
        if (!NavMesh.SamplePosition(end, out endHit, 2f, NavMesh.AllAreas))
            return Mathf.Infinity;
        
        if (!NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path))
            return Mathf.Infinity;
        
        if (path.status != NavMeshPathStatus.PathComplete)
            return Mathf.Infinity;
        
        float distance = 0f;
        for (int i = 1; i < path.corners.Length; i++)
        {
            distance += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }
        
        return distance;
    }

    private bool HasLineOfSight(Vector3 targetPos)
    {
        Vector3 direction = targetPos - transform.position;
        float distance = direction.magnitude;
        
        if (distance < 0.1f) return true;
        
        RaycastHit[] hits = Physics.RaycastAll(
            transform.position + Vector3.up * 0.6f, 
            direction.normalized, 
            distance
        );
        
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.isTrigger)
                continue;
            
            EvacuationBeacon beaconHit = hit.collider.GetComponent<EvacuationBeacon>();
            if (beaconHit != null && Vector3.Distance(beaconHit.Position, targetPos) < 0.5f)
            {
                return true;
            }
            
            return false;
        }
        
        return true;
    }

    private Vector3? FindNearestCorridorPoint()
    {
        GameObject[] corridors = GameObject.FindGameObjectsWithTag("Corridor");
        
        if (corridors.Length == 0) return null;

        Vector3? nearestPoint = null;
        float minDist = Mathf.Infinity;

        foreach (var corridor in corridors)
        {
            if (corridor.transform.position.y > transform.position.y + 1f) continue;

            Collider corridorCollider = corridor.GetComponent<Collider>();
            if (corridorCollider != null)
            {
                Vector3 closestPoint = corridorCollider.ClosestPoint(transform.position);
                float dist = Vector3.Distance(transform.position, closestPoint);

                if (dist < minDist)
                {
                    minDist = dist;
                    nearestPoint = closestPoint;
                }
            }
        }

        return nearestPoint;
    }

    private bool IsNearExit(Vector3 pos, float radius)
    {
        GameObject[] exits = GameObject.FindGameObjectsWithTag("Exit");
        foreach (var exit in exits)
        {
            if (Vector3.Distance(pos, exit.transform.position) < radius)
                return true;
        }
        return false;
    }

    public void SetWanderBounds(Bounds bounds)
    {
        wanderBounds = bounds;
    }

    private void WanderAround()
    {
        Vector3 targetPos;

        if (wanderBounds.HasValue)
        {
            Bounds b = wanderBounds.Value;
            targetPos = new Vector3(
                Random.Range(b.min.x, b.max.x),
                transform.position.y,
                Random.Range(b.min.z, b.max.z)
            );
        }
        else
        {
            float wanderRadius = 15f;
            targetPos = transform.position + Random.insideUnitSphere * wanderRadius;
            targetPos.y = transform.position.y;
        }

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 5f, NavMesh.AllAreas))
        {
            if (!IsNearExit(hit.position, 5f))
            {
                navAgent.SetDestination(hit.position);
            }
        }
    }

    private void WanderAroundOnCorridor()
    {
        float wanderRadius = 10f;
        Vector3 randomPoint = transform.position + Random.insideUnitSphere * Random.Range(5f, wanderRadius);
        randomPoint.y = transform.position.y;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomPoint, out hit, 5f, NavMesh.AllAreas))
        {
            if (!IsNearExit(hit.position, 5f))
            {
                navAgent.SetDestination(hit.position);
            }
        }
    }

    private void WanderAroundPanic()
    {
        float panicRadius = 20f;
        Vector3 targetPos = transform.position + Random.insideUnitSphere * panicRadius;
        targetPos.y = transform.position.y;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 10f, NavMesh.AllAreas))
        {
            navAgent.SetDestination(hit.position);
        }
    }

    private BoxCollider FindNearestRoom()
    {
        GameObject[] rooms = GameObject.FindGameObjectsWithTag("HazardZone");
        
        if (rooms.Length == 0)
        {
            Debug.LogWarning($"{name}: No HazardZones found!");
            return null;
        }

        BoxCollider nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var room in rooms)
        {
            BoxCollider roomCollider = room.GetComponent<BoxCollider>();
            if (roomCollider == null) continue;

            float heightDiff = Mathf.Abs(roomCollider.bounds.center.y - transform.position.y);
            if (heightDiff > 2f) continue;

            float dist = Vector3.Distance(transform.position, roomCollider.bounds.center);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = roomCollider;
            }
        }

        return nearest;
    }

    private void WanderInCurrentRoom()
    {
        if (currentRoom == null)
        {
            currentRoom = FindNearestRoom();
            if (currentRoom == null)
            {
                WanderAround();
                return;
            }
        }

        Vector3 targetPos = GetRandomPointInRoom(currentRoom);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 5f, NavMesh.AllAreas))
        {
            navAgent.SetDestination(hit.position);
            Debug.Log($"{name} wandering in room {currentRoom.name}");
        }
        else
        {
            Debug.LogWarning($"{name}: No NavMesh in room {currentRoom.name}, finding new room");
            MoveToNewRoom();
        }
    }

    private void MoveToNewRoom()
    {
        GameObject[] rooms = GameObject.FindGameObjectsWithTag("HazardZone");
        
        if (rooms.Length == 0)
        {
            WanderAround();
            return;
        }

        var availableRooms = new System.Collections.Generic.List<BoxCollider>();
        
        foreach (var room in rooms)
        {
            BoxCollider roomCollider = room.GetComponent<BoxCollider>();
            if (roomCollider == null || roomCollider == currentRoom) continue;

            float heightDiff = Mathf.Abs(roomCollider.bounds.center.y - transform.position.y);
            if (heightDiff > 2f) continue;

            availableRooms.Add(roomCollider);
        }

        if (availableRooms.Count == 0)
        {
            Debug.Log($"{name} no other rooms available, staying");
            timeInCurrentRoom = 0f;
            targetTimeInRoom = Random.Range(minTimeInRoom, maxTimeInRoom);
            WanderInCurrentRoom();
            return;
        }

        BoxCollider newRoom = availableRooms[Random.Range(0, availableRooms.Count)];
        
        Vector3? corridorPoint = FindCorridorBetweenRooms(currentRoom, newRoom);
        
        if (corridorPoint.HasValue)
        {
            navAgent.SetDestination(corridorPoint.Value);
            Debug.Log($"{name} moving to corridor towards new room");
        }
        else
        {
            navAgent.SetDestination(GetRandomPointInRoom(newRoom));
        }

        currentRoom = newRoom;
        timeInCurrentRoom = 0f;
        targetTimeInRoom = Random.Range(minTimeInRoom, maxTimeInRoom);
    }

    private Vector3? FindCorridorBetweenRooms(BoxCollider roomA, BoxCollider roomB)
    {
        GameObject[] corridors = GameObject.FindGameObjectsWithTag("Corridor");
        
        if (corridors.Length == 0) return null;

        Vector3 midPoint = (roomA.bounds.center + roomB.bounds.center) / 2f;

        Vector3? bestPoint = null;
        float minDist = Mathf.Infinity;

        foreach (var corridor in corridors)
        {
            Collider corridorCollider = corridor.GetComponent<Collider>();
            if (corridorCollider == null) continue;

            float heightDiff = Mathf.Abs(corridorCollider.bounds.center.y - transform.position.y);
            if (heightDiff > 2f) continue;

            Vector3 closestPoint = corridorCollider.ClosestPoint(midPoint);
            float dist = Vector3.Distance(midPoint, closestPoint);

            if (dist < minDist)
            {
                minDist = dist;
                bestPoint = closestPoint;
            }
        }

        return bestPoint;
    }

    private Vector3 GetRandomPointInRoom(BoxCollider room)
    {
        Vector3 min = room.bounds.min;
        Vector3 max = room.bounds.max;

        return new Vector3(
            Random.Range(min.x, max.x),
            transform.position.y,
            Random.Range(min.z, max.z)
        );
    }

    public bool HasNearbyAgents(float radius)
    {
        AgentController[] agents = FindObjectsByType<AgentController>(FindObjectsSortMode.None);
        
        foreach (var other in agents)
        {
            if (other == this) continue;
            if (Vector3.Distance(transform.position, other.transform.position) < radius)
            {
                return true;
            }
        }
        
        return false;
    }
}