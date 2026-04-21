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
    [SerializeField] private float patrolRadius = 5f;
    [SerializeField] private float maxSearchTime = 30f;
    private Vector3 lastTargetPosition = Vector3.zero;
    public float searchTimer = 0f; //ToDo: make it private
    public bool pursuing = false; //ToDo: make it private
    public bool walking = false; //ToDo: make it private
    public bool searching = false; //ToDo: make it private

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        visionRange = GetComponent<SphereCollider>();
        spawnLocation = transform.position;
        agent = GetComponent<NavMeshAgent>();
    }

    Vector3 ChooseRandomPoint(Vector3 startingPoint)
    {
        return (Random.insideUnitSphere * patrolRadius) + startingPoint;
    }

    void GoToLocation(Vector3 location)
    {
        walking = true;

        Vector3 adjustedTargetPosition = new Vector3(location.x, transform.position.y, location.z);

        agent.SetDestination(adjustedTargetPosition);

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
                pursuing = true;
            }
        }
    }

    void PursuePlayer()
    {
        GoToLocation(lastTargetPosition);
    }

    void Patrol(Vector3 areaCenter)
    {
        GoToLocation(ChooseRandomPoint(areaCenter));
    }
    
    void SearchPlayer()
    {
        Patrol(lastTargetPosition);
    }

    // Update is called once per frame
    void Update()
    {
        if (pursuing)
        {
            PursuePlayer();
        }
        else if (searching && !walking)
        {
            SearchPlayer();
        }
        else if (!walking) 
        {
            Patrol(spawnLocation);
        }
        if (!agent.pathPending && agent.remainingDistance <= 0.5f)
        {
            walking = false;
            if (pursuing)
            {
                pursuing = false; 
                searching = true;
            }
        }
        if (searching)
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= maxSearchTime)
            {
                searching = false;
                searchTimer = 0f;
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            PlayerDetector(other);
        }
    }
}
