using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshObstacle))]
public class FireSource : MonoBehaviour
{
    [SerializeField] private float damageRadius = 2f;
    [SerializeField] private float spreadChance = 0.1f; // Chance to spawn new fire nearby

    private void Start()
    {
        // Ensure obstacle carves the navmesh
        NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
        obstacle.carving = true;
        obstacle.shape = NavMeshObstacleShape.Capsule;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}