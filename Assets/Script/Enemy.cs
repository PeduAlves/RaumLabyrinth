using System.Collections.Generic;
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
    [SerializeField] private float patrolRadius = 7f;
    [Range(0f, 1f)] private float minPatrolRadiusPercentage = 0.5f;
    private float minPatrolRadius;
    [SerializeField] private float maxSearchTime = 30f;
    [SerializeField] private float maxStunTime = 2f;
    [SerializeField] private LayerMask groundLayer;

    // internal state variables
    private bool isPlayerInSight = false;
    private Vector3 lastTargetPosition = Vector3.zero;
    private float searchTimer = 0f;
    private float stunTimer = 0f;
    private Dictionary<EnemyDamagablePart, EnemyPart> bodyParts = new Dictionary<EnemyDamagablePart, EnemyPart>();

    [SerializeField] private bool isDisabled = false;
    [SerializeField] private bool isStunned = false;
    [SerializeField] private EnemyState[] states = new EnemyState[2];
    [SerializeField] private EnemyMovementState movementState;

    void Start()
    {
        minPatrolRadius = patrolRadius * minPatrolRadiusPercentage;
        ChangeStates(EnemyState.Idle);
        movementState = EnemyMovementState.Airborne;
        visionRange = GetComponent<SphereCollider>();
        spawnLocation = transform.position;
        agent = GetComponent<NavMeshAgent>();
        
        EnemyPart[] parts = GetComponentsInChildren<EnemyPart>();
        foreach (EnemyPart part in parts)
        {
            bodyParts[part.partType] = part;
        }
    }

    EnemyState getCurrentState()
    {
        return states[0];
    }

    EnemyState GetLastState()
    {
        return states[1];
    }

    void ChangeStates(EnemyState newState)
    {
        // This method can be expanded to include any necessary logic when changing states, such as resetting timers or triggering animations.
        if (getCurrentState() == newState) return;
        states[1] = states[0];
        states[0] = newState;
    }

    void ChangeMovementState(EnemyMovementState newState)
    {
        // This method can be expanded to include any necessary logic when changing movement states, such as adjusting speed or enabling/disabling certain behaviors.
        movementState = newState;
    }

    public void Stun()
    {
        isStunned = true;
        stunTimer = maxStunTime;
        agent.enabled = false;
    }

    void Unstun()
    {
        isStunned = false;
        agent.enabled = true;
    }

    public void DisableAi()
    {
        isDisabled = true;
        agent.enabled = false;
    }

    public void EnableAi()
    {
        isDisabled = false;
        agent.enabled = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (movementState == EnemyMovementState.Airborne)
        {
            if (((1 << collision.gameObject.layer) & groundLayer) != 0)
            {

                agent.enabled = true;

                Destroy(GetComponent<Rigidbody>());
                Destroy(GetComponent<BoxCollider>());
                ChangeMovementState(EnemyMovementState.Idle);
            }
        }
    }

    Vector3 ChooseRandomPoint(Vector3 startingPoint)
    {
        // 1. Gera uma direção aleatória em um círculo (eixo X e Z)
        Vector2 randomDir = Random.insideUnitCircle.normalized;

        // 2. Define uma distância aleatória entre o raio mínimo e o máximo
        float randomDistance = Random.Range(minPatrolRadius, patrolRadius);

        // 3. Calcula a posição final
        Vector3 point = new Vector3(
            randomDir.x * randomDistance,
            0,
            randomDir.y * randomDistance
        );

        return startingPoint + point;
    }

    void GoToLocation(Vector3 location)
    {
        ChangeMovementState(EnemyMovementState.Moving);

        Vector3 adjustedTargetPosition = new Vector3(location.x, transform.position.y, location.z);

        agent.SetDestination(adjustedTargetPosition);

    }

    void Patrol(Vector3 areaCenter)
    {
        GoToLocation(ChooseRandomPoint(areaCenter));
    }

    // Update is called once per frame
    // Working here primarely as a state machine controller
    void Update()
    {
        if (isDisabled) return;

        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f)
            {
                Unstun();
            } else return;
        }

        if (isPlayerInSight && getCurrentState() != EnemyState.Pursuing)
        {
            ChangeStates(EnemyState.Pursuing);
        }

        if (getCurrentState() == EnemyState.Pursuing) 
        {
            GoToLocation(lastTargetPosition);
        }

        if (!agent.pathPending && agent.remainingDistance <= 0.1f)
        {
            ChangeMovementState(EnemyMovementState.Idle);
            if (getCurrentState() == EnemyState.Pursuing)
            {
                ChangeStates(EnemyState.Searching);
            }
        }

        if (getCurrentState() == EnemyState.Searching)
        {
            searchTimer += Time.deltaTime;
            if (searchTimer >= maxSearchTime)
            {
                ChangeStates(EnemyState.Idle);
                searchTimer = 0f;
            }
        }

        if (movementState == EnemyMovementState.Idle)
        {
            if (getCurrentState() == EnemyState.Searching)
            {
                Patrol(lastTargetPosition);
                return;
            }
            if (getCurrentState() == EnemyState.Idle)
            {
                Patrol(spawnLocation);
                return;
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

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            if(isPlayerInSight) isPlayerInSight = false;
        }
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
                isPlayerInSight = false;
            }
            else if (Physics.Raycast(headCenter, targetDirection, distanciaParaAlvo, playerLayer))
            {
                Debug.DrawRay(headCenter, targetDirection * distanciaParaAlvo, Color.green);
                lastTargetPosition = target.transform.position;
                isPlayerInSight = true;
            }
        }
    }

    internal void TakeDamage(EnemyDamagablePart part)
    {
        if (part == EnemyDamagablePart.Head)
        {
            DisableAi();
        }
        else if (part == EnemyDamagablePart.LeftArm || part == EnemyDamagablePart.RightArm)
        {
            Stun();
        }
        else if (part == EnemyDamagablePart.LeftLeg || part == EnemyDamagablePart.RightLeg)
        {
            agent.acceleration *= 0.5f;
            agent.speed *= 0.5f;
            // cripped = true;
            Stun();
        }
        else if (part == EnemyDamagablePart.Torso)
        {
            throw new System.NotImplementedException("Mas O torço é implacável!");
        }

    }

    internal void Heal(EnemyDamagablePart part)
    {
        if (part == EnemyDamagablePart.Head)
        {
            EnableAi();
        }
        else if (part == EnemyDamagablePart.LeftLeg || part == EnemyDamagablePart.RightLeg)
        {
            agent.acceleration *= 2f;
            agent.speed *= 2f;
            // cripped = false;
        }
        else if (part == EnemyDamagablePart.Torso)
        {
            throw new System.NotImplementedException("Standard healing");
        }
    }
}
