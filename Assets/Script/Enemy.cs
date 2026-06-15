using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    // References
    private SphereCollider visionRange;
    private NavMeshAgent agent;
    private Animator animator;
    private Dictionary<EnemyDamagablePart, EnemyPart> bodyParts = new Dictionary<EnemyDamagablePart, EnemyPart>();

    [Header("Perception Settings")]
    [SerializeField] private float visionAngle = 80f;
    [SerializeField] private float perceptionDistance = 5f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float maxSearchTime = 30f;

    [Header("Movement Settings")]
    [SerializeField] private float patrolRadius = 7f;
    [Range(0f, 1f)][SerializeField] private float minPatrolRadiusPercentage = 0.5f;
    [SerializeField] private LayerMask groundLayer;
    private float minPatrolRadius;
    private Vector3 spawnLocation;

    [Header("Attack Settings")]
    [SerializeField] private Collider rightFistCollider;
    [SerializeField] private Collider leftFistCollider;
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private GameObject shokwavePrefab;

    [Header("State Variables")]
    [SerializeField] private EnemyStates currentState;
    [SerializeField] private bool isSearching = false;
    [SerializeField] private bool isHeadBroken = false;
    [SerializeField] private bool isMoving = false;
    [SerializeField] private bool isAirborne = true;

    private bool isPlayerInSight = false;
    private Vector3 lastTargetPosition;
    private float searchTimer = 0f;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        visionRange = GetComponent<SphereCollider>();

        minPatrolRadius = patrolRadius * minPatrolRadiusPercentage;
        spawnLocation = lastTargetPosition = transform.position;

        rightFistCollider.enabled = leftFistCollider.enabled = false;

        foreach (var part in GetComponentsInChildren<EnemyPart>())
        {
            bodyParts[part.partType] = part;
        }
    }

    void Update()
    {
        if (IsBroken()) return;

        if (isSearching)
        {
            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0f) isSearching = false;
        }

        if (isMoving && !agent.pathPending && agent.remainingDistance <= 0.1f)
        {
            ResetAi();
        }
    }

    // --- STATE MACHINE & CONDITIONS ---
    void ChangeStates(EnemyStates newState, bool force = false)
    {
        if (currentState == newState || IsBroken()) return;
        if (!force && currentState.GetStatePriority() > newState.GetStatePriority()) return;

        currentState = newState;
        animator.SetTrigger(newState.GetAnimationTrigger());
    }

    void SetCondition(EnemyConditions condition, bool value) => animator.SetBool(condition.GetAnimatorConditionName(), value);
    bool GetCondition(EnemyConditions condition) => animator.GetBool(condition.GetAnimatorConditionName());
    bool IsBroken() => GetCondition(EnemyConditions.Broken);

    private bool CheckIfPartBroken()
    {
        bool anyArm = GetCondition(EnemyConditions.BrokeLeftArm) || GetCondition(EnemyConditions.BrokeRightArm);
        bool anyLeg = GetCondition(EnemyConditions.BrokeLeftLeg) || GetCondition(EnemyConditions.BrokeRightLeg);
        bool bothLegs = GetCondition(EnemyConditions.BrokeLeftLeg) && GetCondition(EnemyConditions.BrokeRightLeg);

        return bothLegs || (anyArm && anyLeg) || isHeadBroken;
    }

    // --- AI CONTROLLERS ---
    public void DisableAi()
    {
        SetCondition(EnemyConditions.Broken, true);
        agent.enabled = false;
        isMoving = false;
    }

    public void EnableAi() => SetCondition(EnemyConditions.Broken, false);

    public void FinishEnableAi()
    {
        agent.enabled = true;
        Patrol(spawnLocation);
    }
        

    public void ResetAi()
    {
        isMoving = false;
        ChangeStates(EnemyStates.Looking, true);
    }

    // --- MOVEMENT ---
    void GoToLocation(Vector3 location)
    {
        isMoving = true;
        ChangeStates(EnemyStates.Walking);
        agent.isStopped = false;
        agent.SetDestination(new Vector3(location.x, transform.position.y, location.z));
    }

    void Patrol(Vector3 areaCenter)
    {
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        float randomDist = Random.Range(minPatrolRadius, patrolRadius);
        Vector3 point = new Vector3(randomDir.x * randomDist, 0, randomDir.y * randomDist);

        GoToLocation(areaCenter + point);
    }

    public void FinishLooking() => Patrol(isSearching ? lastTargetPosition : spawnLocation);

    // --- PERCEPTION & COLLISIONS ---
    private void OnCollisionEnter(Collision collision)
    {
        if (isAirborne && ((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            isAirborne = false;
            if (!IsBroken()) agent.enabled = true;

            Destroy(GetComponent<Rigidbody>());
            Destroy(GetComponent<BoxCollider>());
            ResetAi();
            Patrol(spawnLocation);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (((1 << other.gameObject.layer) & playerLayer) != 0) PlayerDetector(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsBroken() || ((1 << other.gameObject.layer) & playerLayer) == 0) return;

        if (isPlayerInSight) PlayerSpotted(null);
    }

    void PlayerDetector(Collider target)
    {
        if (IsBroken() ||
            !agent.enabled ||
            currentState == EnemyStates.Attacking) return;

        Vector3 targetPos = target.transform.position;
        Vector3 headCenter = transform.TransformPoint(visionRange.center);
        float targetDistance = Vector3.Distance(headCenter, targetPos);

        if (targetDistance <= attackRange)
        {
            ExecuteAttack(targetPos);
            return;
        }

        Vector3 targetDirection = (targetPos - headCenter).normalized;

        if (Vector3.Angle(transform.forward, targetDirection) < visionAngle / 2f || targetDistance < perceptionDistance)
        {
            if (Physics.Raycast(headCenter, targetDirection, targetDistance, obstacleLayer))
            {
                PlayerSpotted(null);
            }
            else if (Physics.Raycast(headCenter, targetDirection, targetDistance, playerLayer))
            {
                PlayerSpotted(targetPos);
            }
        }
    }

    private void PlayerSpotted(Vector3? targetPosition)
    {
        if (targetPosition.HasValue)
        {
            isPlayerInSight = true;
            if (Vector3.Distance(lastTargetPosition, targetPosition.Value) > 1f || currentState != EnemyStates.Walking)
            {
                lastTargetPosition = targetPosition.Value;
                GoToLocation(lastTargetPosition);
            }
        }
        else if (isPlayerInSight)
        {
            isPlayerInSight = false;
            isSearching = true;
            searchTimer = maxSearchTime;
        }
    }

    // --- COMBAT & DAMAGE ---
    internal void TakeDamage(EnemyDamagablePart part)
    {
        if (part == EnemyDamagablePart.Head) isHeadBroken = true;
        else if (part != EnemyDamagablePart.Torso)
        {
            agent.acceleration *= 0.5f;
            agent.speed *= 0.5f;
            SetCondition(part.GetContionOfPartDamage(), true);
        }

        if (CheckIfPartBroken()) DisableAi();
    }

    internal void Heal(EnemyDamagablePart part)
    {
        if (part == EnemyDamagablePart.Head) isHeadBroken = false;
        else if (part != EnemyDamagablePart.Torso)
        {
            agent.acceleration *= 2f;
            agent.speed *= 2f;
            SetCondition(part.GetContionOfPartDamage(), false);
        }

        if (!CheckIfPartBroken()) EnableAi();
    }

    void ExecuteAttack(Vector3 playerPos)
    {
        if (bodyParts[EnemyDamagablePart.RightArm].isDisabled && bodyParts[EnemyDamagablePart.LeftArm].isDisabled) return;

        isMoving = false;
        agent.isStopped = true;
        ChangeStates(EnemyStates.Attacking);

        rightFistCollider.enabled = !bodyParts[EnemyDamagablePart.RightArm].isDisabled;
        leftFistCollider.enabled = !bodyParts[EnemyDamagablePart.LeftArm].isDisabled;
    }

    void SpawnShokwave()
    {// não sei pq, mas as mãos estão invertidas,
     // então o shokwave da mão direita sai da mão esquerda e vice versa,
     // isso é algo que pode ser corrigido futuramente, mas por enquanto é mais fácil deixar assim do que arrumar a animação
        if (!bodyParts[EnemyDamagablePart.RightArm].isDisabled)
            Instantiate(shokwavePrefab, leftFistCollider.transform.position, shokwavePrefab.transform.rotation);

        if (!bodyParts[EnemyDamagablePart.LeftArm].isDisabled)
            Instantiate(shokwavePrefab, rightFistCollider.transform.position, shokwavePrefab.transform.rotation);
    }

    void EndAttack()
    {
        ResetAi();
        rightFistCollider.enabled = leftFistCollider.enabled = false;
    }
}