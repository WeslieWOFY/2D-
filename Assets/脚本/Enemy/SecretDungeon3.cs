using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 秘密关卡 · 地宫3 —— BOSS 流程管理器。
/// 挂载到关卡场景中的 GameObject 上，OnEnable 自动开始流程。
///
/// 流程：
///   1. 游戏开始后 firstBossDelay(3s) 启用第 1 个 BOSS
///   2. 每个 BOSS 最多存活 bossLifetime(25s)：
///        · 期间被消灭（禁用）→ 提前进入下一阶段
///        · 25s 后仍未禁用 → 调用 ISecretBoss.TriggerRetreat 让其撤退
///   3. BOSS 结束（消灭或撤退完）后 nextBossDelay(5s) 召唤下一个
///   4. 第 9 个 BOSS 消灭后 finalWarnDelay(3s) → 红屏闪烁 warningFlashDuration(5s) + 警报音效
///   5. 红屏结束后 finalBossDelay(2s) → 启用最终 BOSS（第 10 个）
/// </summary>
public class SecretDungeon3 : MonoBehaviour
{
    [Header("BOSS 序列")]
    [SerializeField] private GameObject[] bosses;                  // 10 个预配置 BOSS（第 10 个为最终 BOSS）

    [Header("流程节奏")]
    [SerializeField] private float firstBossDelay = 3f;            // 游戏开始后多久启用第一个 BOSS
    [SerializeField] private float bossLifetime = 25f;            // 每个 BOSS 存活时长，超时则撤退
    [SerializeField] private float nextBossDelay = 5f;            // 上一个 BOSS 结束后多久召唤下一个
    [SerializeField] private float finalWarnDelay = 3f;           // 第 9 个 BOSS 消灭后多久开始红屏警报
    [SerializeField] private float warningFlashDuration = 5f;     // 红屏闪烁总时长（秒）
    [SerializeField] private float finalBossDelay = 2f;           // 红屏结束后多久启用最终 BOSS

    [Header("红屏警报")]
    [SerializeField] private Canvas warningCanvas;                // 红屏挂载的 Canvas（留空则新建顶层 Overlay 画布）
    [SerializeField] private AudioClip warningSFX;                // 警报音效
    [SerializeField] private float flashFadeTime = 0.1f;          // 红屏淡入淡出时间
    [SerializeField] private float flashMaxAlpha = 0.6f;         // 红屏最大透明度
    [SerializeField] private float flashHoldTime = 0.1f;         // 峰值保持时间
    [SerializeField] private float flashGapTime = 0.1f;          // 谷底间隔时间
    [SerializeField] private Color flashColor = new Color(1f, 0.08f, 0.08f, 1f); // 红色基调

    private Coroutine flowCoroutine;

    private void OnEnable()
    {
        StartFlow();
    }

    private void OnDisable()
    {
        StopFlow();
    }

    private void StartFlow()
    {
        StopFlow();
        flowCoroutine = StartCoroutine(FlowRoutine());
    }

    private void StopFlow()
    {
        if (flowCoroutine != null)
        {
            StopCoroutine(flowCoroutine);
            flowCoroutine = null;
        }
    }

    // ==================== 主流程 ====================

    private IEnumerator FlowRoutine()
    {
        // 先全部禁用，防止场景里残留已激活的 BOSS
        foreach (var boss in bosses)
        {
            if (boss != null) boss.SetActive(false);
        }

        // 1. 游戏开始后 firstBossDelay 启用第一个 BOSS
        yield return new WaitForSeconds(firstBossDelay);

        // 2. 前 9 个 BOSS 依次出场
        for (int i = 0; i < 9; i++)
        {
            if (bosses[i] == null) continue;

            bosses[i].SetActive(true);
            yield return StartCoroutine(WaitBossEnd(bosses[i]));

            // 3. 上一个 BOSS 结束后，等 nextBossDelay 召唤下一个
            yield return new WaitForSeconds(nextBossDelay);
        }

        // 4. 第 9 个 BOSS 消灭后 finalWarnDelay → 红屏闪烁 + 警报
        yield return new WaitForSeconds(finalWarnDelay);
        yield return StartCoroutine(WarningFlashRoutine());

        // 5. 红屏结束后 finalBossDelay → 启用最终 BOSS（第 10 个）
        yield return new WaitForSeconds(finalBossDelay);
        if (bosses.Length >= 10 && bosses[9] != null)
            bosses[9].SetActive(true);
    }

