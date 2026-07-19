using UnityEngine;
using System.Collections;
using Game.Managers;

/// <summary>
/// 自动武器弹体基类：墨滴飞弹，自动生命周期管理 + 对象池回收。
/// 参考：Weapon.cs (VampireSurvivors clone)
/// 适配：横版2D + 水墨画风格
/// </summary>
public class AutoWeapon : MonoBehaviour
{
    [Tooltip("攻击力")]
    public int attackPower = 10;
    [Tooltip("存在时间（秒），到期自动回收")]
    public float inactiveDelay = 2f;
    [Tooltip("是否穿透（不命中消失）")]
    public bool piercing;

    protected string poolKey;
    protected Rigidbody2D rb;
    protected SpriteRenderer sr;
    protected CharacterStats _playerStats;
    private bool _returnRequested;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>设置弹体参数，由 Spawner 在取出池后调用</summary>
    public void SetParameters(int atk, float delay, string key, Sprite sprite, Color color, CharacterStats playerStats = null)
    {
        attackPower = atk;
        inactiveDelay = delay;
        poolKey = key;
        _playerStats = playerStats;
        if (sr != null)
        {
            sr.sprite = sprite;
            sr.color = color;
        }
    }

    private void OnEnable()
    {
        _returnRequested = false;
        StartCoroutine(AutoReturn());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    /// <summary>自动返回池的协程</summary>
    protected virtual IEnumerator AutoReturn()
    {
        yield return new WaitForSeconds(inactiveDelay);
        ReturnToPool();
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Enemy")) return;

        var enemy = collision.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            int finalDamage = CalculateWeaponDamage(enemy);
            var knockbackDir = (enemy.transform.position - transform.position).normalized;
            var hurtbox = collision.GetComponent<Hurtbox>() ?? collision.GetComponentInParent<Hurtbox>();
            CombatHitResolver.ResolveAndPublish(
                hurtbox,
                new Game.Gameplay.CombatHit(finalDamage, knockbackDir.x, 5f, false, null),
                _playerStats != null ? _playerStats.gameObject : gameObject,
                CombatFeedbackSourceKind.PlayerRanged,
                CombatFeedbackStrength.Light,
                knockbackDir.x < 0f ? -1 : 1);

            // 触发击中事件（显示伤害数字 + 墨迹特效）
        }

        if (!piercing)
        {
            ReturnToPool();
        }
    }

    int CalculateWeaponDamage(EnemyBase enemy)
    {
        if (_playerStats == null) return RandomDamage(attackPower);

        SecondaryAttributes sec = _playerStats.Secondary;
        int attrAtk = sec.GetAtk(_playerStats.combatStyle);
        int attrDef = enemy.GetDefense(_playerStats.combatStyle);

        return DamageCalculator.CalculateHpDamage(
            attrAtk, attrDef,
            damageReduction: _playerStats.damageReduction,
            critValue: sec.critValue + _playerStats.extraCritValue,
            critResistValue: sec.critResistValue,
            critDamageBonus: _playerStats.critDamageBonus,
            attackerLevel: _playerStats.level
        );
    }

    /// <summary>随机伤害 ±20% 浮动</summary>
    public static int RandomDamage(int baseDamage)
    {
        return Mathf.RoundToInt(baseDamage * Random.Range(0.8f, 1.2f));
    }

    /// <summary>归还对象池</summary>
    public void ReturnToPool()
    {
        if (_returnRequested) return;
        _returnRequested = true;

        if (!string.IsNullOrEmpty(poolKey))
        {
            var pool = ObjectPool.ExistingInstance;
            if (pool != null)
            {
                pool.Return(poolKey, gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
