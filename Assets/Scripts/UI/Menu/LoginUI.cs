using UnityEngine;
using UnityEngine.UI;
using Game.Managers;

/// <summary>
/// 水墨风格登录界面 — 登录/注册/游客三种模式
/// </summary>
public class LoginUI : MonoBehaviour
{
    [Header("引用")]
    public LoginManager loginManager;

    private enum Mode { Login, Register, Guest }
    private Mode _currentMode = Mode.Login;

    private GameObject _root;
    private InkInputField _usernameInput;
    private InkInputField _passwordInput;
    private Text _titleText;
    private Text _errorText;
    private Font _font;

    void Start()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
    }

    void BuildUI()
    {
        // Canvas
        var canvasGo = new GameObject("LoginCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 根面板
        _root = CreatePanel(canvasGo.transform, "Root", 800, 900);
        _root.transform.localPosition = Vector3.zero;

        // 标题
        _titleText = CreateText(_root.transform, "Title", "剑 · 登录", 60, TextAnchor.MiddleCenter);
        _titleText.color = new Color(0.08f, 0.08f, 0.08f);
        var titleRect = _titleText.rectTransform;
        titleRect.anchoredPosition = new Vector2(0, 340);
        titleRect.sizeDelta = new Vector2(700, 80);

        // 用户名
        var userLabel = CreateText(_root.transform, "UserLabel", "账号", 32, TextAnchor.MiddleLeft);
        userLabel.rectTransform.anchoredPosition = new Vector2(-250, 200);
        userLabel.rectTransform.sizeDelta = new Vector2(100, 50);

        _usernameInput = CreateInputField(_root.transform, "UserInput");
        _usernameInput.transform.localPosition = new Vector3(100, 180, 0);

        // 密码
        var passLabel = CreateText(_root.transform, "PassLabel", "密码", 32, TextAnchor.MiddleLeft);
        passLabel.rectTransform.anchoredPosition = new Vector2(-250, 100);
        passLabel.rectTransform.sizeDelta = new Vector2(100, 50);

        _passwordInput = CreateInputField(_root.transform, "PassInput");
        _passwordInput.contentType = InputField.ContentType.Password;
        _passwordInput.transform.localPosition = new Vector3(100, 80, 0);

        // 错误信息
        _errorText = CreateText(_root.transform, "Error", "", 24, TextAnchor.MiddleCenter);
        _errorText.color = new Color(0.75f, 0.2f, 0.2f);
        _errorText.rectTransform.anchoredPosition = new Vector2(0, 10);
        _errorText.rectTransform.sizeDelta = new Vector2(600, 40);

        // 模式切换按钮
        float btnY = -60;
        CreateButton(_root.transform, "BtnLogin", "登录", () => OnSubmit(), new Vector2(0, btnY));

        var tabY = -150;
        CreateSmallButton(_root.transform, "TabLogin", "账号登录", () => SwitchMode(Mode.Login), new Vector2(-180, tabY));
        CreateSmallButton(_root.transform, "TabRegister", "注册", () => SwitchMode(Mode.Register), new Vector2(0, tabY));
        CreateSmallButton(_root.transform, "TabGuest", "游客", () => SwitchMode(Mode.Guest), new Vector2(180, tabY));

        UpdateModeUI();
    }

    void SwitchMode(Mode mode)
    {
        _currentMode = mode;
        _errorText.text = "";
        UpdateModeUI();
    }

    void UpdateModeUI()
    {
        switch (_currentMode)
        {
            case Mode.Login:
                _titleText.text = "剑 · 登录";
                break;
            case Mode.Register:
                _titleText.text = "剑 · 注册";
                break;
            case Mode.Guest:
                _titleText.text = "剑 · 游客";
                _usernameInput.gameObject.SetActive(false);
                _passwordInput.gameObject.SetActive(false);
                break;
        }
        _usernameInput.gameObject.SetActive(_currentMode != Mode.Guest);
        _passwordInput.gameObject.SetActive(_currentMode != Mode.Guest);
    }

    void OnSubmit()
    {
        switch (_currentMode)
        {
            case Mode.Login:
            case Mode.Register:
            case Mode.Guest:
                Debug.Log("[LoginUI] 尝试登录...");
                if (loginManager != null)
                    loginManager.WechatLogin();
                break;
        }
    }

    public void ShowError(string msg)
    {
        if (_errorText != null) _errorText.text = msg;
    }

    #region UI工具方法

    GameObject CreatePanel(Transform parent, string name, int w, int h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        go.AddComponent<InkPanel>();
        return go;
    }

    Text CreateText(Transform parent, string name, string content, int size, TextAnchor align)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        var txt = go.AddComponent<Text>();
        txt.text = content;
        txt.fontSize = size;
        txt.alignment = align;
        txt.color = new Color(0.1f, 0.1f, 0.1f);
        txt.font = _font;
        return txt;
    }

    InkInputField CreateInputField(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<InkInputField>();
    }

    void CreateButton(Transform parent, string name, string text, System.Action onClick, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(300, 70);

        var inkBtn = go.AddComponent<InkButton>();
        inkBtn.buttonText = text;
        inkBtn.fontSize = 32;

        go.GetComponent<Button>().onClick.AddListener(() => onClick());
    }

    void CreateSmallButton(Transform parent, string name, string text, System.Action onClick, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(160, 50);

        var inkBtn = go.AddComponent<InkButton>();
        inkBtn.buttonText = text;
        inkBtn.fontSize = 22;

        go.GetComponent<Button>().onClick.AddListener(() => onClick());
    }

    #endregion
}