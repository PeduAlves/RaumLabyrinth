using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using FMODUnity;

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
    private float baseAcceleration;
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

    [Header("Game Juice / Camera Shake")]
    [Tooltip("Distância em que o shake do inimigo zera (perto = mais forte)")]
    [SerializeField] private float shakeMaxDistance = 15f;
    [SerializeField] private float footstepTrauma = 0.22f;
    [SerializeField] private float runFootstepTrauma = 0.40f;
    [Tooltip("Shake contínuo durante a perseguição normal (Chasing)")]
    [SerializeField] private float chaseContinuousTrauma = 0.10f;
    [Tooltip("Shake contínuo durante a perseguição rápida (Running)")]
    [SerializeField] private float runContinuousTrauma = 0.28f;
    [SerializeField] private float attackTrauma = 0.6f;
    [SerializeField] private float attackHitStop = 0.08f;
    [Tooltip("Gera pisões automaticamente. Desligue se usar Animation Events chamando Footstep().")]
    [SerializeField] private bool proceduralFootsteps = true;
    [Tooltip("Intervalo entre pisões andando (diminui conforme a velocidade aumenta)")]
    [SerializeField] private float walkStepInterval = 0.5f;
    [Tooltip("Tempo mínimo entre passos. Trava de segurança contra passos empilhados (som + shake).")]
    [SerializeField] private float minFootstepInterval = 0.3f;
    private float footstepTimer = 0f;
    private float lastFootstepTime = -999f;
    private Transform playerTransform;

    [Header("Áudio (FMOD)")]
    [Tooltip("Arraste o evento FMOD do passo do golem aqui")]
    [SerializeField] private EventReference footstepSound;
    [Tooltip("Arraste o evento FMOD do ataque do golem aqui")]
    [SerializeField] private EventReference attackSound;

    [Header("State Variables")]
    [SerializeField] private EnemyStates currentState;
    [SerializeField] private bool isSearching = false;
    [SerializeField] private bool isHeadBroken = false;
    [SerializeField] private bool isAirborne = true;

    private bool isPlayerInSight = false;
    // Cache do estado "broken" (a verdade lógica). Mantido em sincronia com o
    // animator em DisableAi/EnableAi para não ler animator.GetBool todo frame.
    private bool isBroken = false;
    private Vector3 lastTargetPosition;
    [SerializeField] private float searchTimer = 0f;
    private float attackCooldownTimer = 0f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        visionRange = GetComponent<SphereCollider>();

        runningSpeed = walkSpeed * runningSpeedMultiplier * chaseSpeedMultiplier;
        baseAcceleration = agent.acceleration;
        agent.speed = walkSpeed * chaseSpeedMultiplier;
        agent.angularSpeed = chaseAngularSpeed;
        // animator.speed = 1 / chaseSpeedMultiplier;
    }

    void Start()
    {
        minPatrolRadius = patrolRadius * minPatrolRadiusPercentage;
        spawnLocation = lastTargetPosition = transform.position;
        isBroken = GetCondition(EnemyConditions.Broken);

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

        if (agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= 0.1f && currentState.IsMovingState())
        {
            FinishLooking();
        }

        UpdateChaseShake();
        if (proceduralFootsteps) UpdateProceduralFootsteps();
    }

    // --- GAME JUICE / CAMERA SHAKE ---

    /// <summary>0..1 conforme a proximidade do player (1 = colado, 0 = além de shakeMaxDistance).</summary>
    private float ProximityFactor()
    {
        Transform p = GetPlayer();
        if (p == null) return 0f;
        float d = Vector3.Distance(transform.position, p.position);
        return Mathf.Clamp01(1f - d / shakeMaxDistance);
    }

    private Transform GetPlayer()
    {
        // O player é spawnado depois dos inimigos, então busca preguiçosamente até achar.
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
        return playerTransform;
    }

    /// <summary>Shake sustentado enquanto persegue — mais forte na perseguição rápida (Running).</summary>
    private void UpdateChaseShake()
    {
        if (CameraShake.Instance == null) return;

        float amount = 0f;
        if (currentState == EnemyStates.Running) amount = runContinuousTrauma;
        else if (currentState == EnemyStates.Chasing) amount = chaseContinuousTrauma;

        if (amount > 0f)
            CameraShake.Instance.ReportContinuousTrauma(amount * ProximityFactor());
    }

    private void UpdateProceduralFootsteps()
    {
        if (!currentState.IsMovingState() || !agent.isOnNavMesh) return;

        float speed = agent.velocity.magnitude;
        if (speed < 0.1f) return; // parado, sem pisão

        footstepTimer -= Time.deltaTime;
        if (footstepTimer <= 0f)
        {
            DoFootstep();
            // Passos mais rápidos quanto maior a velocidade, mas nunca abaixo do mínimo.
            footstepTimer = Mathf.Clamp(walkStepInterval * (walkSpeed / Mathf.Max(speed, 0.1f)), minFootstepInterval, walkStepInterval);
        }
    }

    /// <summary>Pisão via Animation Event: coloque um evento "Footstep" no frame em que o
    /// braço toca o chão. O ritmo vem da animação, então NÃO passa pela trava de tempo.</summary>
    public void Footstep() => DoFootstep(fromAnimation: true);

    private void DoFootstep(bool fromAnimation = false)
    {
        // Passos PROCEDURAIS podem disparar rápido demais (alta velocidade) → trava de tempo.
        // Animation Events já vêm no ritmo certo da animação → tocam direto, sem trava.
        if (!fromAnimation && Time.time - lastFootstepTime < minFootstepInterval) return;
        lastFootstepTime = Time.time;

        // Som do passo sempre toca (a atenuação por distância é do próprio evento FMOD 3D);
        // o shake da câmera é que depende da proximidade.
        if (!footstepSound.IsNull) RuntimeManager.PlayOneShot(footstepSound, transform.position);

        if (CameraShake.Instance == null) return;

        float prox = ProximityFactor();
        if (prox <= 0f) return;

        float baseTrauma = currentState == EnemyStates.Running ? runFootstepTrauma : footstepTrauma;
        // prox ao quadrado dá mais peso quando o player está bem perto.
        CameraShake.Instance.AddTrauma(baseTrauma * prox * prox);
    }

    private void TriggerAttackJuice()
    {
        if (!attackSound.IsNull) RuntimeManager.PlayOneShot(attackSound, transform.position);

        if (CameraShake.Instance != null)
            CameraShake.Instance.AddTrauma(attackTrauma * Mathf.Max(ProximityFactor(), 0.4f));
        HitStop.Do(attackHitStop);
    }

    // --- STATE MACHINE & CONDITIONS ---
    void ChangeStates(EnemyStates newState, bool force = false)
    {
        if (currentState == newState || isBroken) return;
        if (!force && currentState.GetStatePriority() > newState.GetStatePriority()) return;

        OnExitState(currentState, newState);
        currentState = newState;
        OnEnterState(newState);
    }

    // Lógica ao SAIR de um estado.
    void OnExitState(EnemyStates from, EnemyStates to)
    {
        // Desliga o blend de corrida ao deixar o Running — exceto indo atacar, onde o
        // ataque "corrido" precisa do bool ainda ligado (EndAttack o limpa depois).
        if (from == EnemyStates.Running && to != EnemyStates.Attacking)
            animator.SetBool(EnemyStates.Running.GetAnimationStateBooleanName(), false);
    }

    // Lógica ao ENTRAR num estado: agente, animação e velocidades num lugar só.
    void OnEnterState(EnemyStates state)
    {
        // O agente só fica ativo nos estados de movimento; desligado nos demais.
        agent.enabled = state.IsMovingState();

        animator.SetTrigger(state.GetAnimationTrigger());
        if (state == EnemyStates.Running)
            animator.SetBool(state.GetAnimationStateBooleanName(), true);

        ApplyStatePhysics();
    }

    // Recalcula velocidade / aceleração / velocidade angular do estado atual aplicando
    // o modificador de membros quebrados. Pode ser chamado fora de uma troca de estado
    // (ex: ao quebrar/curar um membro) para refletir o novo modificador.
    void ApplyStatePhysics()
    {
        float baseSpeed = walkSpeed;
        float baseAngular = chaseAngularSpeed;
        animator.speed = 1f;

        switch (currentState)
        {
            case EnemyStates.Walking:
                baseSpeed = walkSpeed;
                baseAngular = chaseAngularSpeed * angularSpeedWalkMultiplier;
                animator.speed = 1f / chaseSpeedMultiplier;
                break;
            case EnemyStates.Chasing:
                baseSpeed = walkSpeed * chaseSpeedMultiplier;
                baseAngular = chaseAngularSpeed;
                break;
            case EnemyStates.Running:
                baseSpeed = runningSpeed;
                baseAngular = chaseAngularSpeed * angularSpeedRunningMultiplier;
                break;
        }

        float damageModifier = CrippleSpeedModifier();
        agent.speed = baseSpeed * damageModifier;
        agent.angularSpeed = baseAngular;
        agent.acceleration = baseAcceleration * damageModifier;
    }

    // Produto dos multiplicadores de cada membro quebrado (1 = sem dano).
    float CrippleSpeedModifier()
    {
        float m = 1f;
        if (GetCondition(EnemyConditions.BrokeLeftLeg)) m *= crippleSpeedMultiplier;
        if (GetCondition(EnemyConditions.BrokeRightLeg)) m *= crippleSpeedMultiplier;
        if (GetCondition(EnemyConditions.BrokeLeftArm)) m *= crippleSpeedMultiplier;
        if (GetCondition(EnemyConditions.BrokeRightArm)) m *= crippleSpeedMultiplier;
        return m;
    }

    void SetCondition(EnemyConditions condition, bool value) => animator.SetBool(condition.GetAnimatorConditionName(), value);
    bool GetCondition(EnemyConditions condition) => animator.GetBool(condition.GetAnimatorConditionName());
    bool IsBroken() => isBroken;

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
        isBroken = true;
        SetCondition(EnemyConditions.Broken, true);
        agent.enabled = false;
    }

    public void EnableAi()
    {
        isBroken = false;
        agent.enabled = true;
        SetCondition(EnemyConditions.Broken, false);
    }

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
        if (isBroken) return;

        // ChangeStates reativa o agente nos estados de movimento; por isso o guard de
        // NavMesh vem DEPOIS dele (senão, ao sair de Looking/Attacking com o agente
        // ainda desligado, nunca conseguiríamos andar).
        ChangeStates(moveState, allowOverride);

        if (!agent.enabled || !agent.isOnNavMesh) return;

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
        Transform player = GetPlayer();
        if (player == null) return;

        lastTargetPosition = player.position;
        isPlayerInSight = true;

        // Sem membros quebrados, parte para a perseguição rápida; senão, perseguição normal.
        GoToLocation(lastTargetPosition, HasAllLimbs() ? EnemyStates.Running : EnemyStates.Chasing);
    }

    internal void DestroyPart(EnemyDamagablePart part)
    {
        if (part == EnemyDamagablePart.Head) isHeadBroken = true;
        else if (part != EnemyDamagablePart.Torso)
            SetCondition(part.GetContionOfPartDamage(), true);

        if (CheckIfPartBroken() || currentState == EnemyStates.Running) DisableAi();

        // O modificador de velocidade é aplicado num só lugar (ApplyStatePhysics);
        // não mexemos em agent.speed/acceleration manualmente para não conflitar.
        ApplyStatePhysics();
    }

    internal void Heal(EnemyDamagablePart part)
    {
        if (part == EnemyDamagablePart.Head) isHeadBroken = false;
        else if (part != EnemyDamagablePart.Torso)
            SetCondition(part.GetContionOfPartDamage(), false);

        if (!CheckIfPartBroken()) EnableAi();

        // Restaura a velocidade conforme os membros que voltaram a funcionar.
        ApplyStatePhysics();
    }

    void ExecuteAttack(Vector3 playerPos)
    {
        if (bodyParts[EnemyDamagablePart.RightArm].isDisabled && bodyParts[EnemyDamagablePart.LeftArm].isDisabled) return;

        if (agent.isOnNavMesh) agent.isStopped = true;
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
        {
            InstantiateAndSetupShockwave(rightFistCollider.transform.position);
            TriggerAttackJuice();
        }
    }

    void SpawnLeftShokwave(float shockwaveSpeed)
    {
        if (!bodyParts[EnemyDamagablePart.LeftArm].isDisabled)
        {
            InstantiateAndSetupShockwave(leftFistCollider.transform.position);
            TriggerAttackJuice();
        }
    }

    void EndAttack()
    {
        rightFistCollider.enabled = leftFistCollider.enabled = false;
        attackCooldownTimer = attackCooldown;
        animator.SetBool(EnemyStates.Running.GetAnimationStateBooleanName(), false);

        if (isPlayerInSight)
            GoToLocation(lastTargetPosition, EnemyStates.Chasing, true);
        else
            ResetAi();
    }
}