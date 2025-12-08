using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement : MonoBehaviour
{
    public Transform Target;
    public float UpdateSpeed = 0.1f;
    private NavMeshAgent Agent;
    
    [SerializeField] private string exitTag = "Exit"; // Tag dla wyjścia

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        // Jeśli nie ma przypisanego celu, szukaj wyjścia
        if (Target == null)
        {
            GameObject exit = GameObject.FindWithTag(exitTag);
            if (exit != null)
            {
                Target = exit.transform;
                Debug.Log("Znaleziono wyjście!");
            }
            else
            {
                Debug.LogWarning("Nie znaleziono wyjścia! Oznacz je tagiem 'Exit'");
            }
        }

        StartCoroutine(FollowTarget());
    }
    private IEnumerator FollowTarget()
    {
        WaitForSeconds wait = new WaitForSeconds(UpdateSpeed);

        while(enabled)
        {
            if(Target != null)
            {
                Agent.SetDestination(Target.position);
            }
            yield return wait;
        }
    }
}