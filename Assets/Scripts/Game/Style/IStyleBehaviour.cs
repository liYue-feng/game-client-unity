using UnityEngine;

/// <summary>
/// 流派行为接口：每个流派实现不同的被动和特殊技能逻辑。
/// 战斗系统通过这个接口调用流派特有的效果，
/// 而不是在每个地方写 if-else 判断当前流派。
/// </summary>
public interface IStyleBehaviour
{
    /// <summary>攻击命中时触发</summary>
    void OnAttackHit(EnemyBase enemy);
    /// <summary>弹反成功时触发</summary>
    void OnParrySuccess();
    /// <summary>激活特殊技能</summary>
    void ActivateSpecial(GameObject player);
    /// <summary>每帧被动逻辑</summary>
    void PassiveUpdate();
    /// <summary>获取流派数据</summary>
    StyleData GetData();
}
