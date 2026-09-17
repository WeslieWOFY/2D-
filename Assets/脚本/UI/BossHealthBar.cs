using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BOSS 血条 —— 通用组件，与任何具体 BOSS 完全解耦，只通过 LevelEventBus 事件联动。
///
/// 事件契约见 <see cref="BossEvents"/>：
///   BossAppear     data=int maxHP     → 显示血条并满血
///   BossHpChanged  data=int currentHP → 按 maxHP 换算比例更新填充
///   BossDied       （无参）           → 延迟 hideDelay 秒后隐藏
///   BossGone       （无参）           → 立即隐藏（BOSS 撤退/超时逃跑，没死）
///
/// 一条血条同一时间只跟随一个 BOSS：收到谁的事件就显示谁的血量。
/// 任何关卡里只有总 BOSS 需要挂血条，由总 BOSS 主动广播这些事件；
/// 小 BOSS 不广播，就自然不会出现在血条上，无需在血条侧做任何筛选。
///
/// 用法：把脚本挂到血条所在物体上，Inspector 里拖入 panel（血条根，用于显示/隐藏）和 fillImage（填充图）。
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("血条")]
    [Tooltip("整个血条 UI（背景+填充的根物体），用于显示/隐藏")]
    [SerializeField] private GameObject panel;
    [Tooltip("血条填充图（Image，fillAmount 0~1，越往右越满）")]
    [SerializeField] private Image fillImage;

    [Header("隐藏时机")]
    [Tooltip("BOSS 阵亡后延迟隐藏血条的秒数，0 表示立即隐藏")]
    [SerializeField] private float hideDelay = 2f;
    [Tooltip("BOSS 撤退/退场（没死）时是否立即隐藏血条")]
    [SerializeField] private bool hideOnGone = true;

    // 最近一次出场时记录的最大血量，用于换算填充比例
    private int maxHp = 1;
    // 当前是否有 BOSS 正在占用血条；没有时忽略后续的掉血/死亡事件，避免残留事件改到血条
    private bool isShowing;
    private Coroutine hideCoroutine;

    private void Start()
    {
        // 在 Start 订阅：此时 LevelEventBus 一定就绪，避免 OnEnable 时序问题导致订阅失败
        LevelEventBus.On(BossEvents.Appear, OnBossAppear);
        LevelEventBus.On(BossEvents.HpChanged, OnBossHpChanged);
        LevelEventBus.On(BossEvents.Died, OnBossDied);
        LevelEventBus.On(BossEvents.Gone, OnBossGone);

        // 初始隐藏，等总 BOSS 出场
        Hide();
    }

    private void OnDestroy()
    {
        LevelEventBus.Off(BossEvents.Appear, OnBossAppear);
        LevelEventBus.Off(BossEvents.HpChanged, OnBossHpChanged);
        LevelEventBus.Off(BossEvents.Died, OnBossDied);
        LevelEventBus.Off(BossEvents.Gone, OnBossGone);

        CancelHide();
    }

    // ==================== 事件回调 ====================

    /// <summary>BOSS 出场：显示血条并满血</summary>
    private void OnBossAppear(object data)
    {
        CancelHide();

        // 出场事件必须带 maxHP，否则填充比例无从换算；此时沿用上一次的 maxHP 并给出提示
        if (data is int max && max > 0)
            maxHp = max;
        else
            Debug.LogWarning($"[BossHealthBar] \"{BossEvents.Appear}\" 事件缺少有效的 maxHP（收到 {data}），本次沿用 maxHP={maxHp} 换算血条。");

        isShowing = true;
        if (panel != null) panel.SetActive(true);
        if (fillImage != null) fillImage.fillAmount = 1f; // 出现即满血
    }

    /// <summary>BOSS 掉血：按「当前血量 / 最大血量」更新填充</summary>
    private void OnBossHpChanged(object data)
    {
        if (!isShowing) return;
        if (!(data is int currentHp)) return;

        if (fillImage != null)
            fillImage.fillAmount = Mathf.Clamp01((float)currentHp / Mathf.Max(1, maxHp));
    }

    /// <summary>BOSS 阵亡：延迟 hideDelay 秒后隐藏血条</summary>
    private void OnBossDied()
    {
        if (!isShowing) return;

        CancelHide();

        if (hideDelay <= 0f)
        {
            Hide();
            return;
        }
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    /// <summary>BOSS 退场但没死（撤退/超时逃跑）：立即隐藏血条</summary>
    private void OnBossGone()
    {
        if (!hideOnGone || !isShowing) return;
        Hide();
    }

    // ==================== 显示 / 隐藏 ====================

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(hideDelay);
        hideCoroutine = null;
        Hide();
    }

    /// <summary>立即隐藏血条（不影响 BOSS 本体）</summary>
    public void Hide()
    {
        CancelHide();
        isShowing = false;
        if (panel != null) panel.SetActive(false);
    }

    private void CancelHide()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }
}
