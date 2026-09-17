using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BOSS 血条 —— 与派罗斯完全解耦，只通过 LevelEventBus 事件联动。
///
/// 监听事件（与 PyrosHitPoint 广播约定一致）：
///   "BossAppear"    data=int maxHP   → 显示血条并满血
///   "BossHpChanged" data=int currentHP → 按当前血量更新填充
///   "BossDied"      （无参）         → 延迟 hideDelay 秒后隐藏血条
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

    [Header("死亡后隐藏")]
    [Tooltip("派罗斯死亡后延迟隐藏血条的秒数")]
    [SerializeField] private float hideDelay = 2f;

    // 事件名约定（与 PyrosHitPoint 广播一致）
    private const string BossAppearEvent = "BossAppear";
    private const string BossHpChangedEvent = "BossHpChanged";
    private const string BossDiedEvent = "BossDied";

    private int maxHp = 1;          // 最近一次出现时记录的最大血量
    private Coroutine hideCoroutine;

    private void Start()
    {
        // 在 Start 订阅：此时 LevelEventBus 一定就绪，避免 OnEnable 时序问题导致订阅失败
        LevelEventBus.On(BossAppearEvent, OnBossAppear);
        LevelEventBus.On(BossHpChangedEvent, OnBossHpChanged);
        LevelEventBus.On(BossDiedEvent, OnBossDied);

        // 初始隐藏，等派罗斯出现
        if (panel != null) panel.SetActive(false);
    }

    private void OnDestroy()
    {
        LevelEventBus.Off(BossAppearEvent, OnBossAppear);
        LevelEventBus.Off(BossHpChangedEvent, OnBossHpChanged);
        LevelEventBus.Off(BossDiedEvent, OnBossDied);
    }

    /// <summary>派罗斯出现：显示血条并满血</summary>
    private void OnBossAppear(object data)
    {
        if (hideCoroutine != null) { StopCoroutine(hideCoroutine); hideCoroutine = null; }

        if (data is int max)
            maxHp = Mathf.Max(1, max);

        if (panel != null) panel.SetActive(true);
        if (fillImage != null) fillImage.fillAmount = 1f; // 出现即满血
    }

    /// <summary>派罗斯掉血：按当前血量更新填充</summary>
    private void OnBossHpChanged(object data)
    {
        if (data is int currentHp && fillImage != null)
            fillImage.fillAmount = Mathf.Clamp01((float)currentHp / maxHp);
    }

    /// <summary>派罗斯死亡：延迟 hideDelay 秒后隐藏血条</summary>
    private void OnBossDied()
    {
        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(hideDelay);
        if (panel != null) panel.SetActive(false);
    }
}
