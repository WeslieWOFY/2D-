using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 超级 BOSS —— 多形态 BOSS 的形态协调者。刻意【不继承 Enemy】：
/// 它没有血量、没有碰撞体、不承接伤害，只干「切形态 + 管受击开关」这一件事。
/// 伤害全部由各形态的受击点（自己写的 Enemy 子类）承接。
///
/// ── 放权说明 ──
///   · 血量与受击：在受击点上（继承 Enemy，自带走血条、受击闪红、爆炸那一套）
///   · 血条上传：受击点自己调 BossEvents.NotifyAppear / NotifyHpChanged
///   · 受击开关：Enemy 基类上的 hitEnabled 标志，本类只负责按时机拨它
///   · 形态逻辑：BossFormBase 子物体脚本，或子类重写本类的虚方法
///   · 本类只管：把形态按顺序推下去、到点开受击开关、最后宣布死亡
///
/// ── 完整流程 ──
///   启用 → 进入形态 1：启用该形态的子物体，并把它里面所有 Enemy 的受击开关关掉
///   → 等本形态的 enableHitDelay 秒 → 打开受击开关（此时才打得动）
///   → 受击点自己扣血、自己传血条；血量见底时调本类的 OnFormHitPointDestroyed()
///   → 还有下一形态：等 switchDelay 秒后切过去
///     已是最终形态：宣布阵亡（广播 BossDied 让关卡结算）
///
/// ── 你写的受击点长这样 ──
/// <code>
/// public class Form1HitPoint : Enemy          // 直接继承 Enemy，不需要别的基类
/// {
///     public override void OnMove() { }
///     public override void OnAttack() { }
///
///     public override void TakeDamage(int damage)
///     {
///         base.TakeDamage(damage);                // 开关关着时 base 自己会忽略
///         BossEvents.NotifyHpChanged(currentHP);  // 顺便上传血条
///     }
///
///     protected override void OnDeath()
///     {
///         GetComponentInParent&lt;SuperBoss&gt;().OnFormHitPointDestroyed();  // 通知父物体：变形还是死亡
///         base.OnDeath();
///     }
/// }
/// </code>
/// </summary>
public class SuperBoss : MonoBehaviour
{
    /// <summary>一个形态的配置</summary>
    [System.Serializable]
    public class BossForm
    {
        [Tooltip("形态名，只用于在 Inspector 和 Console 日志里辨认")]
        public string formName = "形态";

        [Tooltip("该形态要启用的子物体（其余形态的子物体会被自动禁用）。" +
                 "里面要有这个形态的受击点（任意继承 Enemy 的脚本）")]
        public GameObject[] formObjects;

        [Tooltip("本形态被打爆后，等多少秒才切到下一形态（留给爆炸和变身特效的时间）")]
        public float switchDelay = 2f;

        [Tooltip("进入本形态后，等多少秒才打开受击开关（给玩家 1~2 秒反应时间）")]
        public float enableHitDelay = 1.5f;
    }

    [Header("形态配置（按顺序，最后一个为最终形态）")]
    [SerializeField] private BossForm[] forms;

    [Header("阵亡表现")]
    [Tooltip("阵亡后是否自动禁用根节点，让整个 BOSS 消失（各形态的子物体会一起被隐藏）")]
    [SerializeField] private bool disableRootOnDefeat = true;

    [Tooltip("阵亡后等多少秒才禁用根节点，留给爆炸特效播完")]
    [SerializeField] private float defeatDisableDelay = 2f;

    [Header("调试")]
    [Tooltip("切换形态 / 阵亡时在 Console 打印日志")]
    [SerializeField] private bool logFormChange = true;

    // 当前形态下标，-1 表示还没进入任何形态
    private int formIndex = -1;
    // 是否正处于两个形态之间的间隙
    private bool transitioning;
    // 是否已经阵亡
    private bool defeated;
    // 当前形态子物体上收集到的 BossFormBase 脚本
    private readonly List<BossFormBase> activeFormBehaviours = new();
    // 当前形态子物体上收集到的受击目标（任意 Enemy），由本类统一开关受击
    private readonly List<Enemy> activeHitTargets = new();
    // 延迟打开受击开关的协程
    private Coroutine enableHitCoroutine;

    // ==================== 对外只读状态 ====================

    /// <summary>当前形态下标（从 0 开始）</summary>
    public int FormIndex => formIndex;

    /// <summary>形态总数</summary>
    public int FormCount => forms != null ? forms.Length : 0;

    /// <summary>当前是否是最后一个形态</summary>
    public bool IsLastForm => forms != null && formIndex >= forms.Length - 1;

    /// <summary>当前是否正处于两个形态之间的间隙</summary>
    public bool IsTransitioning => transitioning;

    /// <summary>是否已经阵亡</summary>
    public bool IsDefeated => defeated;

