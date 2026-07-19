using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Game.Gameplay;

/// <summary>
/// 波次刷怪器：管理战斗房间内的敌人波次。
/// 使用 ObjectPool 管理敌人实例，避免频繁创建销毁。
///
/// 参考：SikPang/Unity_VampireSurvivors_Copy 的对象池设计
/// </summary>
public class WaveSpawner : MonoBehaviour, System.IDisposable
{
    [Header("波次配置")]
    [Tooltip("波次列表")]
    public EnemySpawnGroup[] waves;
    [Tooltip("波间延迟（秒）")]
    public float waveDelay = 2f;
    [Tooltip("每种敌人的池预分配数量")]
    public int poolSizePerType = 10;

    [Header("敌人属性成长")]
    [Tooltip("每波 HP 倍率增长 (1.0 = 不变, 1.2 = +20%/波)")]
    public float enemyHpMultiplier = 1.15f;
    [Tooltip("每波伤害倍率增长")]
    public float enemyDamageMultiplier = 1.1f;
    [Tooltip("每波速度倍率增长")]
    public float enemySpeedMultiplier = 1.05f;

    private int _currentWave;
    private List<GameObject> _aliveEnemies = new List<GameObject>();
    private readonly HashSet<string> _registeredPoolKeys = new HashSet<string>();
    private readonly Dictionary<EnemyBase, System.Action<EnemyBase>> _deathHandlers =
        new Dictionary<EnemyBase, System.Action<EnemyBase>>();
    private bool _poolsRegistered;
    private bool _disposed;
    private BattleArenaBounds _arenaBounds;
    private Transform _player;
    private Camera _camera;
    private bool _arenaConfigured;

    /// <summary>所有波次完成事件</summary>
    public event System.Action OnAllWavesComplete;
    /// <summary>新一波开始事件</summary>
    public event System.Action<int> OnWaveStart;

    private void Awake()
    {
        RegisterPools();
    }

    /// <summary>向 ObjectPool 注册所有敌人类型的工厂</summary>
    private void RegisterPools()
    {
        if (_disposed || _poolsRegistered) return;

        // 收集波次配置中所有用到的敌人类型
        var types = new HashSet<string>();
        if (waves != null)
        {
            foreach (var wave in waves)
            {
                if (wave.enemies == null) continue;
                foreach (var entry in wave.enemies)
                {
                    if (!string.IsNullOrEmpty(entry.enemyType))
                        types.Add(entry.enemyType);
                }
            }
        }

        // 动态添加组件时 Awake 早于外部配置，不能把空配置标记为已注册。
        if (types.Count == 0) return;

        var pool = ObjectPool.Instance;
        foreach (var type in types)
        {
            if (pool.Register(type, () => CreateEnemy(type), poolSizePerType))
            {
                _registeredPoolKeys.Add(type);
            }
        }

        _poolsRegistered = _registeredPoolKeys.Count > 0;
    }

    /// <summary>创建敌人（由 ObjectPool 调用）</summary>
    private GameObject CreateEnemy(string enemyType)
    {
        GameObject obj = new GameObject($"Enemy_{enemyType}");
        obj.SetActive(false);

        var sr = obj.AddComponent<SpriteRenderer>();
        var rb = obj.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = obj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.32f, 0.48f);

        obj.AddComponent<HitEffectPlayer>();
        obj.tag = "Enemy";

        // 根据类型配置外观和脚本
        EnemyBase enemyBase = null;
        switch (enemyType)
        {
            case "grunt":
                sr.sprite = AiSpriteLoader.GruntSprite();
                enemyBase = obj.AddComponent<Grunt>();
                enemyBase.expValue = 1;
                break;
            case "archer":
                sr.sprite = AiSpriteLoader.ArcherSprite();
                enemyBase = obj.AddComponent<Archer>();
                enemyBase.expValue = 2;
                break;
            case "elite":
                sr.sprite = AiSpriteLoader.EliteSprite();
                enemyBase = obj.AddComponent<Elite>();
                enemyBase.expValue = 5;
                break;
            case "boss":
                sr.sprite = AiSpriteLoader.BossSprite();
                enemyBase = obj.AddComponent<Boss>();
                enemyBase.expValue = 20;
                break;
            default:
                sr.sprite = AiSpriteLoader.EnemySprite();
                enemyBase = obj.AddComponent<Grunt>();
                enemyBase.expValue = 1;
                break;
        }

