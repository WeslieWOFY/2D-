using System.Collections;
using UnityEngine;

/// <summary>
/// 参天炮台 —— 秘密关卡 BOSS。
/// 不继承 Enemy，独立实现 ISecretBoss（父物体不是主要受击部位）。
///
/// 行为：
///   Entering → 入场向左移动，到达目标位置后进入 Idle
///   Idle     → 激活子物体向下移动，移动结束后切换动画形态，开始攻击循环，原地待命（不漂移）
///   Retreat  → 向右加速离场，出屏禁用
/// </summary>
public class CantianCannon : MonoBehaviour, ISecretBoss
{
    [Header("秘密关卡-入场")]
    [SerializeField] private float enterSpeed = 3f;
    [SerializeField] private float enterDuration = 3f;

    [Header("秘密关卡-子物体移动")]
    [SerializeField] private Transform movingChild;           // 需要向下移动的子物体（参天炮台底部）
    [SerializeField] private float childMoveDistance = 3f;    // 子物体向下移动距离
    [SerializeField] private float childMoveDuration = 2f;    // 子物体向下移动持续时间

    [Header("秘密关卡-动画切换")]
    [SerializeField] private string activateAnimName = "Activate2"; // 子物体移动结束后播放的动画状态名

    [Header("秘密关卡-攻击")]
    [SerializeField] private float initialAttackDelay = 1f;   // 首次攻击延迟（子物体就绪后等待）
    [SerializeField] private float attackInterval = 3f;       // 攻击间隔（秒）
    [SerializeField] [Range(0f, 1f)] private float attackAProbability = 0.5f; // A攻击触发概率
    [SerializeField] private string at1TriggerName = "At1";   // A攻击 Animator Trigger 参数名
    [SerializeField] private string at2TriggerName = "At2";   // B攻击 Animator Trigger 参数名
    [Header("  A攻击-发射")]
    [SerializeField] private Transform firePointA;            // A攻击发射点
    [SerializeField] private Transform aimPointA;             // A攻击方向对准点（子弹朝此点方向飞）
    [SerializeField] private GameObject bulletPrefabA1;       // A攻击子弹1
    [SerializeField] private GameObject bulletPrefabA2;       // A攻击子弹2

    [Header("  B攻击-发射")]
    [SerializeField] private Transform firePointB;            // B攻击发射点
    [SerializeField] private Transform aimPointB;             // B攻击方向对准点
    [SerializeField] private GameObject bulletPrefabB;        // B攻击子弹

    [Header("  通用")]
    [SerializeField] private int bulletDamage = 10;           // 子弹伤害
    [SerializeField] private float bulletSpeed = 5f;          // 子弹速度

    [Header("秘密关卡-撤退")]
    [SerializeField] private float retreatSpeed = 6f;
    [SerializeField] private float retreatScreenMargin = 1.3f;

    private enum CannonState { Entering, Idle, Retreat }
    private CannonState currentState;

    private float enterElapsed;
    private Camera mainCam;
    private Vector3 childStartLocalPos;
    private Vector3 childTargetLocalPos;
    private Animator childAnimator;  // 子物体上的 Animator（运行时获取，控制底部动画）
    private Animator selfAnimator;   // 父物体自身的 Animator（控制攻击动画 At1/At2）

    private Coroutine enterCoroutine;
    private Coroutine childMoveCoroutine;
    private Coroutine retreatCoroutine;
    private Coroutine attackCoroutine;
    private bool childReady;  // 子物体就绪后才开始攻击

    private void Awake()
    {
        mainCam = Camera.main;
        selfAnimator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        currentState = CannonState.Entering;
        enterElapsed = 0f;

        if (enterCoroutine != null) StopCoroutine(enterCoroutine);
        enterCoroutine = StartCoroutine(EnterTimeoutRoutine());
    }

    private void Start()
    {
        // 订阅放 Start（保证 LevelEventBus.Awake 已执行，Instance 不为 null）
        LevelEventBus.On("CantianCannonMiddleDestroyed", OnMiddleDestroyed);
    }

    private void OnDisable()
    {
        if (enterCoroutine != null) { StopCoroutine(enterCoroutine); enterCoroutine = null; }
        if (childMoveCoroutine != null) { StopCoroutine(childMoveCoroutine); childMoveCoroutine = null; }
        if (retreatCoroutine != null) { StopCoroutine(retreatCoroutine); retreatCoroutine = null; }
        if (attackCoroutine != null) { StopCoroutine(attackCoroutine); attackCoroutine = null; }

        // 取消监听
        LevelEventBus.Off("CantianCannonMiddleDestroyed", OnMiddleDestroyed);
    }

    private void Update()
    {
        switch (currentState)
        {
            case CannonState.Entering:
                enterElapsed += Time.deltaTime;
                if (enterElapsed >= enterDuration)
                {
                    currentState = CannonState.Idle;
                    StartChildMoveDown();
                }
                else
                {
                    transform.Translate(enterSpeed * Time.deltaTime * Vector3.left);
                }
                break;

            case CannonState.Idle:
                // 原地待命，不漂移
                break;

            case CannonState.Retreat:
                transform.Translate(retreatSpeed * Time.deltaTime * Vector3.right);
                if (mainCam != null && mainCam.WorldToViewportPoint(transform.position).x > retreatScreenMargin)
                    gameObject.SetActive(false);
                break;
        }
    }

    /// <summary>
    /// 入场超时兜底协程
    /// </summary>
    private IEnumerator EnterTimeoutRoutine()
    {
        yield return new WaitForSeconds(enterDuration);
        if (currentState == CannonState.Entering)
        {
            currentState = CannonState.Idle;
            StartChildMoveDown();
        }
    }

