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
    private float wanderInterval = 5f; // Zwiększone z 3 na 5 sekund
    private Bounds? wanderBounds = null;
    private float timeSinceLastBeaconSeen = 0f; // Czas od ostatniego zobaczenia beacona
    #endregion
    #region Panic System
    [Header("Panic System")]
    [SerializeField] private float panicLevel = 0f; // 0-1, gdzie 1 = pełna panika
    [SerializeField] private float panicIncreaseRate = 0.05f; // Jak szybko rośnie panika
    [SerializeField] private float panicDecreaseRate = 0.1f; // Jak szybko spada panika
    [SerializeField] private float panicThreshold = 0.7f; // Próg paniki (>0.7 = panikuje)
    #endregion
    #region Smoke Disorientation
    [Header("Smoke Disorientation")]
    [SerializeField] private bool isInSmoke = false;
    [SerializeField] private float smokeDisorientationChance = 0.3f; // 30% szans na dezorientację
    [SerializeField] private float smokeVisionReduction = 0.5f; // Zmniejszenie zasięgu wzroku o 50%
    #endregion
    #region  Publiczne properties
    public AgentType Type => agentType;
    public AgentTraits Traits => traits;
    public AgentState CurrentState => currentState;
    public float TimeSinceLastBeaconSeen => timeSinceLastBeaconSeen;
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

        wanderTimer = Random.Range(0f, wanderInterval);
        WanderAround();
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

    // Reakcja na alarm
    public void OnAlarmTriggered(Vector3 alarmPosition)
    {
        if (currentState == AgentState.Evacuating) return;

        StartCoroutine(ReactToAlarm(traits.ReactionTime));
    }

    private IEnumerator ReactToAlarm(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        Debug.Log($"{name} heard alarm! Starting evacuation!");
        currentState = AgentState.Evacuating;
    }

    // Odpalanie Update zaleznie od stanu
    private void Update()
    {
        if (currentState == AgentState.Wandering)
        {
            UpdateWandering();
        }
        else if (currentState == AgentState.Evacuating)
        {
            UpdateEvacuating();
            if (isInSmoke)
                ApplySmokeDisorientation();
        }
    }

    private void UpdateWandering()
    {
        wanderTimer += Time.deltaTime;
        
        bool needsNewDestination = !navAgent.hasPath || 
                                   navAgent.remainingDistance < 1.5f ||
                                   wanderTimer >= wanderInterval;
        
        if (needsNewDestination)
        {
            wanderTimer = 0f;
            WanderAround();
        }
    }

    private void UpdateEvacuating()
    {
        timeSinceLastBeaconSeen += Time.deltaTime;
        
        UpdatePanicLevel();
        

        if (IsNearExit(transform.position, 3f))
        {
            Debug.Log($"{name} reached Exit - evacuation complete!");
            gameObject.SetActive(false);
            return;
        }

        if (currentTargetBeacon != null && navAgent.hasPath)
        {
            float heightDiff = Mathf.Abs(currentTargetBeacon.Position.y - transform.position.y);
            float horizontalDist = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(currentTargetBeacon.Position.x, 0, currentTargetBeacon.Position.z)
            );
            
            if (heightDiff < 0.75f && horizontalDist < 3f)
            {
                timeSinceLastBeaconSeen = 0f;
                
                if (currentTargetBeacon.NextBeacon != null)
                {
                    Debug.Log($"{name} reached beacon, moving to next: {currentTargetBeacon.NextBeacon.name}");
                    currentTargetBeacon = currentTargetBeacon.NextBeacon;
                    navAgent.SetDestination(currentTargetBeacon.Position);
                }
                else
                {
                    Debug.Log($"{name} reached last beacon, heading to exit");
                    currentTargetBeacon = null;
                    Transform exit = FindNearestExit();
                    if (exit != null)
                    {
                        navAgent.SetDestination(exit.position);
                    }
                }
                return;
            }
        }

        
        if (!navAgent.hasPath || navAgent.remainingDistance < 1f)
        {
            DecideEvacuationTarget();
        }
    }
    // TODO: Ulepsz UpdatePanicLevel aby lepiej symulować panikę
    private void UpdatePanicLevel()
    {
        // Panika ROŚNIE:
        if (timeSinceLastBeaconSeen > 5f)
            panicLevel += panicIncreaseRate * Time.deltaTime;
        
        if (!HasNearbyAgents(5f))
            panicLevel += panicIncreaseRate * 0.5f * Time.deltaTime;
        
        if (!navAgent.hasPath)
            panicLevel += panicIncreaseRate * 2f * Time.deltaTime;
     
        // Panika SPADA:
        if (currentTargetBeacon != null && timeSinceLastBeaconSeen < 5f)
            panicLevel -= panicDecreaseRate * Time.deltaTime;
        
        if (navAgent.hasPath && Vector3.Distance(transform.position, navAgent.destination) < traits.VisionRange)
            panicLevel -= panicDecreaseRate * 0.5f * Time.deltaTime;

        panicLevel = Mathf.Clamp01(panicLevel);
        
        // === EFEKTY PANIKI ===
        if (panicLevel > panicThreshold)
        {
            // Przy wysokiej panice - zwiększ prędkość ale gorsze decyzje
            navAgent.speed = traits.MoveSpeed * (1f + panicLevel * 0.25f);
            
            // Losowo zmieniaj kierunek
            if (Random.value < 0.1f * panicLevel)
            {
                WanderAroundPanic();
            }
        }
        else
        {
            navAgent.speed = traits.MoveSpeed;
        }
    }
    
    // Sprawdza czy w pobliżu są inni agenci
    private bool HasNearbyAgents(float radius)
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

    // JESLI AGENT NIE MA BEACONA
    private void DecideEvacuationTarget()
    {
        // PRIORYTET 1: Szukaj widocznego beacona
        EvacuationBeacon visibleBeacon = FindNearestVisibleBeacon();
        if (visibleBeacon != null)
        {
            Debug.Log($"{name} sees beacon: {visibleBeacon.name}");
            currentTargetBeacon = visibleBeacon;
            timeSinceLastBeaconSeen = 0f;
            navAgent.SetDestination(visibleBeacon.Position);
            return;
        }

        // PRIORYTET 2: Jeśli nie widzi beacona - idź na korytarz
        Vector3? corridorPoint = FindNearestCorridorPoint();
        if (corridorPoint.HasValue)
        {
            Debug.Log($"{name} heading to corridor to find beacon");
            navAgent.SetDestination(corridorPoint.Value);
            return;
        }

        // PRIORYTET 3: Podążaj za agentem który zna drogę
        AgentController leader = FindReliableAgentWithBeacon();
        if (leader != null)
        {
            Debug.Log($"{name} following {leader.name} who recently saw beacon");
            navAgent.SetDestination(leader.transform.position);
            return;
        }

        // PRIORYTET 4: Szukaj Exit bezpośrednio
        Transform exit = FindNearestExitNotAbove();
        if (exit != null && Vector3.Distance(transform.position, exit.position) < traits.VisionRange)
        {
            Debug.Log($"{name} heading directly to exit");
            navAgent.SetDestination(exit.position);
            return;
        }

        // PRIORYTET 5: Biegaj losowo i szukaj beacona
        Debug.LogWarning($"{name} lost! Running randomly to find beacon");
        WanderAroundPanic();
    }

    private EvacuationBeacon FindNearestVisibleBeacon()
    {
        EvacuationBeacon[] beacons = FindObjectsByType<EvacuationBeacon>(FindObjectsSortMode.None);
        EvacuationBeacon best = null;
        float minDist = Mathf.Infinity;

        // Zmniejsz zasięg wzroku jeśli agent jest w dymie
        float effectiveVisionRange = traits.VisionRange;
        if (isInSmoke)
        {
            effectiveVisionRange *= smokeVisionReduction;
        }

        foreach (var beacon in beacons)
        {
            if (!beacon.IsActive) continue;
            
            float heightDiff = Mathf.Abs(beacon.Position.y - transform.position.y);
            if (heightDiff > 2.2f) continue;
            if (beacon.Position.y > transform.position.y + 0.5f) continue;

            float pathDist = GetNavMeshPathDistance(transform.position, beacon.Position);          
            if (pathDist == Mathf.Infinity) continue;
            
            if (pathDist <= effectiveVisionRange && pathDist < minDist && HasLineOfSight(beacon.Position))
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
        
        // Przyciągnij punkty do NavMesh
        NavMeshHit startHit, endHit;
        if (!NavMesh.SamplePosition(start, out startHit, 2f, NavMesh.AllAreas))
            return Mathf.Infinity;
        if (!NavMesh.SamplePosition(end, out endHit, 2f, NavMesh.AllAreas))
            return Mathf.Infinity;
        
        // Oblicz ścieżkę
        if (!NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path))
            return Mathf.Infinity;
        
        if (path.status != NavMeshPathStatus.PathComplete)
            return Mathf.Infinity;
        
        // Oblicz długość ścieżki
        float distance = 0f;
        for (int i = 1; i < path.corners.Length; i++)
        {
            distance += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }
        
        return distance;
    }

    private AgentController FindReliableAgentWithBeacon()
    {
        AgentController[] agents = FindObjectsByType<AgentController>(FindObjectsSortMode.None);
        AgentController best = null;
        float minDist = Mathf.Infinity;
        float maxTimeSinceBeacon = 10f; // Agent musi widzieć beacon max 10 sekund temu

        foreach (var other in agents)
        {
            if (other == this) continue;
            if (other.currentState != AgentState.Evacuating) continue;
            if (other.currentTargetBeacon == null) continue;
            if (other.timeSinceLastBeaconSeen > maxTimeSinceBeacon) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            
            if (dist <= traits.VisionRange && dist < minDist)
            {
                minDist = dist;
                best = other;
            }
        }
        
        return best;
    }

    private Transform FindNearestExitNotAbove()
    {
        GameObject[] exits = GameObject.FindGameObjectsWithTag("Exit");
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var exit in exits)
        {
            if (exit.transform.position.y > transform.position.y + 1f) continue;
            
            float dist = Vector3.Distance(transform.position, exit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = exit.transform;
            }
        }
        
        return nearest;
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

    private Transform FindNearestExit()
    {
        GameObject[] exits = GameObject.FindGameObjectsWithTag("Exit");
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var exit in exits)
        {
            float dist = Vector3.Distance(transform.position, exit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = exit.transform;
            }
        }
        
        return nearest;
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

    private bool HasLineOfSight(Vector3 targetPos)
    {
        Vector3 direction = targetPos - transform.position;
        float distance = direction.magnitude;
        
        if (Physics.Raycast(transform.position + Vector3.up, direction, out RaycastHit hit, distance))
        {
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Default") && !hit.collider.isTrigger)
            {
                return false;
            }
        }
        return true;
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

    private void WanderAroundPanic()
    {
        // Podczas paniki biegaj w losowym kierunku szukając beacona
        float panicRadius = 20f; // Większy promień niż zwykłe wanderowanie
        Vector3 targetPos = transform.position + Random.insideUnitSphere * panicRadius;
        targetPos.y = transform.position.y;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetPos, out hit, 10f, NavMesh.AllAreas))
        {
            navAgent.SetDestination(hit.position);
        }
    }

    private void ApplySmokeDisorientation()
    {
        // Zwiększ panikę w dymie
        panicLevel += panicIncreaseRate * 2f * Time.deltaTime;
        
        // Losowa szansa na dezorientację
        if (Random.value < smokeDisorientationChance * Time.deltaTime)
        {
            // Dezorientacja: losowe odchylenie od ścieżki
            Vector3 randomOffset = Random.insideUnitSphere * 3f;
            randomOffset.y = 0;
            
            Vector3 newDestination = transform.position + randomOffset;
            
            NavMeshHit hit;
            if (NavMesh.SamplePosition(newDestination, out hit, 5f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"{name} disoriented by smoke!");
                navAgent.SetDestination(hit.position);
            }
        }
        
        // Losowa szansa na "kaszel" - krótkie zatrzymanie
        if (Random.value < 0.05f * Time.deltaTime)
        {
            StartCoroutine(CoughInSmoke());
        }
    }

    private IEnumerator CoughInSmoke()
    {
        float originalSpeed = navAgent.speed;
        navAgent.speed = 0f; // Zatrzymaj się
        Debug.Log($"{name} coughing in smoke!");
        
        yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
        
        navAgent.speed = originalSpeed;
    }

    // Triggerem wykrywaj dym (postaw BoxCollider z triggerem i tagiem "Smoke")
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Smoke"))
        {
            isInSmoke = true;
            Debug.Log($"{name} entered smoke area");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Smoke"))
        {
            isInSmoke = false;
            Debug.Log($"{name} left smoke area");
        }
    }
}