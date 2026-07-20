using System;
using UnityEngine;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    public event Action OnRestart;
    public event Action OnBackToMenu;

    private GameObject _overlay;
    private Text _resultText;
    private Font _font;
    private GameObject _canvasRoot;
    private Texture2D _overlayTexture;

    private void Awake()
    {
        if (Instance != null && !ReferenceEquals(Instance, this))
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public static void Show(bool victory, CombatResultData data = null)
    {
        if (Instance == null)
        {
            Debug.LogError("[GameOverUI] Scene-owned instance is not installed.");
            return;
        }

        Instance.DisplayGameOver(victory, data);
    }

    public void DisplayGameOver(bool victory, CombatResultData data = null)
    {
        if (_canvasRoot == null)
        {
            Build(data);
        }

        _resultText.text = victory ? "\u80dc \u5229" : "\u843d \u8d25";
        _resultText.color = victory
            ? new Color(0.65f, 0.15f, 0.15f)
            : new Color(0.1f, 0.1f, 0.1f);
        _canvasRoot.SetActive(true);
    }

    private void Build(CombatResultData data)
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _canvasRoot = new GameObject("OverlayCanvas");
        _canvasRoot.transform.SetParent(transform, false);
        var canvas = _canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        _canvasRoot.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        _canvasRoot.AddComponent<GraphicRaycaster>();

        _overlay = new GameObject("DarkOverlay");
        _overlay.transform.SetParent(_canvasRoot.transform, false);
        var overlayImage = _overlay.AddComponent<RawImage>();
        _overlayTexture = MakeOverlayTexture(4, 4);
        overlayImage.texture = _overlayTexture;
        overlayImage.color = new Color(0f, 0f, 0f, 0.7f);
        var overlayRect = _overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;

        var panel = CreatePanel(_canvasRoot.transform, "ResultPanel", 600, 500);
        panel.transform.localPosition = Vector3.zero;

        _resultText = CreateText(panel.transform, "Title", string.Empty, 72, TextAnchor.MiddleCenter);
        _resultText.rectTransform.anchoredPosition = new Vector2(0, 160);
        _resultText.rectTransform.sizeDelta = new Vector2(400, 100);

        if (data != null)
        {
            const int statsY = 80;
            CreateStatLine(panel.transform, "\u51fb\u6740\u6570", data.killCount.ToString(), new Vector2(0, statsY));
            CreateStatLine(panel.transform, "\u83b7\u5f97\u7ecf\u9a8c", data.expGained.ToString(), new Vector2(0, statsY - 45));
            CreateStatLine(panel.transform, "\u6700\u5927\u8fde\u51fb", data.maxCombo.ToString(), new Vector2(0, statsY - 90));
            CreateStatLine(
                panel.transform,
                "\u5b58\u6d3b\u65f6\u95f4",
                $"{data.survivalTime / 60:D2}:{data.survivalTime % 60:D2}",
                new Vector2(0, statsY - 135));
        }

        CreateInkButton(
            panel.transform,
            "BtnRestart",
            "\u518d\u6765\u4e00\u5c40",
            () => OnRestart?.Invoke(),
            new Vector2(0, -125));
        CreateInkButton(
            panel.transform,
            "BtnMainMenu",
            "\u8fd4\u56de\u4e3b\u83dc\u5355",
            () => OnBackToMenu?.Invoke(),
            new Vector2(0, -205));
    }

    private Texture2D MakeOverlayTexture(int width, int height)
    {
        var texture = new Texture2D(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, Color.black);
            }
        }

        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();
        return texture;
    }

    private void CreateStatLine(Transform parent, string label, string value, Vector2 position)
    {
        var statObject = new GameObject($"Stat_{label}");
        statObject.transform.SetParent(parent, false);
        var statRect = statObject.AddComponent<RectTransform>();
        statRect.anchoredPosition = position;
        statRect.sizeDelta = new Vector2(400, 40);

        var text = CreateText(
            statObject.transform,
            "T",
            $"{label}: {value}",
            28,
            TextAnchor.MiddleCenter);
        text.color = new Color(0.15f, 0.15f, 0.15f);
        text.rectTransform.anchoredPosition = Vector2.zero;
        text.rectTransform.sizeDelta = new Vector2(400, 40);
    }

    private GameObject CreatePanel(Transform parent, string objectName, int width, int height)
    {
        var panel = new GameObject(objectName);
        panel.transform.SetParent(parent, false);
        var rect = panel.AddComponent<RectTransform>();
        var inkPanel = panel.AddComponent<InkPanel>();
        inkPanel.panelWidth = width;
        inkPanel.panelHeight = height;
        rect.sizeDelta = new Vector2(width, height);
        return panel;
    }

    private Text CreateText(
        Transform parent,
        string objectName,
        string content,
        int fontSize,
        TextAnchor alignment)
    {
        var textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        textObject.AddComponent<RectTransform>();
        var text = textObject.AddComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.font = _font;
        return text;
    }

    private void CreateInkButton(
        Transform parent,
        string objectName,
        string label,
        Action callback,
        Vector2 position)
    {
        var buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);
        var rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(240, 60);
        var background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.96f, 0.94f, 0.91f, 1f);
        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(() => callback());

        var labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);
        var labelText = labelObject.AddComponent<Text>();
        labelText.text = label;
        labelText.font = _font;
        labelText.fontSize = 28;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        labelText.raycastTarget = false;
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        OnRestart = null;
        OnBackToMenu = null;
        if (_overlayTexture != null)
        {
            Destroy(_overlayTexture);
            _overlayTexture = null;
        }

        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }
}

public class CombatResultData
{
    public int killCount;
    public int expGained;
    public int maxCombo;
    public int survivalTime;
    public int playerLevel;
    public int bossKills;
    public int elementalUpgradeCount;
    public int summonUpgradeCount;
    public int styleSwitchCount;
    public int[] obtainedUpgradeIds;
}
