using System;
using Game.Gameplay;
using UnityEngine;

public sealed class BattleRunController : MonoBehaviour, IDisposable
{
    private readonly BattleRunStateMachine _runState = new BattleRunStateMachine();

    private GameObject _player;
    private CharacterStats _playerStats;
    private PlayerStateMachine _playerStateMachine;
    private PlayerInputBridge _playerInputBridge;
    private PlayerController _playerController;
    private Rigidbody2D _playerBody;
    private WaveSpawner _waveSpawner;
    private BattleTimeController _battleTimeController;
    private GameOverUI _gameOverUI;
    private BattleSceneSetup _battleHotkeyOwner;
    private Func<CombatResultData> _resultDataProvider;
    private TimeScaleRequestToken _battleResultToken;
    private bool _configured;
    private bool _cameraRigConfigured;
    private bool _combatFeedbackConfigured;
    private bool _disposed;
    private BattleCameraRig _cameraRig;
    private CombatFeedbackController _combatFeedback;

    public BattleRunState State => _runState.State;
    public BattleRunOutcome Outcome => _runState.Outcome;
    public bool IsDisposed => _disposed;

    public void Configure(
        GameObject player,
        CharacterStats playerStats,
        PlayerStateMachine playerStateMachine,
        PlayerInputBridge playerInputBridge,
        WaveSpawner waveSpawner,
        BattleTimeController battleTimeController,
        GameOverUI gameOverUI,
        BattleSceneSetup battleHotkeyOwner,
        Func<CombatResultData> resultDataProvider)
    {
        if (_configured)
        {
            throw new InvalidOperationException("BattleRunController can only be configured once.");
        }

        _player = player != null ? player : throw new ArgumentNullException(nameof(player));
        _playerStats = playerStats != null ? playerStats : throw new ArgumentNullException(nameof(playerStats));
        _playerStateMachine = playerStateMachine != null
            ? playerStateMachine
            : throw new ArgumentNullException(nameof(playerStateMachine));
        _playerInputBridge = playerInputBridge != null
            ? playerInputBridge
            : throw new ArgumentNullException(nameof(playerInputBridge));
        _playerController = _player.GetComponent<PlayerController>();
        _playerBody = _player.GetComponent<Rigidbody2D>();
        if (_playerController == null || _playerBody == null)
        {
            throw new InvalidOperationException(
                "BattleRunController requires PlayerController and Rigidbody2D on Player.");
        }

        _waveSpawner = waveSpawner != null ? waveSpawner : throw new ArgumentNullException(nameof(waveSpawner));
        _battleTimeController = battleTimeController != null
            ? battleTimeController
            : throw new ArgumentNullException(nameof(battleTimeController));
        _gameOverUI = gameOverUI != null ? gameOverUI : throw new ArgumentNullException(nameof(gameOverUI));
        _battleHotkeyOwner = battleHotkeyOwner != null
            ? battleHotkeyOwner
            : throw new ArgumentNullException(nameof(battleHotkeyOwner));
        _resultDataProvider = resultDataProvider ?? throw new ArgumentNullException(nameof(resultDataProvider));

        _playerStats.OnDeath += HandlePlayerDeath;
        _waveSpawner.OnAllWavesComplete += HandleAllWavesComplete;
        _gameOverUI.OnRestart += Restart;
        _gameOverUI.OnBackToMenu += ReturnToMainMenu;
        _configured = true;
    }

    /// <summary>
    /// 在既有战局配置后注入场景相机所有者，保持 B1 Configure 契约不变并让终局先停止跟随。
    /// </summary>
    public void ConfigureCameraRig(BattleCameraRig rig)
    {
        if (!_configured)
        {
            throw new InvalidOperationException(
                "BattleRunController must be configured before its camera rig.");
        }

        if (_cameraRigConfigured)
        {
            throw new InvalidOperationException("BattleRunController camera rig can only be configured once.");
        }

        _cameraRig = rig != null ? rig : throw new ArgumentNullException(nameof(rig));
        _cameraRigConfigured = true;
    }

    public void ConfigureCombatFeedback(CombatFeedbackController controller)
    {
        if (!_configured)
        {
            throw new InvalidOperationException(
                "BattleRunController must be configured before combat feedback.");
        }

        if (_combatFeedbackConfigured)
        {
            throw new InvalidOperationException(
                "BattleRunController combat feedback can only be configured once.");
        }

        _combatFeedback = controller != null
            ? controller
            : throw new ArgumentNullException(nameof(controller));
        _combatFeedbackConfigured = true;
    }

    private void HandlePlayerDeath()
    {
        Complete(BattleRunOutcome.Defeat);
    }

    private void HandleAllWavesComplete()
    {
        Complete(BattleRunOutcome.Victory);
    }

    private void Complete(BattleRunOutcome outcome)
    {
        if (_disposed || !_runState.TryComplete(outcome))
        {
            return;
        }

        _waveSpawner.CancelActiveCombatActions();
        if (_combatFeedback != null)
        {
            _combatFeedback.ClearTransient();
        }
        _playerInputBridge.SetInputEnabled(false);
        _playerController.enabled = false;
        _playerBody.velocity = Vector2.zero;
        _battleHotkeyOwner.BattleHotkeysEnabled = false;
        if (_cameraRig != null)
        {
            _cameraRig.SetFollowEnabled(false);
        }
        _battleResultToken = _battleTimeController.RequestTimeScale(
            BattleTimeController.BattleResultReason,
            0f);

        if (outcome == BattleRunOutcome.Defeat)
        {
            _playerStateMachine.ForceDie();
            CombatEvents.InvokePlayerDeath();
        }

        _gameOverUI.DisplayGameOver(
            outcome == BattleRunOutcome.Victory,
            _resultDataProvider());
    }

    public void Restart()
    {
        if (_disposed || !_runState.BeginRestart())
        {
            return;
        }

        Dispose();
        SceneTransitionManager.Instance.LoadScene("BattleScene");
    }

    public void ReturnToMainMenu()
    {
        if (_disposed || !_runState.BeginRestart())
        {
            return;
        }

        Dispose();
        SceneTransitionManager.Instance.GoToMainMenu();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_combatFeedback != null)
        {
            _combatFeedback.Dispose();
        }
        if (_cameraRig != null)
        {
            _cameraRig.SetFollowEnabled(false);
        }

        if (_configured)
        {
            _playerStats.OnDeath -= HandlePlayerDeath;
            _waveSpawner.OnAllWavesComplete -= HandleAllWavesComplete;
            _gameOverUI.OnRestart -= Restart;
            _gameOverUI.OnBackToMenu -= ReturnToMainMenu;
        }

        if (_battleResultToken.IsValid)
        {
            _battleTimeController.ReleaseTimeScale(_battleResultToken);
            _battleResultToken = default;
        }

        if (_playerInputBridge != null)
        {
            _playerInputBridge.SetInputEnabled(true);
        }

        if (_playerController != null)
        {
            _playerController.enabled = true;
        }

        if (_battleHotkeyOwner != null)
        {
            _battleHotkeyOwner.BattleHotkeysEnabled = true;
        }

        if (_waveSpawner != null)
        {
            _waveSpawner.Dispose();
        }

        _runState.Dispose();
        _resultDataProvider = null;
    }

    private void OnDestroy()
    {
        Dispose();
    }
}
