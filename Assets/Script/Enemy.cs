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
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float chaseSpeedMultiplier = 5f;
    [SerializeField] private float crippleSpeedMultiplier = 0.5f;
    [SerializeField] private float runningSpeedMultiplier = 1.5f;
    [SerializeField] private float chaseAngularSpeed = 100;
    [SerializeField] private float angularSpeedWalkMultiplier = 0.5f;
    [SerializeField] private float angularSpeedRunningMultiplier = 2f;
    private float runningSpeed;
    [SerializeField] private float patrolRadius = 7f;
    [Range(0f, 1f)][SerializeField] private float minPatrolRadiusPercentage = 0.5f;
    [SerializeField] private LayerMask groundLayer;
    private float minPatrolRadius;
    private Vector3 spawnLocation;

    [Header("Attack Settings")]
    [SerializeField] private Collider rightFistCollider;
    [SerializeField] private Collider leftFistCollider;
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private GameObject shokwavePrefab;

    [Header("State Variables")]
    [SerializeField] private EnemyStates currentState;
    [SerializeField] private bool isSearching = false;
    [SerializeField] private bool isHeadBroken = false;
    [SerializeField] private bool isAirborne = true;

    private bool isPlayerInSight = false;
    private Vector3 lastTargetPosition;
    [SerializeField] private float searchTimer = 0f;
    private float attackCooldownTimer = 0f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        visionRange = GetComponent<SphereCollider>();

        runningSpeed = walkSpeed * runningSpeedMultiplier * chaseSpeedMultiplier;
        agent.speed = walkSpeed * chaseSpeedMultiplier;
        agent.angularSpeed = chaseAngularSpeed;
        // animator.speed = 1 / chaseSpeedMultiplier;
    }

    void Start()
    {
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

        if (attackCooldownTimer > 0f)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        if (!agent.pathPending && agent.remainingDistance <= 0.1f && currentState.IsMovingState())
        {
            FinishLooking();
        }
    }

    // --- STATE MACHINE & CONDITIONS ---
    void ChangeStates(EnemyStates newState, bool force = false)
    {
        if (currentState == newState || IsBroken()) return;
        if (!force && currentState.GetStatePriority() > newState.GetStatePriority()) return;

        if (currentState == EnemyStates.Running && newState != EnemyStates.Attacking)
        {
            animator.SetBool(currentState.GetAnimationStateBooleanName(), false);
        }

        if (newState.IsMovingState())
        {
            agent.enabled = true;
        }
        else
        {
            agent.enabled = false;
        }

        currentState = newState;
        animator.SetTrigger(newState.GetAnimationTrigger());

        ApplyStatePhysics(currentState);
    }

    void ApplyStatePhysics(EnemyStates state) {

        float baseSpeed = walkSpeed;
        float baseAngular = chaseAngularSpeed;
        animator.speed = 1f;

        switch (currentState)
        {
            case EnemyStates.Walking:
                baseSpeed = walkSpeed;
                baseAngular = chaseAngularSpeed * angularSpeedWalkMultiplier;
                animator.speed = 1/chaseSpeedMultiplier;
                break;
            case EnemyStates.Chasing:
                baseSpeed = walkSpeed * chaseSpeedMultiplier;
                baseAngular = chaseAngularSpeed;
                break;
            case EnemyStates.Running:
                baseSpeed = runningSpeed;
                baseAngular = chaseAngularSpeed * angularSpeedRunningMultiplier;
                animator.SetBool(currentState.GetAnimationStateBooleanName(), true);
                break;
        }

        float damageModifier = 1f;
        if (GetCondition(EnemyConditions.BrokeLeftLeg)) damageModifier *= crippleSpeedMultiplier;
        if (GetCondition(EnemyConditions.BrokeRightLeg)) damageModifier *= crippleSpeedMultiplier;
        if (GetCondition(EnemyConditions.BrokeLeftArm)) damageModifier *= crippleSpeedMultiplier;
        if (GetCondition(EnemyConditions.BrokeRightArm)) damageModifier *= crippleSpeedMultiplier;

        agent.speed = baseSpeed * damageModifier;
        agent.angularSpeed = baseAngular;
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
    private bool HasAllLimbs()
    {
        return !GetCondition(EnemyConditions.BrokeLeftArm) &&
               !GetCondition(EnemyConditions.BrokeRightArm) &&
               !GetCondition(EnemyConditions.BrokeLeftLeg) &&
               !GetCondition(EnemyConditions.BrokeRightLeg);
    }

    // --- AI CONTROLLERS ---
    public void DisableAi()
    {
        SetCondition(EnemyConditions.Broken, true);
        agent.enabled = false;
    }

    public void EnableAi() => SetCondition(EnemyConditions.Broken, false);

    public void FinishEnableAi()
    {
        ResetAi();
    }
        

    public void ResetAi()
    {
        ChangeStates(EnemyStates.Looking, true);
    }

    // --- MOVEMENT ---
    void GoToLocation(Vector3 location, EnemyStates moveState = EnemyStates.Walking, bool allowOverride = false)
    {
        ChangeStates(moveState, allowOverride);
        agent.isStopped = false;
        agent.SetDestination(new Vector3(location.x, transform.position.y, location.z));
    }

    void Patrol(Vector3 areaCenter)// ToDo: código gerado, examinar depois
    {
        Vector3 finalPoint = transform.position;
        float minDistanceSqr = minPatrolRadius * minPatrolRadius; // Evita usar Vector3.Distance (que usa raiz quadrada)
        int maxAttempts = 5; // Limite para evitar loops infinitos

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomDist = Random.Range(minPatrolRadius, patrolRadius);
            Vector3 candidatePoint = areaCenter + new Vector3(randomDir.x * randomDist, 0, randomDir.y * randomDist);

            // Compara a distância ao quadrado entre a posição atual e o ponto candidato
            if ((candidatePoint - transform.position).sqrMagnitude >= minDistanceSqr)
            {
                finalPoint = candidatePoint;
                break;
            }

            // Se estourar as tentativas, usa o último gerado como fallback para não travar a IA
            finalPoint = candidatePoint;
        }

        GoToLocation(finalPoint, EnemyStates.Walking, true);
    }

    public void FinishLooking() => Patrol(isSearching ? lastTargetPosition : spawnLocation);

    public void FinishTripping()
    {
        if (!CheckIfPartBroken()) EnableAi();
    }

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

        if (targetDistance <= attackRange && attackCooldownTimer <= 0f)
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
                GoToLocation(lastTargetPosition, EnemyStates.Chasing);
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
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        lastTargetPosition = player.transform.position;
        isPlayerInSight = true;
        if (player != null)
        {
            if (HasAllLimbs())
            {
                GoToLocation(lastTargetPosition, EnemyStates.Running);
            } else
            {
                GoToLocation(lastTargetPosition, EnemyStates.Chasing);
            }
        }
    }

    internal void DestroyPart(EnemyDamagablePart part)
    {
        if (part == EnemyDamagablePart.Head) isHeadBroken = true;
        else if (part != EnemyDamagablePart.Torso)
        {
            agent.acceleration *= 0.5f;
            agent.speed *= 0.5f;
            SetCondition(part.GetContionOfPartDamage(), true);
        }
        if (CheckIfPartBroken() || currentState == EnemyStates.Running) DisableAi();

        ApplyStatePhysics(currentState);
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

        agent.isStopped = true;
        ChangeStates(EnemyStates.Attacking);

        rightFistCollider.enabled = !bodyParts[EnemyDamagablePart.RightArm].isDisabled;
        leftFistCollider.enabled = !bodyParts[EnemyDamagablePart.LeftArm].isDisabled;
    }

    void InstantiateAndSetupShockwave(Vector3 spawnPosition)
    {
        GameObject wave = Instantiate(shokwavePrefab, spawnPosition, shokwavePrefab.transform.rotation);
        EnemyShockwave waveScript = wave.GetComponent<EnemyShockwave>();
    }

    // não sei pq, mas as mãos estão invertidas,
    // então o shokwave da mão direita sai da mão esquerda e vice versa,
    // isso é algo que pode ser corrigido futuramente, mas por enquanto é mais fácil deixar assim do que arrumar a animação
    void SpawnRightShokwave(float shockwaveSpeed)
    {
        if (!bodyParts[EnemyDamagablePart.RightArm].isDisabled)
            InstantiateAndSetupShockwave(rightFistCollider.transform.position);
    }

    void SpawnLeftShokwave(float shockwaveSpeed)
    {
        if (!bodyParts[EnemyDamagablePart.LeftArm].isDisabled) 
            InstantiateAndSetupShockwave(leftFistCollider.transform.position);
    }

    void EndAttack()
    {
        rightFistCollider.enabled = leftFistCollider.enabled = false;
        attackCooldownTimer = attackCooldown;
        animator.SetBool(EnemyStates.Running.GetAnimationStateBooleanName(), false);

        if (isPlayerInSight)
        {
            ChangeStates(EnemyStates.Chasing, true);
            agent.isStopped = false;
            agent.SetDestination(new Vector3(lastTargetPosition.x, transform.position.y, lastTargetPosition.z));
        }
        else
        {
            ResetAi();
        }
    }
}