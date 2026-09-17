using System.Collections;
using UnityEngine;

/// <summary>
/// 黄金大象 —— 秘密关卡特供敌人。
/// 继承 Enemy，实现 ISecretBoss。
///
/// 行为：
///   Entering  → 入场向左移动，到达时长后进入漂移
///   Wandering → 在锚点周围随机漂移
///   Retreat   → 收到撤退命令后向右加速离场，出屏后禁用
/// </summary>
public class GoldenElephant : Enemy, ISecretBoss
{
    [Header("黄金大象-入场")]
    [SerializeField] private float enterDuration = 3f;

    [Header("黄金大象-随机漂移")]
    [SerializeField] private float wanderRadius = 0.8f;
    [SerializeField] private float wanderSpeed = 0.5f;
    [SerializeField] private float changeDirectionInterval = 2f;

    [Header("黄金大象-撤退")]
    [SerializeField] private float retreatSpeed = 4f;

    [Header("黄金大象-子弹组发射")]
    [SerializeField] private GameObject bulletGroupPrefab;       // 挂载 BulletGroupController 的预制体
    [SerializeField] private Transform firePoint;                // 发射点（留空则使用自身位置）
    [SerializeField] private int bulletGroupDamage = 20;         // 伤害A：子子弹伤害值
    [SerializeField] private float bulletGroupSpeed = 2f;        // 子弹组移动速度
    [SerializeField] private float initialFireDelay = 2f;        // 首次发射前的启动时间（秒），所有攻击方式共用
    [SerializeField] private float fireInterval = 3f;            // 发射间隔（秒）
    [SerializeField] private float fanAngle = 90f;               // 扇形总角度（度），以 Vector2.left 为中心
    [SerializeField] private float fanRadius = 2f;               // 扇形半径（发射位置的散布距离）

    [Header("黄金大象-扇形子弹（两排）")]
    [SerializeField] private GameObject fanBulletPrefab;         // 扇形子弹预制体（挂载 EnemyBullet）
    [SerializeField] private int fanBulletCountRow1 = 6;         // 第一排子弹数量
    [SerializeField] private int fanBulletCountRow2 = 4;         // 第二排子弹数量（较少，角度插在第一排中间）
    [SerializeField] private Transform fanBulletFirePoint;       // 扇形子弹专用发射点（留空则使用自身位置）
    [SerializeField] private float fanBulletAngle = 120f;        // 扇形总角度（度），以 Vector2.left 为中心
    [SerializeField] private float fanBulletSpeed = 3f;          // 扇形子弹移动速度
    [SerializeField] private int fanBulletDamage = 15;           // 扇形子弹伤害
    [SerializeField] private float rowSpacing = 0.8f;            // 两排子弹之间的前后间距（靠后一排向右偏移）
    [SerializeField] private float fanBulletInterval = 4f;       // 扇形子弹发射间隔（独立CD，不共用旋转子弹组CD）

    [Header("黄金大象-斜角扇形攻击（左上/左下）")]
    [SerializeField] private GameObject altFanBulletPrefabUp;     // 朝左上的扇形子弹预制体
    [SerializeField] private GameObject altFanBulletPrefabDown;   // 朝左下的扇形子弹预制体
    [SerializeField] private Transform altFanFirePoint;           // 斜角扇形专用发射点（留空则使用自身位置）
    [SerializeField] private int altFanBulletCount = 5;           // 子弹数量
    [SerializeField] private float altFanBulletAngle = 60f;       // 扇形总角度（度）
    [SerializeField] private float altFanBulletSpeed = 3f;        // 子弹移动速度
    [SerializeField] private int altFanBulletDamage = 12;         // 子弹伤害
    [SerializeField] private float altFanCenterAngleUp = 150f;    // 左上扇形中心角度（度，180=正左，90=正上）
    [SerializeField] private float altFanCenterAngleDown = 210f;  // 左下扇形中心角度（度，180=正左，270=正下）
    [SerializeField] private float altFanBulletInterval = 5f;     // 发射间隔（独立CD）