        enemyBase.InitializeCombatBaseline();

        return obj;
    }

    /// <summary>
    /// 显式绑定当前战局的世界边界、玩家与相机，避免 Spawner Transform 再充当第二套坐标权威。
    /// </summary>
    public void ConfigureArena(BattleArenaBounds bounds, Transform player, Camera camera)
    {
        _arenaBounds = bounds;
        _player = player;
        _camera = camera;
        _arenaConfigured = player != null && camera != null;
    }

    /// <summary>开始第一波</summary>
    public void StartWaves()
    {
        if (_disposed) return;
        RegisterPools();
        if (!_poolsRegistered) return;
        _currentWave = 0;
        StartCoroutine(SpawnWaveCoroutine(0));
    }

    private IEnumerator SpawnWaveCoroutine(int waveIndex)
    {
        if (_disposed) yield break;
        if (waveIndex >= waves.Length)
        {
            OnAllWavesComplete?.Invoke();
            yield break;
        }

        OnWaveStart?.Invoke(waveIndex);
        var wave = waves[waveIndex];

        foreach (var entry in wave.enemies)
        {
            for (int i = 0; i < entry.count; i++)
            {
                if (_disposed) yield break;
                SpawnEnemy(entry);
                yield return new WaitForSeconds(wave.spawnDelay);
            }
        }

        // 等待当前波所有敌人死亡（或归还池）
        yield return new WaitUntil(() => _disposed || _aliveEnemies.Count == 0);
        if (_disposed) yield break;

        // 波间延迟
        yield return new WaitForSeconds(waveDelay);
        if (_disposed) yield break;

        _currentWave++;
        StartCoroutine(SpawnWaveCoroutine(_currentWave));
    }

    /// <summary>从对象池取出一个敌人</summary>
    private void SpawnEnemy(EnemySpawnEntry entry)
    {
        if (_disposed || !_arenaConfigured) return;
        GameObject enemyObj = ObjectPool.Instance.Get(entry.enemyType);
        if (enemyObj == null) return;

        var enemyBase = enemyObj.GetComponent<EnemyBase>();
        if (enemyBase != null)
        {
            UnbindDeathHandler(enemyBase);
            enemyBase.InitializeCombatBaseline();
            var waveStats = EnemyWaveScaling.Calculate(
                enemyBase.Baseline,
                _currentWave,
                new EnemyWaveMultipliers(
                    enemyHpMultiplier,
                    enemyDamageMultiplier,
                    enemySpeedMultiplier));
            enemyBase.PrepareForSpawn(waveStats);

            // Spawn entry 只表达侧别，最终位置由战场世界坐标权威统一规划。
            var cameraHalfWidth = _camera.orthographicSize * _camera.aspect;
            var verticalDistance = (double)transform.position.y - _player.position.y;
            var chaseDistance = System.Math.Max(0d, enemyBase.chaseRange);
            // Enemy AI 使用二维距离；先扣除固定出生高度差，避免横向预算取满后落在追击半径外。
            var horizontalChaseRange = (float)System.Math.Sqrt(System.Math.Max(
                0d,
                chaseDistance * chaseDistance - verticalDistance * verticalDistance));
            var spawnX = ArenaSpawnPlanner.PlanX(
                _arenaBounds,
                _player.position.x,
                entry.preferredSide,
                cameraHalfWidth,
                0.5f,
                horizontalChaseRange);
            enemyObj.transform.SetPositionAndRotation(
                new Vector3(spawnX, transform.position.y, 0f),
                Quaternion.identity);

            // 监听死亡 — 使用局部变量避免闭包问题
            string typeKey = entry.enemyType;
            System.Action<EnemyBase> onDeath = e => HandleEnemyDeath(e, typeKey, enemyObj);
            _deathHandlers.Add(enemyBase, onDeath);
            enemyBase.OnDeath += onDeath;
        }

        _aliveEnemies.Add(enemyObj);
    }

    /// <summary>延迟归还对象到池（等待死亡淡出动画完成）</summary>
    private IEnumerator ReturnToPool(string key, GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_disposed || obj == null) yield break;

        var pool = ObjectPool.ExistingInstance;
        if (pool != null)
        {
            var enemy = obj.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.CancelActiveLease();
            }
            pool.Return(key, obj);
        }
    }

    private void HandleEnemyDeath(EnemyBase enemy, string key, GameObject enemyObject)
    {
        if (!_deathHandlers.TryGetValue(enemy, out var handler))
        {
            return;
        }

        enemy.OnDeath -= handler;
        _deathHandlers.Remove(enemy);
        _aliveEnemies.Remove(enemyObject);
        if (!_disposed)
        {
            StartCoroutine(ReturnToPool(key, enemyObject, 0.6f));
        }
    }

    private void UnbindDeathHandler(EnemyBase enemy)
    {
        if (enemy == null || !_deathHandlers.TryGetValue(enemy, out var handler))
        {
            return;
        }

        enemy.OnDeath -= handler;
        _deathHandlers.Remove(enemy);
    }

    /// <summary>
    /// Stops attacks owned by the current scene without changing pool leases.
    /// </summary>
    public void CancelActiveCombatActions()
    {
        for (var index = _aliveEnemies.Count - 1; index >= 0; index--)
        {
            var enemyObject = _aliveEnemies[index];
            if (enemyObject == null)
            {
                continue;
            }

            var enemy = enemyObject.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.CancelCombatActions();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopAllCoroutines();
        CancelActiveCombatActions();
        foreach (var enemyObject in new List<GameObject>(_aliveEnemies))
        {
            if (enemyObject != null)
            {
                var enemy = enemyObject.GetComponent<EnemyBase>();
                if (enemy != null)
                {
                    enemy.CancelActiveLease();
                }
            }
        }

        foreach (var registration in new List<KeyValuePair<EnemyBase, System.Action<EnemyBase>>>(_deathHandlers))
        {
            if (registration.Key != null)
            {
                registration.Key.OnDeath -= registration.Value;
            }
        }

        _deathHandlers.Clear();
        _aliveEnemies.Clear();
        _currentWave = 0;
        OnAllWavesComplete = null;
        OnWaveStart = null;

        var pool = ObjectPool.ExistingInstance;
        if (pool != null)
        {
            foreach (var key in _registeredPoolKeys)
            {
                pool.Clear(key);
            }
        }

        _registeredPoolKeys.Clear();
        _poolsRegistered = false;
        _player = null;
        _camera = null;
        _arenaConfigured = false;
    }

    private void OnDestroy()
    {
        Dispose();
    }

}

/// <summary>
/// 敌人生成组（一波）
/// </summary>
[System.Serializable]
public class EnemySpawnGroup
{
    [Tooltip("本波敌人列表")]
    public EnemySpawnEntry[] enemies;
    [Tooltip("每个敌人生成间隔")]
    public float spawnDelay = 0.5f;
}

/// <summary>
/// 敌人生成条目
/// </summary>
[System.Serializable]
public class EnemySpawnEntry
{
    [Tooltip("敌人类型：grunt/archer/elite/boss")]
    public string enemyType = "grunt";
    [Tooltip("数量")]
    public int count = 1;
    [Tooltip("优先生成侧别；最终世界坐标由战场规划器计算")]
    public ArenaSpawnSide preferredSide = ArenaSpawnSide.Right;
}
