using System.Collections;
using Game.Gameplay;
using UnityEngine;

public class Elite : EnemyBase
{
    [Header("Elite")]
    public int comboCount = 3;
    public float comboInterval = 0.4f;
    public float heavyTelegraphDuration = 1f;
    public int heavyDamage = 25;
    public float dodgeRange = 3f;
    public float heavyAttackChance = 0.3f;

    private int _currentCombo;
    private bool _isHeavyAttack;

    protected override void Awake()
    {
        hp = 80;
        maxHp = 80;
        moveSpeed = 3f;
        damage = 20;
        attackRange = 1.5f;
        chaseRange = 8f;
        telegraphDuration = 0.5f;
        attackDuration = 0.3f;
        isCurrentAttackParryable = true;
        base.Awake();
    }

    protected override void ResetSubclassState()
    {
        _currentCombo = 0;
        _isHeavyAttack = false;
    }

    protected override EnemyAttackPlan PrepareAttackPlan()
    {
        _currentCombo = 0;
        _isHeavyAttack = Random.value < Mathf.Clamp01(heavyAttackChance);
        if (_isHeavyAttack)
        {
            return EnemyAttackPlan.Box(
                "elite_heavy",
                heavyTelegraphDuration,
                attackDuration,
                0.25f,
                true,
                new Vector2(_facingDirection * 0.8f, 0.2f),
                new Vector2(1.4f, 1f),
                _facingDirection,
                new Vector2(_facingDirection, 0f),
                1,
                0f,
                heavyDamage,
                8f);
        }

        return EnemyAttackPlan.Box(
            "elite_combo",
            telegraphDuration,
            attackDuration,
            0.2f,
            true,
            new Vector2(_facingDirection * 0.7f, 0.2f),
            new Vector2(1f, 0.8f),
            _facingDirection,
            new Vector2(_facingDirection, 0f),
            comboCount,
            comboInterval,
            damage,
            5f);
    }

    protected override IEnumerator ExecuteAttackPlan(EnemyAttackPlan plan)
    {
        for (var index = 0; index < plan.HitCount; index++)
        {
            if (CurrentAttackPhase != EnemyAttackPhase.Commit)
            {
                yield break;
            }

            _currentCombo = index + 1;
            ResolvePlanHit(
                plan,
                plan.AttackId == "elite_heavy"
                    ? CombatFeedbackStrength.Heavy
                    : CombatFeedbackStrength.Light);
            if (CurrentAttackPhase != EnemyAttackPhase.Commit)
            {
                yield break;
            }

            if (index + 1 < plan.HitCount && plan.HitInterval > 0f)
            {
                yield return new WaitForSeconds(plan.HitInterval);
            }
        }

        _currentCombo = 0;
        _isHeavyAttack = false;
    }

    protected override void OnOwnedAttackCancelled()
    {
        _currentCombo = 0;
        _isHeavyAttack = false;
    }
}
