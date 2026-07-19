using System.Collections;
using Game.Gameplay;
using UnityEngine;

public class Grunt : EnemyBase
{
    protected override void Awake()
    {
        hp = 30;
        maxHp = 30;
        moveSpeed = 2f;
        damage = 10;
        attackRange = 1.2f;
        telegraphDuration = 0.5f;
        attackDuration = 0.3f;
        isCurrentAttackParryable = true;
        base.Awake();
    }

    protected override EnemyAttackPlan PrepareAttackPlan()
    {
        return EnemyAttackPlan.Box(
            "grunt_slash",
            telegraphDuration,
            attackDuration,
            0.1f,
            true,
            new Vector2(_facingDirection * 0.6f, 0.2f),
            new Vector2(0.8f, 0.6f),
            _facingDirection,
            new Vector2(_facingDirection, 0f),
            1,
            0f,
            damage,
            5f);
    }

    protected override IEnumerator ExecuteAttackPlan(EnemyAttackPlan plan)
    {
        ResolvePlanHit(plan);
        yield break;
    }
}
