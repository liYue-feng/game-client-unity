using System.Collections;
using Game.Gameplay;
using UnityEngine;

public class Boss : EnemyBase
{
    [Header("Boss Phase")]
    public float enrageThreshold = 0.5f;
    public float enrageSpeedMult = 1.5f;
    public float enrageDamageMult = 1.3f;

    [Header("Boss Attacks")]
    public float chargeRange = 5f;
    public float chargeSpeed = 10f;
    public int slamDamage = 30;
    public int aoeDamage = 20;

    private bool _isEnraged;
    private int _attackPattern;

    public int CurrentPhase { get; private set; } = 1;
    public event System.Action<int> OnPhaseChanged;

    protected override void Awake()
    {
        hp = 300;
        maxHp = 300;
        moveSpeed = 2.5f;
        damage = 25;
        attackRange = 2f;
        chaseRange = 12f;
        telegraphDuration = 0.8f;
        attackDuration = 0.5f;
        base.Awake();
    }

    protected override void ResetSubclassState()
    {
        _isEnraged = false;
        _attackPattern = 0;
        SetPhase(1);
    }

    protected override void Update()
    {
        if (!_isEnraged && hp <= maxHp * enrageThreshold)
        {
            EnterEnrage();
        }

        base.Update();
    }

    private void EnterEnrage()
    {
        if (_isEnraged)
        {
            return;
        }

        _isEnraged = true;
        SetPhase(2);
        moveSpeed *= enrageSpeedMult;
        damage = Mathf.RoundToInt(damage * enrageDamageMult);
        if (_sprite != null)
        {
            _sprite.color = new Color(1f, 0.4f, 0.3f);
        }
    }

    private void SetPhase(int phase)
    {
        if (CurrentPhase == phase)
        {
            return;
        }

        CurrentPhase = phase;
        OnPhaseChanged?.Invoke(phase);
    }

    protected override void UpdateChase()
    {
        if (_player == null)
        {
            return;
        }

        FacePlayer();
        var direction = _player.position.x > transform.position.x ? 1f : -1f;
        _rb.velocity = new Vector2(direction * moveSpeed, _rb.velocity.y);
        if (_distanceToPlayer <= attackRange)
        {
            ChooseAttackPattern();
            TryStartPreparedAttack();
        }
    }

    private void ChooseAttackPattern()
    {
        var roll = Random.value;
        if (_isEnraged)
        {
            _attackPattern = roll < 0.3f ? 0 : roll < 0.55f ? 1 : roll < 0.75f ? 2 : 3;
        }
        else
        {
            _attackPattern = roll < 0.4f ? 0 : roll < 0.7f ? 1 : 2;
        }
    }

    protected override EnemyAttackPlan PrepareAttackPlan()
    {
        var durationMultiplier = _isEnraged ? 0.7f : 1f;
        switch (_attackPattern)
        {
            case 1:
                return EnemyAttackPlan.Box(
                    "boss_charge",
                    0.8f * durationMultiplier,
                    0.5f,
                    0.25f,
                    true,
                    new Vector2(_facingDirection * 1.5f, 0f),
                    new Vector2(3f, 1.2f),
                    _facingDirection,
                    new Vector2(_facingDirection, 0f),
                    1,
                    0f,
                    damage,
                    12f);
            case 2:
                return EnemyAttackPlan.Circle(
                    "boss_slam",
                    1f * durationMultiplier,
                    0.5f,
                    0.3f,
                    true,
                    Vector2.zero,
                    2f,
                    _facingDirection,
                    new Vector2(_facingDirection, 0f),
                    1,
                    0f,
                    slamDamage,
                    15f);
            case 3:
                return EnemyAttackPlan.Circle(
                    "boss_aoe",
                    0.6f * durationMultiplier,
                    0.5f,
                    0.35f,
                    false,
                    Vector2.zero,
                    4f,
                    _facingDirection,
                    new Vector2(_facingDirection, 0f),
                    1,
                    0f,
                    aoeDamage,
                    8f);
            default:
                return EnemyAttackPlan.Box(
                    "boss_slash",
                    0.5f * durationMultiplier,
                    0.6f,
                    0.2f,
                    true,
                    new Vector2(_facingDirection, 0f),
                    new Vector2(1.5f, 1.2f),
                    _facingDirection,
                    new Vector2(_facingDirection, 0f),
                    3,
                    0.3f,
                    damage,
                    8f);
        }
    }

    protected override IEnumerator ExecuteAttackPlan(EnemyAttackPlan plan)
    {
        switch (plan.AttackId)
        {
            case "boss_charge":
                yield return HoldCommitVelocity(
                    new Vector2(plan.FacingDirection * chargeSpeed, 0f),
                    Mathf.Min(0.3f, plan.CommitDuration));
                _rb.velocity = Vector2.zero;
                ResolvePlanHit(plan, CombatFeedbackStrength.Heavy);
                break;
            case "boss_slam":
                yield return HoldCommitVelocity(
                    new Vector2(0f, 10f),
                    Mathf.Min(0.3f, plan.CommitDuration));
                _rb.velocity = Vector2.zero;
                ResolvePlanHit(plan, CombatFeedbackStrength.Heavy);
                break;
            case "boss_aoe":
                yield return new WaitForSeconds(0.2f);
                ResolvePlanHit(plan, CombatFeedbackStrength.Heavy);
                break;
            default:
                for (var index = 0; index < plan.HitCount; index++)
                {
                    if (CurrentAttackPhase != EnemyAttackPhase.Commit)
                    {
                        yield break;
                    }

                    ResolvePlanHit(plan, CombatFeedbackStrength.Heavy);
                    if (CurrentAttackPhase != EnemyAttackPhase.Commit)
                    {
                        yield break;
                    }

                    if (index + 1 < plan.HitCount && plan.HitInterval > 0f)
                    {
                        yield return new WaitForSeconds(plan.HitInterval);
                    }
                }
                break;
        }
    }

    private IEnumerator HoldCommitVelocity(Vector2 velocity, float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration && CurrentAttackPhase == EnemyAttackPhase.Commit)
        {
            _rb.velocity = velocity;
            yield return null;
            elapsed += Time.deltaTime;
        }
    }

    protected override void UpdateIdle()
    {
        if (_player != null)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    protected override void UpdatePatrol()
    {
        if (_player != null)
        {
            ChangeState(EnemyState.Chase);
        }
    }
}
