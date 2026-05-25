using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 水墨风格设置界面 — 音量 / 画质 / 操作说明
/// 通过 SettingsUI.Show() 静态调用
/// </summary>
public class SettingsUI : MonoBehaviour
{
    private Font _font;
    private GameObject _root;
    private Text _masterVolumeLabel;
    private Text _sfxVolumeLabel;
    private Text _qualityLabel;
    private int _currentQuality;

    private float _masterVolume = 0.8f;
    private float _sfxVolume = 0.8f;
    private string[] _qualityNames = { "低", "中", "高" };

    void Awake()
    {
        // 加载存档设置
        _masterVolume = PlayerPrefs.GetFloat("audio_master", 0.8f);
        _sfxVolume = PlayerPrefs.GetFloat("audio_sfx", 0.8f);
        _currentQuality = PlayerPrefs.GetInt("graphics_quality", QualitySettings.GetQualityLevel());
    }

    /// <summary>关闭回调</summary>
    public event Action OnClose;

    /// <summary> 显示设置界面（模态） </summary>
    public static SettingsUI Show()
    {
        var go = new GameObject("SettingsUI_Runtime");
        var ui = go.AddComponent<SettingsUI>();
        ui.BuildUI();
        return ui;
    }

    /// <summary> 关闭设置界面 </summary>
    public void Close()
    {
        OnClose?.Invoke();
        Destroy(gameObject);
    }

    void BuildUI()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("SettingsCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasGo.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 暗色遮罩
        var maskGo = new GameObject("Mask");
        maskGo.transform.SetParent(canvasGo.transform, false);
        var mask = maskGo.AddComponent<RawImage>();
        mask.texture = MakeOverlayTex(4, 4);
        mask.color = new Color(0, 0, 0, 0.5f);
        var maskRect = maskGo.GetComponent<RectTransform>();
        maskRect.anchorMin = Vector2.zero;
        maskRect.anchorMax = Vector2.one;
        maskRect.sizeDelta = Vector2.zero;

        // 设置面板
        _root = CreatePanel(canvasGo.transform, "SettingsPanel", 700, 700);
        _root.transform.localPosition = Vector3.zero;

        // 标题
        var title = CreateText(_root.transform, "Title", "设  置", 56, TextAnchor.MiddleCenter);
        title.color = new Color(0.08f, 0.08f, 0.08f);
        title.rectTransform.anchoredPosition = new Vector2(0, 260);
        title.rectTransform.sizeDelta = new Vector2(400, 80);

        // === 音量 ===
        var audioTitle = CreateText(_root.transform, "AudioLabel", "— 音量 —", 32, TextAnchor.MiddleCenter);
        audioTitle.color = new Color(0.3f, 0.3f, 0.3f);
        audioTitle.rectTransform.anchoredPosition = new Vector2(0, 180);
        audioTitle.rectTransform.sizeDelta = new Vector2(200, 50);

        _masterVolumeLabel = CreateVolumeRow(_root.transform, "主音量", _masterVolume, new Vector2(0, 120),
            (v) => { _masterVolume = v; PlayerPrefs.SetFloat("audio_master", v); AudioManager.Instance.SetMasterVolume(v); });

        _sfxVolumeLabel = CreateVolumeRow(_root.transform, "音效", _sfxVolume, new Vector2(0, 70),
            (v) => { _sfxVolume = v; PlayerPrefs.SetFloat("audio_sfx", v); AudioManager.Instance.SetSfxVolume(v); });

        // === 画质 ===
        var gfxTitle = CreateText(_root.transform, "GfxLabel", "— 画质 —", 32, TextAnchor.MiddleCenter);
        gfxTitle.color = new Color(0.3f, 0.3f, 0.3f);
        gfxTitle.rectTransform.anchoredPosition = new Vector2(0, 10);
        gfxTitle.rectTransform.sizeDelta = new Vector2(200, 50);

        CreatePresetButtons(_root.transform, "QualityBtns", _qualityNames, _currentQuality,
            (idx) =>
            {
                _currentQuality = idx;
                QualitySettings.SetQualityLevel(idx);
                PlayerPrefs.SetInt("graphics_quality", idx);
                if (_qualityLabel != null)
                    _qualityLabel.text = $"画质: {_qualityNames[idx]}";
            }, new Vector2(0, -40));

        // === 操作说明 ===
        var ctrlTitle = CreateText(_root.transform, "CtrlLabel", "— 操作说明 —", 32, TextAnchor.MiddleCenter);
        ctrlTitle.color = new Color(0.3f, 0.3f, 0.3f);
        ctrlTitle.rectTransform.anchoredPosition = new Vector2(0, -100);
        ctrlTitle.rectTransform.sizeDelta = new Vector2(200, 50);

        var guideText = CreateText(_root.transform, "Guide", "移动: WASD / 摇杆\n攻击: 鼠标左键 / J\n闪避: 右键 / K\n弹反: 空格（时机判定）\n暂停: ESC", 22, TextAnchor.MiddleLeft);
        guideText.color = new Color(0.25f, 0.25f, 0.25f);
        guideText.rectTransform.anchoredPosition = new Vector2(-100, -200);
        guideText.rectTransform.sizeDelta = new Vector2(400, 150);

        // 关闭按钮
        CreateInkButton(_root.transform, "BtnClose", "关闭", Close, new Vector2(0, -280), 200, 50);
    }

