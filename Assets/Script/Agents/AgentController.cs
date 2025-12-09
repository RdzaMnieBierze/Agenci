using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class AgentController : MonoBehaviour, IAlarmObserver
{
    [Header("Agent Settings")]
    [SerializeField] private AgentType agentType;
    [SerializeField] private AgentTraits traits;

    private NavMeshAgent navAgent;
    private AgentState currentState = AgentState.Working;

    // State Machine Enum
    public enum AgentState
    {
        Working,
        Evacuating
    }

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        // Register to Alarm System
        if (AlarmSystem.Instance != null)
        {
            AlarmSystem.Instance.RegisterObserver(this);
        }

        // Start wandering immediately (randomize first wander time)
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
            
            if (agentType == AgentType.Elderly || agentType == AgentType.Disabled || agentType == AgentType.Blind)
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

    public void OnAlarmTriggered(Vector3 alarmPosition)
    {
        if (currentState == AgentState.Evacuating) return;

        // Reaction delay based on traits
        StartCoroutine(ReactToAlarm(traits.ReactionTime));
    }

    private IEnumerator ReactToAlarm(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        Debug.Log($"{name} heard the alarm! Evacuating!");
        currentState = AgentState.Evacuating;
    }

    private EvacuationBeacon currentTargetBeacon = null;
    private AgentController guidingAgent = null;
    private bool isBeingGuided = false;
    
    private float wanderTimer = 0f;
    private float wanderInterval = 3f; // Change direction every 3 seconds

    private void Update()
    {
        if (currentState == AgentState.Working)
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
        else if (currentState == AgentState.Evacuating)
        {
            if (isBeingGuided && guidingAgent != null)
            {
                float distanceToGuide = Vector3.Distance(transform.position, guidingAgent.transform.position);
                
                if (distanceToGuide > 2f)
                {
                    navAgent.SetDestination(guidingAgent.transform.position);
                }
                else if (distanceToGuide < 0.5f)
                {
                    navAgent.isStopped = true;
                }
                else
                {
                    navAgent.isStopped = false;
                }
                
                if (IsNearFinish(guidingAgent.transform.position, 3f))
                {
                    Debug.Log($"{name} (blind) reached Finish with guide!");
                    gameObject.SetActive(false);
                }
                return;
            }
            
            float distToBeacon = currentTargetBeacon != null ? Vector3.Distance(transform.position, currentTargetBeacon.Position) : Mathf.Infinity;
            bool isStuckNearBeacon = distToBeacon < 8.0f && navAgent.velocity.sqrMagnitude < 0.2f;
            
            if (currentTargetBeacon != null && (distToBeacon < 4.0f || isStuckNearBeacon))
            {
                if (currentTargetBeacon.NextBeacon != null)
                {
                    Debug.Log($"{name} reached {currentTargetBeacon.name}, moving to next beacon: {currentTargetBeacon.NextBeacon.name}");
                    currentTargetBeacon = currentTargetBeacon.NextBeacon;
                    navAgent.SetDestination(currentTargetBeacon.Position);
                }
                else
                {
                    Debug.Log($"{name} reached last beacon, searching for Finish...");
                    currentTargetBeacon = null;
                    Transform finish = FindNearestFinish();
                    if (finish != null)
                    {
                        Debug.Log($"{name} heading to Finish: {finish.name}");
                        navAgent.SetDestination(finish.position);
                    }
                }
            }
            else if (navAgent.hasPath && navAgent.remainingDistance < 2f && IsNearFinish(transform.position, 3f))
            {
                Debug.Log($"{name} reached Finish - evacuation complete!");
                gameObject.SetActive(false);
            }
            else if (currentTargetBeacon != null)
            {
                if (!navAgent.hasPath || navAgent.remainingDistance < 0.5f)
                {
                    navAgent.SetDestination(currentTargetBeacon.Position);
                }
            }
            else if (!navAgent.hasPath || navAgent.pathStatus != UnityEngine.AI.NavMeshPathStatus.PathComplete)
            {
                DecideEvacuationTarget();
            }
        }
    }

    private void DecideEvacuationTarget()
    {
        if (agentType == AgentType.Blind && !isBeingGuided)
        {
            AgentController guide = FindNearestSightedEvacuatingAgent();
            if (guide != null)
            {
                Debug.Log($"{name} (blind) found guide: {guide.name}");
                guidingAgent = guide;
                isBeingGuided = true;
                navAgent.SetDestination(guide.transform.position);
                return;
            }
            else
            {
                Debug.LogWarning($"{name} (blind): No guide found! Panicking!");
                WanderAround();
                return;
            }
        }
        
        Vector3 fireAvoidanceDir = GetFireAvoidanceDirection();
        
        EvacuationBeacon bestBeacon = FindNearestVisibleBeacon(fireAvoidanceDir);
        if (bestBeacon != null)
        {
            Debug.Log($"{name} sees a beacon at {bestBeacon.name} and fleeing from fire!");
            currentTargetBeacon = bestBeacon;
            navAgent.SetDestination(bestBeacon.Position);
            return;
        }

        AgentController leader = FindNearestEvacuatingAgentWithBeacon();
        if (leader != null)
        {
            Debug.Log($"{name} is following {leader.name} who knows the way!");
            navAgent.SetDestination(leader.transform.position);
            return;
        }

        Transform finish = FindNearestFinish();
        if (finish != null)
        {
            Debug.Log($"{name} is heading directly to Finish (no beacons visible)");
            currentTargetBeacon = null;
            navAgent.SetDestination(finish.position);
            return;
        }

        if (fireAvoidanceDir != Vector3.zero)
        {
            Vector3 fleePoint = transform.position + fireAvoidanceDir * 10f;
            navAgent.SetDestination(fleePoint);
            Debug.LogWarning($"{name}: Fleeing blindly from fire!");
        }
        else
        {
            Debug.LogWarning($"{name}: No exit found! Panic!");
            WanderAround();
        }
    }

    private Vector3 GetFireAvoidanceDirection()
    {
        FireSource[] fires = FindObjectsByType<FireSource>(FindObjectsSortMode.None);
        if (fires.Length == 0) return Vector3.zero;

        Vector3 avoidanceDir = Vector3.zero;
        int threatsCount = 0;

        foreach (var fire in fires)
        {
            Vector3 dirFromFire = transform.position - fire.transform.position;
            float distance = dirFromFire.magnitude;

            if (distance < 20f)
            {
                float weight = 1f - (distance / 20f);
                avoidanceDir += dirFromFire.normalized * weight;
                threatsCount++;
            }
        }

        if (threatsCount > 0)
        {
            avoidanceDir.y = 0;
            return avoidanceDir.normalized;
        }

        return Vector3.zero;
    }

    private EvacuationBeacon FindNearestVisibleBeacon(Vector3 preferredDirection)
    {
        EvacuationBeacon[] beacons = FindObjectsByType<EvacuationBeacon>(FindObjectsSortMode.None);
        EvacuationBeacon best = null;
        float bestScore = -Mathf.Infinity;

        foreach (var beacon in beacons)
        {
            if (!beacon.IsActive) continue;
            
            float dst = Vector3.Distance(transform.position, beacon.Position);
            if (dst <= traits.VisionRange)
            {
                if (HasLineOfSight(beacon.Position))
                {
                    Vector3 toBeacon = (beacon.Position - transform.position).normalized;
                    
                    if (preferredDirection != Vector3.zero)
                    {
                        float alignment = Vector3.Dot(toBeacon, preferredDirection);
                        
                        if (alignment < -0.3f)
                        {
                            continue;
                        }
                    }
                    
                    float distanceScore = 1f - (dst / traits.VisionRange);
                    
                    float directionScore = 1f;
                    if (preferredDirection != Vector3.zero)
                    {
                        directionScore = Vector3.Dot(toBeacon, preferredDirection);
                        directionScore = Mathf.Max(0, directionScore);
                    }
                    
                    float totalScore = distanceScore * 0.3f + directionScore * 0.7f;
                    
                    if (totalScore > bestScore)
                    {
                        bestScore = totalScore;
                        best = beacon;
                    }
                }
            }
        }
        return best;
    }

    private AgentController FindNearestEvacuatingAgent()
    {
        AgentController[] agents = FindObjectsByType<AgentController>(FindObjectsSortMode.None);
        AgentController best = null;
        float minDst = Mathf.Infinity;

        foreach (var other in agents)
        {
            if (other == this) continue;
            if (other.currentState != AgentState.Evacuating) continue;
            
            if (!other.navAgent.hasPath) continue;

            float dst = Vector3.Distance(transform.position, other.transform.position);
            if (dst <= traits.VisionRange && dst < minDst)
            {
                if (HasLineOfSight(other.transform.position))
                {
                    minDst = dst;
                    best = other;
                }
            }
        }
        return best;
    }

    private AgentController FindNearestEvacuatingAgentWithBeacon()
    {
        AgentController[] agents = FindObjectsByType<AgentController>(FindObjectsSortMode.None);
        AgentController best = null;
        float minDst = Mathf.Infinity;

        foreach (var other in agents)
        {
            if (other == this) continue;
            if (other.currentState != AgentState.Evacuating) continue;
            
            if (other.currentTargetBeacon == null) continue;
            if (!other.navAgent.hasPath) continue;

            float myDistToTarget = currentTargetBeacon != null ? Vector3.Distance(transform.position, currentTargetBeacon.Position) : Mathf.Infinity;
            float theirDistToTarget = Vector3.Distance(other.transform.position, other.currentTargetBeacon.Position);

            if (theirDistToTarget >= myDistToTarget) continue;

            float dst = Vector3.Distance(transform.position, other.transform.position);
            if (dst <= traits.VisionRange && dst < minDst)
            {
                if (HasLineOfSight(other.transform.position))
                {
                    minDst = dst;
                    best = other;
                }
            }
        }
        return best;
    }

    private AgentController FindNearestSightedEvacuatingAgent()
    {
        AgentController[] agents = FindObjectsByType<AgentController>(FindObjectsSortMode.None);
        AgentController best = null;
        float minDst = Mathf.Infinity;

        foreach (var other in agents)
        {
            if (other == this) continue;
            if (other.agentType == AgentType.Blind) continue;
            if (other.currentState != AgentState.Evacuating) continue;
            
            bool hasBeacon = other.currentTargetBeacon != null;
            
            float dst = Vector3.Distance(transform.position, other.transform.position);
            
            if (dst <= traits.HearingRange && dst < minDst)
            {
                if (hasBeacon || best == null)
                {
                    minDst = dst;
                    best = other;
                }
            }
        }
        return best;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (currentState != AgentState.Evacuating) return;

        AgentController other = collision.gameObject.GetComponent<AgentController>();
        if (other != null && other.currentState == AgentState.Evacuating)
        {
            ShareInfoWith(other);
        }
    }

    private void ShareInfoWith(AgentController other)
    {
        if (currentTargetBeacon == null || currentTargetBeacon.NextBeacon == null) return;
        if (other.currentTargetBeacon == null) return;

        if (other.currentTargetBeacon == currentTargetBeacon.NextBeacon)
        {
            Debug.Log($"{name} received info from {other.name} (collision): Skipping {currentTargetBeacon.name}, going to {currentTargetBeacon.NextBeacon.name}");
            currentTargetBeacon = currentTargetBeacon.NextBeacon;
            navAgent.SetDestination(currentTargetBeacon.Position);
        }
    }

    private bool IsNearAnyExit(Vector3 pos, float radius)
    {
        GameObject[] exits = GameObject.FindGameObjectsWithTag("Exit");
        foreach (var exit in exits)
        {
            if (Vector3.Distance(pos, exit.transform.position) < radius) return true;
        }
        return false;
    }

    private Transform FindNearestExit()
    {
        GameObject[] exits = GameObject.FindGameObjectsWithTag("Exit");
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var exit in exits)
        {
            // Use NavMesh path distance for accuracy, or Euclidean for performance
            float d = Vector3.Distance(transform.position, exit.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = exit.transform;
            }
        }
        return nearest;
    }

    private Transform FindNearestFinish()
    {
        GameObject[] finishes = GameObject.FindGameObjectsWithTag("Exit");
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (var finish in finishes)
        {
            float d = Vector3.Distance(transform.position, finish.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = finish.transform;
            }
        }
        return nearest;
    }

    private bool IsNearFinish(Vector3 pos, float radius)
    {
        GameObject[] finishes = GameObject.FindGameObjectsWithTag("Exit");
        foreach (var finish in finishes)
        {
            if (Vector3.Distance(pos, finish.transform.position) < radius) return true;
        }
        return false;
    }

    // DetectFire removed - global alarm used instead

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


    private Bounds? wanderBounds = null;

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

        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(targetPos, out hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            if (!IsNearAnyExit(hit.position, 5f))
            {
                navAgent.SetDestination(hit.position);
            }
        }
    }

    private bool IsNearExit(Vector3 position, float distance)
    {
        GameObject[] exits = GameObject.FindGameObjectsWithTag("Exit");
        foreach (var exit in exits)
        {
            if (Vector3.Distance(position, exit.transform.position) < distance)
            {
                return true;
            }
        }
        return false;
    }

    public AgentType Type => agentType;
    public AgentTraits Traits => traits;
}