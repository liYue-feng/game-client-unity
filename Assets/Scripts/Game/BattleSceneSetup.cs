using Game.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 战斗场景搭建器：运行时动态创建场景中的所有对象。
/// 为什么不在编辑器中手动搭建：项目还没有场景文件，
/// 代码生成更灵活，方便后续接入地牢系统的房间生成。
///
/// 创建内容：
/// - 主相机（含屏幕震动 + 卡帧）
/// - 地面平台（绿色矩形）
/// - 玩家角色（蓝色矩形 + 全套组件）
/// - 木桩敌人（红色矩形 + Hurtbox）
/// </summary>
public class BattleSceneSetup : MonoBehaviour
{
    [Header("场景参数")]
    [Tooltip("地面宽度")]
    public float groundWidth = 30f;
    [Tooltip("地面厚度")]
    public float groundHeight = 1f;
    [Tooltip("玩家出生位置")]
    public Vector3 playerSpawnPos = new Vector3(0f, 1f, 0f);
    [Tooltip("敌人出生位置")]
    public Vector3 enemySpawnPos = new Vector3(5f, 1f, 0f);

    // 运行时引用
    private GameObject _player;
    private UpgradeManager _upgradeManager;
    private WaveSpawner _waveSpawner;
    private BattleRunController _battleRunController;
    private GameOverUI _gameOverUI;
    private BattleCameraRig _battleCameraRig;
    private InkParticlePool _inkParticlePool;
    private CombatFeedbackController _combatFeedbackController;

    // 战斗统计
    private int _killCount;
    private int _bossKills;
    private float _startTime;
    private int _elementalUpgradeCount;
    private int _summonUpgradeCount;
    private int _styleSwitchCount;

    // 运行时引用
    private InputMediator _inputMediator;
    private BattleTimeController _battleTimeController;
    private PauseMenuUI _pauseMenu;
    private InventoryUI _inventoryUI;
    private InkHitEffect _inkEffect;
    private InkSlashEffect _slashEffect;
    private Inventory _inventory;
    private bool _combatEventsSubscribed;
    private bool _inventoryEventSubscribed;
    private bool _pauseMenuEventsSubscribed;
    private bool _isPaused;
    private bool _isInventoryOpen;

    public bool BattleHotkeysEnabled { get; set; } = true;

    private void Start()
    {
        _startTime = Time.time;

        // 初始化全局系统
        AiSpriteLoader.PreloadAllSprites();
        RequireService(AudioManager.Instance, nameof(AudioManager)).PlayBGM("dash");
        var _ = DamageNumberPool.Instance;
        var __ = ElementalEffectManager.Instance;
        var ___ = SummonManager.Instance;
        RequireService(LoadingScreen.Instance, nameof(LoadingScreen)).Hide();

        EnsureEventSystem();
        CreateBattleTimeController();
        CreateCamera();
        CreateInkParticlePool();
        CreateGround();
        CreateUpgradeManager();  // 先创建UpgradeManager，WeaponSystem可以找到它
        CreatePlayer();
        InitializeUpgradeManager();
        _inputMediator = _player.GetComponent<InputMediator>();
        ApplyTalentBonuses();
        SummonManager.Instance.InitializeForBattle(_player);
        CreateWaveSpawner();
        CreateGameOverUI();
        CreateBattleRunController();
        CreateHUD();
        CreateInventoryUI();
        CreatePauseMenu();
        SetupEffectListeners();
        StartBattleWaves();
    }

    private static T RequireService<T>(T service, string serviceName) where T : UnityEngine.Object
    {
        if (service == null)
        {
            throw new System.InvalidOperationException(
                $"BattleScene initialization requires preinstalled {serviceName} service.");
        }

        return service;
    }

    private void CreateBattleTimeController()
    {
        var controllerObject = new GameObject("BattleTimeController");
        _battleTimeController = controllerObject.AddComponent<BattleTimeController>();
    }

    private void CreateInkParticlePool()
    {
        var poolObject = new GameObject("[InkParticlePool]");
        _inkParticlePool = poolObject.AddComponent<InkParticlePool>();
    }

