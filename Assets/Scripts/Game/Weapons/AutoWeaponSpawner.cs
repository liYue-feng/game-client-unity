using UnityEngine;
using System.Collections;

/// <summary>
/// 自动武器生成器基类。挂在玩家 GameObject 上。
/// 参考：WeaponSpawner.cs (VampireSurvivors clone)
/// 适配：横版2D — Direction 改为 Forward/Opposite/Up/Down
/// </summary>
public abstract class AutoWeaponSpawner : MonoBehaviour
{
    public enum Direction { Forward, Opposite, Up, Down }

    [Header("武器标识")]
    [Tooltip("武器行为ID，用于对象池键和升级查找")]
    public string weaponId = "ink_bolt";

    [Header("基础数值")]
    [Tooltip("单发攻击力")]
    public int baseAttackPower = 8;
    [Tooltip("攻击间隔（秒）")]
    public float attackSpeed = 1.5f;
    [Tooltip("弹体存在时间（秒）")]
    public float inactiveDelay = 2f;
    [Tooltip("弹体基础缩放")]
    public float baseScale = 1f;

    protected int level = 1;
    protected int finalAttackPower;
    protected float finalAttackSpeed;
    protected float additionalScale = 1f;
    protected CharacterStats playerStats;
    protected Sprite projectileSprite;
    protected Color projectileColor = ShuiMoPalette.InkBlack;
    protected bool isActive;

    /// <summary>启动武器（首次获得时调用）</summary>
    public virtual void StartWeapon()
    {
        if (isActive) return;
        isActive = true;

        playerStats = GetComponent<CharacterStats>();
        RegisterPool();
        UpdateFinalStats();
        StartCoroutine(StartAttack());
    }

    /// <summary>升级武器</summary>
    public virtual void IncreaseLevel()
    {
        level++;
        UpdateFinalStats();
        LevelUp();
    }

    /// <summary>获取当前等级</summary>
    public int CurrentLevel => level;

    /// <summary>子类实现：定义攻击协程</summary>
    protected abstract IEnumerator StartAttack();

    /// <summary>子类可选重写：升级时的特殊效果</summary>
    protected virtual void LevelUp() { }

    /// <summary>向对象池注册弹体</summary>
    protected virtual void RegisterPool()
    {
        if (ObjectPool.Instance.AvailableCount(weaponId) > 0) return;
        ObjectPool.Instance.Register(weaponId, CreateProjectile, 10);
    }

    /// <summary>创建弹体工厂方法</summary>
    protected virtual GameObject CreateProjectile()
    {
        var obj = new GameObject($"Proj_{weaponId}");
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        var col = obj.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.15f;

        var rb = obj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        var weapon = obj.AddComponent<AutoWeapon>();
        weapon.piercing = false;
        obj.SetActive(false);
        return obj;
    }

    /// <summary>更新最终数值（受玩家属性影响）</summary>
    protected void UpdateFinalStats()
    {
        float playerAtkMult = playerStats != null ? Mathf.Max(0.1f, playerStats.attack / 10f) : 1f;
        finalAttackPower = Mathf.RoundToInt(baseAttackPower * playerAtkMult);
        finalAttackSpeed = Mathf.Max(0.1f, attackSpeed);
    }

    /// <summary>生成弹体到指定方向</summary>
    protected GameObject SpawnWeapon(Direction direction)
    {
        var obj = ObjectPool.Instance.Get(weaponId);
        if (obj == null) return null;

        obj.transform.position = transform.position + GetDirectionOffset(direction);
        obj.transform.rotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one * baseScale * additionalScale;

        var weapon = obj.GetComponent<AutoWeapon>();
        if (weapon != null)
        {
            weapon.SetParameters(finalAttackPower, inactiveDelay, weaponId, projectileSprite, projectileColor, playerStats);
        }

        // 朝向翻转
        var sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.flipX = (direction == Direction.Opposite);
        }

        return obj;
    }

    /// <summary>获取方向的偏移向量</summary>
    protected Vector3 GetDirectionOffset(Direction dir)
    {
        float facingSign = playerStats != null && playerStats.transform.localScale.x < 0 ? -1f : 1f;
        switch (dir)
        {
            case Direction.Forward: return Vector3.right * facingSign * 0.5f;
            case Direction.Opposite: return Vector3.left * facingSign * 0.5f;
            case Direction.Up: return Vector3.up * 0.5f;
            case Direction.Down: return Vector3.down * 0.5f;
            default: return Vector3.zero;
        }
    }

    /// <summary>获取最近敌人的位置，没有敌人返回前方远处</summary>
    protected Vector3 GetNearestEnemyPosition()
    {
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float nearestDist = float.MaxValue;
        Vector3 nearestPos = transform.position + Vector3.right * 10f;

        foreach (var enemy in enemies)
        {
            if (!enemy.activeInHierarchy) continue;
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestPos = enemy.transform.position;
            }
        }
        return nearestPos;
    }
}