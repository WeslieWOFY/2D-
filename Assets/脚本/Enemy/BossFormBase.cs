using UnityEngine;

/// <summary>
/// 形态脚本基类 —— 挂在 SuperBoss 的「形态子物体」上，每个形态写一个自己的脚本，重写下面四个回调。
///
/// 由 SuperBoss 在切换形态时自动启用/禁用所在子物体，并调用这些回调。
/// 不需要手动拖引用：Boss 和 FormIndex 会在子物体被激活之前自动注入。
///
/// 调用顺序（每个形态一次）：
///   Bind（注入 Boss / FormIndex）→ Awake → OnEnable → OnFormEnter
///   → 每帧 OnFormMove / OnFormAttack → OnFormExit → （子物体被禁用）
///
/// 所以 Awake / OnEnable 里就可以安全地读 Boss 和 FormIndex，
/// 但形态相关的初始化（启动协程、复位状态）建议统一放在 OnFormEnter。
/// </summary>
public abstract class BossFormBase : MonoBehaviour
{
    /// <summary>所属的超级 BOSS，由 SuperBoss 自动注入</summary>
    protected SuperBoss Boss { get; private set; }

    /// <summary>当前是第几个形态（从 0 开始），由 SuperBoss 自动注入</summary>
    protected int FormIndex { get; private set; }

    /// <summary>由 SuperBoss 调用，注入宿主引用。子类不用管，也不要自己调。</summary>
    public void Bind(SuperBoss boss, int formIndex)
    {
        Boss = boss;
        FormIndex = formIndex;
    }

    /// <summary>进入本形态时调用 —— 写该形态的初始化逻辑</summary>
    public virtual void OnFormEnter() { }

    /// <summary>离开本形态时调用 —— 清理该形态启动的协程 / 状态</summary>
    public virtual void OnFormExit() { }

    /// <summary>本形态每帧的移动逻辑（由 SuperBoss 的 Update 转发）</summary>
    public virtual void OnFormMove() { }

    /// <summary>本形态每帧的攻击逻辑（由 SuperBoss 的 Update 转发）</summary>
    public virtual void OnFormAttack() { }
}
