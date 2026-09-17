using System.Collections;
using UnityEngine;

/// <summary>
/// 枪弹新兵 —— 秘密关卡 BOSS。
/// 不继承 Enemy，独立实现 ISecretBoss。
///
/// 行为：
///   Entering → 入场向左移动，到达目标位置后锚定
///   Idle     → 原地待命，中途不动，等待外部调用 TriggerRetreat
///   Retreat  → 向右离场，出屏禁用
/// </summary>
public class GunnerRecruit : MonoBehaviour, ISecretBoss
{
    [Header("入场")]
    [SerializeField] private float enterSpeed = 3f;
    [SerializeField] private float enterDuration = 3f;
    [SerializeField] private float enterTargetX = -2f;

    [Header("撤退")]
    [SerializeField] private float retreatSpeed = 6f;
    [SerializeField] private float retreatScreenMargin = 1.3f;

    private enum RecruitState { Entering, Idle, Retreat }
    private RecruitState currentState;

    private float enterElapsed;
    private Camera mainCam;

    private Coroutine enterCoroutine;
    private Coroutine retreatCoroutine;

    private void Awake()
    {
        mainCam = Camera.main;
    }

    private void OnEnable()
    {
        currentState = RecruitState.Entering;
        enterElapsed = 0f;

        if (enterCoroutine != null) StopCoroutine(enterCoroutine);
        enterCoroutine = StartCoroutine(EnterTimeoutRoutine());
    }

    private void OnDisable()
    {
        if (enterCoroutine != null) { StopCoroutine(enterCoroutine); enterCoroutine = null; }
        if (retreatCoroutine != null) { StopCoroutine(retreatCoroutine); retreatCoroutine = null; }
    }

    /// <summary>
    /// 头部死亡后调用：停止全部协程并禁用整个 BOSS 对象。
    /// </summary>
    public void DisableBoss()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        switch (currentState)
        {
            case RecruitState.Entering:
                enterElapsed += Time.deltaTime;
                if (transform.position.x <= enterTargetX || enterElapsed >= enterDuration)
                    currentState = RecruitState.Idle;
                else
                    transform.Translate(enterSpeed * Time.deltaTime * Vector3.left);
                break;

            case RecruitState.Idle:
                // 中途不动，原地待命
                break;

            case RecruitState.Retreat:
                transform.Translate(retreatSpeed * Time.deltaTime * Vector3.right);
                if (mainCam != null && mainCam.WorldToViewportPoint(transform.position).x > retreatScreenMargin)
                    gameObject.SetActive(false);
                break;
        }
    }

    private IEnumerator EnterTimeoutRoutine()
    {
        yield return new WaitForSeconds(enterDuration);
        if (currentState == RecruitState.Entering)
            currentState = RecruitState.Idle;
    }

    public void TriggerRetreat()
    {
        if (currentState == RecruitState.Retreat) return;
        currentState = RecruitState.Retreat;

        if (retreatCoroutine != null) StopCoroutine(retreatCoroutine);
        retreatCoroutine = StartCoroutine(RetreatTimeoutRoutine());
    }

    private IEnumerator RetreatTimeoutRoutine()
    {
        yield return new WaitForSeconds(10f);
        if (currentState == RecruitState.Retreat)
            gameObject.SetActive(false);
    }
}
