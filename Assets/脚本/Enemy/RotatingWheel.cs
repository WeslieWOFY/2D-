using System.Collections;
using UnityEngine;

/// <summary>
/// 旋转轮盘：不断自旋，单发 + 连发 + 圆周运动 全自动循环
/// </summary>
public class RotatingWheel : MonoBehaviour
{
    [Header("自旋")]
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private float burstSpinSpeed = 270f;
    [SerializeField] private float orbitSpinSpeed = 540f;

    [Header("发射口")]
    [SerializeField] private Transform[] fireMuzzles;
    [SerializeField] private Transform aimTarget;

    [Header("子弹")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int bulletDamage = 10;

    [Header("单发")]
    [SerializeField] private float fireInterval = 1.5f;        // 单发间隔（秒）

    [Header("连发")]
    [SerializeField] private float burstCooldown = 4f;         // 连发冷却（两次连发之间的间隔）
    [SerializeField] private float burstInterval = 0.15f;      // 连发每轮间隔（秒）
    [SerializeField] private int burstCount = 5;               // 连发轮数

    [Header("圆周运动")]
    [SerializeField] private float orbitCooldown = 6f;        // 圆周运动冷却（秒），自动触发
    [SerializeField] private float orbitRadius = 5f;          // 弧线半径
    [SerializeField] private float orbitArcDuration = 1.5f;   // 弧线飞行耗时（秒）
    [SerializeField] private Vector2 orbitCenterOffset = new Vector2(-1f, -2f); // 圆心偏移（左移、下移）

    [Header("初始延迟")]
    [SerializeField] private float initialDelay = 2f;

    [Header("碰撞伤害")]
    [SerializeField] private int colliderdamege = 20;
    [SerializeField] private float damageCooldown = 0.5f;  // 伤害冷却，配合玩家弹开

    private enum WheelState
    {
        Waiting,     // 等待初始延迟
        Idle,        // 空闲：正常自旋，单发 + 等待连发
        Burst,       // 连发中：加快自旋 + 短间隔连续发射
        Orbiting     // 圆周运动：停止攻击 + 最快自旋 + 绕圈
    }

    private WheelState currentState = WheelState.Waiting;
    private float currentSpinSpeed;

    private Coroutine fireCoroutine;
    private Coroutine burstCycleCoroutine;
    private Coroutine orbitCycleCoroutine;
    private Coroutine initialDelayCoroutine;

    private Rigidbody2D rb;
    private float lastDamageTime = -1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentSpinSpeed = spinSpeed;
    }

    private void OnEnable()
    {
        currentState = WheelState.Waiting;
        currentSpinSpeed = spinSpeed;

        if (initialDelayCoroutine != null) StopCoroutine(initialDelayCoroutine);
        initialDelayCoroutine = StartCoroutine(InitialDelayRoutine());
    }

    private void OnDisable()
    {
        CleanupAllCoroutines();
    }

    private void Update()
    {
        transform.Rotate(0, 0, currentSpinSpeed * Time.deltaTime);
    }

    // ==================== 初始延迟 ====================

    private IEnumerator InitialDelayRoutine()
    {
        yield return new WaitForSeconds(initialDelay);

        currentState = WheelState.Idle;
        fireCoroutine = StartCoroutine(FireRoutine());
        burstCycleCoroutine = StartCoroutine(BurstCycleRoutine());
        orbitCycleCoroutine = StartCoroutine(OrbitCycleRoutine());
    }

    // ==================== 单发协程 ====================

