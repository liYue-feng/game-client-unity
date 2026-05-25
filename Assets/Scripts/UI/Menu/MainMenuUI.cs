using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 水墨风格主菜单 — 开始游戏 / 排行榜 / 设置
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    private Font _font;

    void Start()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
    }

    void BuildUI()
    {
        // Canvas
        var canvasGo = new GameObject("MenuCanvas");
        canvasGo.transform.SetParent(transform);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 宣纸背景
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bg = bgGo.AddComponent<RawImage>();
        bg.texture = CreatePaperTexture(1080, 1920);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 标题面板
        var titlePanel = CreatePanel(canvasGo.transform, "TitlePanel", 600, 250);
        titlePanel.transform.localPosition = new Vector3(0, 500, 0);

        // 游戏标题
        var titleText = CreateText(titlePanel.transform, "Title", "剑", 120, TextAnchor.MiddleCenter);
        titleText.color = new Color(0.08f, 0.08f, 0.08f);
        titleText.rectTransform.anchoredPosition = new Vector2(0, 50);
        titleText.rectTransform.sizeDelta = new Vector2(500, 140);

        var subTitle = CreateText(titlePanel.transform, "Subtitle", "— 水墨武侠 Roguelite —", 28, TextAnchor.MiddleCenter);
        subTitle.color = new Color(0.35f, 0.3f, 0.25f);
        subTitle.rectTransform.anchoredPosition = new Vector2(0, -40);
        subTitle.rectTransform.sizeDelta = new Vector2(500, 50);

        // 按钮面板
        var btnPanel = CreatePanel(canvasGo.transform, "BtnPanel", 500, 500);
        btnPanel.transform.localPosition = new Vector3(0, -200, 0);

        // 按钮
        CreateMenuButton(btnPanel.transform, "BtnStart", "开始游戏", 48, () =>
        {
            Debug.Log("[MainMenu] 开始游戏");
            SceneTransitionManager.Instance?.LoadScene("LobbyScene");
        }, new Vector2(0, 160));

        CreateMenuButton(btnPanel.transform, "BtnRank", "排行榜", 48, () =>
        {
            Debug.Log("[MainMenu] 排行榜");
            // TODO: 打开排行榜界面
        }, new Vector2(0, 40));

        CreateMenuButton(btnPanel.transform, "BtnSettings", "设置", 48, () =>
        {
            Debug.Log("[MainMenu] 设置");
            SettingsUI.Show();
        }, new Vector2(0, -80));

        CreateMenuButton(btnPanel.transform, "BtnQuit", "退出", 36, () =>
        {
            Debug.Log("[MainMenu] 退出");
            Application.Quit();
        }, new Vector2(0, -200));

        // 版本号
        var versionText = CreateText(canvasGo.transform, "Version", "v0.1.0 alpha", 20, TextAnchor.MiddleCenter);
        versionText.color = new Color(0.5f, 0.45f, 0.4f);
        versionText.rectTransform.anchoredPosition = new Vector2(0, -860);
        versionText.rectTransform.sizeDelta = new Vector2(300, 40);
    }

    Texture2D CreatePaperTexture(int w, int h)
    {
        var tex = new Texture2D(w, h);
        var colors = new Color32[w * h];
        var paper = new Color32(245, 240, 232, 255);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var idx = y * w + x;
                var noise = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                var grain = (byte)(noise * 25);
                colors[idx] = new Color32((byte)(paper.r - grain), (byte)(paper.g - grain), (byte)(paper.b - grain), 255);
            }
        }
        tex.SetPixels32(colors);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
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
        go.AddComponent<RectTransform>();
        var txt = go.AddComponent<Text>();
        txt.text = content;
        txt.fontSize = size;
        txt.alignment = align;
        txt.font = _font;
        return txt;
    }

    void CreateMenuButton(Transform parent, string name, string text, int fontSize, UnityEngine.Events.UnityAction onClick, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(360, 80);

        var inkBtn = go.AddComponent<InkButton>();
        inkBtn.buttonText = text;
        inkBtn.fontSize = fontSize;

        go.GetComponent<Button>().onClick.AddListener(onClick);
    }

    #endregion
}