    /// <summary>当前形态的配置；还没进入形态时为 null</summary>
    public BossForm CurrentForm =>
        (forms != null && formIndex >= 0 && formIndex < forms.Length) ? forms[formIndex] : null;

    // ==================== 生命周期 ====================

    private void OnEnable()
    {
        defeated = false;
        transitioning = false;
        enableHitCoroutine = null;
        activeFormBehaviours.Clear();
        activeHitTargets.Clear();

        if (forms == null || forms.Length == 0)
        {
            Debug.LogError($"[SuperBoss] {name} 没有配置任何形态（forms 为空），超级 BOSS 无法运行。");
            return;
        }

        // 从第一个形态开始
        EnterForm(0);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        transitioning = false;
        enableHitCoroutine = null;
        activeFormBehaviours.Clear();
        activeHitTargets.Clear();
    }

    private void Update()
    {
        if (defeated) return;

        // 把每帧的移动/攻击驱动转发给当前形态的两种写法
        OnFormMove(formIndex);
        for (int i = 0; i < activeFormBehaviours.Count; i++)
            activeFormBehaviours[i].OnFormMove();

        OnFormAttack(formIndex);
        for (int i = 0; i < activeFormBehaviours.Count; i++)
            activeFormBehaviours[i].OnFormAttack();
    }

    // ==================== 给受击点调的接口 ====================

    /// <summary>
    /// 【受击点血量见底时调这个】告诉父物体：本形态被打爆了，由父物体决定是变形还是死亡。
    /// 典型写法：GetComponentInParent&lt;SuperBoss&gt;().OnFormHitPointDestroyed();
    /// </summary>
    public void OnFormHitPointDestroyed()
    {
        if (defeated) return;

        if (transitioning)
        {
            // 正常流程下不会走到这里，防一下重复通知
            Debug.LogWarning($"[SuperBoss] {name} 正在换形态，又收到了受击点被打爆的通知，已忽略。");
            return;
        }

        if (IsLastForm)
        {
            // 已经是最终形态 —— 该真死了
            Defeat();
            return;
        }

        // 还有后续形态 —— 变形
        transitioning = true;
        StartCoroutine(FormTransitionRoutine());
    }

    // ==================== 变形流程 ====================

    /// <summary>受击点被打爆后：通知旧形态退出 → 等变身间隔 → 进入下一个形态</summary>
    private IEnumerator FormTransitionRoutine()
    {
        BossForm exiting = CurrentForm;
        NotifyFormExit(formIndex);

        if (logFormChange)
            Debug.Log($"[SuperBoss] {name} 形态「{exiting?.formName}」被打爆，{exiting?.switchDelay} 秒后切下一形态");

        if (exiting != null && exiting.switchDelay > 0f)
            yield return new WaitForSeconds(exiting.switchDelay);

        transitioning = false;
        EnterForm(formIndex + 1);
    }

    /// <summary>进入某个形态：切子物体 → 执行该形态的逻辑 → 延迟打开受击开关</summary>
    private void EnterForm(int index)
    {
        if (forms == null || index < 0 || index >= forms.Length)
        {
            Debug.LogError($"[SuperBoss] {name} 形态下标 {index} 越界（共 {FormCount} 个形态），已忽略。");
            return;
        }

        formIndex = index;
        BossForm form = forms[index];

        // 1. 启用该形态的子物体、禁用其余形态的（顺带收集形态脚本与受击目标）
        ApplyFormObjects(form, index);

        // 2. 执行该形态的逻辑（虚方法 + 子物体脚本，两种都调）
        OnFormEnter(index);
        for (int i = 0; i < activeFormBehaviours.Count; i++)
            activeFormBehaviours[i].OnFormEnter();

        // 3. 这个形态里一个 Enemy 都没有的话，它永远打不死
        if (activeHitTargets.Count == 0)
        {
            Debug.LogWarning($"[SuperBoss] {name} 的形态「{form.formName}」里没有找到任何 Enemy（受击点），" +
                             $"这个形态将无法被打死。");
        }

        // 4. 打开受击开关 —— 登场时开关一律是关的，这里按 enableHitDelay 延迟打开
        if (enableHitCoroutine != null)
        {
            StopCoroutine(enableHitCoroutine);
            enableHitCoroutine = null;
        }

        if (form.enableHitDelay > 0f)
            enableHitCoroutine = StartCoroutine(EnableHitAfterDelay(form, index));
        else
            OpenHitSwitch(form);

        if (logFormChange)
            Debug.Log($"[SuperBoss] {name} 进入第 {index + 1}/{FormCount} 形态「{form.formName}」");
    }

