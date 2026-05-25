using UnityEngine;

/// <summary>
/// 毒流派：持续伤害，毒液资源，毒雾特殊技能。
/// 被动：攻击叠加毒素（3层=2s DoT），每次攻击+5毒液
/// 特殊：毒雾——5秒范围DoT区域
/// </summary>
public class PoisonStyle : IStyleBehaviour
{
    private StyleData _data;

    public PoisonStyle()
    {
        _data = StyleDatabase.GetStyle(CombatStyleID.Poison);
    }

    public void OnAttackHit(EnemyBase enemy)
    {
        // 攻击叠毒：简化为直接附加DoT伤害
        StyleManager.Instance.AddSpecialResource(5);

        // 给敌人附加毒素效果
        var poison = enemy.GetComponent<PoisonDot>();
        if (poison == null)
        {
            poison = enemy.gameObject.AddComponent<PoisonDot>();
        }
        poison.AddStack();
    }

    public void OnParrySuccess()
    {
        // 毒流派弹反也加毒液
        StyleManager.Instance.AddSpecialResource(8);
    }

    public void ActivateSpecial(GameObject player)
    {
        // 毒雾：5秒范围DoT
        var cloud = new GameObject("PoisonCloud");
        cloud.transform.position = player.transform.position + new Vector3(player.GetComponent<PlayerController>()?.FacingDirection ?? 1, 0f, 0f) * 2f;

        var sr = cloud.AddComponent<SpriteRenderer>();
        sr.sprite = PlaceholderSpriteFactory.CreateCircle(20, new Color(0.23f, 0.42f, 0.42f, 0.4f));
        sr.sortingOrder = 3;

        var cloudEffect = cloud.AddComponent<PoisonCloudEffect>();
        cloudEffect.duration = 5f;
        cloudEffect.damage = 5;
        cloudEffect.radius = 2f;
    }

    public void PassiveUpdate()
    {
        // 毒流派无特殊被动
    }

    public StyleData GetData() => _data;
}

/// <summary>毒素DoT效果</summary>
public class PoisonDot : MonoBehaviour
{
    private int _stacks;
    private float _tickTimer;
    private float _tickInterval = 0.5f;
    private float _duration = 2f;
    private float _timer;

    public void AddStack()
    {
        _stacks = Mathf.Min(5, _stacks + 1);
        _timer = _duration; // 刷新持续时间
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0)
        {
            Destroy(this);
            return;
        }

        _tickTimer -= Time.deltaTime;
        if (_tickTimer <= 0 && _stacks >= 3) // 3层以上触发DoT
        {
            _tickTimer = _tickInterval;
            var enemy = GetComponent<EnemyBase>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(_stacks * 2);
            }
        }
    }
}

/// <summary>毒雾效果</summary>
public class PoisonCloudEffect : MonoBehaviour
{
    public float duration = 5f;
    public int damage = 5;
    public float radius = 2f;

    private float _timer;
    private float _tickTimer;

    private void Update()
    {
        _timer += Time.deltaTime;
        _tickTimer += Time.deltaTime;

        if (_tickTimer >= 0.5f)
        {
            _tickTimer = 0f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Enemy"))
                {
                    var enemy = hit.GetComponent<EnemyBase>();
                    if (enemy != null && !enemy.IsDead)
                    {
                        enemy.TakeDamage(damage);
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
