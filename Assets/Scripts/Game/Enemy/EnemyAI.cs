using UnityEngine;

/// <summary>
/// 敌人AI辅助组件：封装决策逻辑，可挂在不同敌人上。
/// 为什么独立于 EnemyBase：AI行为可能与敌人类型正交组合，
/// 比如不同AI策略可以复用同一个敌人类型。
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("AI参数")]
    [Tooltip("追击范围")]
    public float chaseRange = 8f;
    [Tooltip("攻击范围")]
    public float attackRange = 1.5f;
    [Tooltip("丢失目标后回到巡逻的延迟")]
    public float loseTargetDelay = 2f;
    [Tooltip("随机巡逻方向变更间隔")]
    public float patrolChangeInterval = 2f;

    private EnemyBase _enemy;
    private float _patrolTimer;
    private int _patrolDirection = 1;

    private void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
    }

    private void Update()
    {
        if (_enemy.IsDead) return;

        _patrolTimer -= Time.deltaTime;
    }

    /// <summary>获取巡逻方向（定期随机切换）</summary>
    public int GetPatrolDirection()
    {
        if (_patrolTimer <= 0)
        {
            _patrolDirection = Random.value > 0.5f ? 1 : -1;
            _patrolTimer = patrolChangeInterval + Random.Range(-0.5f, 0.5f);
        }
        return _patrolDirection;
    }
}
