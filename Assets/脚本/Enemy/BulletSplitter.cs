using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 子弹分裂组件：挂载到子弹对象上（不是EnemyBullet的子类，作为额外组件使用）。
/// 两种触发模式：
///   ● 时间模式（默认）：激活后按 activationDelay 计时，到时分裂
///   ● 底部模式（splitAtBottom = true）：无视时间，子弹到达屏幕底部后延迟 bottomSplitDelay 秒再分裂
/// 分裂时强制停止移动，按配置的多个波次向四周发射子子弹。
/// 每波可独立配置半径、初始角度、间隔角度、发射数量。
/// </summary>
[RequireComponent(typeof(EnemyBullet))]
[DefaultExecutionOrder(-50)]  // 在 EnemyBullet.Update 之前运行，抢在 CheckBounds 禁用前检测到底部
public class BulletSplitter : MonoBehaviour
{
    [Header("分裂触发模式")]
    [SerializeField] private bool splitAtBottom = false;      // 开启后无视时间，到底部才分裂

    [Header("激活延迟（时间模式）")]
    [SerializeField] private float activationDelay = 0.8f;   // 激活后多久开始发射子子弹（总时长）

    [Header("分裂前停顿（两种模式共用）")]
    [SerializeField] private float stopBeforeSplit = 0.5f;   // 分裂前多少秒强制停止移动（时间模式须 <= activationDelay）

    [Header("到底部分裂设置（底部模式）")]
    [SerializeField] private float bottomBoundsOffset = 0f;  // 相对主摄像机下沿的偏移：0=下沿，正值=屏幕内提前触发
    [SerializeField] private float bottomTimeout = 10f;      // 底部模式超时保底（防止永远不到底部）

    [Header("波次设置")]
    [SerializeField] private WaveConfig[] waves;             // 每一波的配置（所有波同时发射）

    [Header("子弹设置")]
    [SerializeField] private GameObject childBulletPrefab;   // 要发射的子子弹预制体
    [SerializeField] private float damageCoefficient = 1f;   // 子子弹伤害 = 自身伤害 × 系数
    [SerializeField] private float childMoveSpeed = 4f;      // 子子弹移速（0则保持预制体默认值）

    [Header("圆周运动")]
    [SerializeField] private bool circularMotion = false;    // 子子弹若有 BulletCircularMotion，是否以分裂点为中心公转
    [SerializeField] private float circularOrbitSpeed = 0f;  // 轨道角速度（度/秒），0则保持预制体默认值

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
    /// 单波配置
    /// </summary>
    [System.Serializable]
    public class WaveConfig
    {
        [Tooltip("子子弹离发射点的距离")]
        public float radius = 0.6f;
        [Tooltip("该波第一个子弹的角度（度），0=右，90=上，-90=下，180=左")]
        public float startAngle = 0f;
        [Tooltip("相邻子弹之间的角度间隔（度）")]
        public float angleStep = 30f;
        [Tooltip("该波发射的子弹数量")]
        public int bulletCount = 8;
    }

    private void Awake()
    {
        parentBullet = GetComponent<EnemyBullet>();
        originalDamageCoefficient = damageCoefficient;

        // 如果没有配置波次，给一个默认波次避免空
        if (waves == null || waves.Length == 0)
        {
            waves = new WaveConfig[]
            {
                new WaveConfig
                {
                    radius = 0.6f,
                    startAngle = 0f,
                    angleStep = 45f,
                    bulletCount = 8
                }
            };
        }
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

        // 阶段2：短暂停顿（让子弹稳定，视觉过渡）
        if (stopBeforeSplit > 0f)
            yield return new WaitForSeconds(stopBeforeSplit);

        if (hasSplit) yield break;
        hasSplit = true;

        // 阶段3：播放特效 + 立刻分裂（不再额外延迟）
        if (splitEffect != null)
        {
            PoolManager.Release(splitEffect, transform.position);
        }
        SpawnAllWaves();

        // 自身子弹结束
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 分裂主协程（时间模式）：正常飞行 → 强制停止 → 逐波发射
    /// </summary>
    private IEnumerator SplitRoutine()
    {
        // 阶段1：正常飞行（activationDelay - stopBeforeSplit 秒）
        float moveDuration = Mathf.Max(0f, activationDelay - stopBeforeSplit);
        if (moveDuration > 0f)
            yield return new WaitForSeconds(moveDuration);

        // 阶段2：强制停止移动
        if (parentBullet != null)
        {
            originalSpeed = parentBullet.GetMoveSpeed();
            parentBullet.SetMoveSpeed(0f);
        }

        yield return new WaitForSeconds(stopBeforeSplit);

        if (hasSplit) yield break;
        hasSplit = true;

        // 阶段3：播放分裂特效 + 发射所有波次
        if (splitEffect != null)
        {
            PoolManager.Release(splitEffect, transform.position);
        }
        SpawnAllWaves();

        // 自身子弹结束
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 发射所有波次（不含特效，特效由各模式协程自行控制时机）
    /// </summary>
    private void SpawnAllWaves()
    {
        for (int w = 0; w < waves.Length; w++)
        {
            if (waves[w] == null) continue;
            SpawnWave(waves[w]);
        }
    }

    /// <summary>
    /// 发射一波子弹
    /// </summary>
    private void SpawnWave(WaveConfig wave)
    {
        if (childBulletPrefab == null) return;

        // 如果父子弹在做圆周运动（轨道角速度≠0），叠加当前轨道角度
        float orbitAngleOffset = 0f;
        var parentCircular = GetComponent<BulletCircularMotion>();
        if (parentCircular != null && parentCircular.GetOrbitSpeed() != 0f)
        {
            orbitAngleOffset = parentCircular.GetCurrentAngleDeg();
        }

        for (int i = 0; i < wave.bulletCount; i++)
        {
            float angle = wave.startAngle + wave.angleStep * i + orbitAngleOffset;
            float rad = angle * Mathf.Deg2Rad;

            // 远离中心点的方向
            Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // 计算子子弹生成位置（在父子弹周围，偏移 radius）
            Vector3 spawnPos = transform.position + (Vector3)(direction * wave.radius);

            // 旋转：精灵默认朝左(Vector2.left)，+180° 使其朝向远离中心点的方向
            // rot * Vector2.left = direction，因此 rot = Quaternion.Euler(0,0, angle+180)
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle + 180f);

            GameObject child = PoolManager.Release(childBulletPrefab, spawnPos, rotation);
            if (child != null)
            {
                EnemyBullet eb = child.GetComponent<EnemyBullet>();
                if (eb != null)
                {
                    int baseDamage = parentBullet != null ? parentBullet.GetDamage() : 10;
                    eb.SetDamage(Mathf.FloorToInt(baseDamage * damageCoefficient));

                    if (circularMotion)
                    {
                        var circular = child.GetComponent<BulletCircularMotion>();
                        if (circular != null)
                        {
                            circular.Initialize(transform.position, angle);
                            if (circularOrbitSpeed > 0f)
                                circular.SetOrbitSpeed(circularOrbitSpeed);
                        }
                    }
                    else
                    {
                        eb.SetMoveDirection(direction);
                        if (childMoveSpeed > 0f)
                        {
                            eb.SetMoveSpeed(childMoveSpeed);
                        }
                    }
                }
            }
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
