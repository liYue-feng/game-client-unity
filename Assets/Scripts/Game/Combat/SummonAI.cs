using UnityEngine;
using System.Collections;

/// <summary>
/// 召唤物AI：挂在召唤物GameObject上，负责移动和攻击。
/// 支持两种模式：
///   FollowAndAttack — 跟随玩家，检测敌人，自动攻击
///   OrbitAndStrike  — 环绕玩家旋转，周期性冲刺攻击
/// </summary>
public class SummonAI : MonoBehaviour
{
    public enum SummonType
    {
        InkSpirit,     // 墨魂 — 跟随+近战
        SwordSpirit    // 剑灵 — 环绕+远程
    }

    [Header("类型")]
    public SummonType type = SummonType.InkSpirit;

    [Header("属性")]
    public float orbitRadius = 1.5f;   // 环绕半径
    public float orbitSpeed = 90f;     // 环绕角速度（度/秒）
    public float moveSpeed = 4f;       // 接近敌人时的移动速度
    public int damage = 8;            // 单次伤害
    public float attackCooldown = 0.8f; // 攻击冷却
    public float attackRange = 2f;     // 攻击范围

    [Header("外观")]
    public Color summonColor = ShuiMoPalette.InkBlack;

    private Transform _player;
    private float _attackTimer;
    private float _orbitAngle;
    private EnemyBase _currentTarget;
    private SpriteRenderer _sr;

    void Start()
    {
        _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        _orbitAngle = Random.Range(0f, 360f);

        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null)
            _sr = gameObject.AddComponent<SpriteRenderer>();

        _sr.sprite = CreateSummonSprite();
        _sr.color = summonColor;

        // 碰撞体
        var col = gameObject.AddComponent<CircleCollider2D>();
        col.radius = 0.3f;
        col.isTrigger = true;
    }

    Sprite CreateSummonSprite()
    {
        int size = 24;
        var tex = new Texture2D(size, size);
        var center = new Vector2(size / 2f, size / 2f);
        var colors = new Color32[size * size];
        var inkCol = new Color32(26, 26, 26, 200);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var idx = y * size + x;
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float maxR = size / 2f;

                if (dist < maxR * 0.3f)
                {
                    colors[idx] = inkCol; // 核心墨点
                }
                else if (dist < maxR)
                {
                    // 墨晕：越远越淡
                    float alpha = 1f - (dist - maxR * 0.3f) / (maxR * 0.7f);
                    byte a = (byte)(180 * alpha * alpha);
                    colors[idx] = new Color32(26, 26, 26, a);
                }
                else
                {
                    colors[idx] = new Color32(0, 0, 0, 0);
                }
            }
        }
        tex.SetPixels32(colors);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    void Update()
    {
        if (_player == null) return;

        switch (type)
        {
            case SummonType.InkSpirit:
                UpdateInkSpirit();
                break;
            case SummonType.SwordSpirit:
                UpdateSwordSpirit();
                break;
        }
    }

    /// <summary>墨魂：跟随玩家偏移位置，检测敌人并攻击</summary>
    void UpdateInkSpirit()
    {
        // 目标位置：玩家身后偏移
        float angle = _orbitAngle + 150f; // 偏向玩家后方
        var targetPos = _player.position + new Vector3(
            Mathf.Cos(angle * Mathf.Deg2Rad) * orbitRadius,
            Mathf.Sin(angle * Mathf.Deg2Rad) * orbitRadius * 0.6f,
            0);

        // 检测最近的敌人
        _currentTarget = FindClosestEnemy(attackRange * 2);
        if (_currentTarget != null)
        {
            // 移动到敌人附近
            var dir = (_currentTarget.transform.position - transform.position).normalized;
            targetPos = transform.position + dir * moveSpeed * Time.deltaTime;
            _orbitAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 3f);

        // 攻击
        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0 && _currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, _currentTarget.transform.position);
            if (dist < attackRange)
            {
                AttackTarget(_currentTarget);
                _attackTimer = attackCooldown;
            }
        }
    }

    /// <summary>剑灵：环绕玩家旋转，周期性飞出攻击</summary>
    void UpdateSwordSpirit()
    {
        _orbitAngle += orbitSpeed * Time.deltaTime;
        float rad = _orbitAngle * Mathf.Deg2Rad;
        var orbitPos = _player.position + new Vector3(
            Mathf.Cos(rad) * orbitRadius,
            Mathf.Sin(rad) * orbitRadius * 0.5f,
            0);
        transform.position = Vector3.Lerp(transform.position, orbitPos, Time.deltaTime * 5f);

        // 旋转朝向
        transform.rotation = Quaternion.Euler(0, 0, _orbitAngle);

        // 自动攻击最近敌人
        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0)
        {
            var target = FindClosestEnemy(attackRange * 2.5f);
            if (target != null)
            {
                StartCoroutine(DashAttack(target));
                _attackTimer = attackCooldown;
            }
        }
    }

    IEnumerator DashAttack(EnemyBase target)
    {
        var startPos = transform.position;
        var endPos = target.transform.position;
        float t = 0;
        float duration = 0.15f;

        // 画出墨线轨迹
        var lineGo = new GameObject("SwordTrail");
        var lr = lineGo.AddComponent<LineRenderer>();
        lr.startWidth = 0.03f;
        lr.endWidth = 0.01f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = ShuiMoPalette.InkLight;
        lr.endColor = ShuiMoPalette.InkPale;
        Destroy(lineGo, 0.3f);

        while (t < duration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, t / duration);
            lr.SetPosition(0, transform.position);
            lr.SetPosition(1, endPos);
            yield return null;
        }

        if (target != null && !target.IsDead)
        {
            AttackTarget(target);
        }

        // 回弹
        t = 0;
        duration = 0.3f;
        var backPos = startPos;
        startPos = transform.position;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, backPos, t / duration);
            yield return null;
        }
    }

    void AttackTarget(EnemyBase target)
    {
        var stats = target.GetComponent<CharacterStats>();
        if (stats != null)
        {
            int totalDmg = damage;

            // 策令加成：检查背包中的策令等级
            int cmdLevel = SummonManager.Instance?.GetSummonLevel("summon_command") ?? 0;
            if (cmdLevel > 0) totalDmg = Mathf.RoundToInt(totalDmg * (1f + 0.2f * cmdLevel));

            stats.TakeDamage(totalDmg);
            DamageNumberPool.Spawn(totalDmg, target.transform.position + Vector3.up * 0.5f, DamageType.Normal);
            AudioManager.Instance.PlaySFX("hit");
            CombatEvents.InvokeHitLanded(target.transform.position, totalDmg);
        }
    }

    EnemyBase FindClosestEnemy(float range)
    {
        var enemies = FindObjectsOfType<EnemyBase>();
        EnemyBase closest = null;
        float minDist = range;

        foreach (var e in enemies)
        {
            if (e.IsDead) continue;
            float dist = Vector3.Distance(transform.position, e.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = e;
            }
        }
        return closest;
    }
}

