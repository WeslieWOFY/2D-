/// <summary>
/// BOSS 血条事件契约 —— 所有 BOSS 与 BossHealthBar 之间的唯一约定。
///
/// BOSS 侧：任何关卡的总 BOSS，在合适的时机调用下面四个方法即可，血条自动跟随。
///   BossEvents.NotifyAppear(maxHP);        // 出场：血条显示并满血
///   BossEvents.NotifyHpChanged(currentHP); // 掉血：血条按 maxHP 换算比例更新
///   BossEvents.NotifyDied();               // 阵亡：血条延迟隐藏（同时是关卡结算信号）
///   BossEvents.NotifyGone();               // 退场但没死（撤退/超时逃跑）：血条立即隐藏
///
/// 血条侧：BossHealthBar 订阅这些事件，不持有任何 BOSS 引用。
///
/// ── 接入一个新 BOSS（总 BOSS）的步骤 ──
///   1. 受击点脚本继承 Enemy（自动拿到 maxHP / currentHP）
///   2. OnEnable 里调用 BossEvents.NotifyAppear(maxHP)
///   3. 重写 TakeDamage，扣血后调用 BossEvents.NotifyHpChanged(currentHP)
///   4. 死亡流程里调用 BossEvents.NotifyDied()
///   5. 如果它会中途撤退，撤退时调用 BossEvents.NotifyGone()
///
/// 非总 BOSS 什么都不用做 —— 不广播就自然不会出现在血条上。
/// </summary>
public static class BossEvents
{
    /// <summary>出场，data = int maxHP</summary>
    public const string Appear = "BossAppear";
    /// <summary>掉血，data = int currentHP</summary>
    public const string HpChanged = "BossHpChanged";
    /// <summary>阵亡（无参）</summary>
    public const string Died = "BossDied";
    /// <summary>退场但没死：撤退 / 超时逃跑（无参）</summary>
    public const string Gone = "BossGone";

    /// <summary>广播「BOSS 出场」，血条据此显示并满血</summary>
    public static void NotifyAppear(int maxHp) => LevelEventBus.Trigger(Appear, maxHp);

    /// <summary>广播「BOSS 掉血」，血条据此更新填充比例</summary>
    public static void NotifyHpChanged(int currentHp) => LevelEventBus.Trigger(HpChanged, currentHp);

    /// <summary>广播「BOSS 阵亡」，血条延迟隐藏；关卡系统据此结算胜利</summary>
    public static void NotifyDied() => LevelEventBus.Trigger(Died);

    /// <summary>广播「BOSS 退场但没死」（撤退 / 超时逃跑），血条立即隐藏</summary>
    public static void NotifyGone() => LevelEventBus.Trigger(Gone);
}
