using UnityEngine;
using System.Collections;

/// <summary>
/// 墨击：锁定最近敌人，从其上方降下墨柱打击。
/// 参考：Lightning.cs + LightningSpawner.cs (VampireSurvivors clone)
/// 适配：横版2D，从天而降的纵向墨条
/// </summary>
public class InkStrikeSpawner : AutoWeaponSpawner
{
    [Header("墨击配置")]
    [Tooltip("墨柱落下高度（从多高处开始）")]
    public float strikeHeight = 8f;
    [Tooltip("每波最大打击数")]
    public int maxStrikes = 3;
    [Tooltip("墨柱宽度")]
    public float columnWidth = 0.4f;

    private void Awake()
    {
        weaponId = "ink_strike";
        baseAttackPower = 12;
        attackSpeed = 2.5f;
        inactiveDelay = 0.6f;
        projectileSprite = PlaceholderSpriteFactory.CreateInkColumnSprite(8, 32, ShuiMoPalette.InkDeep);
        projectileColor = ShuiMoPalette.Vermillion;
    }

    protected override void RegisterPool()
    {
        if (ObjectPool.Instance.AvailableCount(weaponId) > 0) return;
        ObjectPool.Instance.Register(weaponId, () => {
            var obj = CreateProjectile();
            obj.GetComponent<SpriteRenderer>().sprite = projectileSprite;
            obj.GetComponent<SpriteRenderer>().color = projectileColor;
            // 墨柱用 BoxCollider（纵向）
            Destroy(obj.GetComponent<CircleCollider2D>());
            var col = obj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(columnWidth, 1.5f);
            return obj;
        }, 6);
    }

    protected override IEnumerator StartAttack()
    {
        while (isActive && playerStats != null && !playerStats.IsDead)
        {
            int count = Mathf.Min(maxStrikes, 1 + level / 2); // Lv1=2, Lv5=3

            for (int i = 0; i < count; i++)
            {
                Vector3 targetPos = GetNearestEnemyPosition();
                // 每次打击稍微偏移以覆盖多个敌人
                targetPos += new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0f, 1f), 0f);

                var obj = ObjectPool.Instance.Get(weaponId);
                if (obj == null) continue;

                // 从上方出现
                obj.transform.position = new Vector3(targetPos.x, targetPos.y + strikeHeight, 0f);
                obj.transform.localScale = new Vector3(baseScale * additionalScale, baseScale * additionalScale * 2f, 1f);

                var weapon = obj.GetComponent<AutoWeapon>();
                if (weapon != null)
                    weapon.SetParameters(finalAttackPower, inactiveDelay, weaponId, projectileSprite, projectileColor, playerStats);

                // 动画：快速落向目标
                StartCoroutine(StrikeDown(obj, targetPos));

                yield return new WaitForSeconds(0.15f);
            }

            yield return new WaitForSeconds(finalAttackSpeed);
        }
    }

    private IEnumerator StrikeDown(GameObject obj, Vector3 targetPos)
    {
        if (obj == null) yield break;
        float elapsed = 0f;
        Vector3 startPos = obj.transform.position;
        Vector3 endPos = targetPos;

        while (elapsed < 0.2f && obj != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / 0.2f;
            obj.transform.position = Vector3.Lerp(startPos, endPos, t * t); // 加速下落
            yield return null;
        }
    }

    protected override void LevelUp()
    {
        if (level == 3) baseAttackPower += 5;
        if (level == 5) { maxStrikes = 4; baseAttackPower += 5; }
        UpdateFinalStats();
    }
}