using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗HUD总控：水墨风格，动态创建所有战斗UI组件。
/// </summary>
public class BattleHUD : MonoBehaviour
{
    private static BattleHUD _instance;
    public static BattleHUD Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[BattleHUD]");
                _instance = go.AddComponent<BattleHUD>();
            }
            return _instance;
        }
    }

    private Canvas _canvas;
    private PlayerHPBar _playerHPBar;
    private StaminaBar _staminaBar;
    private ComboCounter _comboCounter;
    private ExpBar _expBar;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // 创建 Canvas
        _canvas = GetComponent<Canvas>();
        if (_canvas == null)
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
    }

    /// <summary>
    /// 创建水墨风格的UI面板背景（带笔触边框）
    /// </summary>
    private Image CreateInkPanel(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.sprite = PlaceholderSpriteFactory.CreateInkPanelSprite((int)size.x, (int)size.y);
        img.type = Image.Type.Sliced;
        img.color = ShuiMoPalette.RicePaper;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        return img;
    }

    /// <summary>
    /// 创建水墨风格的滑动条
    /// </summary>
    private (Slider slider, Image fillImg) CreateInkSlider(Transform parent, string name, Vector2 size, Vector2 position, Color fillColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var slider = go.AddComponent<Slider>();
        slider.interactable = false;

        // 背景
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(go.transform, false);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = PlaceholderSpriteFactory.CreateRoughRectSprite((int)size.x, (int)size.y);
        bgImg.color = ShuiMoPalette.InkLight;
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 填充区域
        var fillAreaGo = new GameObject("Fill Area");
        fillAreaGo.transform.SetParent(go.transform, false);
        var fillAreaRect = fillAreaGo.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1, 0.75f);
        fillAreaRect.sizeDelta = new Vector2(-20, 0);

        // 填充
        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = PlaceholderSpriteFactory.CreateInkStrokeSprite((int)(size.x - 20), (int)(size.y * 0.5f));
        fillImg.color = fillColor;
        var fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0, 1);
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.sizeDelta = new Vector2(0, 0);

        slider.fillRect = fillRect;

        return (slider, fillImg);
    }

    /// <summary>
    /// 为玩家初始化HUD
    /// </summary>
    public void InitializeForPlayer(CharacterStats playerStats)
    {
        // 左上角：HP+耐力面板
        var panel = CreateInkPanel(_canvas.transform, "StatusPanel", new Vector2(320, 140), new Vector2(180, -100));

        // 血条
        var (hpSlider, hpFillImg) = CreateInkSlider(panel.transform, "HPBar", new Vector2(280, 36), new Vector2(0, 20), ShuiMoPalette.CinnabarRed);
        var hpBarGo = hpSlider.gameObject;
        var hpBar = hpBarGo.AddComponent<PlayerHPBar>();
        hpBar.hpSlider = hpSlider;
        hpBar.fillImage = hpFillImg;
        hpBar.Initialize(playerStats);
        _playerHPBar = hpBar;

        // 耐力条
        var (staminaSlider, staminaFillImg) = CreateInkSlider(panel.transform, "StaminaBar", new Vector2(280, 30), new Vector2(0, -25), ShuiMoPalette.FlowerBlue);
        var staminaBarGo = staminaSlider.gameObject;
        var staminaBar = staminaBarGo.AddComponent<StaminaBar>();
        staminaBar.staminaSlider = staminaSlider;
        staminaBar.fillImage = staminaFillImg;
        staminaBar.Initialize(playerStats);
        _staminaBar = staminaBar;

        // HP标签
        CreateLabel(panel.transform, "HP", "HP", new Vector2(-120, 20), ShuiMoPalette.InkBlack);
        // 耐力标签
        CreateLabel(panel.transform, "StaminaLabel", "气", new Vector2(-120, -25), ShuiMoPalette.InkBlack);

        // 底部：经验条
        CreateExpBar(playerStats);

        // 右上角：连击计数器
        CreateComboCounter();
    }

    /// <summary>
    /// 创建水墨风格连击计数器
    /// </summary>
    private void CreateComboCounter()
    {
        var go = new GameObject("ComboCounter");
        go.transform.SetParent(_canvas.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-100, -100);
        rect.sizeDelta = new Vector2(300, 100);

        var txt = go.AddComponent<Text>();
        txt.text = "";
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 36;
        txt.fontStyle = FontStyle.Bold;
        txt.color = ShuiMoPalette.InkBlack;
        txt.alignment = TextAnchor.UpperRight;

        var counter = go.AddComponent<ComboCounter>();
        counter.comboText = txt;
        _comboCounter = counter;
    }

    /// <summary>
    /// 创建水墨风格经验条
    /// </summary>
    private void CreateExpBar(CharacterStats stats)
    {
        // 底部面板
        var panel = CreateInkPanel(_canvas.transform, "ExpPanel", new Vector2(500, 80), new Vector2(0, -50));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0);
        panelRect.anchorMax = new Vector2(0.5f, 0);
        panelRect.pivot = new Vector2(0.5f, 0);

        // 经验条滑块
        var (expSlider, expFillImg) = CreateInkSlider(panel.transform, "ExpBar", new Vector2(420, 30), new Vector2(20, 0), ShuiMoPalette.Gamboge);

        // 等级文字
        var levelText = CreateLabel(panel.transform, "LevelText", $"Lv.{stats.level}", new Vector2(-200, 0), ShuiMoPalette.InkBlack);
        var levelRect = levelText.GetComponent<RectTransform>();
        levelRect.anchorMin = Vector2.zero;
        levelRect.anchorMax = Vector2.zero;
        levelRect.pivot = new Vector2(0, 0.5f);

        // ExpBar组件
        var expBarGo = expSlider.gameObject;
        var expBar = expBarGo.AddComponent<ExpBar>();
        expBar.expSlider = expSlider;
        expBar.fillImage = expFillImg;
        expBar.levelText = levelText;
        expBar.Initialize(stats);
        _expBar = expBar;
    }

    /// <summary>
    /// 创建水墨风格文本标签
    /// </summary>
    private Text CreateLabel(Transform parent, string name, string text, Vector2 position, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100, 40);
        rect.anchoredPosition = position;

        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 24;
        txt.fontStyle = FontStyle.Bold;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;

        return txt;
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
}