    Text CreateVolumeRow(Transform parent, string label, float value, Vector2 pos, Action<float> onChange)
    {
        // 标签
        var labelText = CreateText(parent, $"VolLabel_{label}", label, 28, TextAnchor.MiddleLeft);
        labelText.rectTransform.anchoredPosition = pos + new Vector2(-200, 0);
        labelText.rectTransform.sizeDelta = new Vector2(100, 40);

        // 减号按钮
        CreateSmallBtn(parent, "−", () =>
        {
            var v = Mathf.Max(0, value - 0.1f);
            onChange(v);
            var t = parent.Find($"VolLabel_{label}");
            if (t != null) { /* 更新显示 */ }
        }, pos + new Vector2(-80, 0));

        // 值显示
        var valText = CreateText(parent, $"VolVal_{label}", $"{Mathf.Round(value * 100)}%", 24, TextAnchor.MiddleCenter);
        valText.rectTransform.anchoredPosition = pos;
        valText.rectTransform.sizeDelta = new Vector2(80, 40);

        // 加号按钮
        CreateSmallBtn(parent, "+", () =>
        {
            var v = Mathf.Min(1, value + 0.1f);
            onChange(v);
        }, pos + new Vector2(80, 0));

        return valText;
    }

    void CreatePresetButtons(Transform parent, string name, string[] presets, int current,
        Action<int> onSelect, Vector2 pos)
    {
        var groupGo = new GameObject(name);
        groupGo.transform.SetParent(parent, false);
        var groupRt = groupGo.AddComponent<RectTransform>();
        groupRt.anchoredPosition = pos;
        groupRt.sizeDelta = new Vector2(400, 50);

        var spacing = 280 / presets.Length;
        for (int i = 0; i < presets.Length; i++)
        {
            var idx = i;
            CreateSmallBtn(groupGo.transform, presets[i], () => onSelect(idx),
                new Vector2((i - (presets.Length - 1) / 2f) * spacing, 0));
        }
    }

    void CreateSmallBtn(Transform parent, string text, Action onClick, Vector2 pos)
    {
        var go = new GameObject($"Btn_{text}");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(70, 40);
        var ib = go.AddComponent<InkButton>();
        ib.buttonText = text; ib.fontSize = 22;
        go.GetComponent<Button>().onClick.AddListener(() => onClick());
    }

    Texture2D MakeOverlayTex(int w, int h)
    {
        var tex = new Texture2D(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, Color.black);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    #region 工具方法

    GameObject CreatePanel(Transform p, string n, int w, int h)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(w, h);
        go.AddComponent<InkPanel>();
        return go;
    }

    Text CreateText(Transform p, string n, string c, int s, TextAnchor a)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        go.AddComponent<RectTransform>();
        var t = go.AddComponent<Text>();
        t.text = c; t.fontSize = s; t.alignment = a; t.font = _font;
        return t;
    }

    void CreateInkButton(Transform p, string n, string t, Action cb, Vector2 pos, int w, int h)
    {
        var go = new GameObject(n); go.transform.SetParent(p, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(w, h);
        var ib = go.AddComponent<InkButton>();
        ib.buttonText = t; ib.fontSize = 28;
        go.GetComponent<Button>().onClick.AddListener(() => cb());
    }

    #endregion
}