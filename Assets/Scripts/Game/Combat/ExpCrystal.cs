using UnityEngine;
using System.Collections;

/// <summary>
/// 经验水晶：敌人死亡时掉落，两阶段磁吸拾取。
/// 参考：Crystal.cs (VampireSurvivors clone) 的磁吸机制
/// 阶段1：进入吸引范围时施加初始推力
/// 阶段2：0.4秒后加速飞向玩家
/// 安全超时：5秒后自动回收
/// 水墨风格：藤黄色墨点
/// </summary>
public class ExpCrystal : MonoBehaviour
{
    [Tooltip("经验值")]
    public int expValue = 1;
    [Tooltip("吸引范围（米）")]
    public float attractRange = 3f;
    [Tooltip("吸引力加速度")]
    public float attractAcceleration = 3f;
    [Tooltip("初始推力")]
    public float initialForce = 4f;
    [Tooltip("自动消失时间（秒）")]
    public float lifetime = 15f;

    private Transform _player;
    private CharacterStats _playerStats;
    private bool _isAttracted;
    private bool _isCollecting;
    private float _speed;
    private Rigidbody2D _rb;

    private void Start()
    {
        _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.drag = 6f;
        _rb.bodyType = RigidbodyType2D.Dynamic;

        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
            _playerStats = playerObj.GetComponent<CharacterStats>();
        }

        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = PlaceholderSpriteFactory.CreateCircle(8, ShuiMoPalette.Gamboge, 0.5f);
        sr.sortingOrder = 50;

        var col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.25f;

        // 安全超时
        StartCoroutine(SafetyTimeout(lifetime));
    }

    private void Update()
    {
        if (_player == null) return;
        if (_isCollecting) return;

        float dist = Vector2.Distance(transform.position, _player.position);

        // 进入吸引范围 → 阶段1：初始推力
        if (!_isAttracted && dist <= attractRange)
        {
            _isAttracted = true;
            StartCoroutine(AttractionPhases());
        }

        // 阶段2：加速吸引
        if (_isAttracted)
        {
            _speed += attractAcceleration * Time.deltaTime;
            _speed = Mathf.Min(_speed, 20f);
            Vector2 dir = (_player.position - transform.position).normalized;
            _rb.velocity = dir * _speed;
        }
    }

    private IEnumerator AttractionPhases()
    {
        // 阶段1：初始推力（朝玩家方向）
        if (_rb != null && _player != null)
        {
            Vector2 dir = (_player.position - transform.position).normalized;
            _rb.AddForce(dir * initialForce, ForceMode2D.Impulse);
        }

        // 0.4 秒后进入阶段2（加速吸引）
        yield return new WaitForSecondsRealtime(0.4f);
        _isCollecting = true;
    }

    private IEnumerator SafetyTimeout(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (this != null && gameObject != null)
        {
            ForceCollect();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isCollecting || _player == null) return;
        if (other.CompareTag("Player") || other.gameObject == _player.gameObject)
        {
            Collect();
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!_isCollecting || _player == null) return;
        if (other.CompareTag("Player") || other.gameObject == _player.gameObject)
        {
            Collect();
        }
    }

    private void Collect()
    {
        if (_playerStats != null)
        {
            int exp = expValue;
            // 经验加成倍率
            float expMult = 1f + (Inventory.Instance != null ? Inventory.Instance.TotalExpBonus / 100f : 0f);
            exp = Mathf.RoundToInt(exp * expMult);
            _playerStats.AddExp(exp);
        }
        AudioManager.Instance.PlaySFX("exp_pickup");
        Destroy(gameObject);
    }

    private void ForceCollect()
    {
        if (_playerStats != null)
        {
            int exp = expValue;
            float expMult = 1f + (Inventory.Instance != null ? Inventory.Instance.TotalExpBonus / 100f : 0f);
            exp = Mathf.RoundToInt(exp * expMult);
            _playerStats.AddExp(exp);
        }
        if (gameObject != null) Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attractRange);
    }
}