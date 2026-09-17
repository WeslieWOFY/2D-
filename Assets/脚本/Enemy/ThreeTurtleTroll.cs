using System.Collections;
using UnityEngine;

/// <summary>
/// 三龟巨魔 —— 秘密关卡 BOSS。继承 Enemy，实现 ISecretBoss。
/// 整体平移移动，子对象 foot 通过 Animator.Play(状态名) 切换 left/right/jump。
/// </summary>
public class ThreeTurtleTroll : Enemy, ISecretBoss
{
    [Header("入场")]
    [SerializeField] private float enterSpeed = 4f;
    [SerializeField] private float enterDuration = 2f;   // 入场时长：期间纯左走，无其他动作

    [Header("巡逻边界（世界坐标 X）")]
    [SerializeField] private float leftBoundaryX = -4f;
    [SerializeField] private float rightBoundaryX = 4f;

    [Header("跳跃")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float jumpDuration = 0.5f;

    [Header("加速（落地后有概率触发）")]
    [Range(0f, 1f)] [SerializeField] private float accelerateChance = 0.5f;
    [SerializeField] private float accelerateSpeed = 6f;   // 加速时的移动速度（独立于平时速度）

    [Header("撤退")]
    [SerializeField] private float retreatSpeed = 6f;
    [SerializeField] private float retreatScreenMargin = 1.2f;

    [Header("子对象")]
    [SerializeField] private Transform foot;

    [Header("环形子弹攻击")]
    [SerializeField] private GameObject bulletPrefab;       // 子弹预制体
    [SerializeField] private Transform firePoint;           // 发射点
    [SerializeField] private float initialFireDelay = 2f;   // 初始发射时间（入场后多久开始发射）
    [SerializeField] private float fireInterval = 3f;       // 发射间隔
    [SerializeField] private int bulletCount = 24;          // 环形子弹数量
    [SerializeField] private float bulletSpeed = 3f;        // 子弹速度
    [SerializeField] private int bulletDamage = 10;         // 子弹伤害

    [Header("子弹组发射")]
    [SerializeField] private GameObject bulletGroupPrefab;           // 挂载 BulletGroupController 的预制体
    [SerializeField] private Transform bulletGroupFirePoint;         // 子弹组发射点（留空则使用自身位置）
    [SerializeField] private int bulletGroupDamage = 20;             // 子弹组子子弹伤害
    [SerializeField] private float bulletGroupSpeed = 2f;            // 子弹组移动速度
    [SerializeField] private float bulletGroupInitialFireDelay = 2f;  // 子弹组初始发射时间
    [SerializeField] private float bulletGroupFireInterval = 4f;     // 子弹组发射间隔
    [SerializeField] private float bulletGroupFanAngle = 90f;        // 子弹组扇形总角度（度），以 Vector2.left 为中心
    [SerializeField] private float bulletGroupFanRadius = 2f;        // 子弹组扇形半径（发射位置散布距离）
    [SerializeField] private int bulletGroupMinBurst = 1;            // 连发最少次数
    [SerializeField] private int bulletGroupMaxBurst = 3;            // 连发最多次数
    [SerializeField] private float bulletGroupBurstDelay = 0.3f;     // 连发间隔

    [Header("扇形子弹")]
    [SerializeField] private GameObject fanBulletPrefab;             // 扇形子弹预制体
    [SerializeField] private int fanBulletCount = 5;                 // 扇形子弹数量
    [SerializeField] private float fanBulletAngle = 90f;             // 扇形总角度（度），以 Vector2.left 为中心
    [SerializeField] private float fanBulletSpeed = 3f;              // 扇形子弹速度
    [SerializeField] private int fanBulletDamage = 15;               // 扇形子弹伤害
    [SerializeField] private int fanMinBurst = 1;                    // 扇形连发最少次数
    [SerializeField] private int fanMaxBurst = 3;                    // 扇形连发最多次数
    [SerializeField] private float fanBurstDelay = 0.3f;             // 扇形连发间隔

    [Header("碎石攻击")]
    [SerializeField] private GameObject rockPrefab;                   // 碎石预制体（挂载 EnemyBullet，方向向下）
    [SerializeField] private GameObject rockEffectPrefab;             // 碎石预警特效预制体
    [SerializeField] private float rockEffectDuration = 0.5f;         // 预警特效持续时长（秒），结束后才出碎石
    [SerializeField] private int rockMinCount = 5;                    // 每次最少碎石数
    [SerializeField] private int rockMaxCount = 10;                   // 每次最多碎石数
    [SerializeField] private float rockSpeed = 5f;                    // 碎石下落速度
    [SerializeField] private int rockDamage = 20;                     // 碎石伤害
    [SerializeField] private float rockInitialDelay = 5f;             // 碎石初始发射时间
    [SerializeField] private float rockFireInterval = 6f;             // 碎石发射间隔
    [SerializeField] private float rockSpawnDelay = 0.1f;            // 每颗碎石之间的召唤间隔

    private enum TrollState { Entering, Patrolling, Retreating }
    private TrollState currentState;

    private float baseMoveSpeed;
    private float currentSpeed;
    private int currentDir;            // -1=左 +1=右
    private float groundY;
    private float centerX;
    private float enterElapsed;

    private bool isJumping;
    private bool jumpedThisLeg;
    private float jumpElapsed;

    private Animator footAnimator;
    private string currentFootState;
    private Camera mainCam;

    private Coroutine fireCoroutine;
    private Coroutine bulletGroupFireCoroutine;
    private Coroutine rockFireCoroutine;

    protected override void Awake()
    {
        base.Awake();
        mainCam = Camera.main;
        baseMoveSpeed = moveSpeed;
        if (foot != null) footAnimator = foot.GetComponent<Animator>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        groundY = transform.position.y;
        centerX = (leftBoundaryX + rightBoundaryX) * 0.5f;
        currentDir = -1;
        currentSpeed = enterSpeed;
        enterElapsed = 0f;
        isJumping = false;
        jumpedThisLeg = false;
        jumpElapsed = 0f;
        currentFootState = null;
        currentState = TrollState.Entering;
        ApplyFoot("left");
    }

    public override void OnMove()
    {
        if (isDie) return;
        switch (currentState)
        {
            case TrollState.Entering:   EnterTick();   break;
            case TrollState.Patrolling: PatrolTick();  break;
            case TrollState.Retreating: RetreatTick(); break;
        }
    }

    public override void OnAttack() { }

    // ── 入场：向左走，foot=left，到右边界后进入巡逻 ──
    // ── 入场：固定时长纯左走，期间无任何其他动作 ──
    private void EnterTick()
    {
        enterElapsed += Time.deltaTime;
        Vector3 pos = transform.position;
        pos.x -= currentSpeed * Time.deltaTime;
        pos.y = groundY;
        transform.position = pos;
        if (enterElapsed >= enterDuration)
            StartPatrol();
    }

    // ── 巡逻：左右边界循环，每段中途跳一次，落地后有概率加速 ──
    private void StartPatrol()
    {
        currentState = TrollState.Patrolling;
        currentDir = -1;
        currentSpeed = baseMoveSpeed;
        jumpedThisLeg = false;
        isJumping = false;
        ApplyFoot("left");

        // 开始环形子弹发射
        if (fireCoroutine != null) StopCoroutine(fireCoroutine);
        fireCoroutine = StartCoroutine(FireRoutine());

        // 开始子弹组发射
        if (bulletGroupFireCoroutine != null) StopCoroutine(bulletGroupFireCoroutine);
        bulletGroupFireCoroutine = StartCoroutine(FireBulletGroupRoutine());

        // 开始碎石攻击
        if (rockFireCoroutine != null) StopCoroutine(rockFireCoroutine);
        rockFireCoroutine = StartCoroutine(FireRockRoutine());
    }

    private void PatrolTick()
    {
        float dt = Time.deltaTime;
        Vector3 pos = transform.position;
        pos.x += currentDir * currentSpeed * dt;

        // 越过中点 → 起跳（每段一次）
        if (!isJumping && !jumpedThisLeg)
        {
            bool crossed = (currentDir < 0 && pos.x <= centerX) ||
                           (currentDir > 0 && pos.x >= centerX);
            if (crossed) StartJump();
        }

        // 跳跃纵向（抛物线）
        if (isJumping)
        {
            jumpElapsed += dt;
            float p = Mathf.Clamp01(jumpElapsed / jumpDuration);
            pos.y = groundY + jumpHeight * 4f * p * (1f - p);
            if (jumpElapsed >= jumpDuration)
            {
                pos.y = groundY;
                OnJumpLand();
            }
        }
        else pos.y = groundY;
        transform.position = pos;

        // 边界变向（变向时速度恢复）
        if (!isJumping)
        {
            if (currentDir < 0 && pos.x <= leftBoundaryX)
            {
                pos.x = leftBoundaryX; transform.position = pos;
                TurnAround(+1, "right");
            }
            else if (currentDir > 0 && pos.x >= rightBoundaryX)
            {
                pos.x = rightBoundaryX; transform.position = pos;
                TurnAround(-1, "left");
            }
        }
    }

    private void StartJump()
    {
        isJumping = true;
        jumpedThisLeg = true;
        jumpElapsed = 0f;
        ApplyFoot("jump");
    }

    private void OnJumpLand()
    {
        isJumping = false;
        // 跳下来才有概率加速
        currentSpeed = Random.value < accelerateChance
            ? accelerateSpeed
            : baseMoveSpeed;
        ApplyFoot(currentDir < 0 ? "left" : "right");
    }

    private void TurnAround(int newDir, string walkState)
    {
        currentDir = newDir;
        currentSpeed = baseMoveSpeed;   // 变向时速度恢复
        jumpedThisLeg = false;
        isJumping = false;
        Vector3 pos = transform.position; pos.y = groundY; transform.position = pos;
        ApplyFoot(walkState);
    }

    // ── 撤退：跳跃中立刻落下，foot=right 向右出屏 ──
    // ── 环形子弹发射 ──

    private IEnumerator FireRoutine()
    {
        yield return new WaitForSeconds(initialFireDelay);
        while (!isDie && currentState != TrollState.Retreating)
        {
            FireRing();
            yield return new WaitForSeconds(fireInterval);
        }
    }

    private Vector3 GetFireOrigin(Transform fp)
    {
        Vector3 pos = fp != null ? fp.position : transform.position;
        if (isJumping) pos.y = groundY;
        return pos;
    }

    private void FireRing()
    {
        if (bulletPrefab == null) return;

        Vector3 origin = GetFireOrigin(firePoint);
        float angleStep = 360f / bulletCount;

        for (int i = 0; i < bulletCount; i++)
        {
            float angle = i * angleStep;
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Quaternion rot = Quaternion.Euler(0f, 0f, angle - 90f);

            GameObject bullet = PoolManager.Release(bulletPrefab, origin, rot);
            if (bullet == null) continue;

            EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                eb.SetDamage(bulletDamage);
                eb.SetMoveDirection(dir);
                eb.SetMoveSpeed(bulletSpeed);
            }
        }
    }

