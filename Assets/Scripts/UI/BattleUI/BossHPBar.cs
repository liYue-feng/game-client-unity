using System;
using UnityEngine;
using UnityEngine.UI;

public class BossHPBar : MonoBehaviour
{
    public Slider bossSlider;
    public Text bossNameText;
    public Text phaseText;

    private Boss _boss;

    public Boss BoundBoss => _boss;

    public void BindBoss(Boss boss, string bossName = "Boss")
    {
        UnbindBoss();
        if (boss == null)
        {
            return;
        }

        _boss = boss;
        _boss.OnHealthChanged += HandleHealthChanged;
        _boss.OnPhaseChanged += HandlePhaseChanged;
        _boss.OnDeath += HandleBossDeath;
        if (bossNameText != null)
        {
            bossNameText.text = bossName;
        }

        HandleHealthChanged(_boss.hp, _boss.maxHp);
        HandlePhaseChanged(_boss.CurrentPhase);
        gameObject.SetActive(true);
    }

    public void UnbindBoss()
    {
        if (!ReferenceEquals(_boss, null))
        {
            _boss.OnHealthChanged -= HandleHealthChanged;
            _boss.OnPhaseChanged -= HandlePhaseChanged;
            _boss.OnDeath -= HandleBossDeath;
        }

        _boss = null;
        gameObject.SetActive(false);
    }

    private void HandleHealthChanged(int current, int maximum)
    {
        if (bossSlider == null)
        {
            return;
        }

        bossSlider.maxValue = maximum;
        bossSlider.value = current;
    }

    private void HandlePhaseChanged(int phase)
    {
        if (phaseText != null)
        {
            phaseText.text = $"\u9636\u6bb5 {phase}";
        }
    }

    private void HandleBossDeath(EnemyBase enemy)
    {
        if (ReferenceEquals(_boss, enemy))
        {
            UnbindBoss();
        }
    }

    private void OnDisable()
    {
        if (!ReferenceEquals(_boss, null))
        {
            UnbindBoss();
        }
    }

    private void OnDestroy()
    {
        UnbindBoss();
    }
}
