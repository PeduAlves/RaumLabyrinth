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
    [SerializeField] private float perceptionDistance = 5f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private LayerMask playerLayer;

    [Header("Movement Settings")]
    [SerializeField] private float patrolRadius = 7f;
    [Range(0f, 1f)] private float minPatrolRadiusPercentage = 0.5f;
    private float minPatrolRadius;
    [SerializeField] private float maxSearchTime = 30f;
    [SerializeField] private float maxStunTime = 2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Attack Settings")]
    [SerializeField] private SphereCollider rightFistCollider;
    [SerializeField] private SphereCollider leftFistCollider;
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private GameObject shokwavePrefab;

    [Header("State Variables")]
    [SerializeField] private bool isDisabled = false;
    [SerializeField] private bool isStunned = false;
    [SerializeField] private bool isSearching = false;
    [SerializeField] private EnemyState[] states = new EnemyState[2];   //ToDo: remove serializefield
    [SerializeField] private EnemyMovementState movementState;          //ToDo: remove serializefield
    private bool isPlayerInSight = false;
    private Vector3 lastTargetPosition;
    private float searchTimer = 0f;
    private float stunTimer = 0f;
    private Dictionary<EnemyDamagablePart, EnemyPart> bodyParts = new Dictionary<EnemyDamagablePart, EnemyPart>();
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        minPatrolRadius = patrolRadius * minPatrolRadiusPercentage;
        ChangeStates(EnemyState.Broke);
        movementState = EnemyMovementState.Airborne;
        visionRange = GetComponent<SphereCollider>();
        spawnLocation = transform.position;
        lastTargetPosition = spawnLocation;
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
        animator.SetTrigger(newState.GetAnimationTrigger());
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
        agent.isStopped = true;
        ChangeStates(EnemyState.Broke);
    }

    void Unstun()
    {
        isStunned = false;
        agent.isStopped = false;
        ChangeStates(GetLastState());
    }

    public void DisableAi()
    {
        isDisabled = true;
        agent.enabled = false;
        ChangeStates(EnemyState.Broke);
    }

    public void EnableAi()
    {
        isDisabled = false;
        agent.enabled = true;
        if (isStunned)
        {
            Unstun();
        }
        else
        {
            ChangeStates(GetLastState());
        }
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
                ChangeStates(EnemyState.Looking);
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
        ChangeStates(EnemyState.Walking);

        Vector3 adjustedTargetPosition = new Vector3(location.x, transform.position.y, location.z);
        if (agent.isStopped) agent.isStopped = false;
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

        if (isSearching)
        {
            searchTimer -= Time.deltaTime;

            if (searchTimer <= 0f)
            {
                isSearching = false;
            }
        }

        if (movementState == EnemyMovementState.Moving && !agent.pathPending && agent.remainingDistance <= 0.1f)
        {
            ChangeMovementState(EnemyMovementState.Idle);
            ChangeStates(EnemyState.Looking);
        }
    }

    public void FinishLooking()
    {
        if (isSearching) Patrol(lastTargetPosition);
        else Patrol(spawnLocation);
    }

    private void PlayerSpotted(Vector3? targetPosition)
    {
        if (targetPosition.HasValue)
        {
            isPlayerInSight = true;
            if (Vector3.Distance(lastTargetPosition, targetPosition.Value) > 1f || getCurrentState() != EnemyState.Walking)
            {
                lastTargetPosition = targetPosition.Value;
                GoToLocation(lastTargetPosition);
            }
        }
        else
        {
            isPlayerInSight = false;
            isSearching = true;
            searchTimer = maxSearchTime;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            if (isPlayerInSight)
            {
                PlayerSpotted(null);
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

    void PlayerDetector(Collider target)
    {
        if (isDisabled || isStunned || getCurrentState() == EnemyState.Attacking) return;

        Vector3 headCenter = transform.TransformPoint(visionRange.center);
        Vector3 targetDirection = (target.transform.position - headCenter).normalized;
        float targetDistance = Vector3.Distance(headCenter, target.transform.position);

        // 1. Faz as checagens de visão PRIMEIRO, independente de onde o alvo está
        if ((Vector3.Angle(transform.forward, targetDirection) < visionAngle / 2) ||
            (Vector3.Distance(transform.position, target.transform.position) < perceptionDistance))
        {
            if (Physics.Raycast(headCenter, targetDirection, targetDistance, obstacleLayer))
            {
                Debug.DrawRay(headCenter, targetDirection * targetDistance, Color.red);
                PlayerSpotted(null);
            }
            else if (Physics.Raycast(headCenter, targetDirection, targetDistance, playerLayer))
            {
                Debug.DrawRay(headCenter, targetDirection * targetDistance, Color.green);

                PlayerSpotted(target.transform.position);
            }
        }

        if (Vector3.Distance(transform.position, target.transform.position) <= attackRange && getCurrentState() != EnemyState.Attacking)
        {
            ExecuteAttack(target.transform.position);
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

    void ExecuteAttack(Vector3 playerPos)
    {
        if (bodyParts[EnemyDamagablePart.RightArm].isDisabled && bodyParts[EnemyDamagablePart.LeftArm].isDisabled)
        {
            // Both arms are disabled, cannot attack
            return;
        }

        ChangeMovementState(EnemyMovementState.Idle);
        agent.isStopped = true;
        ChangeStates(EnemyState.Attacking);

        if (!bodyParts[EnemyDamagablePart.RightArm].isDisabled)
            rightFistCollider.enabled = true;
            
        if (!bodyParts[EnemyDamagablePart.LeftArm].isDisabled)
            leftFistCollider.enabled = true;
        
    }

    void SpawnShokwave()
    {
        if (!bodyParts[EnemyDamagablePart.RightArm].isDisabled)
        {
            Instantiate(shokwavePrefab, rightFistCollider.transform.position, shokwavePrefab.transform.rotation);
        }

        if (!bodyParts[EnemyDamagablePart.LeftArm].isDisabled)
        {
            Instantiate(shokwavePrefab, leftFistCollider.transform.position, shokwavePrefab.transform.rotation);
        }
    }

    void EndAttack() {
        ChangeStates(EnemyState.Looking);
        rightFistCollider.enabled = false;
        leftFistCollider.enabled = false;
    }

}