    [Header("黄金大象-激光")]
    [SerializeField] private GameObject laserEffectObject;        // 激光特效物体（挂 LaserEffect，初始 inactive）
    [SerializeField] private GameObject[] laserBeams;             // 激光束（和 LaserEffect.lasers 是同一个对象）
    [SerializeField] private float laserInitialDelay = 6f;        // 首次发射延迟（独立CD）
    [SerializeField] private float laserInterval = 12f;           // 发射间隔（独立CD）
    [SerializeField] private float laserScreenMargin = 0.5f;      // 激光距屏幕边缘余量
    [SerializeField] private float laserMinDistFromPlayer = 2f;   // 激光离玩家的最小距离

    private enum ElephantState
    {
        Entering,
        Wandering,
        Retreat,
    }
    private ElephantState currentState;

    private Coroutine wanderCoroutine;
    private Coroutine retreatCoroutine;
    private Coroutine bulletGroupFireCoroutine;
    private Coroutine fanBulletFireCoroutine;
    private Coroutine altFanFireCoroutine;
    private Coroutine laserFireCoroutine;
    private Transform emitPoint;
    private Transform fanEmitPoint;
    private Transform altFanEmitPoint;
    private Vector2 initialPosition;
    private Vector2 wanderTarget;
    private Camera mainCam;
    private WaitForSeconds enterWait;
    private WaitForSeconds changeDirectionWait;