    /// <summary>
    /// 固定等待 bossLifetime 秒，然后检查一次：
    ///   到点仍激活 → 调用 TriggerRetreat 让其撤退（不等撤退完）；
    ///   早已被消灭（已禁用）→ 什么都不做。
    /// 不做持续检测，只是固定等满时长。
    /// </summary>
    private IEnumerator WaitBossEnd(GameObject boss)
    {
        // 固定等满 bossLifetime 秒，无论 BOSS 是否提前死亡
        yield return new WaitForSeconds(bossLifetime);

        // 到点检查一次：还活着就触发撤退，已死则忽略
        if (boss != null && boss.activeSelf)
        {
            var secret = boss.GetComponent<ISecretBoss>();
            if (secret != null)
                secret.TriggerRetreat();
        }
    }

    // ==================== 红屏警报 ====================

    /// <summary>实例化一个全屏 Canvas（Screen Space - Overlay，置于所有 UI 之上），用于承载红屏警报</summary>
    private Canvas InstantiateWarningCanvas()
    {
        GameObject canvasObj = new GameObject("WarningCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000; // 盖在所有 UI 之上，保证警报一定能被看到
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>全屏红屏闪烁 + 警报音效</summary>
    private IEnumerator WarningFlashRoutine()
    {
        // 警报音效
        if (warningSFX != null)
            AudioManager.Instance.PlaySFX(warningSFX);

        // 确保有 Canvas：优先用配置的，否则新建一个顶层 Overlay Canvas。
        // 不再用 FindObjectOfType 找场景里的 Canvas —— 重玩关卡时常驻场景
        // （DontDestroyOnLoad）里可能残留无法渲染的画布（如引用了已销毁摄像机的
        // 血条画布、黑幕过渡画布），红屏挂上去会渲染不出来。
        bool createdCanvas = false;
        Canvas canvas = warningCanvas;
        if (canvas == null)
        {
            canvas = InstantiateWarningCanvas();
            createdCanvas = true;
        }

        // 创建全屏红色 Image
        GameObject flashObj = new GameObject("WarningFlash");
        flashObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = flashObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        Image flashImage = flashObj.AddComponent<Image>();
        flashObj.transform.SetAsLastSibling();

        Color baseColor = flashColor;
        float elapsed = 0f;

        // 淡入 → 保持 → 淡出 → 间隔，循环到总时长结束
        while (elapsed < warningFlashDuration)
        {
            // 淡入
            float fadeElapsed = 0f;
            while (fadeElapsed < flashFadeTime)
            {
                float alpha = Mathf.Lerp(0f, flashMaxAlpha, fadeElapsed / flashFadeTime);
                flashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                fadeElapsed += Time.deltaTime;
                yield return null;
            }

            // 峰值保持
            flashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, flashMaxAlpha);
            yield return new WaitForSeconds(flashHoldTime);

            if (elapsed + flashFadeTime + flashHoldTime >= warningFlashDuration) break;

            // 淡出
            fadeElapsed = 0f;
            while (fadeElapsed < flashFadeTime)
            {
                float alpha = Mathf.Lerp(flashMaxAlpha, 0f, fadeElapsed / flashFadeTime);
                flashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                fadeElapsed += Time.deltaTime;
                yield return null;
            }

            flashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
            yield return new WaitForSeconds(flashGapTime);

            elapsed += flashFadeTime * 2 + flashHoldTime + flashGapTime;
        }

        Destroy(flashObj);
        if (createdCanvas) Destroy(canvas.gameObject); // 只清理自己临时创建的画布
    }
}