    private void EnsureEventSystem()
    {
        var activeScene = gameObject.scene;
        EventSystem keeper = null;
        foreach (var eventSystem in Resources.FindObjectsOfTypeAll<EventSystem>())
        {
            if (eventSystem == null || !eventSystem.gameObject.scene.IsValid())
            {
                continue;
            }

            if (keeper == null && eventSystem.gameObject.scene == activeScene)
            {
                keeper = eventSystem;
                continue;
            }

            Destroy(eventSystem.gameObject);
        }

        if (keeper == null)
        {
            var eventSystemObject = new GameObject("EventSystem");
            keeper = eventSystemObject.AddComponent<EventSystem>();
        }

        var modules = keeper.GetComponents<StandaloneInputModule>();
        if (modules.Length == 0)
        {
            keeper.gameObject.AddComponent<StandaloneInputModule>();
        }
        else
        {
            for (var index = 1; index < modules.Length; index++)
            {
                Destroy(modules[index]);
            }
        }
    }

    /// <summary>创建主相机，挂载屏幕震动和卡帧</summary>
    private void CreateCamera()
    {
        // 如果场景中已有 Camera，复用它
        Camera existingCam = Camera.main;
        GameObject camObj;
        if (existingCam != null)
        {
            camObj = existingCam.gameObject;
        }
        else
        {
            camObj = new GameObject("Main Camera");
            camObj.AddComponent<Camera>();
        }

        var rigObject = new GameObject("[BattleCameraRig]");
        _battleCameraRig = rigObject.AddComponent<BattleCameraRig>();
        camObj.transform.SetParent(rigObject.transform, false);
        camObj.transform.localPosition = new Vector3(0f, 0f, -10f);
        camObj.tag = "MainCamera";

        // 水墨风格：相机背景设为宣纸色
        var cam = camObj.GetComponent<Camera>();
        if (cam != null) cam.backgroundColor = ShuiMoPalette.RicePaper;

        // 打击反馈组件
        if (camObj.GetComponent<CameraShaker>() == null)
            camObj.AddComponent<CameraShaker>();
        var hitStopController = camObj.GetComponent<HitStopController>();
        if (hitStopController == null)
            hitStopController = camObj.AddComponent<HitStopController>();
        hitStopController.ConfigureBattleTimeController(_battleTimeController);
    }

    /// <summary>创建地面平台</summary>
    private void CreateGround()
    {
        GameObject ground = new GameObject("Ground");
        ground.transform.position = new Vector3(0f, -0.5f, 0f);

        // 精灵
        var sr = ground.AddComponent<SpriteRenderer>();
        sr.sprite = AiSpriteLoader.InkGroundSprite((int)(groundWidth * 100), (int)(groundHeight * 100));
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(groundWidth, groundHeight);

        // 碰撞体
        var col = ground.AddComponent<BoxCollider2D>();
        col.size = new Vector2(groundWidth, groundHeight);

        // 静态刚体
        var rb = ground.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        // 设置层级为 Ground
        ground.layer = LayerMask.NameToLayer("Default");
    }

    /// <summary>创建玩家角色</summary>
    private void CreatePlayer()
    {
        _player = new GameObject("Player");
        _player.tag = "Player";
        _player.transform.position = playerSpawnPos;

        // 精灵
        var sr = _player.AddComponent<SpriteRenderer>();
        sr.sprite = AiSpriteLoader.PlayerSprite();

        // 物理组件
        var rb = _player.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.gravityScale = 3f;

        var col = _player.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.32f, 0.48f);

        // 战斗组件
        _player.AddComponent<CharacterStats>();
        var stateMachine = _player.AddComponent<PlayerStateMachine>();
        stateMachine.ConfigureBattleTimeController(_battleTimeController);
        _player.AddComponent<StaminaController>();
        _player.AddComponent<InputHandler>(); // 用旧输入系统，更稳定
        _player.AddComponent<InputMediator>();
        _player.AddComponent<PlayerInputBridge>();
        var controller = _player.AddComponent<PlayerController>();

        // 受击判定
        var hurtbox = _player.AddComponent<Hurtbox>();
        hurtbox.stats = _player.GetComponent<CharacterStats>();
        hurtbox.stateMachine = stateMachine;

        // 攻击 Hitbox（子物体）
        var hitboxObj = new GameObject("AttackHitbox");
        hitboxObj.transform.SetParent(_player.transform);
        hitboxObj.transform.localPosition = new Vector3(0.5f, 0.2f, 0f);
        var hitboxCol = hitboxObj.AddComponent<BoxCollider2D>();
        var hitbox = hitboxObj.AddComponent<Hitbox>();
        hitbox.damage = 10;
        hitbox.owner = _player;
        hitbox.autoDisableTime = 0f;
        hitboxCol.isTrigger = true;
        hitboxCol.size = new Vector2(0.6f, 0.4f);
        hitboxCol.offset = Vector2.zero;
        stateMachine.ConfigureAttackHitbox(hitbox);

