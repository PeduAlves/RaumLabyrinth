using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{

    private SphereCollider visionRange;
    private Vector3 spawnLocation;
    private NavMeshAgent agent;
    [SerializeField] private float visionAngle = 80f;
    [SerializeField] private float perceptionDistance = 5;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float idleSpeed = 0.5f;
    [SerializeField] private float pursueSpeed = 1;
    [SerializeField] private float turningSpeed = 1;
    [SerializeField] private float headTurnSpeed = 1.5f;
    private Vector3 lastTargetPosition = Vector3.zero;
    private bool searching = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        visionRange = GetComponent<SphereCollider>();
        spawnLocation = transform.position;
        agent = GetComponent<NavMeshAgent>();
    }

    void PlayerDetector(Collider target)
    {
        Vector3 headCenter = transform.TransformPoint(visionRange.center);
        Vector3 targetDirection = (target.transform.position - headCenter).normalized;
        if ((Vector3.Angle(transform.forward, targetDirection) < visionAngle / 2) ||
            (Vector3.Distance(transform.position, target.transform.position) < perceptionDistance))
        {
            float distanciaParaAlvo = Vector3.Distance(headCenter, target.transform.position);

            if (Physics.Raycast(headCenter, targetDirection, distanciaParaAlvo, obstacleLayer))
            {
                Debug.DrawRay(headCenter, targetDirection * distanciaParaAlvo, Color.red);
            }
            else if (Physics.Raycast(headCenter, targetDirection, distanciaParaAlvo, playerLayer))
            {
                Debug.DrawRay(headCenter, targetDirection * distanciaParaAlvo, Color.green);
                lastTargetPosition = target.transform.position;
                searching = true;
            }
        }
    }

    void PursuePlayer()
    {
        if (searching)
        {
            Vector3 adjustedTargetPosition = new Vector3(lastTargetPosition.x, transform.position.y, lastTargetPosition.z);
            /* Vector3 targetDirection = (adjustedTargetPosition - transform.position).normalized;

            if (targetDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turningSpeed * Time.deltaTime);
            }

            transform.Translate(Vector3.forward * pursueSpeed * Time.deltaTime); */

            agent.SetDestination(adjustedTargetPosition);

            if (Vector3.Distance(adjustedTargetPosition, transform.position) <= 0.5f)
            {
                searching = false;
            }
        }
    }

    void Patrol(Vector3 areaCenter)
    {
        // ToDo: Implement patrol behavior here: the enemy should move in a radious around the spawn location, changing direction randomly after a certain time or distance.
        // if it's out of the patrol range, it should start the searchingForPlayer behavior.
    }

    void Seeking()
    {
        // ToDo: Implement searching for player behavior here: the enemy should patrol the last known position of the player and look around for a short time, if it doesn't find the player, it should come back to spawn.
    }

    private void OnTriggerStay(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            PlayerDetector(other);
        }
    }

    // Update is called once per frame
    void Update()
    {
        PursuePlayer();
    }
}
