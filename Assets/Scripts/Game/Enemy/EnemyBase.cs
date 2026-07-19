using System;
using System.Collections;
using Game.Gameplay;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour, IParryResponder
{
    [Header("Primary Attributes")]
    public PrimaryAttributes primary = new PrimaryAttributes
    {
        strength = 3,
        innerForce = 1,
        vitality = 2,
        spirit = 1,
        comprehension = 1
    };
    public CombatStyle combatStyle = CombatStyle.Sword;

    [Header("Base Combat")]
    public int hp = 30;
    public int maxHp = 30;
    public float moveSpeed = 2f;
    public int damage = 10;
    public float attackRange = 1.5f;
    public float chaseRange = 8f;
    public float attackDuration = 0.3f;
    public int expValue = 1;
    public float damageReduction;

    [Header("Telegraph")]
    public float telegraphDuration = 0.6f;
    public bool isCurrentAttackParryable = true;
    public Color parryableColor = new Color(1f, 0.9f, 0f);
    public Color unparryableColor = new Color(1f, 0f, 0f);

    [Header("AI")]
    public float decisionInterval = 0.5f;
    public float idleChance = 0.1f;

    public EnemyState CurrentState { get; protected set; } = EnemyState.Idle;
    public bool IsDead { get; protected set; }
    public EnemyStatBaseline Baseline { get; private set; }
    public EnemyAttackPlan CurrentAttackPlan { get; private set; }
    public EnemyAttackPhase CurrentAttackPhase { get; private set; } = EnemyAttackPhase.Complete;
    public event Action<EnemyBase> OnDeath;
    public event Action<int, int> OnHealthChanged;

    protected Rigidbody2D _rb;
    protected SpriteRenderer _sprite;
    protected Transform _player;
    protected float _distanceToPlayer;
    protected float _stateTimer;
    protected float _decisionTimer;
    protected int _facingDirection = 1;

    private Hurtbox _hurtbox;
    private AttackTelegraphView _telegraphView;
    private Coroutine _attackRoutine;
    private Coroutine _deathFadeRoutine;
    private bool _baselineInitialized;
    private Color _baselineColor;
    private bool _baselineParryable;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sprite = GetComponent<SpriteRenderer>();
        _hurtbox = GetComponent<Hurtbox>();
        if (_hurtbox == null)
        {
            _hurtbox = gameObject.AddComponent<Hurtbox>();
        }

        if (GetComponent<Collider2D>() == null)
        {
            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.32f, 0.48f);
        }

        _telegraphView = GetComponent<AttackTelegraphView>();
        if (_telegraphView == null)
        {
            _telegraphView = gameObject.AddComponent<AttackTelegraphView>();
        }
    }

    protected virtual void Start()
    {
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
    }

    public void InitializeCombatBaseline()
    {
        if (_baselineInitialized)
        {
            return;
        }

        RecalculateStats();
        hp = maxHp;
        _baselineColor = _sprite != null ? _sprite.color : Color.white;
        _baselineParryable = isCurrentAttackParryable;
        Baseline = new EnemyStatBaseline(
            maxHp,
            damage,
            moveSpeed,
            damageReduction,
            telegraphDuration,
            attackDuration);
        _baselineInitialized = true;
    }

    public void PrepareForSpawn(EnemyWaveStats stats)
    {
        if (!_baselineInitialized)
        {
            InitializeCombatBaseline();
        }

        CancelOwnedAttack();
        StopDeathFade();
        IsDead = false;
        CurrentState = EnemyState.Idle;
        maxHp = stats.MaxHp;
        hp = stats.MaxHp;
        damage = stats.Damage;
        moveSpeed = stats.MoveSpeed;
        damageReduction = Baseline.DamageReduction;
        telegraphDuration = Baseline.TelegraphDuration;
        attackDuration = Baseline.AttackDuration;
        isCurrentAttackParryable = _baselineParryable;
        CurrentAttackPlan = default;
        CurrentAttackPhase = EnemyAttackPhase.Complete;
        _stateTimer = 0f;
        _decisionTimer = 0f;
        _distanceToPlayer = 0f;
        _facingDirection = 1;

        if (_sprite != null)
        {
            _sprite.color = _baselineColor;
            _sprite.flipX = false;
        }

        if (_rb != null)
        {
            _rb.velocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        var collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = true;
        }

        ResetSubclassState();
        enabled = true;
        OnHealthChanged?.Invoke(hp, maxHp);
    }

    protected virtual void ResetSubclassState()
    {
    }

    internal void CancelActiveLease()
    {
        CancelCombatActions();
        StopDeathFade();
        CurrentState = EnemyState.Idle;
        _stateTimer = 0f;
        _decisionTimer = 0f;
        if (_rb != null)
        {
            _rb.velocity = Vector2.zero;
            _rb.angularVelocity = 0f;
        }

        if (_baselineInitialized && _sprite != null)
        {
            _sprite.color = _baselineColor;
        }

        enabled = false;
    }

    public void RecalculateStats()
    {
        var secondary = PrimaryAttributeConverter.Convert(primary, 1);
        maxHp = secondary.maxHp;
        if (hp > maxHp || hp == 0)
        {
            hp = maxHp;
        }
    }

    public int GetDefense(CombatStyle attackerStyle)
    {
        var secondary = PrimaryAttributeConverter.Convert(primary, 1);
        return secondary.GetDef(attackerStyle);
    }

    protected virtual void Update()
    {
        if (IsDead)
        {
            return;
        }

        if (_player != null)
        {
            _distanceToPlayer = Vector2.Distance(transform.position, _player.position);
        }

        _decisionTimer -= Time.deltaTime;
        _stateTimer -= Time.deltaTime;
        switch (CurrentState)
        {
            case EnemyState.Idle:
                UpdateIdle();
                break;
            case EnemyState.Patrol:
                UpdatePatrol();
                break;
            case EnemyState.Chase:
                UpdateChase();
                break;
            case EnemyState.Telegraph:
                UpdateTelegraph();
                break;
            case EnemyState.Attack:
                UpdateAttack();
                break;
            case EnemyState.Hurt:
                UpdateHurt();
                break;
            case EnemyState.Stunned:
                UpdateStunned();
                break;
        }
    }

    protected virtual void UpdateIdle()
    {
        if (_decisionTimer > 0f)
        {
            return;
        }

        _decisionTimer = decisionInterval;
        ChangeState(_distanceToPlayer <= chaseRange ? EnemyState.Chase : EnemyState.Patrol);
    }

    protected virtual void UpdatePatrol()
    {
        if (_decisionTimer > 0f)
        {
            return;
        }

        _decisionTimer = decisionInterval;
        if (_distanceToPlayer <= chaseRange)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        _rb.velocity = new Vector2(_facingDirection * moveSpeed * 0.5f, _rb.velocity.y);
    }

    protected virtual void UpdateChase()
    {
        if (_player == null)
        {
            return;
        }

        FacePlayer();
        if (_distanceToPlayer <= attackRange)
        {
            TryStartPreparedAttack();
            return;
        }

        if (_distanceToPlayer > chaseRange)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        var direction = _player.position.x > transform.position.x ? 1f : -1f;
        _rb.velocity = new Vector2(direction * moveSpeed, _rb.velocity.y);
    }

    protected virtual void UpdateTelegraph()
    {
        _rb.velocity = Vector2.zero;
    }

    protected virtual void UpdateAttack()
    {
        _rb.velocity = Vector2.zero;
    }

    protected virtual void UpdateHurt()
    {
        if (_stateTimer <= 0f)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    protected virtual void UpdateStunned()
    {
        if (_stateTimer <= 0f)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    protected virtual void ChangeState(EnemyState newState)
    {
        CurrentState = newState;
        switch (newState)
        {
            case EnemyState.Telegraph:
                _stateTimer = CurrentAttackPlan.IsValid
                    ? CurrentAttackPlan.TelegraphDuration
                    : telegraphDuration;
                break;
            case EnemyState.Attack:
                _stateTimer = CurrentAttackPlan.IsValid
                    ? CurrentAttackPlan.CommitDuration
                    : attackDuration;
                break;
            case EnemyState.Hurt:
                _stateTimer = 0.3f;
                break;
            case EnemyState.Stunned:
                _stateTimer = 1f;
                break;
            default:
                _stateTimer = 0f;
                break;
        }
    }

    protected bool TryStartPreparedAttack()
    {
        if (_attackRoutine != null || IsDead)
        {
            return false;
        }

        var plan = PrepareAttackPlan();
        if (!plan.IsValid)
        {
            return false;
        }

        CurrentAttackPlan = plan;
        CurrentAttackPhase = EnemyAttackPhase.Telegraph;
        isCurrentAttackParryable = plan.IsParryable;
        _attackRoutine = StartCoroutine(RunOwnedAttack(plan));
        return true;
    }

    private IEnumerator RunOwnedAttack(EnemyAttackPlan plan)
    {
        CurrentAttackPhase = EnemyAttackPhase.Telegraph;
        ChangeState(EnemyState.Telegraph);
        _telegraphView.Show(plan);
        var elapsed = 0f;
        while (elapsed < plan.TelegraphDuration)
        {
            _telegraphView.SetProgress(
                plan.TelegraphDuration <= 0f ? 1f : elapsed / plan.TelegraphDuration);
            yield return null;
            elapsed += Time.deltaTime;
        }

        _telegraphView.SetProgress(1f);
        _telegraphView.Hide();
        CurrentAttackPhase = EnemyAttackPhase.Commit;
        ChangeState(EnemyState.Attack);
        var commitStartedAt = Time.time;
        yield return ExecuteAttackPlan(plan);
        if (CurrentAttackPhase != EnemyAttackPhase.Commit)
        {
            yield break;
        }

        var remainingCommit = plan.CommitDuration - (Time.time - commitStartedAt);
        if (remainingCommit > 0f)
        {
            yield return new WaitForSeconds(remainingCommit);
        }

        CurrentAttackPhase = EnemyAttackPhase.Recovery;
        if (plan.RecoveryDuration > 0f)
        {
            yield return new WaitForSeconds(plan.RecoveryDuration);
        }

        _attackRoutine = null;
        CurrentAttackPhase = EnemyAttackPhase.Complete;
        ChangeState(EnemyState.Chase);
    }

    protected abstract EnemyAttackPlan PrepareAttackPlan();
    protected abstract IEnumerator ExecuteAttackPlan(EnemyAttackPlan plan);

    protected void ResolvePlanHit(EnemyAttackPlan plan)
    {
        ResolvePlanHit(plan, CombatFeedbackStrength.Light);
    }

    protected void ResolvePlanHit(
        EnemyAttackPlan plan,
        CombatFeedbackStrength strength)
    {
        var center = (Vector2)transform.TransformPoint(plan.LocalOffset);
        Collider2D[] hits;
        if (plan.Shape == EnemyTelegraphShape.Box)
        {
            var worldForward = plan.AimDirection.normalized;
            var worldAngle = Mathf.Atan2(worldForward.y, worldForward.x) * Mathf.Rad2Deg;
            hits = Physics2D.OverlapBoxAll(center, plan.Size, worldAngle);
        }
        else
        {
            hits = Physics2D.OverlapCircleAll(center, plan.Radius);
        }
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player"))
            {
                continue;
            }

            var hurtbox = hit.GetComponent<Hurtbox>() ?? hit.GetComponentInParent<Hurtbox>();
            if (hurtbox == null)
            {
                continue;
            }

            CombatHitResolver.ResolveAndPublish(
                hurtbox,
                new CombatHit(
                    plan.Damage,
                    plan.FacingDirection,
                    plan.Knockback,
                    plan.IsParryable,
                    this),
                gameObject,
                CombatFeedbackSourceKind.EnemyMelee,
                strength,
                plan.FacingDirection);
            return;
        }
    }

    protected void CancelOwnedAttack()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
        }

        _attackRoutine = null;
        CurrentAttackPhase = EnemyAttackPhase.Complete;
        if (_telegraphView != null)
        {
            _telegraphView.Hide();
        }

        if (_rb != null)
        {
            _rb.velocity = Vector2.zero;
        }

        OnOwnedAttackCancelled();
    }

    protected virtual void OnOwnedAttackCancelled()
    {
    }

    public void CancelCombatActions()
    {
        CancelOwnedAttack();
        if (_telegraphView != null)
        {
            _telegraphView.Hide();
        }
    }

    public virtual void TakeDamage(int amount, float knockbackDirX = 0f, float knockbackForce = 5f)
    {
        if (IsDead)
        {
            return;
        }

        var secondary = PrimaryAttributeConverter.Convert(primary, 1);
        var averageDefense =
            (secondary.swordDef + secondary.bladeDef + secondary.sealDef +
             secondary.poisonDef + secondary.bloodDef) / 5;
        var reduction = Mathf.Min(0.9f, averageDefense * 0.001f);
        var finalDamage = Mathf.Max(
            1,
            Mathf.RoundToInt(amount * (1f - reduction) * (1f - damageReduction)));
        var previousHp = hp;
        hp = Mathf.Max(0, hp - finalDamage);
        if (hp != previousHp)
        {
            OnHealthChanged?.Invoke(hp, maxHp);
        }

        if (hp <= 0)
        {
            Die();
            ApplyHitImpulse(knockbackDirX, knockbackForce);
        }
        else
        {
            CancelOwnedAttack();
            ApplyHitImpulse(knockbackDirX, knockbackForce);
            ChangeState(EnemyState.Hurt);
        }
    }

    private void ApplyHitImpulse(float knockbackDirX, float knockbackForce)
    {
        _rb.velocity = Vector2.zero;
        _rb.AddForce(new Vector2(knockbackDirX * knockbackForce, 2f), ForceMode2D.Impulse);
    }

    public virtual void Stun(float duration = 1f)
    {
        CancelOwnedAttack();
        ChangeState(EnemyState.Stunned);
        _stateTimer = duration;
    }

    public void OnParried()
    {
        if (IsDead || CurrentState == EnemyState.Stunned)
        {
            return;
        }

        if (_rb != null)
        {
            _rb.velocity = Vector2.zero;
        }

        Stun();
    }

    protected virtual void Die()
    {
        CancelOwnedAttack();
        StopDeathFade();
        IsDead = true;
        CurrentState = EnemyState.Die;
        CombatEvents.InvokeEnemyDeath(gameObject);
        OnDeath?.Invoke(this);
        DropExpCrystal();
        _deathFadeRoutine = StartCoroutine(DieFadeCoroutine());
    }

    protected virtual void DropExpCrystal()
    {
        if (expValue <= 0)
        {
            return;
        }

        var crystalObject = new GameObject("ExpCrystal");
        crystalObject.transform.position = transform.position + Vector3.up * 0.5f;
        var crystal = crystalObject.AddComponent<ExpCrystal>();
        crystal.expValue = expValue;
    }

    public virtual void ResetForPool()
    {
        if (!_baselineInitialized)
        {
            InitializeCombatBaseline();
        }

        PrepareForSpawn(new EnemyWaveStats(
            Baseline.MaxHp,
            Baseline.Damage,
            Baseline.MoveSpeed));
    }

    protected void FacePlayer()
    {
        if (_player == null)
        {
            return;
        }

        _facingDirection = _player.position.x > transform.position.x ? 1 : -1;
        if (_sprite != null)
        {
            _sprite.flipX = _facingDirection == -1;
        }
    }

    private void StopDeathFade()
    {
        if (_deathFadeRoutine == null)
        {
            return;
        }

        StopCoroutine(_deathFadeRoutine);
        _deathFadeRoutine = null;
    }

    private IEnumerator DieFadeCoroutine()
    {
        const float duration = 0.5f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (_sprite != null)
            {
                var color = _sprite.color;
                color.a = 1f - elapsed / duration;
                _sprite.color = color;
            }

            yield return null;
        }

        _deathFadeRoutine = null;
        gameObject.SetActive(false);
    }

    protected virtual void OnDisable()
    {
        CancelOwnedAttack();
    }
}