        // 受击闪白效果
        _player.AddComponent<HitEffectPlayer>();

        // 挥砍墨线
        var slashObj = new GameObject("InkSlash");
        slashObj.transform.SetParent(_player.transform);
        slashObj.transform.localPosition = Vector3.zero;
        slashObj.AddComponent<InkSlashEffect>();

        // 墨迹飞溅
        var inkEffect = _player.AddComponent<InkHitEffect>();

        // 武器系统（监听升级事件，自动激活武器）
        _player.AddComponent<WeaponSystem>();

        // 地面检测层级
        _player.layer = LayerMask.NameToLayer("Default");
    }

    /// <summary>创建水墨风格战斗HUD</summary>
    private void CreateHUD()
    {
        // 创建HUD对象
        var hudObj = new GameObject("[BattleHUD]");
        var hud = hudObj.AddComponent<BattleHUD>();

        // 等待一帧后初始化（确保玩家组件都已就绪）
        StartCoroutine(InitializeHUDCoroutine(hud));
    }

    private System.Collections.IEnumerator InitializeHUDCoroutine(BattleHUD hud)
    {
        var stats = _player.GetComponent<CharacterStats>();
        if (stats != null && hud != null)
        {
            hud.InitializeForBattle(stats, _waveSpawner);
        }

        yield break;
    }

    /// <summary>创建波次刷怪器</summary>
    private void CreateWaveSpawner()
    {
        var spawnerObj = new GameObject("WaveSpawner");
        spawnerObj.transform.position = new Vector3(0f, 1f, 0f);
        _waveSpawner = spawnerObj.AddComponent<WaveSpawner>();

        // 配置波次（代码配置，无需场景文件）
        ConfigureWaves();

        var arenaBounds = new BattleArenaBounds(-groundWidth * 0.5f, groundWidth * 0.5f);
        var battleCamera = Camera.main;
        _battleCameraRig.Configure(_player.transform, arenaBounds, battleCamera);
        _waveSpawner.ConfigureArena(arenaBounds, _player.transform, battleCamera);

        // 开始刷怪
    }

    /// <summary>配置测试波次</summary>
    private void StartBattleWaves()
    {
        _waveSpawner.StartWaves();
    }

    private void CreateGameOverUI()
    {
        var gameOverObject = new GameObject("[GameOverUI]");
        _gameOverUI = gameOverObject.AddComponent<GameOverUI>();
    }

    private void CreateBattleRunController()
    {
        var controllerObject = new GameObject("BattleRunController");
        _battleRunController = controllerObject.AddComponent<BattleRunController>();
        _battleRunController.Configure(
            _player,
            _player.GetComponent<CharacterStats>(),
            _player.GetComponent<PlayerStateMachine>(),
            _player.GetComponent<PlayerInputBridge>(),
            _waveSpawner,
            _battleTimeController,
            _gameOverUI,
            this,
            CaptureCombatResultData);
        _battleRunController.ConfigureCameraRig(_battleCameraRig);
        CreateCombatFeedbackController();
    }

    private void CreateCombatFeedbackController()
    {
        _inkEffect = _player.GetComponent<InkHitEffect>();
        _slashEffect = _player.GetComponentInChildren<InkSlashEffect>();
        var cameraObject = Camera.main.gameObject;
        var controllerObject = new GameObject("[CombatFeedbackController]");
        _combatFeedbackController = controllerObject.AddComponent<CombatFeedbackController>();
        _combatFeedbackController.Configure(
            _player,
            _inkParticlePool,
            _inkEffect,
            _slashEffect,
            cameraObject.GetComponent<CameraShaker>(),
            cameraObject.GetComponent<HitStopController>());
        _battleRunController.ConfigureCombatFeedback(_combatFeedbackController);
    }

    private void ConfigureWaves()
    {
        var waves = new EnemySpawnGroup[10];

        // 简单的递增波次
        for (int w = 0; w < waves.Length; w++)
        {
            var wave = new EnemySpawnGroup();
            wave.spawnDelay = Mathf.Max(0.3f, 0.8f - w * 0.05f);

            int gruntCount = 3 + w * 2;
            int archerCount = w >= 2 ? w : 0;
            int eliteCount = w >= 5 ? (w - 4) : 0;
            int bossCount = (w + 1) % 5 == 0 ? 1 : 0;

            var entries = new System.Collections.Generic.List<EnemySpawnEntry>();
            ArenaSpawnSide NextSide() => ((w + entries.Count) & 1) == 0
                ? ArenaSpawnSide.Right
                : ArenaSpawnSide.Left;

            if (gruntCount > 0)
            {
                entries.Add(new EnemySpawnEntry
                {
                    enemyType = "grunt",
                    count = gruntCount,
                    preferredSide = NextSide()
                });
            }
            if (archerCount > 0)
            {
                entries.Add(new EnemySpawnEntry
                {
                    enemyType = "archer",
                    count = archerCount,
                    preferredSide = NextSide()
                });
            }
            if (eliteCount > 0)
            {
                entries.Add(new EnemySpawnEntry
                {
                    enemyType = "elite",
                    count = eliteCount,
                    preferredSide = NextSide()
                });
            }
            if (bossCount > 0)
            {
                entries.Add(new EnemySpawnEntry
                {
                    enemyType = "boss",
                    count = bossCount,
                    preferredSide = NextSide()
                });
            }

            wave.enemies = entries.ToArray();
            waves[w] = wave;
        }

        _waveSpawner.waves = waves;
    }

    /// <summary>设置全局特效监听</summary>
    private void SetupEffectListeners()
    {
        if (_combatEventsSubscribed)
        {
            return;
        }

        CombatEvents.OnPlayerDeath += HandlePlayerDeath;
        CombatEvents.OnEnemyDeath += HandleEnemyDeath;
        _combatEventsSubscribed = true;
    }

    private void HandlePlayerDeath()
    {
        AudioManager.Instance.PlaySFX("game_over");
        var stats = _player.GetComponent<CharacterStats>();
        if (stats != null)
        {
            TalentManager.Instance.AddTalentPoints(stats.level);

            // 汇报成就数据
            var data = new CombatResultData
            {
                killCount = _killCount,
                expGained = stats.currentExp + stats.ExpToNextLevel * (stats.level - 1),
                maxCombo = 0, // ComboCounter 目前没有 MaxCombo 属性
                survivalTime = Mathf.RoundToInt(Time.time - _startTime),
                playerLevel = stats.level,
                bossKills = _bossKills,
                elementalUpgradeCount = _elementalUpgradeCount,
                summonUpgradeCount = _summonUpgradeCount,
                styleSwitchCount = _styleSwitchCount
            };
            AchievementManager.Instance.ReportBattleResult(data);
        }
    }

    private CombatResultData CaptureCombatResultData()
    {
        var stats = _player.GetComponent<CharacterStats>();
        return new CombatResultData
        {
            killCount = _killCount,
            expGained = stats.currentExp + stats.ExpToNextLevel * (stats.level - 1),
            maxCombo = 0,
            survivalTime = Mathf.RoundToInt(Time.time - _startTime),
            playerLevel = stats.level,
            bossKills = _bossKills,
            elementalUpgradeCount = _elementalUpgradeCount,
            summonUpgradeCount = _summonUpgradeCount,
            styleSwitchCount = _styleSwitchCount
        };
    }

    private void HandleEnemyDeath(GameObject enemy)
    {
        AudioManager.Instance.PlaySFX("death");
        DamageNumberPool.SpawnText("破", enemy.transform.position, DamageType.Crit);
        _killCount++;
        if (enemy.GetComponent<Boss>() != null || enemy.name.ToLower().Contains("boss"))
            _bossKills++;
    }

    /// <summary>
    /// 创建升级管理器，使玩家创建期间的武器系统能够找到它。
    /// </summary>
    private void CreateUpgradeManager()
    {
        var managerObj = new GameObject("UpgradeManager");
        _upgradeManager = managerObj.AddComponent<UpgradeManager>();
    }

    /// <summary>
    /// 在玩家创建完成后绑定角色属性和升级追踪。
    /// </summary>
    private void InitializeUpgradeManager()
    {
        var stats = _player.GetComponent<CharacterStats>();
        if (stats != null)
        {
            _upgradeManager.Initialize(stats);
            _upgradeManager.levelUpUI.ConfigureBattleTimeController(_battleTimeController);
        }

        // 升级选项被选择后应用加成
        _upgradeManager.OnBeforeGenerateOptions = (options) => { };

        // 追踪升级选择（用于成就统计）
        _inventory = Inventory.Instance;
        if (!_inventoryEventSubscribed)
        {
            _inventory.OnItemChanged += HandleInventoryItemChanged;
            _inventoryEventSubscribed = true;
        }
    }

    private void HandleInventoryItemChanged(int slot, PassiveItem item)
    {
        if (item == null) return;
        if (item.category.Contains("elemental") || item.id.StartsWith("elem_"))
        {
            _elementalUpgradeCount = Mathf.Max(_elementalUpgradeCount,
                CountCategoryInInventory("elemental"));
        }
        if (item.category.Contains("summon") || item.id.StartsWith("summon_"))
        {
            _summonUpgradeCount = Mathf.Max(_summonUpgradeCount,
                CountCategoryInInventory("summon"));
        }
    }

    /// <summary>创建背包UI（按Tab查看）</summary>
    private void CreateInventoryUI()
    {
        var invObj = new GameObject("[InventoryUI]");
        _inventoryUI = invObj.AddComponent<InventoryUI>();
    }

    /// <summary>创建暂停菜单</summary>
    private void CreatePauseMenu()
    {
        if (_pauseMenuEventsSubscribed)
        {
            return;
        }

        var pauseObj = new GameObject("[PauseMenu]");
        _pauseMenu = pauseObj.AddComponent<PauseMenuUI>();
        _pauseMenu.ConfigureBattleTimeController(_battleTimeController);
        _pauseMenu.OnResume += HandleResume;
        _pauseMenu.OnBackToMenu += HandleBackToMenu;
        _pauseMenu.OnSettings += HandleSettings;
        _pauseMenuEventsSubscribed = true;
    }

    private void HandleBackToMenu()
    {
        _isPaused = false;
        LoadingScreen.Instance.Show();
        SceneTransitionManager.Instance.GoToMainMenu();
    }

    private void HandleResume()
    {
        _isPaused = false;
    }

    private void HandleSettings()
    {
        var settingsObj = new GameObject("SettingsUI");
        var settings = settingsObj.AddComponent<SettingsUI>();
        settings.OnClose += () => Destroy(settingsObj);
    }

    private void OnDestroy()
    {
        if (_battleRunController != null)
        {
            _battleRunController.Dispose();
        }

        if (_waveSpawner != null)
        {
            _waveSpawner.Dispose();
        }

        UnsubscribeSceneListeners();
    }

    private void UnsubscribeSceneListeners()
    {
        if (_combatEventsSubscribed)
        {
            CombatEvents.OnPlayerDeath -= HandlePlayerDeath;
            CombatEvents.OnEnemyDeath -= HandleEnemyDeath;
            _combatEventsSubscribed = false;
        }

        if (_inventoryEventSubscribed)
        {
            if (_inventory != null)
            {
                _inventory.OnItemChanged -= HandleInventoryItemChanged;
            }
            _inventoryEventSubscribed = false;
        }

        if (_pauseMenuEventsSubscribed)
        {
            if (_pauseMenu != null)
            {
                _pauseMenu.OnResume -= HandleResume;
                _pauseMenu.OnBackToMenu -= HandleBackToMenu;
                _pauseMenu.OnSettings -= HandleSettings;
            }
            _pauseMenuEventsSubscribed = false;
        }
    }

    private void Update()
    {
        if (!BattleHotkeysEnabled || _inputMediator == null) return;

        // 暂停菜单切换
        if (_inputMediator.PausePressed)
        {
            TogglePause();
        }

        // 背包切换
        if (_inputMediator.InventoryPressed)
        {
            ToggleInventory();
        }
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;

        if (_pauseMenu != null)
        {
            if (_isPaused)
            {
                _pauseMenu.Show();
            }
            else
            {
                _pauseMenu.Hide();
            }
        }
    }

    private void ToggleInventory()
    {
        _isInventoryOpen = !_isInventoryOpen;

        if (_inventoryUI != null)
        {
            if (_isInventoryOpen)
            {
                _inventoryUI.Show();
            }
            else
            {
                _inventoryUI.Hide();
            }
        }
    }

    int CountCategoryInInventory(string category)
    {
        int count = 0;
        for (int i = 0; i < Inventory.Instance.Count; i++)
        {
            var item = Inventory.Instance.Items[i];
            if (item != null && (item.category == category || item.id.StartsWith(category.Substring(0, 4))))
                count++;
        }
        return count;
    }

    /// <summary>将天赋树加成应用到玩家</summary>
    private void ApplyTalentBonuses()
    {
        var stats = _player.GetComponent<CharacterStats>();
        if (stats != null)
        {
            TalentManager.Instance.ApplyToPlayer(stats);
        }
    }
}
