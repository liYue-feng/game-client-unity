using UnityEngine;

/// <summary>
/// 印流派：弹反强化，印记资源，印记引爆特殊技能。
/// 被动：弹反成功在弹反位置放置印记（最多5个），印记持续10s
/// 特殊：引爆所有印记——每个印记造成范围伤害
/// </summary>
public class SealStyle : IStyleBehaviour
{
    private StyleData _data;
    private int _markCount;

    public SealStyle()
    {
        _data = StyleDatabase.GetStyle(CombatStyleID.Seal);
    }

    public void OnAttackHit(EnemyBase enemy)
    {
        // 印流派攻击不增加特殊资源
    }

    public void OnParrySuccess()
    {
        // 弹反放置印记
        _markCount = Mathf.Min(_data.specialResourceMax, _markCount + 1);
        StyleManager.Instance.AddSpecialResource(1);
    }

    public void ActivateSpecial(GameObject player)
    {
        // 引爆所有印记：简化为以玩家为中心的范围爆炸
        if (_markCount <= 0) return;

        int totalDamage = _markCount * 25;
        float radius = 1.5f + _markCount * 0.5f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, radius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                var enemy = hit.GetComponent<EnemyBase>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.TakeDamage(totalDamage);
                }
            }
        }

        _markCount = 0;
    }

    public void PassiveUpdate()
    {
        // 印记无被动逻辑（印记有持续时间，但简化不实现衰减）
    }

    public StyleData GetData() => _data;
}
