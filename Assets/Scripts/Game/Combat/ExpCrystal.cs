using UnityEngine;

/// <summary>
/// 经验水晶：敌人死亡时掉落，自动吸引到玩家附近，被拾取后给玩家经验。
/// 水墨风格：藤黄色墨点精灵。
/// </summary>
public class ExpCrystal : MonoBehaviour
{
    [Tooltip("经验值")]
    public int expValue = 1;
    [Tooltip("吸引范围（米）")]
    public float attractRange = 3f;
    [Tooltip("吸引速度")]
    public float attractSpeed = 8f;
    [Tooltip("自动消失时间（秒）")]
    public float lifetime = 15f;

    private Transform _player;
    private bool _isAttracting;
    private float _spawnTime;

    private void Start()
    {
        _spawnTime = Time.time;

        // 找玩家
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
        }

        // 创建水墨风格精灵
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PlaceholderSpriteFactory.CreateCircle(8, ShuiMoPalette.Gamboge, 0.5f);
        sr.sortingOrder = 50;

        // 添加碰撞体
        var col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.2f;
    }

    private void Update()
    {
        // 超时消失
        if (Time.time - _spawnTime > lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_player == null) return;

        // 检查是否进入吸引范围
        float dist = Vector2.Distance(transform.position, _player.position);
        if (dist <= attractRange)
        {
            _isAttracting = true;
        }

        // 吸引移动
        if (_isAttracting)
        {
            Vector2 dir = (_player.position - transform.position).normalized;
            float speed = attractSpeed * (1f + (1f - dist / attractRange)); // 越近越快
            transform.position += (Vector3)dir * speed * Time.deltaTime;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 被玩家拾取
        if (other.CompareTag("Player") || other.gameObject == _player?.gameObject)
        {
            var stats = other.GetComponent<CharacterStats>();
            if (stats != null)
            {
                stats.AddExp(expValue);
            }
            AudioManager.Instance.PlaySFX("exp_pickup");
            Destroy(gameObject);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attractRange);
    }
}