    private IEnumerator FireRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(fireInterval);
        while (true)
        {
            yield return wait;
            if (currentState == WheelState.Idle)
            {
                FireFromAllMuzzles();
            }
        }
    }

    // ==================== 连发循环协程 ====================

    private IEnumerator BurstCycleRoutine()
    {
        WaitForSeconds cooldownWait = new WaitForSeconds(burstCooldown);

        while (true)
        {
            yield return cooldownWait;

            if (currentState != WheelState.Idle) continue;

            // 进入连发
            currentState = WheelState.Burst;
            float prevSpinSpeed = currentSpinSpeed;
            currentSpinSpeed = burstSpinSpeed;

            WaitForSeconds burstWait = new WaitForSeconds(burstInterval);
            for (int i = 0; i < burstCount; i++)
            {
                FireFromAllMuzzles();
                if (i < burstCount - 1)
                    yield return burstWait;
            }

            // 连发结束，等0.5秒再回到空闲
            yield return new WaitForSeconds(0.5f);
            currentSpinSpeed = prevSpinSpeed;
            currentState = WheelState.Idle;
        }
    }

    // ==================== 统一发射 ====================

    private void FireFromAllMuzzles()
    {
        if (bulletPrefab == null || fireMuzzles == null || aimTarget == null) return;

        foreach (Transform muzzle in fireMuzzles)
        {
            if (muzzle == null) continue;

            Vector2 dir = (muzzle.position - aimTarget.position).normalized;
            float angle = Vector2.SignedAngle(Vector2.left, dir);
            Quaternion rot = Quaternion.Euler(0, 0, angle);

            GameObject bullet = PoolManager.Release(bulletPrefab, muzzle.position, rot);
            EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                eb.SetDamage(bulletDamage);
                eb.SetMoveDirection(dir);
            }
        }
    }

    // ==================== 圆周运动 ====================

    private IEnumerator OrbitCycleRoutine()
    {
        // 从 Idle 开始时记录，用 realtime 确保间隔从轨道 START 算起
        float nextOrbitTime = Time.time + orbitCooldown;

        while (true)
        {
            yield return new WaitForSeconds(0.5f);

            if (Time.time >= nextOrbitTime && currentState == WheelState.Idle)
            {
                yield return StartCoroutine(OrbitRoutine());
                nextOrbitTime = Time.time + orbitCooldown;
            }
        }
    }

    public void TriggerOrbit()
    {
        if (currentState == WheelState.Waiting || currentState == WheelState.Orbiting) return;
        StartCoroutine(OrbitRoutine());
    }

    private IEnumerator OrbitRoutine()
    {
        currentState = WheelState.Orbiting;
        float prevSpinSpeed = currentSpinSpeed;

        // 临时切 kinematic：碰撞保留，物理不抢位置
        RigidbodyType2D prevBodyType = RigidbodyType2D.Dynamic;
        if (rb != null)
        {
            prevBodyType = rb.bodyType;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // 记录相对父物体的本地位置，父物体移动也能正确归位
        Vector3 localPos = transform.localPosition;
        Vector2 worldCenter = transform.position;

        // 旋转加速 + 弧线运动 齐头并进
        float startAngle = 225f * Mathf.Deg2Rad;
        float sweepAngle = -180f * Mathf.Deg2Rad;

        float elapsed = 0f;
        while (elapsed < orbitArcDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / orbitArcDuration);

            currentSpinSpeed = Mathf.Lerp(prevSpinSpeed, orbitSpinSpeed, t);

            float angle = startAngle + t * sweepAngle;
            Vector2 arcDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float blend = Mathf.Sin(t * Mathf.PI);
            transform.position = worldCenter + blend * (orbitCenterOffset + arcDir * orbitRadius);

            yield return null;

            if (elapsed >= orbitArcDuration) break;
        }

        // 根据父物体当前位置重算归位目标
        Vector2 targetPos = transform.parent != null
            ? (Vector2)transform.parent.TransformPoint(localPos)
            : worldCenter;

        // 极小误差直接瞬移归位，否则平滑移回去
        if (Vector2.Distance(transform.position, targetPos) < 0.1f)
            transform.position = targetPos;
        else
            yield return StartCoroutine(SmoothReturn(targetPos));

        // 恢复刚体类型
        if (rb != null) rb.bodyType = prevBodyType;

        currentSpinSpeed = prevSpinSpeed;
        currentState = WheelState.Idle;
    }

    // 平滑归位（父物体移动导致误差较大时）
    private IEnumerator SmoothReturn(Vector2 target)
    {
        Vector2 start = transform.position;
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            transform.position = Vector2.Lerp(start, target, t / 0.15f);
            yield return null;
        }
        transform.position = target;
    }

    // ==================== 撤退 ====================

    /// <summary>
    /// 收到撤退信号时调用：停止自旋以外的全部协程（单发/连发/圆周运动）
    /// </summary>
    public void StopAllAttacks()
    {
        if (fireCoroutine != null) { StopCoroutine(fireCoroutine); fireCoroutine = null; }
        if (burstCycleCoroutine != null) { StopCoroutine(burstCycleCoroutine); burstCycleCoroutine = null; }
        if (orbitCycleCoroutine != null) { StopCoroutine(orbitCycleCoroutine); orbitCycleCoroutine = null; }

        currentState = WheelState.Idle;
        currentSpinSpeed = spinSpeed;
    }

    // ==================== 碰撞 ====================

    // 刚体碰撞：伤害玩家（带冷却，配合玩家弹开）
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        if (Time.time - lastDamageTime < damageCooldown) return;
        lastDamageTime = Time.time;
        PlayerKongzhi Player=collision.gameObject.GetComponent<PlayerKongzhi>();
        Player.ChangeRed();
        GameManager.Instance.PlayerTakeDamage(colliderdamege);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        if (Time.time - lastDamageTime < damageCooldown) return;
        lastDamageTime = Time.time;
        GameManager.Instance.PlayerTakeDamage(colliderdamege);
    }

    // 触发器碰撞：禁用玩家子弹
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerBullet"))
        {
            Bullet bullet = other.GetComponent<Bullet>();
            if(bullet.getfalse())
            other.gameObject.SetActive(false);
        }
    }

    // ==================== 清理 ====================

    private void CleanupAllCoroutines()
    {
        if (initialDelayCoroutine != null) { StopCoroutine(initialDelayCoroutine); initialDelayCoroutine = null; }
        if (fireCoroutine != null) { StopCoroutine(fireCoroutine); fireCoroutine = null; }
        if (burstCycleCoroutine != null) { StopCoroutine(burstCycleCoroutine); burstCycleCoroutine = null; }
        if (orbitCycleCoroutine != null) { StopCoroutine(orbitCycleCoroutine); orbitCycleCoroutine = null; }
    }
}