    protected override void Awake()
    {
        base.Awake();
        mainCam = Camera.main;
        emitPoint = firePoint != null ? firePoint : transform;
        fanEmitPoint = fanBulletFirePoint != null ? fanBulletFirePoint : transform;
        altFanEmitPoint = altFanFirePoint != null ? altFanFirePoint : transform;
        enterWait = new WaitForSeconds(enterDuration);
        changeDirectionWait = new WaitForSeconds(changeDirectionInterval);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        initialPosition = transform.position;
        wanderTarget = transform.position;
        currentState = ElephantState.Entering;
        StartCoroutine(EnterRoutine());

        // 启动子弹组发射协程
        if (bulletGroupFireCoroutine != null) StopCoroutine(bulletGroupFireCoroutine);
        bulletGroupFireCoroutine = StartCoroutine(FireBulletGroupRoutine());

        // 启动扇形子弹发射协程（独立CD，但共用 initialFireDelay）
        if (fanBulletFireCoroutine != null) StopCoroutine(fanBulletFireCoroutine);
        fanBulletFireCoroutine = StartCoroutine(FireFanBulletRoutine());

        // 启动斜角扇形攻击协程（独立CD，但共用 initialFireDelay）
        if (altFanFireCoroutine != null) StopCoroutine(altFanFireCoroutine);
        altFanFireCoroutine = StartCoroutine(FireAltFanRoutine());

        // 启动激光协程（独立CD，直接激活 LaserEffect）
        if (laserFireCoroutine != null) StopCoroutine(laserFireCoroutine);
        laserFireCoroutine = StartCoroutine(FireLaserRoutine());
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
        yield return enterWait;

        initialPosition = transform.position;
        wanderTarget = transform.position;
        currentState = ElephantState.Wandering;

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
            yield return changeDirectionWait;

            if (gameObject.activeSelf && currentState == ElephantState.Wandering && !isDie)
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
            case ElephantState.Entering:
                transform.Translate(moveSpeed * Time.deltaTime * Vector2.left);
                break;

            case ElephantState.Wandering:
                Vector2 newPos = Vector2.MoveTowards(
                    transform.position,
                    wanderTarget,
                    wanderSpeed * Time.deltaTime
                );
                transform.position = newPos;
                break;

            case ElephantState.Retreat:
                Retreat();
                break;
        }
    }

    public override void OnAttack()
    {
        // 攻击由 FireBulletGroupRoutine 协程驱动，不在此处每帧触发
    }

    /// <summary>
    /// 子弹组发射协程：每隔 fireInterval 秒，在发射点的扇形区域内随机位置生成一组子弹。
    /// 父物体移动方向随机但整体向左（在扇形角度范围内）。
    /// </summary>
    private IEnumerator FireBulletGroupRoutine()
    {
        if (bulletGroupPrefab == null) yield break;

        // 首次发射前等待启动时间
        if (initialFireDelay > 0f)
            yield return new WaitForSeconds(initialFireDelay);

        WaitForSeconds wait = new WaitForSeconds(fireInterval);

        // 首次发射前也等一轮自己的 CD
        yield return wait;

        while (true)
        {
            if (gameObject.activeSelf && !isDie)
            {
                FireOneBulletGroup();
            }
            yield return wait;
        }
    }

    /// <summary>
    /// 发射一组子弹：在扇形区域内随机位置生成，随机方向但整体向左。
    /// </summary>
    private void FireOneBulletGroup()
    {
        float halfFan = fanAngle * 0.5f;
        float randomAngle = Random.Range(-halfFan, halfFan);          // 扇形内随机角度
        float randomDist = Random.Range(0f, fanRadius);               // 扇形内随机距离
        float rad = (180f + randomAngle) * Mathf.Deg2Rad;             // 向左为基准
        Vector2 spawnOffset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * randomDist;
        Vector2 spawnPos = (Vector2)emitPoint.position + spawnOffset;

        // 从对象池取出子弹组父物体
        GameObject group = PoolManager.Release(bulletGroupPrefab, spawnPos, Quaternion.identity);
        if (group == null) return;

        // 设置父物体移动方向：随机但整体向左，伤害由大象提供
        BulletGroupController controller = group.GetComponent<BulletGroupController>();
        if (controller != null)
        {
            float dirAngle = Random.Range(-halfFan, halfFan);
            float dirRad = (180f + dirAngle) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(dirRad), Mathf.Sin(dirRad));
            controller.Initialize(dir, bulletGroupDamage, bulletGroupSpeed);
        }
    }

    /// <summary>
    /// 扇形子弹发射协程：每隔 fanBulletInterval 秒，一次性发射两排扇形子弹。
    /// 独立CD，不共用旋转子弹组的 fireInterval，但受 initialFireDelay 约束。
    /// 扇形以 Vector2.down 为中心，子弹均匀分布。
    /// </summary>
    private IEnumerator FireFanBulletRoutine()
    {
        if (fanBulletPrefab == null) yield break;

        // 首次发射前等待启动时间（与旋转子弹组共用 initialFireDelay）
        if (initialFireDelay > 0f)
            yield return new WaitForSeconds(initialFireDelay);

        WaitForSeconds wait = new WaitForSeconds(fanBulletInterval);

        // 首次发射前也等一轮自己的 CD
        yield return wait;

        while (true)
        {
            if (gameObject.activeSelf && !isDie)
            {
                FireFanBullets();
            }
            yield return wait;
        }
    }

    /// <summary>
    /// 一次性发射两排扇形子弹。
    /// 两排子弹以专用发射点为中心、沿垂直方向偏移 rowSpacing，上下各一排。
    /// 第一排 fanBulletCountRow1 颗子弹在扇形角度内均匀分布；
    /// 第二排 fanBulletCountRow2 颗子弹的扇形角度插在第一排中间。
    /// 扇形整体向左（Vector2.left），覆盖 fanBulletAngle 范围。
    /// 所有子弹在同一帧生成，实现"一次发射出来"的效果。
    /// </summary>
    private void FireFanBullets()
    {
        float halfFan = fanBulletAngle * 0.5f;
        Vector2 emitPos = (Vector2)fanEmitPoint.position;

        // 第一排的角步长，第二排共用此步长才能"见缝插针"
        float stepRow1 = (fanBulletCountRow1 > 1) ? fanBulletAngle / (fanBulletCountRow1 - 1) : 0f;

        // 两排：前排在左(row=0)，后排靠右(row=1)
        for (int row = 0; row < 2; row++)
        {
            float rowOffset = (row == 0) ? -rowSpacing * 0.5f : rowSpacing * 0.5f;
            Vector2 rowOrigin = emitPos + Vector2.right * rowOffset;

            int count = (row == 0) ? fanBulletCountRow1 : fanBulletCountRow2;

            for (int i = 0; i < count; i++)
            {
                float angleOffset;
                if (row == 0)
                {
                    // 第一排：均匀覆盖整个扇形 [-halfFan, +halfFan]
                    float t = (count > 1) ? (float)i / (count - 1) : 0.5f;
                    angleOffset = Mathf.Lerp(-halfFan, halfFan, t);
                }
                else
                {
                    // 第二排：共用第一排步长，偏移半个步长，见缝插针
                    // 从第1个缝隙开始（i=0 → 缝隙0），取中间 count 个缝隙
                    float gapStart = -halfFan + stepRow1 * 0.5f;
                    angleOffset = gapStart + i * stepRow1;
                }

                // Vector2.left 为扇形中心，对应 180°
                float rad = (180f + angleOffset) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                GameObject bullet = PoolManager.Release(fanBulletPrefab, rowOrigin, Quaternion.identity);
                if (bullet != null)
                {
                    EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
                    if (eb != null)
                    {
                        eb.SetMoveDirection(dir);
                        eb.SetMoveSpeed(fanBulletSpeed);
                        eb.SetDamage(fanBulletDamage);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 斜角扇形攻击协程：每隔 altFanBulletInterval 秒，随机朝左上或左下发射扇形子弹。
    /// 独立CD，不共用其他攻击方式的CD，但受 initialFireDelay 约束。
    /// 上下方向使用不同的子弹预制体。
    /// </summary>
    private IEnumerator FireAltFanRoutine()
    {
        if (altFanBulletPrefabUp == null && altFanBulletPrefabDown == null) yield break;

        // 首次发射前等待启动时间（共用 initialFireDelay）
        if (initialFireDelay > 0f)
            yield return new WaitForSeconds(initialFireDelay);

        WaitForSeconds wait = new WaitForSeconds(altFanBulletInterval);

        // 首次发射前也等一轮自己的 CD
        yield return wait;

        while (true)
        {
            if (gameObject.activeSelf && !isDie)
            {
                FireAltFan();
            }
            yield return wait;
        }
    }

    /// <summary>
    /// 随机朝左上或左下发射一扇 5 发扇形子弹，60° 范围。
    /// 上下方向使用不同的子弹预制体，按概率交替。
    /// </summary>
    private void FireAltFan()
    {
        // 随机选择方向：true=左上，false=左下
        bool shootUp = Random.value > 0.5f;

        GameObject prefab = shootUp ? altFanBulletPrefabUp : altFanBulletPrefabDown;
        if (prefab == null) return;

        float centerAngle = shootUp ? altFanCenterAngleUp : altFanCenterAngleDown;
        float halfFan = altFanBulletAngle * 0.5f;
        Vector2 emitPos = (Vector2)altFanEmitPoint.position;
        int count = altFanBulletCount;

        for (int i = 0; i < count; i++)
        {
            float t = (count > 1) ? (float)i / (count - 1) : 0.5f;
            float angleOffset = Mathf.Lerp(-halfFan, halfFan, t);
            float rad = (centerAngle + angleOffset) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            GameObject bullet = PoolManager.Release(prefab, emitPos, Quaternion.identity);
            if (bullet != null)
            {
                EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
                if (eb != null)
                {
                    eb.SetMoveDirection(dir);
                    eb.SetMoveSpeed(altFanBulletSpeed);
                    eb.SetDamage(altFanBulletDamage);
                }
            }
        }
    }

    /// <summary>
    /// 激光协程：先挪激光到随机 X，再激活特效。
    /// LaserEffect 由动画事件触发 ActivateLaser，激光已在正确位置。
    /// </summary>
    private IEnumerator FireLaserRoutine()
    {
        if (laserEffectObject == null) yield break;

        if (laserInitialDelay > 0f)
            yield return new WaitForSeconds(laserInitialDelay);

        WaitForSeconds wait = new WaitForSeconds(laserInterval);
        yield return wait;

        while (true)
        {
            if (gameObject.activeSelf && !isDie)
            {
                // 玩家死亡时跳过（获取不到位置）
                var request = new PlayerPositionRequest();
                LevelEventBus.Trigger("RequestPlayerPosition", request);
                if (request.HasPosition)
                {
                    laserEffectObject.SetActive(true);
                    yield return null;
                    PositionLaserBeams(request.Position.x);
                }
            }
            yield return wait;
        }
    }

    private void PositionLaserBeams(float playerX)
    {
        if (laserBeams == null || laserBeams.Length == 0) return;

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        float camHalfH = mainCam.orthographicSize;
        float camHalfW = camHalfH * mainCam.aspect;
        float camX = mainCam.transform.position.x;

        float minX = camX - camHalfW + laserScreenMargin;
        float maxX = Mathf.Min(camX + camHalfW - laserScreenMargin, transform.position.x);
        if (maxX <= minX) maxX = minX + 0.1f;

        float rx = Random.Range(minX, maxX);

        if (laserMinDistFromPlayer > 0f)
        {
            float dist = Mathf.Abs(rx - playerX);
            if (dist < laserMinDistFromPlayer)
            {
                float leftCandidate  = playerX - laserMinDistFromPlayer;
                float rightCandidate = playerX + laserMinDistFromPlayer;

                if (leftCandidate >= minX && rightCandidate <= maxX)
                    rx = Random.value > 0.5f ? leftCandidate : rightCandidate;
                else if (leftCandidate >= minX)
                    rx = leftCandidate;
                else if (rightCandidate <= maxX)
                    rx = rightCandidate;
            }
        }

        rx = Mathf.Clamp(rx, minX, maxX);

        foreach (GameObject beam in laserBeams)
        {
            if (beam == null) continue;
            Vector3 pos = beam.transform.position;
            pos.x = rx;
            beam.transform.position = pos;
        }
    }

    /// <summary>
    /// 由外部调用（ISecretBoss 接口），触发撤退：向右移动直到出屏后禁用
    /// </summary>
    public void TriggerRetreat()
    {
        if (currentState == ElephantState.Retreat) return;
        currentState = ElephantState.Retreat;
    }

    protected override void Retreat()
    {
        if (retreatCoroutine != null) return;

        if (wanderCoroutine != null)
        {
            StopCoroutine(wanderCoroutine);
            wanderCoroutine = null;
        }

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

    protected override void OnDeath()
    {
        base.OnDeath();
    }

    protected override void OnExitScreen()
    {
        CleanupCoroutines();
        base.OnExitScreen();
    }

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
        if (bulletGroupFireCoroutine != null)
        {
            StopCoroutine(bulletGroupFireCoroutine);
            bulletGroupFireCoroutine = null;
        }
        if (fanBulletFireCoroutine != null)
        {
            StopCoroutine(fanBulletFireCoroutine);
            fanBulletFireCoroutine = null;
        }
        if (altFanFireCoroutine != null)
        {
            StopCoroutine(altFanFireCoroutine);
            altFanFireCoroutine = null;
        }
        if (laserFireCoroutine != null)
        {
            StopCoroutine(laserFireCoroutine);
            laserFireCoroutine = null;
        }
        if (flashRedCoroutine != null)
        {
            StopCoroutine(flashRedCoroutine);
            flashRedCoroutine = null;
        }
    }
}
