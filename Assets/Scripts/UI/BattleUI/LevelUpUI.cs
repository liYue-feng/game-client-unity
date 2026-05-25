using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 升级选择界面：水墨风格，3选1升级选项。
/// 升级时弹出，选择后消失并应用加成。
/// </summary>
public class LevelUpUI : MonoBehaviour
{
    [Tooltip("根面板")]
    public GameObject panel;
    [Tooltip("升级选项容器")]
    public Transform optionContainer;
    [Tooltip("选项预制体（可选，运行时创建）")]
    public GameObject optionPrefab;

    /// <summary>升级选择事件，参数为选择的ItemData</summary>
    public event System.Action<ItemData> OnUpgradeSelected;

    private List<ItemData> _currentOptions = new List<ItemData>();
    private List<GameObject> _optionObjects = new List<GameObject>();
    private CharacterStats _playerStats;
    private bool _isOpen;

    /// <summary>是否打开中</summary>
    public bool IsOpen => _isOpen;

    private void Awake()
    {
        // 创建基础UI结构（如果没有通过Inspector设置）
        CreateUI();
    }

    /// <summary>
    /// 初始化升级UI
    /// </summary>
    public void Initialize(CharacterStats playerStats)
    {
        _playerStats = playerStats;
    }

    /// <summary>
    /// 显示升级选择界面
    /// </summary>
    public void Show(List<ItemData> options)
    {
        if (_isOpen) return;

        _isOpen = true;
        _currentOptions = options;

        if (panel != null)
        {
            panel.SetActive(true);
        }

        // 暂停游戏
        Time.timeScale = 0f;

        // 创建选项
        RefreshOptions();
    }

    /// <summary>
    /// 隐藏升级界面
    /// </summary>
    public void Hide()
    {
        _isOpen = false;

        if (panel != null)
        {
            panel.SetActive(false);
        }

        // 恢复游戏
        Time.timeScale = 1f;

        // 清除选项
        ClearOptions();
    }

    /// <summary>
    /// 刷新选项显示
    /// </summary>
    private void RefreshOptions()
    {
        ClearOptions();

        for (int i = 0; i < _currentOptions.Count; i++)
        {
            var option = _currentOptions[i];
            var optionObj = CreateOptionButton(option, i);
            _optionObjects.Add(optionObj);
        }
    }

    /// <summary>
    /// 清除所有选项
    /// </summary>
    private void ClearOptions()
    {
        foreach (var obj in _optionObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        _optionObjects.Clear();
    }

    /// <summary>
    /// 创建水墨风格选项按钮
    /// </summary>
    private GameObject CreateOptionButton(ItemData item, int index)
    {
        var go = new GameObject($"Option_{index}");
        go.transform.SetParent(optionContainer, false);

        // 水墨面板背景
        var bgImg = go.AddComponent<Image>();
        bgImg.sprite = PlaceholderSpriteFactory.CreateInkPanelSprite(280, 200);
        bgImg.type = Image.Type.Sliced;
        bgImg.color = ShuiMoPalette.RicePaper;

        // 按钮组件
        var button = go.AddComponent<Button>();
        button.onClick.AddListener(() => OnOptionSelected(item));

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280, 200);

        // 物品名称
        var nameObj = new GameObject("Name");
        nameObj.transform.SetParent(go.transform, false);
        var nameText = nameObj.AddComponent<Text>();
        nameText.text = item.itemName;
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 28;
        nameText.fontStyle = FontStyle.Bold;
        nameText.color = item.GetColor();
        nameText.alignment = TextAnchor.MiddleCenter;
        var nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.5f, 1f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 1f);
        nameRect.anchoredPosition = new Vector2(0, -30);
        nameRect.sizeDelta = new Vector2(260, 40);

        // 物品描述
        var descObj = new GameObject("Description");
        descObj.transform.SetParent(go.transform, false);
        var descText = descObj.AddComponent<Text>();
        descText.text = item.description;
        descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        descText.fontSize = 18;
        descText.color = ShuiMoPalette.InkBlack;
        descText.alignment = TextAnchor.MiddleCenter;
        descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descText.verticalOverflow = VerticalWrapMode.Overflow;
        var descRect = descObj.GetComponent<RectTransform>();
        descRect.anchorMin = Vector2.zero;
        descRect.anchorMax = Vector2.one;
        descRect.offsetMin = new Vector2(20, 70);
        descRect.offsetMax = new Vector2(-20, -20);

        return go;
    }

    /// <summary>
    /// 选项被选择
    /// </summary>
    private void OnOptionSelected(ItemData item)
    {
        // 应用加成
        ApplyUpgrade(item);

        // 触发事件
        OnUpgradeSelected?.Invoke(item);

        // 关闭界面
        Hide();
    }

    /// <summary>
    /// 应用升级加成
    /// </summary>
    private void ApplyUpgrade(ItemData item)
    {
        if (_playerStats == null) return;

        if (item.attackBonus != 0)
            _playerStats.attack += item.attackBonus;

        if (item.maxHpBonus != 0)
        {
            _playerStats.maxHp += item.maxHpBonus;
            _playerStats.Heal(item.maxHpBonus);
        }

        if (item.moveSpeedBonus != 0f)
            _playerStats.moveSpeed += item.moveSpeedBonus;

        if (item.dashSpeedBonus != 0f)
            _playerStats.baseDashSpeed += item.dashSpeedBonus;

        // 同步存入背包
        Inventory.Instance.AddOrUpgrade(
            id: item.UniqueId,
            displayName: item.itemName,
            description: item.description,
            category: item.type.ToString().ToLower(),
            maxLevel: item.maxLevel,
            attackPer: item.attackBonus,
            hpPer: item.maxHpBonus,
            speedPer: item.moveSpeedBonus,
            cooldownPer: item.cooldownReduction,
            critPer: item.critChanceBonus,
            lifestealPer: item.lifestealBonus
        );
    }

    /// <summary>
    /// 创建基础UI结构
    /// </summary>
    private void CreateUI()
    {
        if (panel == null)
        {
            panel = new GameObject("LevelUpPanel");
            panel.SetActive(false);
            panel.transform.SetParent(transform, false);

            // 水墨面板背景
            var bgImg = panel.AddComponent<Image>();
            bgImg.sprite = PlaceholderSpriteFactory.CreateInkPanelSprite(900, 400);
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.96f, 0.94f, 0.89f, 0.95f); // 半透明宣纸色

            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(100, 100);
            panelRect.offsetMax = new Vector2(-100, -100);

            // 标题
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            var titleText = titleObj.AddComponent<Text>();
            titleText.text = "升级！选择强化";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 42;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = ShuiMoPalette.CinnabarRed;
            titleText.alignment = TextAnchor.MiddleCenter;
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -40);
            titleRect.sizeDelta = new Vector2(600, 60);

            // 选项容器
            var containerObj = new GameObject("OptionContainer");
            containerObj.transform.SetParent(panel.transform, false);
            var containerRect = containerObj.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(30, 100);
            containerRect.offsetMax = new Vector2(-30, 30);

            // 水平布局
            var layout = containerObj.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 30;
            layout.childAlignment = TextAnchor.MiddleCenter;

            optionContainer = containerObj.transform;
        }
    }
}
