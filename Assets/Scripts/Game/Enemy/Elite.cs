using UnityEngine;
using System.Collections;
using Game.Gameplay;

/// <summary>
/// 精英敌人：三连击 + 蓄力重击，可闪避玩家攻击。
/// 比杂兵更具攻击性，攻击频率高，需要玩家善用弹反。
/// 三连击每段都可弹反，蓄力重击也可弹反但伤害更高。
/// </summary>
public class Elite : EnemyBase
{
    [Header("精英特有参数")]
    [Tooltip("三连击段数")]
    public int comboCount = 3;
    [Tooltip("每段连击间隔")]
    public float comboInterval = 0.4f;
    [Tooltip("蓄力重击前摇")]
    public float heavyTelegraphDuration = 1.0f;
    [Tooltip("蓄力重击伤害")]
    public int heavyDamage = 25;
    [Tooltip("闪避玩家攻击的范围")]
    public float dodgeRange = 3f;
    [Tooltip("使用蓄力重击的概率")]
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

    protected override void UpdateTelegraph()
    {
        _rb.velocity = Vector2.zero;

        // 决定这次是连击还是蓄力重击
        if (_stateTimer <= 0)
        {
            if (_currentCombo == 0 && Random.value < heavyAttackChance)
            {
                // 蓄力重击
                _isHeavyAttack = true;
                telegraphDuration = heavyTelegraphDuration;
                isCurrentAttackParryable = true;
            }

            ChangeState(EnemyState.Attack);
        }
    }

    protected override void OnAttackStart()
    {
        if (_isHeavyAttack)
        {
            // 蓄力重击：单次高伤害
            PerformMeleeAttack(heavyDamage, 8f);
            _isHeavyAttack = false;
            _currentCombo = 0;
        }
        else
        {
            // 三连击
            StartCoroutine(ComboCoroutine());
        }
    }

    private IEnumerator ComboCoroutine()
    {
        for (int i = 0; i < comboCount; i++)
        {
            PerformMeleeAttack(damage, 5f);
            yield return new WaitForSeconds(comboInterval);
        }
        _currentCombo = 0;
    }

    /// <summary>执行近战攻击判定</summary>
    private void PerformMeleeAttack(int dmg, float knockback)
    {
        Vector2 hitPos = (Vector2)transform.position + new Vector2(_facingDirection * 0.7f, 0.2f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(hitPos, new Vector2(1.0f, 0.8f), 0f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                var hurtbox = hit.GetComponent<Hurtbox>();
                if (hurtbox != null)
                {
                    hurtbox.ReceiveHit(new CombatHit(
                        dmg,
                        _facingDirection,
                        knockback,
                        isCurrentAttackParryable,
                        this));
                }
            }
        }
    }

    protected override void UpdateAttack()
    {
        _rb.velocity = Vector2.zero;
        // 精英连击由协程控制，这里只等连击结束
        if (_stateTimer <= 0)
        {
            ChangeState(EnemyState.Chase);
        }
    }
}
