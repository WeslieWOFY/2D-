using System.Collections;
using UnityEngine;

/// <summary>
/// 单波分裂子弹组件：BulletSplitter 的简化变体。
/// 与 BulletSplitter（按波数维护、每波共享一个速度）不同，本组件只分裂【一波】，
/// 但这一波里【每发子弹的速度都单独维护】。
///
/// 单波布局由三个参数决定（与 BulletSplitter 的 WaveConfig 一致）：
///   ● startAngle  —— 该波第一发子弹的角度（度），0=右，90=上，-90=下，180=左
///   ● angleStep   —— 相邻子弹之间的角度间隔（度）
///   ● bulletCount —— 该波发射的子弹数量
/// 每发子弹的角度 = startAngle + angleStep * i，其余属性从 childConfigs[i] 取（按下标对应）。
///
/// 每发子弹可单独配置（childConfigs[i]）：
///   ● speed           —— 该发速度（>0 生效，<=0 回退到 defaultChildSpeed）
///   ● splitDelay      —— 若子子弹挂了 BulletSplitter / BulletSplitterSingleWave，设置其分裂时间 activationDelay
///   ● stopBeforeSplit —— 若子子弹挂了 BulletSplitter / BulletSplitterSingleWave，设置其分裂前停止运动时间
///   （splitDelay / stopBeforeSplit 传 <0 表示不修改，保持子子弹预制体默认）
///
/// 两种触发模式（与 BulletSplitter 一致）：
///   ● 时间模式（默认）：激活后按 activationDelay 计时，到时分裂
///   ● 底部模式（splitAtBottom = true）：无视时间，子弹到达屏幕底部后延迟 bottomSplitDelay 秒再分裂
/// 分裂时强制停止移动（含 BulletParabolic）并播放特效，停顿 stopBeforeSplit 秒后立刻分裂。
/// </summary>
[RequireComponent(typeof(EnemyBullet))]
[DefaultExecutionOrder(-50)]  // 在 EnemyBullet.Update 之前运行，抢在 CheckBounds 禁用前检测到底部
public class BulletSplitterSingleWave : MonoBehaviour
{
    [Header("分裂触发模式")]
    [SerializeField] private bool splitAtBottom = false;      // 开启后无视时间，到底部才分裂

    [Header("激活延迟（时间模式）")]
    [SerializeField] private float activationDelay = 0.8f;   // 激活后多久开始分裂（总时长）

    [Header("分裂前停顿（两种模式共用）")]
    [SerializeField] private float stopBeforeSplit = 0.5f;   // 分裂前多少秒强制停止移动（时间模式须 <= activationDelay）

    [Header("到底部分裂设置（底部模式）")]
    [SerializeField] private float bottomBoundsOffset = 0f;  // 相对主摄像机下沿的偏移：0=下沿，正值=屏幕内提前触发
    [SerializeField] private float bottomTimeout = 10f;      // 底部模式超时保底（防止永远不到底部）

    [Header("单波分裂设置")]
    [SerializeField] private GameObject childBulletPrefab;   // 要发射的子子弹预制体
    [Tooltip("子子弹离发射点的距离")]
    [SerializeField] private float radius = 0.6f;
    [Tooltip("该波第一发子弹的角度（度），0=右，90=上，-90=下，180=左")]
    [SerializeField] private float startAngle = 0f;
    [Tooltip("相邻子弹之间的角度间隔（度）")]
    [SerializeField] private float angleStep = 30f;
    [Tooltip("该波发射的子弹数量")]
    [SerializeField] private int bulletCount = 8;
    [SerializeField] private float damageCoefficient = 1f;   // 子子弹伤害 = 自身伤害 × 系数

    [Header("每发子弹配置（单独）")]
    [Tooltip("按下标对应每发子弹：childConfigs[0]→第0发，childConfigs[1]→第1发……\n" +
             "数组长度可小于 bulletCount，未覆盖的子弹：速度回退到 defaultChildSpeed，分裂时序不修改。")]
    [SerializeField] private ChildBulletConfig[] childConfigs;   // 每发子弹单独配置，按下标对应
    [Tooltip("childConfigs 未覆盖（或 speed<=0）的子弹使用此速度；<=0 则保持预制体默认速度")]
    [SerializeField] private float defaultChildSpeed = 4f;

    [Header("特效")]
    [SerializeField] private GameObject splitEffect;         // 分裂时播放的特效（仅播放一次）

    private bool hasSplit;
    private bool isWaitingBottom;      // 底部模式：已到底部，正在等延迟
    private Coroutine splitCoroutine;
    private EnemyBullet parentBullet;
    private float originalSpeed;
    private float originalDamageCoefficient;
    private float bottomBoundaryY;     // 缓存的屏幕底部 Y 坐标

    /// <summary>
    /// 单发子弹配置：速度 + （可选）自身分裂组件的分裂时序
    /// </summary>
    [System.Serializable]
    public class ChildBulletConfig
    {
        [Tooltip("该发子弹速度（>0 生效；<=0 回退到 defaultChildSpeed）")]
        public float speed = 4f;

