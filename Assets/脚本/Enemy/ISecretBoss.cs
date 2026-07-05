/// <summary>
/// 秘密关卡BOSS统一接口 —— 定义撤退契约。
/// 所有秘密关卡BOSS必须实现 TriggerRetreat，由关卡事件管理器按需调用。
/// </summary>
public interface ISecretBoss
{
    /// <summary>
    /// 触发BOSS撤退：向右移动直到出屏后禁用。
    /// 由关卡事件系统（如LevelEventBus / 倒计时结束等）统一调用。
    /// </summary>
    void TriggerRetreat();
}
