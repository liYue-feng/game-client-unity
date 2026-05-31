using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 自动武器系统管理器：桥接 UpgradeManager 和 AutoWeaponSpawner。
/// 挂在玩家 GameObject 上，监听武器激活/升级事件。
/// </summary>
public class WeaponSystem : MonoBehaviour
{
    /// <summary>武器ID -> Spawner组件的映射表</summary>
    private Dictionary<string, AutoWeaponSpawner> _spawners = new Dictionary<string, AutoWeaponSpawner>();

    void Start()
    {
        // 注册到 UpgradeManager（可能在场景中或通过 BattleSceneSetup 初始化）
        var upgradeMgr = FindObjectOfType<UpgradeManager>();
        if (upgradeMgr != null)
        {
            upgradeMgr.OnWeaponActivated += OnWeaponActivated;
        }
    }

    void OnDestroy()
    {
        var upgradeMgr = FindObjectOfType<UpgradeManager>();
        if (upgradeMgr != null)
        {
            upgradeMgr.OnWeaponActivated -= OnWeaponActivated;
        }
    }

    /// <summary>武器激活/升级回调</summary>
    void OnWeaponActivated(string weaponId, int level)
    {
        if (_spawners.TryGetValue(weaponId, out var spawner))
        {
            // 升级已有武器
            spawner.IncreaseLevel();
            AudioManager.Instance.PlaySFX("levelup");
            Debug.Log($"[WeaponSystem] {weaponId} 升级到 Lv.{spawner.CurrentLevel}");
        }
        else
        {
            // 首次获得 — 添加对应 Spawner 组件
            spawner = AddSpawnerForWeapon(weaponId);
            if (spawner != null)
            {
                _spawners[weaponId] = spawner;
                spawner.StartWeapon();
                AudioManager.Instance.PlaySFX("ui_confirm");
                Debug.Log($"[WeaponSystem] {weaponId} 激活 Lv.1");
            }
        }
    }

    AutoWeaponSpawner AddSpawnerForWeapon(string weaponId)
    {
        switch (weaponId)
        {
            case "ink_bolt":
                return gameObject.AddComponent<InkBoltSpawner>();
            case "ink_swirl":
                return gameObject.AddComponent<InkSwirlSpawner>();
            case "ink_strike":
                return gameObject.AddComponent<InkStrikeSpawner>();
            case "ink_slash":
                return gameObject.AddComponent<InkSlashSpawner>();
            default:
                Debug.LogWarning($"[WeaponSystem] 未知武器ID: {weaponId}");
                return null;
        }
    }

    /// <summary>清空所有武器（重新开始时调用）</summary>
    public void ClearAllWeapons()
    {
        foreach (var kvp in _spawners)
        {
            if (kvp.Value is InkSwirlSpawner swirl)
                swirl.ClearAllParticles();
            Destroy(kvp.Value);
        }
        _spawners.Clear();
    }
}