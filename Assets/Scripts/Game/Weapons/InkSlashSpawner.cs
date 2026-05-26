using UnityEngine;
using System.Collections;

/// <summary>
/// 墨斩：沿玩家朝向发出横向墨刃，向前推进。
/// 参考：MagicWand.cs (VampireSurvivors clone) 跟踪弹模式
/// 适配：横版2D，水平推进的宽墨痕
/// </summary>
public class InkSlashSpawner : AutoWeaponSpawner
{
    [Header("墨斩配置")]
    [Tooltip("墨刃宽度")]
    public float slashWidth = 3f;
    [Tooltip("墨刃推进速度")]
    public float slashSpeed = 5f;
    [Tooltip("墨刃高度")]
    public float slashHeight = 1.2f;

    private void Awake()
    {
        weaponId = "ink_slash";
        baseAttackPower = 7;
        attackSpeed = 1.8f;
        inactiveDelay = 1f;
        projectileSprite = PlaceholderSpriteFactory.CreateInkSlashSprite(48, 8, ShuiMoPalette.InkDeep);
        projectileColor = ShuiMoPalette.InkDeep;
    }

    protected override void RegisterPool()
    {
        if (ObjectPool.Instance.AvailableCount(weaponId) > 0) return;
        ObjectPool.Instance.Register(weaponId, () => {
            var obj = CreateProjectile();
            obj.GetComponent<SpriteRenderer>().sprite = projectileSprite;
            obj.GetComponent<SpriteRenderer>().color = projectileColor;
            // 墨斩用宽 BoxCollider
            Destroy(obj.GetComponent<CircleCollider2D>());
            var col = obj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(slashWidth * 0.3f, slashHeight);
            var rb = obj.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            obj.AddComponent<InkSlashBehaviour>();
            return obj;
        }, 8);
    }

    protected override IEnumerator StartAttack()
    {
        while (isActive && playerStats != null && !playerStats.IsDead)
        {
            float facingSign = playerStats.transform.localScale.x < 0 ? -1f : 1f;

            var obj = ObjectPool.Instance.Get(weaponId);
            if (obj == null) { yield return new WaitForSeconds(finalAttackSpeed); continue; }

            obj.transform.position = transform.position + Vector3.right * facingSign * 1f;
            obj.transform.localScale = new Vector3(
                baseScale * additionalScale * slashWidth * 0.2f,
                baseScale * additionalScale * 0.5f,
                1f);

            var weapon = obj.GetComponent<AutoWeapon>();
            if (weapon != null)
                weapon.SetParameters(finalAttackPower, inactiveDelay, weaponId, projectileSprite, projectileColor, playerStats);

            var behaviour = obj.GetComponent<InkSlashBehaviour>();
            if (behaviour != null)
                behaviour.Init(facingSign * slashSpeed, facingSign, this);

            yield return new WaitForSeconds(finalAttackSpeed / 2f);

            // Lv4+ 追加反向斩
            if (level >= 4)
            {
                var obj2 = ObjectPool.Instance.Get(weaponId);
                if (obj2 != null)
                {
                    obj2.transform.position = transform.position + Vector3.left * facingSign * 1f;
                    obj2.transform.localScale = obj.transform.localScale;
                    var w2 = obj2.GetComponent<AutoWeapon>();
                    if (w2 != null)
                        w2.SetParameters(finalAttackPower, inactiveDelay, weaponId, projectileSprite, projectileColor, playerStats);
                    var b2 = obj2.GetComponent<InkSlashBehaviour>();
                    if (b2 != null)
                        b2.Init(-facingSign * slashSpeed, -facingSign, this);
                }
            }

            yield return new WaitForSeconds(finalAttackSpeed / 2f);
        }
    }

    protected override void LevelUp()
    {
        if (level == 3) baseAttackPower += 4;
        if (level == 5) { slashWidth += 1f; slashSpeed += 2f; baseAttackPower += 4; }
        UpdateFinalStats();
    }
}

/// <summary>
/// 墨斩行为：沿方向移动，到达距离后回收
/// </summary>
public class InkSlashBehaviour : MonoBehaviour
{
    private float _moveSpeed;
    private float _direction;
    private AutoWeaponSpawner _spawner;
    private float _traveledDistance;
    private const float MaxDistance = 6f;

    public void Init(float speed, float dir, AutoWeaponSpawner spawner)
    {
        _moveSpeed = speed;
        _direction = dir;
        _spawner = spawner;
        _traveledDistance = 0f;
    }

    private void Update()
    {
        float step = _moveSpeed * Time.deltaTime;
        transform.position += Vector3.right * _direction * step;
        _traveledDistance += step;
        if (_traveledDistance >= MaxDistance)
        {
            var weapon = GetComponent<AutoWeapon>();
            if (weapon != null) weapon.ReturnToPool();
        }
    }
}