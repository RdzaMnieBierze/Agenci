using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    public Transform Target;
    public float UpdateSpeed = 0.1f;

    private NavMeshAgent Agent;
    
    [SerializeField] private string exitTag = "Exit"; // Tag dla wyjścia


    //Logika do podążania za znakami

    [Header("Trasa prowadząca do wyjścia (ustalona w Inspectorze)")]
    public List<Transform> orderedPath = new List<Transform>();
    private Transform currentTarget;
    private Queue<Transform> remainingPath = new Queue<Transform>();
    [SerializeField] private string checkpointTag = "Checkpoint";
    private bool reachedFirstCheckpoint = false;
    [Header("Odległość, na której agent uznaje checkpoint za osiągnięty")]
    public float arrivalDistance = 5f;


    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
    }


    private void Start()
    {
        // 1) Znajdź najbliższy checkpoint (START)
        Transform nearest = FindNearestCheckpoint();
        if (nearest == null)
        {
            Debug.LogWarning("Brak checkpointów!");
            return;
        }

        currentTarget = nearest;

        // 2) Przygotuj ustaloną trasę do wyjścia

        bool firstCheckpoint = false;
        foreach (var cp in orderedPath)
        {
            if (cp == nearest)
            {
                firstCheckpoint = true;
                
            }
            if (firstCheckpoint)
            {
                remainingPath.Enqueue(cp);
            }
        }
       


        StartCoroutine(FollowTarget());
    }

    //private void Start()
    //{
    //    // Jeśli nie ma przypisanego celu, szukaj wyjścia
    //    if (Target == null)
    //    {
    //        GameObject exit = GameObject.FindWithTag(exitTag);
    //        if (exit != null)
    //        {
    //            Target = exit.transform;
    //            Debug.Log("Znaleziono wyjście!");
    //        }
    //        else
    //        {
    //            Debug.LogWarning("Nie znaleziono wyjścia! Oznacz je tagiem 'Exit'");
    //        }
    //    }

    //    StartCoroutine(FollowTarget());
    //}

    //private IEnumerator FollowTarget()
    //{
    //    WaitForSeconds wait = new WaitForSeconds(UpdateSpeed);

    //    while(enabled)
    //    {
    //        if(Target != null)
    //        {
    //            Agent.SetDestination(Target.position);
    //        }
    //        yield return wait;
    //    }
    //}

    private IEnumerator FollowTarget()
    {
        WaitForSeconds wait = new WaitForSeconds(UpdateSpeed);

        while (enabled)
        {
            if (currentTarget != null)
            {
                Agent.SetDestination(currentTarget.position);

                if (!Agent.pathPending && GetNavMeshDistance(transform.position, currentTarget.position) <= arrivalDistance)
                {
                    OnReachedTarget();
                }

            }
            yield return wait;
        }
    }

    private Transform FindNearestCheckpoint() //znajduje najbliższy checkpoint
    {
        GameObject[] cps = GameObject.FindGameObjectsWithTag(checkpointTag);

        Transform nearest = null;
        float best = Mathf.Infinity;

        Debug.Log("Liczba checkpointów: " + cps.Length);
        foreach (var cp in cps)
        {
            float dist = GetNavMeshDistance(transform.position, cp.transform.position);
            if (dist < best)
            {
                best = dist;
                nearest = cp.transform;
            }
        }

        return nearest;
    }

    private void OnReachedTarget()
    {
        Debug.Log("Agent dotarł do: " + currentTarget.name);

        if (!reachedFirstCheckpoint)
        {
            // Pierwszy cel (najbliższy) zakończony → teraz idziemy ustaloną trasą
            reachedFirstCheckpoint = true;

            if (remainingPath.Count > 0)
            {
                currentTarget = remainingPath.Dequeue();
                Debug.Log("Następny punkt (lista): " + currentTarget.name);
            }
                

            else
            {
                Debug.Log("Dotarł do końca trasy");
                currentTarget = null;
            }
             

            return;
        }

        // Kolejne checkpointy z ustalonej trasy
        if (remainingPath.Count > 0)
            currentTarget = remainingPath.Dequeue();
        else
            currentTarget = null;
    }

    public float GetNavMeshDistance(Vector3 start, Vector3 end)
    {
        NavMeshPath path = new NavMeshPath();

        // Przyciągamy punkty do NavMesh
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(start, out hit, 2f, NavMesh.AllAreas))
            return Mathf.Infinity;
        start = hit.position;

        if (!NavMesh.SamplePosition(end, out hit, 2f, NavMesh.AllAreas))
            return Mathf.Infinity;
        end = hit.position;

        if (!NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path))
            return Mathf.Infinity;

        float distance = 0f;

        for (int i = 1; i < path.corners.Length; i++)
        {
            distance += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }

        return distance;
    }



}