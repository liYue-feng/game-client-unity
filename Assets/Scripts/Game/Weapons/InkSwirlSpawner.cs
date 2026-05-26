using UnityEngine;
using System.Collections;

/// <summary>
/// 墨旋：墨滴环绕玩家旋转，形成旋转屏障。
/// 参考：Bible.cs + BibleSpawner.cs (VampireSurvivors clone)
/// 适配：横版2D RotateAround 机制
/// </summary>
public class InkSwirlSpawner : AutoWeaponSpawner
{
    [Header("墨旋配置")]
    [Tooltip("环绕半径")]
    public float orbitRadius = 1.2f;
    [Tooltip("旋转速度（度/秒）")]
    public float rotationSpeed = 180f;

    private void Awake()
    {
        weaponId = "ink_swirl";
        baseAttackPower = 5;
        attackSpeed = 0f; // 不重新生成，持续旋转
        inactiveDelay = 999f; // 不会超时，手动管理
        projectileSprite = PlaceholderSpriteFactory.CreateCircle(5, ShuiMoPalette.FlowerBlue, 0.4f);
        projectileColor = ShuiMoPalette.FlowerBlue;
    }

    protected override void RegisterPool()
    {
        if (ObjectPool.Instance.AvailableCount(weaponId) > 0) return;
        ObjectPool.Instance.Register(weaponId, () => {
            var obj = CreateProjectile();
            var sr = obj.GetComponent<SpriteRenderer>();
            sr.sprite = projectileSprite;
            sr.color = projectileColor;
            // 墨旋需要 Rigidbody 用于 RotateAround
            var rb = obj.GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            obj.AddComponent<InkSwirlBehaviour>();
            return obj;
        }, 8);
    }

    protected override IEnumerator StartAttack()
    {
        // 初始生成墨旋粒子
        SpawnSwirlParticles();
        // 墨旋不重新生成，由 InkSwirlBehaviour 持续旋转
        yield break;
    }

    private void SpawnSwirlParticles()
    {
        int count = 2 + level; // Lv1=3, Lv5=7
        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float angle = i * angleStep;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * orbitRadius,
                Mathf.Sin(angle * Mathf.Deg2Rad) * orbitRadius,
                0f);

            var obj = ObjectPool.Instance.Get(weaponId);
            if (obj == null) continue;

            obj.transform.position = transform.position + offset;
            obj.transform.localScale = Vector3.one * baseScale * additionalScale;

            var weapon = obj.GetComponent<AutoWeapon>();
            if (weapon != null)
                weapon.SetParameters(finalAttackPower, inactiveDelay, weaponId, projectileSprite, projectileColor, playerStats);

            var behaviour = obj.GetComponent<InkSwirlBehaviour>();
            if (behaviour != null)
                behaviour.Init(transform, angle, orbitRadius, rotationSpeed, this);
        }
    }

    public override void IncreaseLevel()
    {
        base.IncreaseLevel();
        // 清除旧粒子，重新生成
        ClearAllParticles();
        SpawnSwirlParticles();
    }

    public void ClearAllParticles()
    {
        var all = GameObject.FindObjectsOfType<InkSwirlBehaviour>();
        foreach (var b in all)
        {
            if (b.spawner == this)
                ObjectPool.Instance.Return(weaponId, b.gameObject);
        }
    }

    protected override void LevelUp()
    {
        if (level == 3) baseAttackPower += 3;
        if (level == 5) { orbitRadius += 0.3f; baseAttackPower += 3; }
        UpdateFinalStats();
    }

    private void OnDestroy()
    {
        ClearAllParticles();
    }
}

/// <summary>
/// 墨旋粒子行为：RotateAround 玩家持续旋转
/// </summary>
public class InkSwirlBehaviour : MonoBehaviour
{
    public Transform target;
    public float angle;
    public float radius;
    public float speed;
    public InkSwirlSpawner spawner;

    private AutoWeapon _weapon;

    public void Init(Transform target_, float angle_, float radius_, float speed_, InkSwirlSpawner spawner_)
    {
        target = target_;
        angle = angle_;
        radius = radius_;
        speed = speed_;
        spawner = spawner_;
        _weapon = GetComponent<AutoWeapon>();
    }

    private void Update()
    {
        if (target == null) return;
        angle += speed * Time.deltaTime;
        Vector3 offset = new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
            Mathf.Sin(angle * Mathf.Deg2Rad) * radius,
            0f);
        transform.position = target.position + offset;
    }
}