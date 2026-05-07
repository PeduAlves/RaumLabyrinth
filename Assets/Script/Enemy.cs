using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{

    // components and references
    private SphereCollider visionRange;
    private Vector3 spawnLocation;
    private NavMeshAgent agent;

    [Header("Perception Settings")]
    [SerializeField] private float visionAngle = 80f;
    [SerializeField] private float perceptionDistance = 5;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private LayerMask playerLayer;

    [Header("Movement Settings")]
    [SerializeField] private float patrolRadius = 5f;
    [SerializeField] private float maxSearchTime = 30f;
    [SerializeField] private float maxStunTime = 2f;
    [SerializeField] private LayerMask groundLayer;

    // internal state variables
    private Vector3 lastTargetPosition = Vector3.zero;
    private float searchTimer = 0f;
    private bool isPursuing = false;
    private bool isWalking = false;
    private bool isSearching = false;
    private bool hasLanded = false;
    private bool isStunned = false;
    // private bool cripped = false; This exists in case of implementing the crippled state, which would be a state where the enemy is not fully disabled but has reduced movement capabilities.
    private bool isDisabled = false;
    private float stunTimer = 0f;

    void Start()
    {
        visionRange = GetComponent<SphereCollider>();
        spawnLocation = transform.position;
        agent = GetComponent<NavMeshAgent>();
    }

    private void OnCollisionEnter(Collision collision)
    {

        if (hasLanded) return;

        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {

            agent.enabled = true;

            Destroy(GetComponent<Rigidbody>());
            Destroy(GetComponent<BoxCollider>());
            hasLanded = true;
        }
    }

    Vector3 ChooseRandomPoint(Vector3 startingPoint)
    {
        return (Random.insideUnitSphere * patrolRadius) + startingPoint;
    }

    void GoToLocation(Vector3 location)
    {
        isWalking = true;

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
                isPursuing = true;
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
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                Unstun();
            }
            return;
        }
        if (isDisabled) return;
        if (isPursuing)
        {
            PursuePlayer();
        }
        else if (isSearching && !isWalking)
        {
            SearchPlayer();
        }
        else if (!isWalking) 
        {
            Patrol(spawnLocation);
        }
        if (!agent.pathPending && agent.remainingDistance <= 0.5f)
        {
            isWalking = false;
            if (isPursuing)
            {
                isPursuing = false; 
                isSearching = true;
            }
        }
        if (isSearching)
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= maxSearchTime)
            {
                isSearching = false;
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

    public void Stun(float time)
    {
        isStunned = true;
        stunTimer = time;
        agent.enabled = false;
    }

    void Unstun()
    {
        isStunned = false;
        if (!isDisabled) agent.enabled = true;
    }

    internal void TakeDamage(EnemyDamagablePart part)
    {
        if (part == EnemyDamagablePart.Head)
        {
            HeadDamamage();
        }
        else if (part == EnemyDamagablePart.Arm)
        {
            ArmDamage();
        }
        else if (part == EnemyDamagablePart.Leg)
        {
            LegDamage();
        }
        else if (part == EnemyDamagablePart.Torso)
        {
            throw new System.NotImplementedException("Standard damage");
        }

    }

    void HeadDamamage()
    {
        agent.enabled = false;
        isDisabled = true;
    }

    void ArmDamage()
    {
        Stun(maxStunTime);
    }

    void LegDamage()
    {
        agent.acceleration *= 0.5f;
        agent.speed *= 0.5f;
        // cripped = true;
        Stun(maxStunTime);
    }

    internal void Heal(EnemyDamagablePart part)
    {
        if (part == EnemyDamagablePart.Head)
        {
            HeadHealing();
        }
        else if (part == EnemyDamagablePart.Arm)
        {
            ArmHEaling();
        }
        else if (part == EnemyDamagablePart.Leg)
        {
            LegHealing();
        }
        else if (part == EnemyDamagablePart.Torso)
        {
            throw new System.NotImplementedException("Standard healing");
        }
    }

    void HeadHealing()
    {
        agent.enabled = true;
        isDisabled = false;
    }

    void ArmHEaling()
    {
        throw new System.NotImplementedException("Enabling attacks");
    }

    void LegHealing()
    {
        agent.acceleration *= 2f;
        agent.speed *= 2f;
        // cripped = false;
    }

}
