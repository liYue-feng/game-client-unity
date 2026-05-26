using UnityEngine;
using System.Collections;

/// <summary>
/// 墨弹：向前方扇形发射墨滴。
/// 参考：FireWandSpawner.cs (VampireSurvivors clone)
/// 适配：横版水平方向扇形散射
/// </summary>
public class InkBoltSpawner : AutoWeaponSpawner
{
    [Header("墨弹配置")]
    [Tooltip("散射角度（度）")]
    public float spreadAngle = 15f;
    [Tooltip("墨弹飞行速度")]
    public float boltSpeed = 6f;

    private void Awake()
    {
        weaponId = "ink_bolt";
        baseAttackPower = 6;
        attackSpeed = 1.2f;
        inactiveDelay = 2f;
        projectileSprite = PlaceholderSpriteFactory.CreateInkProjectileSprite(6, ShuiMoPalette.InkBlack);
        projectileColor = ShuiMoPalette.InkBlack;
    }

    protected override void RegisterPool()
    {
        if (ObjectPool.Instance.AvailableCount(weaponId) > 0) return;
        ObjectPool.Instance.Register(weaponId, () => {
            var obj = CreateProjectile();
            // 设置墨弹专属外观
            var sr = obj.GetComponent<SpriteRenderer>();
            sr.sprite = projectileSprite;
            sr.color = projectileColor;
            return obj;
        }, 10);
    }

    protected override IEnumerator StartAttack()
    {
        while (isActive && playerStats != null && !playerStats.IsDead)
        {
            int count = 2 + level / 2; // Lv1=2, Lv3=3, Lv5=4
            float facingSign = playerStats.transform.localScale.x < 0 ? -1f : 1f;

            for (int i = 0; i < count; i++)
            {
                var obj = ObjectPool.Instance.Get(weaponId);
                if (obj == null) continue;

                obj.transform.position = transform.position;
                obj.transform.localScale = Vector3.one * baseScale * additionalScale;

                var weapon = obj.GetComponent<AutoWeapon>();
                if (weapon != null)
                    weapon.SetParameters(finalAttackPower, inactiveDelay, weaponId, projectileSprite, projectileColor, playerStats);

                // 扇形散射角度
                float offsetAngle = (i - (count - 1) / 2f) * spreadAngle;
                Vector3 dir = Quaternion.Euler(0, 0, -offsetAngle * facingSign) * (Vector3.right * facingSign);

                var rb = obj.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.gravityScale = 0f;
                    rb.velocity = dir * boltSpeed;
                }
            }

            yield return new WaitForSeconds(finalAttackSpeed);
        }
    }

    protected override void LevelUp()
    {
        // Lv3: 额外一发，Lv5: 额外一发 + 伤害提升
        if (level == 3) baseAttackPower += 3;
        if (level == 5) { boltSpeed += 2f; baseAttackPower += 3; }
        UpdateFinalStats();
    }
}