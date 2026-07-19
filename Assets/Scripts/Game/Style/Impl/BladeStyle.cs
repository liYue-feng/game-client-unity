using UnityEngine;

/// <summary>
/// 刃流派：高速连击，怒气资源，刃风暴特殊技能。
/// 被动：每次命中+10怒气，不攻击时怒气每秒-5
/// 特殊：刃风暴——2秒旋转攻击，持续造成范围伤害
/// </summary>
public class BladeStyle : IStyleBehaviour
{
    private StyleData _data;
    private float _decayTimer;

    public BladeStyle()
    {
        _data = StyleDatabase.GetStyle(CombatStyleID.Blade);
    }

    public void OnAttackHit(EnemyBase enemy)
    {
        // 命中+10怒气
        StyleManager.Instance.AddSpecialResource(10);
    }

    public void OnParrySuccess()
    {
        // 弹反也加怒气
        StyleManager.Instance.AddSpecialResource(15);
    }

    public void ActivateSpecial(GameObject player)
    {
        // 刃风暴：2秒旋转攻击
        // 简化实现：在玩家周围持续造成范围伤害
        var go = new GameObject("BladeStorm");
        go.transform.SetParent(player.transform);
        go.transform.localPosition = Vector3.zero;

        var storm = go.AddComponent<BladeStormEffect>();
        storm.duration = 2f;
        storm.damage = 15;
        storm.radius = 1.5f;
        storm.owner = player;
    }

    public void PassiveUpdate()
    {
        // 怒气自然衰减
        _decayTimer += Time.deltaTime;
        if (_decayTimer >= 1f)
        {
            _decayTimer = 0f;
            // 不攻击时怒气衰减（通过 StyleManager 减资源）
            // 简化：不实现衰减，只增不减
        }
    }

    public StyleData GetData() => _data;
}

/// <summary>刃风暴效果组件</summary>
public class BladeStormEffect : MonoBehaviour
{
    public float duration = 2f;
    public int damage = 15;
    public float radius = 1.5f;
    public GameObject owner;

    private float _timer;
    private float _hitInterval = 0.2f;
    private float _hitTimer;

    private void Update()
    {
        _timer += Time.deltaTime;
        _hitTimer += Time.deltaTime;

        if (_hitTimer >= _hitInterval)
        {
            _hitTimer = 0f;
            // 范围伤害
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Enemy"))
                {
                    var enemy = hit.GetComponent<EnemyBase>();
                    if (enemy != null && !enemy.IsDead)
                    {
                        var hurtbox = enemy.GetComponent<Hurtbox>();
                        var facing = enemy.transform.position.x < transform.position.x ? -1 : 1;
                        CombatHitResolver.ResolveAndPublish(
                            hurtbox,
                            new Game.Gameplay.CombatHit(damage, facing, 3f, false, null),
                            owner,
                            CombatFeedbackSourceKind.Style,
                            CombatFeedbackStrength.Heavy,
                            facing);
                    }
                }
            }
        }

        if (_timer >= duration)
        {
            Destroy(gameObject);
        }
    }
}
