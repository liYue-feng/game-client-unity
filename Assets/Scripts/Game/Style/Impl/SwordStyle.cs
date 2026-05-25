using UnityEngine;

/// <summary>
/// 剑流派：均衡反击，专注资源，剑气特殊技能。
/// 被动：完美弹反+15专注，反击伤害+50%
/// 特殊：剑气——远程斩击弹丸
/// </summary>
public class SwordStyle : IStyleBehaviour
{
    private StyleData _data;

    public SwordStyle()
    {
        _data = StyleDatabase.GetStyle(CombatStyleID.Sword);
    }

    public void OnAttackHit(EnemyBase enemy)
    {
        // 剑流派攻击不增加专注
    }

    public void OnParrySuccess()
    {
        // 完美弹反+15专注
        StyleManager.Instance.AddSpecialResource(15);
    }

    public void ActivateSpecial(GameObject player)
    {
        // 剑气：远程斩击弹丸
        var controller = player.GetComponent<PlayerController>();
        int facingDir = controller != null ? controller.FacingDirection : 1;

        GameObject slash = new GameObject("SwordQi");
        slash.transform.position = player.transform.position + new Vector3(facingDir * 0.5f, 0.3f, 0f);

        var sr = slash.AddComponent<SpriteRenderer>();
        sr.sprite = PlaceholderSpriteFactory.CreateRect(20, 8, new Color(0.2f, 0.3f, 0.55f, 0.7f));
        sr.sortingOrder = 5;

        var col = slash.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.4f, 0.15f);

        var rb = slash.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.velocity = new Vector2(facingDir * 12f, 0f);

        var projectile = slash.AddComponent<SwordQiProjectile>();
        projectile.damage = 20;
        projectile.owner = player;
    }

    public void PassiveUpdate()
    {
        // 剑流派无特殊被动
    }

    public StyleData GetData() => _data;
}

/// <summary>剑气弹丸</summary>
public class SwordQiProjectile : MonoBehaviour
{
    public int damage = 20;
    public GameObject owner;
    private float _lifetime = 2f;

    private void Update()
    {
        _lifetime -= Time.deltaTime;
        if (_lifetime <= 0) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == owner) return;

        if (other.CompareTag("Enemy"))
        {
            var enemy = other.GetComponent<EnemyBase>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(damage);
            }
            Destroy(gameObject);
            return;
        }

        if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}
