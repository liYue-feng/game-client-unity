using Game.Online;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuUI : MonoBehaviour
{
    private const string MenuCanvasName = "MenuCanvas";

    private Font _font;
    private OnlineSessionHost _onlineSession;
    private Text _statusText;
    private Text _nicknameText;
    private Button _startButton;
    private Button _rankButton;
    private Button _settingsButton;
    private Button _retryButton;
    private Button _quitButton;
    private Texture2D _paperTexture;
    private Texture2D _inkWashTexture;

    private void Awake()
    {
        var menuUis = GetComponents<MainMenuUI>();
        if (menuUis.Length > 0 && menuUis[0] != this)
        {
            Destroy(this);
        }
    }

    private void Start()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUi();
        BindOnlineSession();
    }

    private void OnDestroy()
    {
        if (_onlineSession != null)
        {
            _onlineSession.StateChanged -= HandleSessionStateChanged;
            _onlineSession = null;
        }

        if (_startButton != null)
        {
            _startButton.onClick.RemoveListener(StartGame);
        }

        if (_settingsButton != null)
        {
            _settingsButton.onClick.RemoveListener(OpenSettings);
        }

        if (_rankButton != null)
        {
            _rankButton.onClick.RemoveListener(OpenRank);
        }

        if (_retryButton != null)
        {
            _retryButton.onClick.RemoveListener(RetrySession);
        }

        if (_quitButton != null)
        {
            _quitButton.onClick.RemoveListener(QuitGame);
        }

        if (_paperTexture != null)
        {
            Destroy(_paperTexture);
            _paperTexture = null;
        }

        if (_inkWashTexture != null)
        {
            Destroy(_inkWashTexture);
            _inkWashTexture = null;
        }
    }

    private void BuildUi()
    {
        if (transform.Find(MenuCanvasName) != null)
        {
            return;
        }

        EnsureEventSystem();

        var canvasObject = new GameObject(MenuCanvasName);
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        var background = CreateRawImage(canvasObject.transform, "PaperBackground");
        _paperTexture = CreatePaperTexture(192, 192);
        background.texture = _paperTexture;
        background.uvRect = new Rect(0, 0, 4, 8);
        Stretch(background.rectTransform);

        var inkWash = CreateRawImage(canvasObject.transform, "InkWash");
        _inkWashTexture = CreateInkWashTexture(256, 64);
        inkWash.texture = _inkWashTexture;
        PlaceBand(inkWash.rectTransform, 0.70f, 0.90f);

        var title = CreateText(canvasObject.transform, "Title", "剑", 92, TextAnchor.MiddleCenter);
        title.color = new Color(0.94f, 0.95f, 0.92f);
        Place(title.rectTransform, new Vector2(0.5f, 0.82f), new Vector2(680, 120));

        var subtitle = CreateText(canvasObject.transform, "Subtitle", "水墨武侠 · 行于无尽", 26, TextAnchor.MiddleCenter);
        subtitle.color = new Color(0.82f, 0.87f, 0.84f);
        Place(subtitle.rectTransform, new Vector2(0.5f, 0.75f), new Vector2(680, 48));

        _nicknameText = CreateText(canvasObject.transform, "Nickname", "游侠", 30, TextAnchor.MiddleCenter);
        Place(_nicknameText.rectTransform, new Vector2(0.5f, 0.64f), new Vector2(600, 52));

        _statusText = CreateText(canvasObject.transform, "Status", "离线游玩", 24, TextAnchor.MiddleCenter);
        _statusText.color = ShuiMoPalette.FlowerBlue;
        _statusText.resizeTextForBestFit = true;
        _statusText.resizeTextMinSize = 14;
        _statusText.resizeTextMaxSize = 24;
        Place(_statusText.rectTransform, new Vector2(0.5f, 0.60f), new Vector2(720, 52));

        _startButton = CreateButton(canvasObject.transform, "BtnStart", "开始战斗", new Vector2(0.5f, 0.46f));
        _startButton.onClick.AddListener(StartGame);

        _rankButton = CreateButton(canvasObject.transform, "BtnRank", "排行榜", new Vector2(0.5f, 0.37f));
        _rankButton.onClick.AddListener(OpenRank);

        _settingsButton = CreateButton(canvasObject.transform, "BtnSettings", "设置", new Vector2(0.5f, 0.28f));
        _settingsButton.onClick.AddListener(OpenSettings);

        _retryButton = CreateButton(canvasObject.transform, "BtnRetry", "重试连接", new Vector2(0.5f, 0.19f));
        _retryButton.onClick.AddListener(RetrySession);
        _retryButton.gameObject.SetActive(false);

        _quitButton = CreateButton(canvasObject.transform, "BtnQuit", "退出游戏", new Vector2(0.5f, 0.10f));
        _quitButton.onClick.AddListener(QuitGame);
    }

    private void BindOnlineSession()
    {
        _onlineSession = OnlineSessionHost.Instance;
        if (_onlineSession == null)
        {
            RefreshSessionDisplay(OnlineSessionState.Idle);
            return;
        }

        _onlineSession.StateChanged += HandleSessionStateChanged;
        RefreshSessionDisplay(_onlineSession.State);
    }

    private void HandleSessionStateChanged(OnlineSessionState state)
    {
        RefreshSessionDisplay(state);
    }

    private void RefreshSessionDisplay(OnlineSessionState state)
    {
        if (_nicknameText != null)
        {
            var nickname = _onlineSession?.Nickname;
            _nicknameText.text = string.IsNullOrWhiteSpace(nickname) ? "游侠" : nickname;
        }

        if (_statusText != null)
        {
            _statusText.text = _onlineSession == null ? GetApplicationStatusLabel() : GetStatusLabel(state);
        }

        if (_retryButton != null)
        {
            _retryButton.gameObject.SetActive(_onlineSession != null && state == OnlineSessionState.Failed);
        }

        if (_startButton != null)
        {
            _startButton.interactable = _onlineSession == null
                ? CanStartWithoutOnlineSession()
                : state == OnlineSessionState.Ready;
        }
    }

    private void StartGame()
    {
        SceneManager.LoadScene("BattleScene", LoadSceneMode.Single);
    }

    private void OpenSettings()
    {
        SettingsUI.Show();
    }

    private void OpenRank()
    {
        RankPanelUI.Show();
    }

    private void RetrySession()
    {
        _onlineSession?.Retry();
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("[MainMenuUI] Quit requested in Editor.");
#else
        Application.Quit();
#endif
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 anchor)
    {
        var buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);
        var rectTransform = buttonObject.AddComponent<RectTransform>();
        Place(rectTransform, anchor, new Vector2(360, 76));
        var inkButton = buttonObject.AddComponent<InkButton>();
        inkButton.buttonText = label;
        inkButton.fontSize = 32;
        return buttonObject.GetComponent<Button>();
    }

    private Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        text.font = _font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.1f, 0.1f, 0.1f);
        text.text = content;
        return text;
    }

    private static RawImage CreateRawImage(Transform parent, string name)
    {
        var imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        return imageObject.AddComponent<RawImage>();
    }

    private static void Place(RectTransform rectTransform, Vector2 anchor, Vector2 size)
    {
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = size;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void PlaceBand(RectTransform rectTransform, float anchorBottom, float anchorTop)
    {
        rectTransform.anchorMin = new Vector2(0, anchorBottom);
        rectTransform.anchorMax = new Vector2(1, anchorTop);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static Texture2D CreatePaperTexture(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var grain = Mathf.PerlinNoise(x * 0.055f, y * 0.08f);
                var fiber = Mathf.PerlinNoise(x * 0.015f, y * 0.32f);
                var tone = (byte)Mathf.Clamp(224 + grain * 18 + fiber * 8, 0, 255);
                pixels[y * width + x] = new Color32(tone, (byte)Mathf.Min(255, tone + 2), tone, 255);
            }
        }

        texture.SetPixels32(pixels);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();
        return texture;
    }

    private static Texture2D CreateInkWashTexture(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color32[width * height];
        for (var y = 0; y < height; y++)
        {
            var edge = Mathf.Sin(Mathf.PI * y / (height - 1f));
            for (var x = 0; x < width; x++)
            {
                var wash = Mathf.PerlinNoise(x * 0.035f, y * 0.09f);
                var alpha = (byte)(Mathf.Clamp01(edge * (0.66f + wash * 0.34f)) * 235);
                pixels[y * width + x] = new Color32(24, 34, 34, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply();
        return texture;
    }

    private string GetStatusLabel(OnlineSessionState state)
    {
        switch (state)
        {
            case OnlineSessionState.Connecting:
                return "连接中";
            case OnlineSessionState.Authenticating:
                return "验证身份";
            case OnlineSessionState.LoadingArchive:
                return "读取存档";
            case OnlineSessionState.Ready:
                return "已就绪";
            case OnlineSessionState.Reconnecting:
                return "重连中";
            case OnlineSessionState.Failed:
                return string.IsNullOrWhiteSpace(_onlineSession?.FailureReason)
                    ? "连接失败"
                    : $"连接失败：{_onlineSession.FailureReason}";
            case OnlineSessionState.Stopped:
                return "已停止";
            default:
                return "等待连接";
        }
    }

    private static bool CanStartWithoutOnlineSession()
    {
        var application = Game.GameApplication.Instance;
        return application == null || application.State == Game.Core.GameApplicationState.Ready;
    }

    private static string GetApplicationStatusLabel()
    {
        var application = Game.GameApplication.Instance;
        if (application == null || application.State == Game.Core.GameApplicationState.Ready)
        {
            return "离线游玩";
        }

        if (application.State == Game.Core.GameApplicationState.Failed)
        {
            return string.IsNullOrWhiteSpace(application.FailureReason)
                ? "启动失败"
                : $"启动失败：{application.FailureReason}";
        }

        return "启动中";
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        var eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }
}