/// <summary>
/// 召唤管理器：根据背包中的召唤升级，在战斗中生成和管理召唤物。
/// 单例模式。
/// </summary>
public class SummonManager : MonoBehaviour
{
    private static SummonManager _instance;
    public static SummonManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[SummonManager]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<SummonManager>();
            }
            return _instance;
        }
    }

    private GameObject _player;
    private readonly System.Collections.Generic.List<GameObject> _activeSummons = new System.Collections.Generic.List<GameObject>();
    private float _inkRainTimer;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>根据背包初始化所有召唤物</summary>
    public void InitializeForBattle(GameObject player)
    {
        _player = player;
        ClearAll();

        for (int i = 0; i < Inventory.Instance.Count; i++)
        {
            var item = Inventory.Instance.Items[i];
            if (item == null) continue;

            switch (item.id)
            {
                case "summon_ink_spirit":
                    SpawnInkSpirit(item.currentLevel);
                    break;
                case "summon_sword_spirit":
                    SpawnSwordSpirit(item.currentLevel);
                    break;
                case "summon_ink_rain":
                    // 墨雨不是持久召唤物，由Update驱动
                    break;
            }
        }

        Debug.Log($"[SummonManager] 召唤物初始化完成: {_activeSummons.Count} 个");
    }

    void SpawnInkSpirit(int level)
    {
        var go = new GameObject($"InkSpirit_Lv{level}");
        go.transform.position = _player.transform.position + Vector3.right;
        var ai = go.AddComponent<SummonAI>();
        ai.type = SummonAI.SummonType.InkSpirit;
        ai.damage = 5 + level * 3;
        ai.attackCooldown = Mathf.Max(0.3f, 0.8f - level * 0.1f);
        ai.orbitRadius = 1.5f;
        ai.summonColor = ShuiMoPalette.InkDeep;

        _activeSummons.Add(go);
    }

    void SpawnSwordSpirit(int level)
    {
        var go = new GameObject($"SwordSpirit_Lv{level}");
        go.transform.position = _player.transform.position + Vector3.up * 1.5f;
        var ai = go.AddComponent<SummonAI>();
        ai.type = SummonAI.SummonType.SwordSpirit;
        ai.damage = 4 + level * 4;
        ai.attackCooldown = Mathf.Max(0.5f, 1.2f - level * 0.15f);
        ai.orbitRadius = 2f;
        ai.orbitSpeed = 90f + level * 30f;
        ai.summonColor = ShuiMoPalette.FlowerBlue;

        _activeSummons.Add(go);
    }

    void Update()
    {
        // 墨雨：周期性AOE
        if (Inventory.Instance != null)
        {
            for (int i = 0; i < Inventory.Instance.Count; i++)
            {
                if (Inventory.Instance.Items[i]?.id == "summon_ink_rain")
                {
                    _inkRainTimer -= Time.deltaTime;
                    if (_inkRainTimer <= 0)
                    {
                        int level = Inventory.Instance.Items[i].currentLevel;
                        _inkRainTimer = Mathf.Max(1.5f, 4f - level * 0.5f);
                        StartCoroutine(InkRainStrike(level));
                    }
                    break;
                }
            }
        }
    }

    IEnumerator InkRainStrike(int level)
    {
        var enemies = FindObjectsOfType<EnemyBase>();
        int targets = Mathf.Min(3 + level, enemies.Length);
        int dmg = 8 * level;

        // 策令加成
        int cmdLevel = GetSummonLevel("summon_command");
        if (cmdLevel > 0) dmg = Mathf.RoundToInt(dmg * (1f + 0.2f * cmdLevel));

        for (int t = 0; t < targets; t++)
        {
            if (t >= enemies.Length) break;
            if (enemies[t].IsDead) continue;

            var pos = enemies[t].transform.position;
            int hitDmg = dmg;

            // 墨点下落视觉效果
            var dropGo = new GameObject("InkDrop");
            dropGo.transform.position = pos + Vector3.up * 3f;
            var sr = dropGo.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDropSprite();
            sr.color = ShuiMoPalette.InkBlack;
            Destroy(dropGo, 1f);

            // 下落动画
            float fallT = 0;
            var startPos = dropGo.transform.position;
            var endPos = pos;
            while (fallT < 0.5f)
            {
                fallT += Time.deltaTime;
                if (dropGo != null)
                    dropGo.transform.position = Vector3.Lerp(startPos, endPos, fallT / 0.5f);
                yield return null;
            }

            // 命中
            var stats = enemies[t].GetComponent<CharacterStats>();
            if (stats != null && !enemies[t].IsDead)
            {
                stats.TakeDamage(hitDmg);
                DamageNumberPool.Spawn(hitDmg, pos + Vector3.up * 0.5f, DamageType.Normal);
                AudioManager.Instance.PlaySFX("heavy_hit");
            }

            yield return new WaitForSeconds(0.15f);
        }
    }

    Sprite CreateDropSprite()
    {
        int s = 16;
        var tex = new Texture2D(s, s);
        var center = new Vector2(s / 2f, s / 2f);
        var colors = new Color32[s * s];

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = dist < s / 2f ? (1f - dist / (s / 2f)) : 0;
                colors[y * s + x] = new Color32(26, 26, 26, (byte)(200 * alpha));
            }
        }
        tex.SetPixels32(colors);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), Vector2.one * 0.5f, s);
    }

    /// <summary>获取指定召唤升级的等级</summary>
    public int GetSummonLevel(string summonId)
    {
        if (Inventory.Instance == null) return 0;
        for (int i = 0; i < Inventory.Instance.Count; i++)
        {
            if (Inventory.Instance.Items[i]?.id == summonId)
                return Inventory.Instance.Items[i].currentLevel;
        }
        return 0;
    }

    /// <summary>清除所有召唤物</summary>
    public void ClearAll()
    {
        foreach (var go in _activeSummons)
        {
            if (go != null) Destroy(go);
        }
        _activeSummons.Clear();
    }

    void OnDestroy()
    {
        ClearAll();
    }
}