        [Tooltip("若子子弹挂了 BulletSplitter / BulletSplitterSingleWave：设置其分裂时间 activationDelay。\n" +
                 ">=0 生效；<0 不修改（保持子子弹预制体默认）")]
        public float splitDelay = -1f;

        [Tooltip("若子子弹挂了 BulletSplitter / BulletSplitterSingleWave：设置其分裂前停止运动时间 stopBeforeSplit。\n" +
                 ">=0 生效；<0 不修改")]
        public float stopBeforeSplit = -1f;
    }

    private void Awake()
    {
        parentBullet = GetComponent<EnemyBullet>();
        originalDamageCoefficient = damageCoefficient;
    }

    private void OnEnable()
    {
        hasSplit = false;
        isWaitingBottom = false;
        damageCoefficient = originalDamageCoefficient;

        // 缓存主摄像机底部 Y 坐标（视口下沿 + 偏移量，正值=屏幕内提前触发）
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, 0));
            bottomBoundaryY = bottomLeft.y + bottomBoundsOffset;
        }
        else
        {
            bottomBoundaryY = -10f;  // 无相机时的兜底值
        }

        if (splitCoroutine != null)
            StopCoroutine(splitCoroutine);

        if (splitAtBottom)
        {
            // 底部模式：关闭水平出界禁用（子弹可能飞出左右边界才到底部）
            // 改用超时保底，防止子弹永远不到底部而无限存活
            if (parentBullet != null)
            {
                parentBullet.SetDisableOutOfBounds(false);
                parentBullet.EnableTimeout(bottomTimeout);
            }
        }
        else
        {
            // 时间模式：原有逻辑
            splitCoroutine = StartCoroutine(SplitRoutine());
        }
    }

    private void OnDisable()
    {
        if (splitCoroutine != null)
        {
            StopCoroutine(splitCoroutine);
            splitCoroutine = null;
        }
    }

    private void Update()
    {
        if (!splitAtBottom || hasSplit || isWaitingBottom) return;

        // 底部模式：预测下一帧位置，抢在 EnemyBullet.CheckBounds 之前检测
        // （本脚本 ExecutionOrder=-50，比 EnemyBullet 先跑）
        Vector2 dir = parentBullet != null ? parentBullet.GetMoveDirection() : Vector2.down;
        float speed = parentBullet != null ? parentBullet.GetMoveSpeed() : 0f;
        float nextY = transform.position.y + dir.y * speed * Time.deltaTime;

        if (nextY <= bottomBoundaryY || transform.position.y <= bottomBoundaryY)
        {
            isWaitingBottom = true;
            splitCoroutine = StartCoroutine(BottomSplitRoutine());
        }
    }

    /// <summary>
    /// 底部分裂协程：到底部 → 钳制位置 → 强制停止（含 BulletParabolic） → 短暂停顿 → 播特效+立刻分裂
    /// </summary>
    private IEnumerator BottomSplitRoutine()
    {
        // 阶段1：强制停止一切移动 + 钳制位置到屏幕底部
        // 必须同时停 EnemyBullet 和 BulletParabolic，
        // 否则 BulletParabolic 每帧用重力更新 velocity 再 SyncToEnemyBullet 会覆盖 speed=0
        if (parentBullet != null)
        {
            originalSpeed = parentBullet.GetMoveSpeed();
            parentBullet.SetMoveSpeed(0f);
        }
        BulletParabolic parabolic = GetComponent<BulletParabolic>();
        if (parabolic != null)
        {
            parabolic.Pause();
        }

        // 钳制位置：防止子弹已飞过边界太远
        Vector3 pos = transform.position;
        if (pos.y < bottomBoundaryY)
        {
            pos.y = bottomBoundaryY;
            transform.position = pos;
        }

        // 阶段2：播放分裂特效 + 短暂停顿（特效在停顿期间播放，让子弹稳定、视觉过渡）
        if (splitEffect != null)
        {
            PoolManager.Release(splitEffect, transform.position);
        }
        if (stopBeforeSplit > 0f)
            yield return new WaitForSeconds(stopBeforeSplit);

        if (hasSplit) yield break;
        hasSplit = true;

        // 阶段3：立刻分裂（不再额外延迟）
        SpawnWave();

        // 自身子弹结束
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 分裂主协程（时间模式）：正常飞行 → 强制停止 → 播特效+发射单波
    /// </summary>
    private IEnumerator SplitRoutine()
    {
        // 阶段1：正常飞行（activationDelay - stopBeforeSplit 秒）
        float moveDuration = Mathf.Max(0f, activationDelay - stopBeforeSplit);
        if (moveDuration > 0f)
            yield return new WaitForSeconds(moveDuration);

        // 阶段2：强制停止移动 + 播放分裂特效（特效在停顿期间播放，而非分裂瞬间）
        if (parentBullet != null)
        {
            originalSpeed = parentBullet.GetMoveSpeed();
            parentBullet.SetMoveSpeed(0f);
        }
        if (splitEffect != null)
        {
            PoolManager.Release(splitEffect, transform.position);
        }

        yield return new WaitForSeconds(stopBeforeSplit);

        if (hasSplit) yield break;
        hasSplit = true;

        // 阶段3：发射单波
        SpawnWave();

        // 自身子弹结束
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 发射单波子弹（每发速度、分裂时序单独配置）
    /// </summary>
    private void SpawnWave()
    {
        if (childBulletPrefab == null) return;

        // 如果父子弹在做圆周运动（轨道角速度≠0），叠加当前轨道角度，保证扇形朝向与视觉一致
        float orbitAngleOffset = 0f;
        var parentCircular = GetComponent<BulletCircularMotion>();
        if (parentCircular != null && parentCircular.GetOrbitSpeed() != 0f)
        {
            orbitAngleOffset = parentCircular.GetCurrentAngleDeg();
        }

        int count = Mathf.Max(0, bulletCount);
        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + angleStep * i + orbitAngleOffset;
            float rad = angle * Mathf.Deg2Rad;

            // 远离中心点的方向
            Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // 计算子子弹生成位置（在父子弹周围，偏移 radius）
            Vector3 spawnPos = transform.position + (Vector3)(direction * radius);

            // 旋转：精灵默认朝左(Vector2.left)，+180° 使其朝向远离中心点的方向
            // rot * Vector2.left = direction，因此 rot = Quaternion.Euler(0,0, angle+180)
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle + 180f);

            GameObject child = PoolManager.Release(childBulletPrefab, spawnPos, rotation);
            if (child == null) continue;

            EnemyBullet eb = child.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                int baseDamage = parentBullet != null ? parentBullet.GetDamage() : 10;
                eb.SetDamage(Mathf.FloorToInt(baseDamage * damageCoefficient));

                eb.SetMoveDirection(direction);

                // 每发子弹速度单独配置：优先取 childConfigs[i].speed，回退到 defaultChildSpeed
                float speed = GetChildSpeed(i);
                if (speed > 0f)
                {
                    eb.SetMoveSpeed(speed);
                }
            }

            // 若子子弹自身带分裂组件，按配置设置其分裂时间与提前停止时间
            ConfigureChildSplit(child, i);
        }
    }

    /// <summary>
    /// 取第 index 发子弹的速度：
    ///   childConfigs[index].speed > 0 → 用它；
    ///   否则（数组过短、值为空、或 <= 0）→ 回退到 defaultChildSpeed。
    /// defaultChildSpeed <= 0 时返回 0，调用处会跳过 SetMoveSpeed 以保持预制体默认速度。
    /// </summary>
    private float GetChildSpeed(int index)
    {
        if (childConfigs != null && index >= 0 && index < childConfigs.Length && childConfigs[index].speed > 0f)
            return childConfigs[index].speed;
        return defaultChildSpeed;
    }

    /// <summary>
    /// 若子子弹挂了 BulletSplitter / BulletSplitterSingleWave，按 childConfigs[index] 设置其分裂时序。
    /// splitDelay / stopBeforeSplit 均 <0 时跳过（保持子子弹预制体默认）。
    /// </summary>
    private void ConfigureChildSplit(GameObject child, int index)
    {
        float splitDelay = -1f;
        float stopBeforeSplit = -1f;
        if (childConfigs != null && index >= 0 && index < childConfigs.Length)
        {
            splitDelay = childConfigs[index].splitDelay;
            stopBeforeSplit = childConfigs[index].stopBeforeSplit;
        }
        if (splitDelay < 0f && stopBeforeSplit < 0f) return;  // 都未配置，保持子子弹默认

        var splitter = child.GetComponent<BulletSplitter>();
        if (splitter != null)
        {
            splitter.SetSplitTiming(splitDelay, stopBeforeSplit);
            return;
        }
        var single = child.GetComponent<BulletSplitterSingleWave>();
        if (single != null)
        {
            single.SetSplitTiming(splitDelay, stopBeforeSplit);
        }
    }

    /// <summary>发射者调用，设置伤害系数</summary>
    public void SetDamageCoefficient(float coefficient)
    {
        damageCoefficient = coefficient;
    }

    /// <summary>
    /// 外部（如父分裂组件）设置本子弹的分裂时间与分裂前停顿时长。
    /// 时间模式下重启分裂协程以使新 activationDelay 立即生效
    /// （activationDelay 在协程启动时即被读取，父组件在 Release 之后才能配置，故需重启）。
    /// 底部模式不依赖 activationDelay，stopBeforeSplit 在到底部时才读取，无需重启。
    /// splitDelay / stopBeforeSplit 传 <0 表示不修改该字段。
    /// </summary>
    public void SetSplitTiming(float splitDelay, float stopBeforeSplit)
    {
        if (splitDelay >= 0f) activationDelay = splitDelay;
        if (stopBeforeSplit >= 0f) this.stopBeforeSplit = stopBeforeSplit;

        // 时间模式：重启协程让新 activationDelay 生效
        if (!splitAtBottom && splitCoroutine != null)
        {
            StopCoroutine(splitCoroutine);
            splitCoroutine = StartCoroutine(SplitRoutine());
        }
    }
}