    // ── 子弹组 / 扇形子弹 交替发射（纯随机选择） ──

    private IEnumerator FireBulletGroupRoutine()
    {
        if (bulletGroupPrefab == null && fanBulletPrefab == null) yield break;

        yield return new WaitForSeconds(bulletGroupInitialFireDelay);

        while (!isDie && currentState != TrollState.Retreating)
        {
            bool useGroup = Random.value > 0.5f;

            if (useGroup && bulletGroupPrefab != null)
            {
                int burstCount = Random.Range(bulletGroupMinBurst, bulletGroupMaxBurst + 1);
                for (int i = 0; i < burstCount; i++)
                {
                    if (isDie || currentState == TrollState.Retreating) yield break;
                    FireOneBulletGroup();
                    if (i < burstCount - 1)
                        yield return new WaitForSeconds(bulletGroupBurstDelay);
                }
            }
            else if (fanBulletPrefab != null)
            {
                int burstCount = Random.Range(fanMinBurst, fanMaxBurst + 1);
                for (int i = 0; i < burstCount; i++)
                {
                    if (isDie || currentState == TrollState.Retreating) yield break;
                    FireOneFan();
                    if (i < burstCount - 1)
                        yield return new WaitForSeconds(fanBurstDelay);
                }
            }

            yield return new WaitForSeconds(bulletGroupFireInterval);
        }
    }