    /// <summary>
    /// 激活子物体并向下移动，移动结束后切换动画形态
    /// </summary>
    private void StartChildMoveDown()
    {
        if (movingChild == null)
        {
            Debug.LogWarning("CantianCannon: movingChild 未指定，跳过子物体移动");
            return;
        }

        // 激活子物体，运行时获取其 Animator（子物体不挂单独脚本）
        movingChild.gameObject.SetActive(true);
        childAnimator = movingChild.GetComponent<Animator>();

        if (childMoveCoroutine != null) StopCoroutine(childMoveCoroutine);
        childMoveCoroutine = StartCoroutine(ChildMoveDownRoutine());
    }

    /// <summary>
    /// 子物体向下移动协程：插值移动到目标位置，结束后切换动画
    /// </summary>
    private IEnumerator ChildMoveDownRoutine()
    {
        childStartLocalPos = movingChild.localPosition;
        childTargetLocalPos = childStartLocalPos + Vector3.down * childMoveDistance;

        float elapsed = 0f;
        while (elapsed < childMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / childMoveDuration;
            movingChild.localPosition = Vector3.Lerp(childStartLocalPos, childTargetLocalPos, t);
            yield return null;
        }
        movingChild.localPosition = childTargetLocalPos;

        // 子物体移动结束，切换动画形态
        SwitchAnimationState();
    }

    /// <summary>
    /// 切换子物体动画形态（子物体移动完成后调用），然后启动攻击循环
    /// </summary>
    private void SwitchAnimationState()
    {
        if (childAnimator != null && !string.IsNullOrEmpty(activateAnimName))
        {
            childAnimator.Play(activateAnimName);
        }

        // 子物体就绪，开始攻击循环
        childReady = true;
        if (attackCoroutine != null) StopCoroutine(attackCoroutine);
        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    /// <summary>
    /// 攻击循环协程：每隔 attackInterval 秒随机触发 At1 或 At2 攻击动画
    /// </summary>
    private IEnumerator AttackRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(attackInterval);

            if (!childReady || currentState != CannonState.Idle) continue;

            // 随机选择 A 或 B 攻击
            if (Random.value < attackAProbability)
                TriggerAttackA();
            else
                TriggerAttackB();
        }
    }

    /// <summary>
    /// 触发 A 攻击：设置父物体 Animator Trigger At1
    /// </summary>
    private void TriggerAttackA()
    {
        if (selfAnimator != null)
            selfAnimator.SetTrigger(at1TriggerName);
    }

    /// <summary>
    /// 触发 B 攻击：设置父物体 Animator Trigger At2
    /// </summary>
    private void TriggerAttackB()
    {
        if (selfAnimator != null)
            selfAnimator.SetTrigger(at2TriggerName);
    }

    // ──────────── 动画事件接口（由 Animation Event 调用）────────────

    /// <summary>
    /// A 攻击子弹发射 —— 由动画事件调用，随机发射 bulletPrefabA1 或 bulletPrefabA2
    /// </summary>
    public void FireBulletA()
    {
        if (firePointA == null) return;

        // 随机选一颗子弹
        GameObject prefab = Random.value < 0.5f ? bulletPrefabA1 : bulletPrefabA2;
        if (prefab == null) return;

        Vector2 dir = GetAimDirection(firePointA, aimPointA);
        FireBullet(prefab, firePointA.position, dir);
    }

    /// <summary>
    /// B 攻击子弹发射 —— 由动画事件调用，发射 bulletPrefabB
    /// </summary>
    public void FireBulletB()
    {
        if (firePointB == null || bulletPrefabB == null) return;

        Vector2 dir = GetAimDirection(firePointB, aimPointB);
        FireBullet(bulletPrefabB, firePointB.position, dir);
    }

    /// <summary>
    /// 根据发射点和对准点计算方向
    /// </summary>
    private Vector2 GetAimDirection(Transform firePoint, Transform aimPoint)
    {
        if (aimPoint != null)
            return (aimPoint.position - firePoint.position).normalized;
        return Vector2.left; // 兜底：默认向左
    }

    /// <summary>
    /// 通用子弹发射
    /// </summary>
    private void FireBullet(GameObject prefab, Vector3 position, Vector2 direction)
    {
        GameObject bullet = PoolManager.Release(prefab, position, Quaternion.identity);
        if (bullet == null) return;

        var eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(bulletDamage);
            eb.SetMoveDirection(direction);
            eb.SetMoveSpeed(bulletSpeed);
        }
    }

    /// <summary>
    /// ISecretBoss 接口：由关卡事件系统调用，触发撤退
    /// </summary>
    public void TriggerRetreat()
    {
        if (currentState == CannonState.Retreat) return;
        currentState = CannonState.Retreat;
        childReady = false;

        if (attackCoroutine != null) { StopCoroutine(attackCoroutine); attackCoroutine = null; }
        if (retreatCoroutine != null) StopCoroutine(retreatCoroutine);
        retreatCoroutine = StartCoroutine(RetreatTimeoutRoutine());
    }

    /// <summary>
    /// 撤退超时兜底：10秒后若仍未出屏，强制禁用
    /// </summary>
    private IEnumerator RetreatTimeoutRoutine()
    {
        yield return new WaitForSeconds(10f);
        if (currentState == CannonState.Retreat)
            gameObject.SetActive(false);
    }

    /// <summary>
    /// 外部可直接调用禁用
    /// </summary>
    public void DisableCannon()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 参天炮台中部摧毁回调 —— CantianCannonMiddle 爆炸后，CantianCannon 禁用自身
    /// </summary>
    private void OnMiddleDestroyed(object data)
    {
        gameObject.SetActive(false);
    }
}
