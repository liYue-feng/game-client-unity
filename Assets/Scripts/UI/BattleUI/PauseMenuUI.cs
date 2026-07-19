using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 暂停菜单：战斗中按ESC暂停，水墨风格覆层。
/// 暂停时间由场景级 BattleTimeController 统一管理。
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    private Canvas _canvas;
    private bool _isPaused;
    private BattleTimeController _battleTimeController;
    private Game.Gameplay.TimeScaleRequestToken _pauseToken;

    public event Action OnResume;
    public event Action OnBackToMenu;
    public event Action OnSettings;

    private void Awake()
    {
        BuildCanvas();
        BuildPanel();
    }

    private void BuildCanvas()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;
        gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // 阻止点击穿透
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildPanel()
    {
        // 半透明遮罩（墨色）
        var overlay = new GameObject("Overlay");
        overlay.transform.SetParent(transform, false);
        var overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0.05f, 0.05f, 0.05f, 0.7f);
        var overlayRect = overlayImg.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;

        // 中间面板（宣纸色）
        var panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.96f, 0.94f, 0.89f, 0.95f); // 宣纸白
        var panelRect = panelImg.rectTransform;
        panelRect.anchorMin = new Vector2(0.2f, 0.3f);
        panelRect.anchorMax = new Vector2(0.8f, 0.7f);
        panelRect.sizeDelta = Vector2.zero;

        // 标题"暂停"
        var title = CreateText("title", "— 暂 停 —", 48, panel.transform);
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.8f);
        titleRect.anchorMax = new Vector2(0.5f, 0.8f);
        titleRect.sizeDelta = new Vector2(300, 60);
        titleRect.anchoredPosition = Vector2.zero;

        // 继续按钮
        CreateButton("btn_resume", "继续游戏", new Vector2(0, 60), panel.transform, () =>
        {
            AudioManager.Instance.PlaySFX("ui_click");
            Resume();
        });

        // 设置按钮
        CreateButton("btn_settings", "设  置", new Vector2(0, -20), panel.transform, () =>
        {
            AudioManager.Instance.PlaySFX("ui_click");
            OnSettings?.Invoke();
        });

        // 返回主菜单按钮
        CreateButton("btn_quit", "返回主菜单", new Vector2(0, -100), panel.transform, () =>
        {
            AudioManager.Instance.PlaySFX("ui_click");
            Close(false);
            OnBackToMenu?.Invoke();
        });

        _canvas.enabled = false;
    }

    private void CreateButton(string name, string label, Vector2 pos, Transform parent, Action onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.85f, 0.82f, 0.75f);
        var rect = img.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(220, 50);
        rect.anchoredPosition = pos;

        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick());

        var txt = CreateText("label", label, 28, go.transform);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0.1f, 0.1f, 0.1f);
        var txtRect = txt.rectTransform;
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;
    }

    private Text CreateText(string name, string label, int fontSize, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.text = label;
        txt.fontSize = fontSize;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0.15f, 0.15f, 0.15f);
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return txt;
    }

    public void Pause()
    {
        _isPaused = true;
        RequestPause();
        _canvas.enabled = true;
    }

    public void Resume()
    {
        Close(true);
    }

    public void ConfigureBattleTimeController(BattleTimeController controller)
    {
        if (_battleTimeController == controller)
        {
            return;
        }

        ReleasePauseRequest();
        _battleTimeController = controller;
        if (_isPaused)
        {
            RequestPause();
        }
    }

    private void RequestPause()
    {
        if (_battleTimeController != null && !_pauseToken.IsValid)
        {
            _pauseToken = _battleTimeController.RequestTimeScale(
                BattleTimeController.PauseReason,
                0f);
        }
    }

    private void ReleasePauseRequest()
    {
        if (_pauseToken.IsValid && _battleTimeController != null)
        {
            _battleTimeController.ReleaseTimeScale(_pauseToken);
        }

        _pauseToken = default;
    }

    private void OnDisable()
    {
        Close(true);
    }

    private void OnDestroy()
    {
        Close(false);
    }

    private void Close(bool notifyResume)
    {
        var wasPaused = _isPaused;
        _isPaused = false;
        ReleasePauseRequest();
        if (_canvas != null)
        {
            _canvas.enabled = false;
        }

        if (notifyResume && wasPaused)
        {
            OnResume?.Invoke();
        }
    }

    public void Show() => Pause();
    public void Hide() => Resume();
}
