using System.Collections;
using UnityEngine;

public class SecretPirateKing : Enemy
{
    [Header("秘密关卡-入场")]
    [SerializeField] private float enterDuration = 3f;      // 入场阶段持续时间（秒），固定向左移动

    [Header("秘密关卡-随机漂移")]
    [SerializeField] private float wanderRadius = 0.8f;     // 漂移半径（相对初始位置）
    [SerializeField] private float wanderSpeed = 0.5f;      // 漂移速度
    [SerializeField] private float changeDirectionInterval = 2f; // 换方向间隔（秒）

    [Header("秘密关卡-撤退")]
    [SerializeField] private float retreatSpeed = 4f;       // 撤退速度（向右）

    private enum SubState
    {
        Entering,    // 入场：固定向左移动
        Wandering,   // 漂移：随机极小范围四处移动
        Retreat,     // 撤退：快速向右移动
    }
    private SubState currentState;

    private Coroutine wanderCoroutine;
    private Coroutine retreatCoroutine;
    private Vector2 initialPosition;    // 入场结束后的位置（漂移锚点）
    private Vector2 wanderTarget;       // 当前漂移目标
    Camera mainCam;
    WaitForSeconds EnterDuration;
    WaitForSeconds ChangeDirectionInterval;

    protected override void Awake()
    {
        base.Awake();
        mainCam = Camera.main;
        EnterDuration = new WaitForSeconds(enterDuration);
        ChangeDirectionInterval = new WaitForSeconds(changeDirectionInterval);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        initialPosition = transform.position;
        wanderTarget = transform.position;

        // 进入入场阶段
        currentState = SubState.Entering;
        StartCoroutine(EnterRoutine());
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        CleanupCoroutines();
    }

    /// <summary>
    /// 入场协程：等待 enterDuration 秒后切换到漂移阶段
    /// </summary>
    private IEnumerator EnterRoutine()
    {
        yield return EnterDuration;

        // 入场结束，记录当前位置作为漂移锚点，切换到漂移阶段
        initialPosition = transform.position;
        wanderTarget = transform.position;
        currentState = SubState.Wandering;

        // 启动随机漂移
        if (wanderCoroutine != null) StopCoroutine(wanderCoroutine);
        wanderCoroutine = StartCoroutine(WanderRoutine());
    }

    /// <summary>
    /// 随机漂移协程：每隔一段时间在锚点周围随机选一个新目标点
    /// </summary>
    private IEnumerator WanderRoutine()
    {
        while (true)
        {
            yield return ChangeDirectionInterval;

            if (gameObject.activeSelf && currentState == SubState.Wandering && !isDie)
            {
                float offsetX = Random.Range(-wanderRadius, wanderRadius);
                float offsetY = Random.Range(-wanderRadius, wanderRadius);
                wanderTarget = initialPosition + new Vector2(offsetX, offsetY);
            }
        }
    }

    public override void OnMove()
    {
        switch (currentState)
        {
            case SubState.Entering:
                // 入场阶段：固定向左移动
                transform.Translate(moveSpeed * Time.deltaTime * Vector2.left);
                break;

            case SubState.Wandering:
                // 漂移阶段：随机极小范围四处移动
                Vector2 newPos = Vector2.MoveTowards(
                    transform.position,
                    wanderTarget,
                    wanderSpeed * Time.deltaTime
                );
                transform.position = newPos;
                break;
            case SubState.Retreat:
                Retreat();
                break;
        }
    }

    public override void OnAttack()
    {
        // 无攻击逻辑
    }

    /// <summary>
    /// 由外部调用，触发撤退：向右移动直到出屏后禁用
    /// </summary>
    public void TriggerRetreat()
    {
        currentState = SubState.Retreat;
    }

    protected override void Retreat()
    {
        if (retreatCoroutine != null) return;

        // 停止漂移
        if (wanderCoroutine != null)
        {
            StopCoroutine(wanderCoroutine);
            wanderCoroutine = null;
        }

        // 启动撤退协程，持续向右移动直到出屏
        retreatCoroutine = StartCoroutine(RetreatRoutine());
    }

    /// <summary>
    /// 撤退协程：固定向右移动，直到离开屏幕后禁用
    /// </summary>
    private IEnumerator RetreatRoutine()
    {
        while (true)
        {
            transform.Translate(retreatSpeed * Time.deltaTime * Vector2.right);
            if (mainCam != null)
            {
                Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);
                if (viewportPos.x > 1.2f)
                {
                    break;
                }
            }
            yield return null;
        }

        gameObject.SetActive(false);
    }

    protected override void OnExitScreen()
    {
        CleanupCoroutines();
        base.OnExitScreen();
    }

    /// <summary>
    /// 统一清理所有协程引用
    /// </summary>
    private void CleanupCoroutines()
    {
        if (wanderCoroutine != null)
        {
            StopCoroutine(wanderCoroutine);
            wanderCoroutine = null;
        }
        if (retreatCoroutine != null)
        {
            StopCoroutine(retreatCoroutine);
            retreatCoroutine = null;
        }
        if (flashRedCoroutine != null)
        {
            StopCoroutine(flashRedCoroutine);
            flashRedCoroutine = null;
        }
    }
}
