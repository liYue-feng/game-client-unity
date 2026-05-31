using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 网络状态指示器 — 屏幕左上角显示连接状态 + RTT
/// </summary>
public class NetworkStatusUI : MonoBehaviour
{
    private Text _statusText;
    private Font _font;
    private NetworkStatus _lastStatus = NetworkStatus.Disconnected;

    void Start()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();

        if (HeartbeatManager.Instance != null)
        {
            HeartbeatManager.Instance.OnStatusChanged += OnStatusChanged;
            OnStatusChanged(HeartbeatManager.Instance.Status);
        }
    }

    void BuildUI()
    {
        // Canvas
        var canvasGo = new GameObject("NetworkStatusCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 背景
        var bgGo = new GameObject("StatusBG");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bg = bgGo.AddComponent<RawImage>();
        bg.texture = MakeBgTex(200, 40);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 1);
        bgRect.anchorMax = new Vector2(0, 1);
        bgRect.pivot = new Vector2(0, 1);
        bgRect.anchoredPosition = new Vector2(10, -10);
        bgRect.sizeDelta = new Vector2(200, 40);

        // 状态文字
        var textGo = new GameObject("StatusText");
        textGo.transform.SetParent(bgGo.transform, false);
        _statusText = textGo.AddComponent<Text>();
        _statusText.text = "...";
        _statusText.fontSize = 22;
        _statusText.alignment = TextAnchor.MiddleCenter;
        _statusText.font = _font;
        _statusText.rectTransform.anchorMin = Vector2.zero;
        _statusText.rectTransform.anchorMax = Vector2.one;
        _statusText.rectTransform.sizeDelta = Vector2.zero;
    }

    Texture2D MakeBgTex(int w, int h)
    {
        var tex = new Texture2D(w, h);
        var cs = new Color32[w * h];
        var bg = new Color32(26, 26, 26, 180);
        for (int i = 0; i < cs.Length; i++) cs[i] = bg;
        tex.SetPixels32(cs);
        tex.Apply();
        return tex;
    }

    void OnStatusChanged(NetworkStatus status)
    {
        _lastStatus = status;
        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        if (_statusText == null) return;

        switch (_lastStatus)
        {
            case NetworkStatus.Connected:
                _statusText.text = $"● 在线";
                _statusText.color = new Color(0.2f, 0.7f, 0.3f);
                break;
            case NetworkStatus.Unstable:
                _statusText.text = $"◐ 不稳";
                _statusText.color = new Color(0.9f, 0.6f, 0.1f);
                break;
            case NetworkStatus.Reconnecting:
                _statusText.text = "↻ 重连中...";
                _statusText.color = new Color(0.9f, 0.6f, 0.1f);
                break;
            case NetworkStatus.Disconnected:
                _statusText.text = "✕ 离线";
                _statusText.color = new Color(0.8f, 0.25f, 0.25f);
                break;
        }
    }

    void OnDestroy()
    {
        if (HeartbeatManager.Instance != null)
        {
            HeartbeatManager.Instance.OnStatusChanged -= OnStatusChanged;
        }
    }
}