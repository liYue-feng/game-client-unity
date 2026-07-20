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
    private Button _settingsButton;
    private Button _retryButton;

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

        if (_retryButton != null)
        {
            _retryButton.onClick.RemoveListener(RetrySession);
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

        var background = CreateImage(canvasObject.transform, "Background", new Color(0.94f, 0.92f, 0.87f));
        Stretch(background.rectTransform);

        var title = CreateText(canvasObject.transform, "Title", "Main Menu", 64, TextAnchor.MiddleCenter);
        Place(title.rectTransform, new Vector2(0.5f, 0.76f), new Vector2(680, 100));

        _nicknameText = CreateText(canvasObject.transform, "Nickname", "Guest", 30, TextAnchor.MiddleCenter);
        Place(_nicknameText.rectTransform, new Vector2(0.5f, 0.67f), new Vector2(600, 52));

        _statusText = CreateText(canvasObject.transform, "Status", "Offline", 24, TextAnchor.MiddleCenter);
        _statusText.color = new Color(0.25f, 0.25f, 0.25f);
        Place(_statusText.rectTransform, new Vector2(0.5f, 0.63f), new Vector2(600, 44));

        _startButton = CreateButton(canvasObject.transform, "BtnStart", "Start", new Vector2(0.5f, 0.49f));
        _startButton.onClick.AddListener(StartGame);

        _settingsButton = CreateButton(canvasObject.transform, "BtnSettings", "Settings", new Vector2(0.5f, 0.40f));
        _settingsButton.onClick.AddListener(OpenSettings);

        _retryButton = CreateButton(canvasObject.transform, "BtnRetry", "Retry", new Vector2(0.5f, 0.31f));
        _retryButton.onClick.AddListener(RetrySession);
        _retryButton.gameObject.SetActive(false);
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
            _nicknameText.text = string.IsNullOrWhiteSpace(nickname) ? "Guest" : nickname;
        }

        if (_statusText != null)
        {
            _statusText.text = _onlineSession == null ? "Offline" : state.ToString();
        }

        if (_retryButton != null)
        {
            _retryButton.gameObject.SetActive(_onlineSession != null && state == OnlineSessionState.Failed);
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

    private void RetrySession()
    {
        _onlineSession?.Retry();
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

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        var imageObject = new GameObject(name);
        imageObject.transform.SetParent(parent, false);
        var image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
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
