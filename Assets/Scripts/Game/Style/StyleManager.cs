using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 流派管理器：管理当前流派、流派资源、切换逻辑。
/// 单例模式，战斗中实时切换流派。
///
/// 流派切换规则：
/// - 可在战斗中切换，0.3s切换动画
/// - 可中断当前连击（牺牲连击换流派）
/// - Hurt/Die/Dash期间不可切换
/// </summary>
public class StyleManager : MonoBehaviour
{
    private static StyleManager _instance;
    public static StyleManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[StyleManager]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<StyleManager>();
            }
            return _instance;
        }
    }

    /// <summary>当前流派ID</summary>
    public CombatStyleID CurrentStyleID { get; private set; } = CombatStyleID.Blade;
    /// <summary>当前流派数据</summary>
    public StyleData CurrentStyleData { get; private set; }
    /// <summary>当前流派行为</summary>
    public IStyleBehaviour CurrentBehaviour { get; private set; }
    /// <summary>流派特殊资源当前值</summary>
    public int SpecialResource { get; private set; }

    /// <summary>流派切换事件</summary>
    public event System.Action<CombatStyleID> OnStyleChanged;
    /// <summary>特殊资源变化事件</summary>
    public event System.Action<int, int> OnSpecialResourceChanged; // current, max

    private Dictionary<CombatStyleID, IStyleBehaviour> _behaviours = new Dictionary<CombatStyleID, IStyleBehaviour>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 注册所有流派行为
        _behaviours[CombatStyleID.Blade] = new BladeStyle();
        _behaviours[CombatStyleID.Seal] = new SealStyle();
        _behaviours[CombatStyleID.Poison] = new PoisonStyle();
        _behaviours[CombatStyleID.Blood] = new BloodStyle();
        _behaviours[CombatStyleID.Sword] = new SwordStyle();

        // 默认流派
        SwitchStyle(CombatStyleID.Blade);
    }

    private void Update()
    {
        // 被动逻辑
        if (CurrentBehaviour != null)
        {
            CurrentBehaviour.PassiveUpdate();
        }
    }

    /// <summary>切换流派</summary>
    public void SwitchStyle(CombatStyleID newStyle)
    {
        if (newStyle == CurrentStyleID) return;

        CombatStyleID oldStyle = CurrentStyleID;
        CurrentStyleID = newStyle;
        CurrentStyleData = StyleDatabase.GetStyle(newStyle);

        if (_behaviours.TryGetValue(newStyle, out var behaviour))
        {
            CurrentBehaviour = behaviour;
        }

        // 重置特殊资源
        SpecialResource = 0;
        OnSpecialResourceChanged?.Invoke(SpecialResource, CurrentStyleData.specialResourceMax);
        OnStyleChanged?.Invoke(newStyle);
    }

    /// <summary>增加特殊资源</summary>
    public void AddSpecialResource(int amount)
    {
        SpecialResource = Mathf.Min(CurrentStyleData.specialResourceMax, SpecialResource + amount);
        OnSpecialResourceChanged?.Invoke(SpecialResource, CurrentStyleData.specialResourceMax);
    }

    /// <summary>尝试使用特殊技能</summary>
    public bool TryUseSpecial(GameObject player)
    {
        // 简化：特殊技能消耗满资源
        if (SpecialResource < CurrentStyleData.specialResourceMax) return false;

        SpecialResource = 0;
        OnSpecialResourceChanged?.Invoke(SpecialResource, CurrentStyleData.specialResourceMax);

        if (CurrentBehaviour != null)
        {
            CurrentBehaviour.ActivateSpecial(player);
        }
        return true;
    }
}
