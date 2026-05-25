using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss血条：屏幕顶部宽条，显示Boss名称和阶段。
/// 只在Boss房间内可见。
/// </summary>
public class BossHPBar : MonoBehaviour
{
    [Tooltip("Boss血条滑块")]
    public Slider bossSlider;
    [Tooltip("Boss名称文字")]
    public Text bossNameText;
    [Tooltip("阶段文字")]
    public Text phaseText;

    private EnemyBase _boss;

    /// <summary>绑定Boss</summary>
    public void BindBoss(EnemyBase boss, string bossName = "Boss")
    {
        _boss = boss;
        if (bossNameText != null) bossNameText.text = bossName;
        UpdateBar();
    }

    private void Update()
    {
        if (_boss == null)
        {
            gameObject.SetActive(false);
            return;
        }

        UpdateBar();

        // 阶段显示
        if (phaseText != null)
        {
            float hpPercent = (float)_boss.hp / _boss.maxHp;
            phaseText.text = hpPercent > 0.5f ? "阶段 1" : "阶段 2";
        }
    }

    private void UpdateBar()
    {
        if (bossSlider == null || _boss == null) return;

        bossSlider.maxValue = _boss.maxHp;
        bossSlider.value = _boss.hp;
    }
}
