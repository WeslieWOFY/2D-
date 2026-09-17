using System.Collections;
using UnityEngine;

/// <summary>
/// 猴子石板 —— 秘密关卡 BOSS。
/// 不继承 Enemy，独立实现 ISecretBoss。
///
/// 行为：
///   Entering             → 入场向左移动，到达目标 X 坐标后锚定
///   Oscillating          → 随机间隔上-下交替摆动 + 子物体动作（加速自旋 → 前移 → 召回）
///   FinishingOscillation → 撤退已触发但正在完成当前摆动
///   Retreating           → 向右加速离场，出屏后禁用
///
/// 振荡循环（每次到达目标点）：
///   1. 子物体 SelfRotator 加速旋转 + 前移 + 召回
///   2. 等待随机间隔 AND 子物体归位 → 摆向反方向目标点
/// </summary>
public class MonkeySlateBoss : MonoBehaviour, ISecretBoss
{
    [Header("入场")]
    [SerializeField] private float enterSpeed = 3f;
    [SerializeField] private float enterDuration = 3f;
    [SerializeField] private float enterTargetX = -2f;

    [Header("中场振荡")]
    [SerializeField] private float oscillationAmplitude = 1.5f;
    [SerializeField] private float oscillationSpeed = 5f;
    [SerializeField] private Vector2 oscillationIntervalRange = new Vector2(1.5f, 4f);

    [Header("子物体动作（到达目标点时触发）")]
    [SerializeField] private Transform childObject;
    [SerializeField] private float childBurstRotateSpeed = 360f;       // 加速自旋速度
    [SerializeField] private Vector2 childMoveOffset = new Vector2(-3f, 0);  // 前移偏移（本地坐标，负=左）
    [SerializeField] private float childMoveOutDuration = 0.4f;        // 移出时长
    [SerializeField] private float childMoveBackDuration = 0.6f;       // 召回时长

    [Header("撤退")]
    [SerializeField] private float retreatSpeed = 6f;
    [SerializeField] private float retreatScreenMargin = 1.3f;

    [Header("受击点死亡")]
    [SerializeField] private string hitPointDestroyEvent = "MonkeySlateHitPointDestroyed";

    // ── 状态机 ──
    private enum SlateState
    {
        Entering,
        Oscillating,
        FinishingOscillation,
        Retreating,
    }

    private SlateState currentState;
    private bool retreatPending;

    private float enterElapsed;
    private Camera mainCam;
    private Vector2 anchorPosition;
    private Vector2 oscillationTarget;

    private Coroutine oscillationCoroutine;

    // 子物体
    private SelfRotator childSelfRotator;
    private MonkeySlateHitPoint childHitPoint;
    private Vector3 childStartLocalPos;
    private bool childActionComplete;

    // ==================== 生命周期 ====================

    private void Awake()
    {
        mainCam = Camera.main;

        if (childObject != null)
        {
            childSelfRotator = childObject.GetComponent<SelfRotator>();
            childHitPoint = childObject.GetComponent<MonkeySlateHitPoint>();
            childStartLocalPos = childObject.localPosition;
        }
    }

    private void Start()
    {
        LevelEventBus.On(hitPointDestroyEvent, OnHitPointDestroyed);
    }

    private void OnEnable()
    {
        currentState = SlateState.Entering;
        enterElapsed = 0f;
        retreatPending = false;
        childActionComplete = true;

        anchorPosition = transform.position;
        oscillationTarget = transform.position;

        // 子物体归位
        ResetChild();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        oscillationCoroutine = null;
        ResetChild();

        LevelEventBus.Off(hitPointDestroyEvent, OnHitPointDestroyed);
    }

    private void Update()
    {
        switch (currentState)
        {
            case SlateState.Entering:
                EnteringTick();
                break;
            case SlateState.Oscillating:
                OscillatingTick();
                break;
            case SlateState.FinishingOscillation:
                FinishingOscillationTick();
                break;
            case SlateState.Retreating:
                RetreatingTick();
                break;
        }
    }

    // ==================== 入场 ====================

    private void EnteringTick()
    {
        enterElapsed += Time.deltaTime;

        if (transform.position.x <= enterTargetX || enterElapsed >= enterDuration)
        {
            anchorPosition = transform.position;
            oscillationTarget = transform.position;

            if (retreatPending)
            {
                currentState = SlateState.Retreating;
            }
            else
            {
                currentState = SlateState.Oscillating;
                StartOscillationCoroutine();
            }
        }
        else
        {
            transform.Translate(enterSpeed * Time.deltaTime * Vector3.left);
        }
    }

    // ==================== 中场振荡 ====================

    private void StartOscillationCoroutine()
    {
        if (oscillationCoroutine != null) StopCoroutine(oscillationCoroutine);
        oscillationCoroutine = StartCoroutine(OscillationRoutine());
    }

    /// <summary>
    /// 振荡主循环：
    ///   到达目标 → 触发子物体动作 → 等间隔+子物体归位 → 设反方向目标 → 等移动到位 → 循环
    /// </summary>
    private IEnumerator OscillationRoutine()
    {
        bool goingUp = true;
        bool firstCycle = true; // 首轮跳过子物体动作，直接从锚点出发

        while (currentState == SlateState.Oscillating)
        {
            if (firstCycle)
            {
                firstCycle = false;
            }
            else
            {
                // ── 1. 到达目标点 → 触发子物体动作 ──
                TriggerChildAction();

                // ── 2. 等待：随机间隔 AND 子物体动作完成 ──
                float interval = Random.Range(oscillationIntervalRange.x, oscillationIntervalRange.y);
                float elapsed = 0f;
                while ((elapsed < interval || !childActionComplete) && currentState == SlateState.Oscillating)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (currentState != SlateState.Oscillating) yield break;
            }

            // ── 3. 设定反方向目标点 ──
            float targetY = goingUp
                ? anchorPosition.y + oscillationAmplitude
                : anchorPosition.y - oscillationAmplitude;
            oscillationTarget = new Vector2(anchorPosition.x, targetY);
            goingUp = !goingUp;

            // ── 4. 等待 Boss 移动到目标点 ──
            while (!HasReachedTarget() && currentState == SlateState.Oscillating)
                yield return null;
        }
    }

