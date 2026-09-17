using System.Collections;
using UnityEngine;

/// <summary>
/// 幽冥船 —— 秘密关卡 BOSS。
/// 不继承 Enemy，独立实现 ISecretBoss。
///
/// 行为：
///   Entering → 入场向左移动，到达目标位置后锚定
///   Idle     → 原地待命，等待外部调用 TriggerRetreat
///   Retreat  → 向右加速离场，出屏禁用
/// </summary>
public class GhostShip : MonoBehaviour, ISecretBoss
{
    [Header("入场")]
    [SerializeField] private float enterSpeed = 3f;
    [SerializeField] private float enterDuration = 3f;
    [SerializeField] private float enterTargetX = -2f;

    [Header("撤退")]
    [SerializeField] private float retreatSpeed = 6f;
    [SerializeField] private float retreatScreenMargin = 1.3f;

    [Header("子物体移动")]
    [SerializeField] private Transform movingChild;
    [SerializeField] private float childMoveDistance = 2f;
    [SerializeField] private float childMoveDuration = 1.5f;

    private enum ShipState { Entering, Idle, Retreat }
    private ShipState currentState;

    private float enterElapsed;
    private Camera mainCam;
    private Vector3 childStartLocalPos;
    private Vector3 childTargetLocalPos;

    private Coroutine enterCoroutine;
    private Coroutine retreatCoroutine;
    private Coroutine childMoveCoroutine;

    private void Awake()
    {
        mainCam = Camera.main;
    }

    private void OnEnable()
    {
        currentState = ShipState.Entering;
        enterElapsed = 0f;

        if (enterCoroutine != null) StopCoroutine(enterCoroutine);
        enterCoroutine = StartCoroutine(EnterTimeoutRoutine());
    }

    private void OnDisable()
    {
        if (enterCoroutine != null) { StopCoroutine(enterCoroutine); enterCoroutine = null; }
        if (retreatCoroutine != null) { StopCoroutine(retreatCoroutine); retreatCoroutine = null; }
        if (childMoveCoroutine != null) { StopCoroutine(childMoveCoroutine); childMoveCoroutine = null; }
    }

    private void Update()
    {
        switch (currentState)
        {
            case ShipState.Entering:
                enterElapsed += Time.deltaTime;
                if (transform.position.x <= enterTargetX || enterElapsed >= enterDuration)
                {
                    currentState = ShipState.Idle;
                    StartChildMove();
                }
                else
                    transform.Translate(enterSpeed * Time.deltaTime * Vector3.left);
                break;

            case ShipState.Idle:
                // 原地待命
                break;

            case ShipState.Retreat:
                transform.Translate(retreatSpeed * Time.deltaTime * Vector3.right);
                if (mainCam != null && mainCam.WorldToViewportPoint(transform.position).x > retreatScreenMargin)
                    gameObject.SetActive(false);
                break;
        }
    }

    private IEnumerator EnterTimeoutRoutine()
    {
        yield return new WaitForSeconds(enterDuration);
        if (currentState == ShipState.Entering)
        {
            currentState = ShipState.Idle;
            StartChildMove();
        }
    }

    public void TriggerRetreat()
    {
        if (currentState == ShipState.Retreat) return;
        currentState = ShipState.Retreat;

        if (retreatCoroutine != null) StopCoroutine(retreatCoroutine);
        retreatCoroutine = StartCoroutine(RetreatTimeoutRoutine());
    }

    private IEnumerator RetreatTimeoutRoutine()
    {
        yield return new WaitForSeconds(10f);
        if (currentState == ShipState.Retreat)
            gameObject.SetActive(false);
    }

    private void StartChildMove()
    {
        if (movingChild == null) return;
        if (childMoveCoroutine != null) StopCoroutine(childMoveCoroutine);
        childMoveCoroutine = StartCoroutine(ChildMoveRoutine());
    }

    private IEnumerator ChildMoveRoutine()
    {
        childStartLocalPos = movingChild.localPosition;
        childTargetLocalPos = childStartLocalPos + Vector3.up * childMoveDistance;

        float elapsed = 0f;
        while (elapsed < childMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / childMoveDuration;
            movingChild.localPosition = Vector3.Lerp(childStartLocalPos, childTargetLocalPos, t);
            yield return null;
        }
        movingChild.localPosition = childTargetLocalPos;
    }

    public void DisableShip()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }
}