    private void FireOneBulletGroup()
    {
        float halfFan = bulletGroupFanAngle * 0.5f;
        float randomAngle = Random.Range(-halfFan, halfFan);
        float randomDist = Random.Range(0f, bulletGroupFanRadius);
        float rad = (180f + randomAngle) * Mathf.Deg2Rad;
        Vector2 spawnOffset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * randomDist;
        Vector3 origin = GetFireOrigin(bulletGroupFirePoint);
        Vector2 spawnPos = (Vector2)origin + spawnOffset;

        GameObject group = PoolManager.Release(bulletGroupPrefab, spawnPos, Quaternion.identity);
        if (group == null) return;

        BulletGroupController controller = group.GetComponent<BulletGroupController>();
        if (controller != null)
        {
            float dirAngle = Random.Range(-halfFan, halfFan);
            float dirRad = (180f + dirAngle) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(dirRad), Mathf.Sin(dirRad));
            controller.Initialize(dir, bulletGroupDamage, bulletGroupSpeed);
        }
    }

    private void FireOneFan()
    {
        if (fanBulletPrefab == null) return;

        Vector2 origin = GetFireOrigin(bulletGroupFirePoint);
        float halfFan = fanBulletAngle * 0.5f;

        for (int i = 0; i < fanBulletCount; i++)
        {
            float t = (fanBulletCount > 1) ? (float)i / (fanBulletCount - 1) : 0.5f;
            float angleOffset = Mathf.Lerp(-halfFan, halfFan, t);
            float rad = (180f + angleOffset) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            GameObject bullet = PoolManager.Release(fanBulletPrefab, origin, Quaternion.identity);
            if (bullet == null) continue;

            EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                eb.SetMoveDirection(dir);
                eb.SetMoveSpeed(fanBulletSpeed);
                eb.SetDamage(fanBulletDamage);
            }
        }
    }

    // ── 碎石攻击（独立CD，随机X位置，Y顶端，先特效再落石） ──

    private IEnumerator FireRockRoutine()
    {
        if (rockPrefab == null) yield break;

        yield return new WaitForSeconds(rockInitialDelay);

        while (!isDie && currentState != TrollState.Retreating)
        {
            int count = Random.Range(rockMinCount, rockMaxCount + 1);

            // 随机生成 X 位置列表
            float[] xPosList = new float[count];
            if (mainCam != null)
            {
                float camHalfW = mainCam.orthographicSize * mainCam.aspect;
                float camX = mainCam.transform.position.x;
                float minX = camX - camHalfW + 1f;
                float maxX = camX + camHalfW - 1f;
                for (int i = 0; i < count; i++)
                    xPosList[i] = Random.Range(minX, maxX);
            }
            else
            {
                for (int i = 0; i < count; i++)
                    xPosList[i] = Random.Range(-5f, 5f);
            }

            // 屏幕顶端 Y（刚好在摄像机顶端）
            float topY = mainCam != null
                ? mainCam.ViewportToWorldPoint(new Vector3(0f, 1f, 0f)).y
                : 8f;

            // 逐个：特效 → 等待 → 碎石 → 间隔 → 下一个
            for (int i = 0; i < count; i++)
            {
                if (isDie || currentState == TrollState.Retreating) yield break;
                Vector3 spawnPos = new Vector3(xPosList[i], topY, 0f);

                // 播放预警特效
                if (rockEffectPrefab != null)
                    PoolManager.Release(rockEffectPrefab, spawnPos);

                yield return new WaitForSeconds(rockEffectDuration);

                // 召唤碎石
                GameObject rock = PoolManager.Release(rockPrefab, spawnPos, Quaternion.identity);
                if (rock != null)
                {
                    EnemyBullet eb = rock.GetComponent<EnemyBullet>();
                    if (eb != null)
                    {
                        eb.SetMoveDirection(Vector2.down);
                        eb.SetMoveSpeed(rockSpeed);
                        eb.SetDamage(rockDamage);
                    }
                }

                if (i < count - 1)
                    yield return new WaitForSeconds(rockSpawnDelay);
            }

            yield return new WaitForSeconds(rockFireInterval);
        }
    }

    public void TriggerRetreat()
    {
        if (currentState == TrollState.Retreating) return;
        if (fireCoroutine != null) { StopCoroutine(fireCoroutine); fireCoroutine = null; }
        if (bulletGroupFireCoroutine != null) { StopCoroutine(bulletGroupFireCoroutine); bulletGroupFireCoroutine = null; }
        if (rockFireCoroutine != null) { StopCoroutine(rockFireCoroutine); rockFireCoroutine = null; }
        if (isJumping)
        {
            isJumping = false;
            Vector3 pos = transform.position; pos.y = groundY; transform.position = pos;
        }
        currentState = TrollState.Retreating;
        currentSpeed = retreatSpeed;
        currentDir = +1;
        ApplyFoot("right");
    }

    private void RetreatTick()
    {
        Vector3 pos = transform.position;
        pos.x += currentSpeed * Time.deltaTime;
        pos.y = groundY;
        transform.position = pos;
        if (mainCam != null && mainCam.WorldToViewportPoint(pos).x > retreatScreenMargin)
            gameObject.SetActive(false);
    }

    // ── foot 动画：改状态名切换，状态变化时重置到初始播放，速度随 currentSpeed 自适应 ──
    private void ApplyFoot(string state)
    {
        if (footAnimator == null) return;
        if (state != currentFootState)
        {
            footAnimator.Play(state, 0, 0f);   // 层0，从0时刻重置到初始播放
            currentFootState = state;
        }
        float denom = baseMoveSpeed > 0f ? baseMoveSpeed : 1f;
        footAnimator.speed = currentSpeed / denom;
    }

    protected override void OnDeath()
    {
        base.OnDeath();
        if (fireCoroutine != null) { StopCoroutine(fireCoroutine); fireCoroutine = null; }
        if (bulletGroupFireCoroutine != null) { StopCoroutine(bulletGroupFireCoroutine); bulletGroupFireCoroutine = null; }
        if (rockFireCoroutine != null) { StopCoroutine(rockFireCoroutine); rockFireCoroutine = null; }
        if (footAnimator != null) footAnimator.speed = 0f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float y = Application.isPlaying ? groundY : transform.position.y;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(leftBoundaryX, y, 0f) + Vector3.up * 2f, new Vector3(leftBoundaryX, y, 0f) + Vector3.down * 2f);
        Gizmos.DrawLine(new Vector3(rightBoundaryX, y, 0f) + Vector3.up * 2f, new Vector3(rightBoundaryX, y, 0f) + Vector3.down * 2f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(centerX, y, 0f) + Vector3.up * 1.5f, new Vector3(centerX, y, 0f) + Vector3.down * 1.5f);
    }
#endif
}