    /// <summary>
    /// Update 驱动：平滑移向目标点，同时检测撤退请求。
    /// </summary>
    private void OscillatingTick()
    {
        transform.position = Vector2.MoveTowards(
            transform.position,
            oscillationTarget,
            oscillationSpeed * Time.deltaTime
        );

        if (!retreatPending) return;

        if (HasReachedTarget())
        {
            StopOscillationCoroutine();
            currentState = SlateState.Retreating;
        }
        else
        {
            currentState = SlateState.FinishingOscillation;
        }
    }

    // ==================== 子物体动作 ====================

    /// <summary>
    /// 触发子物体动作：扔出 → 前移 → 召回。
    /// 若挂有 MonkeySlateHitPoint 则通过 ThrowOut/Recall 控制视觉；
    /// 否则直接操作 SelfRotator。
    /// </summary>
    private void TriggerChildAction()
    {
        if (childObject == null)
        {
            childActionComplete = true;
            return;
        }

        childActionComplete = false;
        StartCoroutine(ChildActionRoutine());
    }

    private IEnumerator ChildActionRoutine()
    {
        // 扔出：通知受击点（优先）或直接加速自旋
        if (childHitPoint != null)
            childHitPoint.ThrowOut();
        else if (childSelfRotator != null)
            childSelfRotator.SetSpeed(childBurstRotateSpeed);

        // 前移
        Vector3 outPos = childStartLocalPos + (Vector3)childMoveOffset;
        yield return MoveChildLocal(outPos, childMoveOutDuration);

        // 召回
        yield return MoveChildLocal(childStartLocalPos, childMoveBackDuration);

        // 召回：通知受击点（优先）或恢复自旋
        if (childHitPoint != null)
            childHitPoint.Recall();
        else if (childSelfRotator != null)
            childSelfRotator.ResetSpeed();

        childActionComplete = true;
    }

    /// <summary>
    /// 子物体本地坐标平滑插值移动。
    /// </summary>
    private IEnumerator MoveChildLocal(Vector3 targetLocalPos, float duration)
    {
        if (childObject == null || duration <= 0f)
        {
            if (childObject != null) childObject.localPosition = targetLocalPos;
            yield break;
        }

        Vector3 start = childObject.localPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            childObject.localPosition = Vector3.Lerp(start, targetLocalPos, t);
            yield return null;
        }
        childObject.localPosition = targetLocalPos;
    }

    /// <summary>
    /// 子物体归位 + 恢复自旋原速。
    /// </summary>
    private void ResetChild()
    {
        if (childObject != null)
            childObject.localPosition = childStartLocalPos;

        if (childHitPoint != null)
            childHitPoint.Recall();
        else if (childSelfRotator != null)
            childSelfRotator.ResetSpeed();

        childActionComplete = true;
    }

    // ==================== 完成当前摆动（撤退前过渡） ====================

    private void FinishingOscillationTick()
    {
        transform.position = Vector2.MoveTowards(
            transform.position,
            oscillationTarget,
            oscillationSpeed * Time.deltaTime
        );

        if (HasReachedTarget())
        {
            currentState = SlateState.Retreating;
        }
    }

    // ==================== 撤退 ====================

    private void RetreatingTick()
    {
        transform.Translate(retreatSpeed * Time.deltaTime * Vector3.right);

        if (mainCam != null &&
            mainCam.WorldToViewportPoint(transform.position).x > retreatScreenMargin)
        {
            gameObject.SetActive(false);
        }
    }

    // ==================== ISecretBoss 接口 ====================

    /// <summary>
    /// 触发撤退：若正在摆动中，先平滑到达当前目标点后再离场。
    /// </summary>
    public void TriggerRetreat()
    {
        if (currentState == SlateState.Retreating) return;
        retreatPending = true;

        switch (currentState)
        {
            case SlateState.Entering:
                break;

            case SlateState.Oscillating:
                if (HasReachedTarget())
                {
                    StopOscillationCoroutine();
                    currentState = SlateState.Retreating;
                }
                break;

            case SlateState.FinishingOscillation:
                break;
        }
    }

    // ==================== 辅助 ====================

    private bool HasReachedTarget()
    {
        return Vector2.Distance(transform.position, oscillationTarget) < 0.01f;
    }

    private void StopOscillationCoroutine()
    {
        if (oscillationCoroutine != null)
        {
            StopCoroutine(oscillationCoroutine);
            oscillationCoroutine = null;
        }
    }

    public void DisableSlate()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    /// <summary>受击点死亡回调：父物体禁用自身</summary>
    private void OnHitPointDestroyed(object data)
    {
        gameObject.SetActive(false);
    }

    // ==================== Editor 可视化 ====================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        if (currentState == SlateState.Oscillating ||
            currentState == SlateState.FinishingOscillation)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(anchorPosition, 0.15f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(oscillationTarget, 0.2f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, oscillationTarget);
        }
    }
#endif
}