    /// <summary>等 enableHitDelay 秒后打开受击开关</summary>
    private IEnumerator EnableHitAfterDelay(BossForm form, int index)
    {
        yield return new WaitForSeconds(form.enableHitDelay);
        enableHitCoroutine = null;

        // 等待期间可能已经切形态 / 阵亡，避免把开关开到别的地方
        if (defeated || transitioning || formIndex != index) yield break;

        OpenHitSwitch(form);
    }

    /// <summary>把当前形态所有受击目标的受击开关打开（Enemy 基类上的 hitEnabled）</summary>
    private void OpenHitSwitch(BossForm form)
    {
        for (int i = 0; i < activeHitTargets.Count; i++)
        {
            if (activeHitTargets[i] != null)
                activeHitTargets[i].EnableHit();
        }

        if (logFormChange)
            Debug.Log($"[SuperBoss] {name} 形态「{form.formName}」的受击开关已打开");
    }

    /// <summary>通知当前形态退出，并清空收集到的形态脚本 / 受击目标</summary>
    private void NotifyFormExit(int index)
    {
        // 离开形态时把「延迟开受击开关」掐掉，避免它把开关开到下一个形态的受击点上
        if (enableHitCoroutine != null)
        {
            StopCoroutine(enableHitCoroutine);
            enableHitCoroutine = null;
        }

        OnFormExit(index);
        for (int i = 0; i < activeFormBehaviours.Count; i++)
            activeFormBehaviours[i].OnFormExit();

        activeFormBehaviours.Clear();
        activeHitTargets.Clear();
    }

    /// <summary>启用目标形态的子物体、禁用其余形态的，并收集上面的 BossFormBase / Enemy</summary>
    private void ApplyFormObjects(BossForm target, int index)
    {
        activeHitTargets.Clear();

        // 先把所有形态的子物体全关掉，保证同一时间只有一个形态在场
        foreach (BossForm form in forms)
        {
            if (form?.formObjects == null) continue;

            foreach (GameObject go in form.formObjects)
            {
                if (go == null) continue;

                if (go == gameObject)
                {
                    Debug.LogWarning($"[SuperBoss] {name} 的形态「{form.formName}」把 SuperBoss 自身放进了 formObjects，" +
                                     $"自动禁用会导致整个 BOSS 停摆，已跳过这一项。");
                    continue;
                }

                go.SetActive(false);
            }
        }

        if (target?.formObjects == null) return;

        // 再启用目标形态的子物体
        foreach (GameObject go in target.formObjects)
        {
            if (go == null || go == gameObject) continue;

            // 受击目标：开关一律先关掉，等本类按 enableHitDelay 来开
            foreach (Enemy enemy in go.GetComponentsInChildren<Enemy>(true))
            {
                enemy.DisableHit();
                activeHitTargets.Add(enemy);
            }

            // 形态脚本：先注入引用再激活，这样它在 OnEnable 里就能拿到 Boss 和 FormIndex
            foreach (BossFormBase behaviour in go.GetComponentsInChildren<BossFormBase>(true))
            {
                behaviour.Bind(this, index);
                activeFormBehaviours.Add(behaviour);
            }

            go.SetActive(true);
        }
    }

    // ==================== 阵亡 ====================

    /// <summary>最终形态也被打爆 —— 超级 BOSS 真的死了</summary>
    private void Defeat()
    {
        defeated = true;
        transitioning = false;
        NotifyFormExit(formIndex);

        if (logFormChange)
            Debug.Log($"[SuperBoss] {name} 最终形态「{CurrentForm?.formName}」被击破，超级 BOSS 阵亡");

        // 广播真正的死亡：血条延迟隐藏 + 关卡系统据此结算胜利
        BossEvents.NotifyDied();

        // 子类的专属阵亡表现
        OnBossDefeated();

        // 让整个 BOSS 消失（各形态的子物体会一起被隐藏）
        if (disableRootOnDefeat)
            StartCoroutine(DisableAfterDefeat());
    }

    private IEnumerator DisableAfterDefeat()
    {
        yield return new WaitForSeconds(defeatDisableDelay);
        gameObject.SetActive(false);
    }

    // ==================== 形态虚方法（写子类时重写这些） ====================

    /// <summary>进入某形态时调用</summary>
    protected virtual void OnFormEnter(int index) { }

    /// <summary>离开某形态时调用</summary>
    protected virtual void OnFormExit(int index) { }

    /// <summary>当前形态每帧的移动逻辑（由本类的 Update 驱动）</summary>
    protected virtual void OnFormMove(int index) { }

    /// <summary>当前形态每帧的攻击逻辑（由本类的 Update 驱动）</summary>
    protected virtual void OnFormAttack(int index) { }

    /// <summary>超级 BOSS 阵亡时调用 —— 子类在这里做专属表现（换 BGM、大爆炸等）</summary>
    protected virtual void OnBossDefeated() { }
}
