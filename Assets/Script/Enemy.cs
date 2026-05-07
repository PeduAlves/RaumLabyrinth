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
    private bool pursuing = false;
    private bool walking = false;
    private bool searching = false;
    private bool hasLanded = false;
    private bool stunned = false;
    // private bool cripped = false; This exists in case of implementing the crippled state, which would be a state where the enemy is not fully disabled but has reduced movement capabilities.
    private bool disabled = false;
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
        if (stunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                Unstun();
            }
            return;
        }
        if (disabled) return;
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

    public void Stun(float time)
    {
        stunned = true;
        stunTimer = time;
        agent.enabled = false;
    }

    void Unstun()
    {
        stunned = false;
        if (!disabled) agent.enabled = true;
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
        disabled = true;
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
        disabled = false;
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
