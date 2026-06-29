using System.Collections;
using System.Collections.Generic;
//using Unity.Mathematics;
using UnityEngine;

public class SubmarineSecret : Enemy
{
    [Header("潜水艇-鱼雷")]
    [SerializeField] private GameObject torpedoPrefab;      // 鱼雷预制体
    [SerializeField] private float torpedoInterval = 3f;    // 鱼雷发射间隔（秒）
    [SerializeField] private Transform torpedoMuzzle;       // 鱼雷发射口位置
    [SerializeField] private float torpedoDelay = 0.3f;     // 鱼雷发射延迟（配合动画）
    [SerializeField] private int torpedoDamage = 15;        // 鱼雷伤害
    [SerializeField] private int torpedoCount = 4;              // 一次发射数量
    [SerializeField] private float torpedoSpreadAngle = 15f;     // 散射总角度（度）
    [SerializeField] private float torpedoSpawnInterval = 0.05f; // 每颗鱼雷之间的间隔
    [Header("潜水艇-榴弹炮")]
    [SerializeField] private GameObject grenadePrefab;            // 榴弹炮预制体（Liudanpao）
    [SerializeField] private Transform grenadeMuzzle;              // 榴弹炮发射口位置
    [SerializeField] private float grenadeInterval = 5f;           // 榴弹炮发射间隔（秒）
    [SerializeField] private int grenadeDamage = 20;               // 榴弹炮伤害
    [SerializeField] private int grenadeMinCount = 1;              // 每次最少发射数量
    [SerializeField] private int grenadeMaxCount = 4;              // 每次最多发射数量
    [SerializeField] private float grenadeSpawnInterval = 0.3f;    // 每颗榴弹炮之间的间隔（秒）

    [Header("潜水艇-激光")]
    [SerializeField] private LaserEmitter laserEmitter;           // 激光发射器

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
        Wandering,    // 漂移：随机极小范围四处移动
        Retreat,
    }
    private SubState currentState;

    private Coroutine torpedoCoroutine;
    private Coroutine grenadeCoroutine;
    private Coroutine wanderCoroutine;
    private Coroutine retreatCoroutine;
    private Vector2 initialPosition;    // 入场结束后的位置（漂移锚点）
    private Vector2 wanderTarget;       // 当前漂移目标
    Camera mainCam ;
    WaitForSeconds EnterDuration;
    WaitForSeconds ChangeDirectionInterval;
    WaitForSeconds TorpedoInterval;
    WaitForSeconds WaitTorpedoDelay;
    WaitForSeconds WaitTorpedoSpawnInterval;
    WaitForSeconds GrenadeInterval;
    WaitForSeconds WaitGrenadeSpawnInterval;
    protected override void Awake()
    {
        base.Awake();
        mainCam  = Camera.main;
        EnterDuration=new WaitForSeconds(enterDuration);
        ChangeDirectionInterval =new WaitForSeconds(changeDirectionInterval);
        TorpedoInterval = new WaitForSeconds(torpedoInterval);
        WaitTorpedoDelay = new WaitForSeconds(torpedoDelay);
        WaitTorpedoSpawnInterval = new WaitForSeconds(torpedoSpawnInterval);
        GrenadeInterval = new WaitForSeconds(grenadeInterval);
        WaitGrenadeSpawnInterval = new WaitForSeconds(grenadeSpawnInterval);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        initialPosition = transform.position;
        wanderTarget = transform.position;

        // 进入入场阶段
        currentState = SubState.Entering;
        StartCoroutine(EnterRoutine());

        // 启动鱼雷发射协程
        if (torpedoCoroutine != null) StopCoroutine(torpedoCoroutine);
        torpedoCoroutine = StartCoroutine(TorpedoRoutine());

        // 启动榴弹炮发射协程
        if (grenadeCoroutine != null) StopCoroutine(grenadeCoroutine);
        grenadeCoroutine = StartCoroutine(GrenadeRoutine());
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

    /// <summary>
    /// 鱼雷发射协程
    /// </summary>
    private IEnumerator TorpedoRoutine()
    {
        while (true)
        {
            yield return TorpedoInterval;

            if (gameObject.activeSelf && currentHP > 0 && !isDie)
            {
                FireTorpedo();
            }
        }
    }

    /// <summary>
    /// 榴弹炮发射协程
    /// </summary>
    private IEnumerator GrenadeRoutine()
    {
        while (true)
        {
            yield return GrenadeInterval;

            if (gameObject.activeSelf && currentHP > 0 && !isDie)
            {
                StartCoroutine(FireGrenadeVolley());
            }
        }
    }

    /// <summary>
    /// 发射一轮榴弹炮：随机数量，每个之间有间隔
    /// </summary>
    private IEnumerator FireGrenadeVolley()
    {
        if (grenadePrefab == null || grenadeMuzzle == null) yield break;

        int count = Random.Range(grenadeMinCount, grenadeMaxCount + 1);

        for (int i = 0; i < count; i++)
        {
            GameObject grenade = PoolManager.Release(grenadePrefab, grenadeMuzzle.position, Quaternion.identity);
            EnemyBullet eb = grenade.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                eb.SetDamage(grenadeDamage);
            }

            if (i < count - 1)
                yield return WaitGrenadeSpawnInterval;
        }
    }

    /// <summary>
    /// 发射鱼雷（4颗扇形散射）
    /// </summary>
    private void FireTorpedo()
    {
        StartCoroutine(FireTorpedoSpread());
    }

    /// <summary>
    /// 延迟后扇形发射多颗鱼雷
    /// </summary>
    private IEnumerator FireTorpedoSpread()
    {
        //yield return WaitTorpedoDelay;
        if (torpedoPrefab == null || torpedoMuzzle == null) yield break;

        float halfSpread = torpedoSpreadAngle * 0.5f;
        float angleStep = torpedoCount > 1 ? torpedoSpreadAngle / (torpedoCount - 1) : 0f;

        for (int i = 0; i < torpedoCount; i++)
        {
            // 散射扇面：精灵图默认朝左，以 Vector2.left 为基准做偏移
            float angle = -halfSpread + angleStep * i;
            Quaternion rot = Quaternion.Euler(0, 0, angle);                    // 旋转 = 散射角（精灵默认朝左）
            Vector2 dir = rot * Vector2.left;                                  // 移动方向 = 同角度旋转的左向量

            GameObject torpedo = PoolManager.Release(torpedoPrefab, torpedoMuzzle.position, rot);
            EnemyBullet eb = torpedo.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                eb.SetDamage(torpedoDamage);
                eb.SetMoveDirection(dir);   // 移动方向和精灵朝向完全一致
            }

            if (i < torpedoCount - 1)
                yield return WaitTorpedoSpawnInterval;
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
        // 鱼雷发射由协程处理
    }

    /// <summary>
    /// 由外部调用，触发撤退：向右移动直到出屏后禁用
    /// </summary>
    public void TriggerRetreat()
    {
        currentState=SubState.Retreat;
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


    protected override void OnDeath()
    {
        // 先彻底停掉激光，再播放爆炸特效
        if (laserEmitter != null)
            laserEmitter.Stop();

        base.OnDeath();
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
        if (torpedoCoroutine != null)
        {
            StopCoroutine(torpedoCoroutine);
            torpedoCoroutine = null;
        }
        if (grenadeCoroutine != null)
        {
            StopCoroutine(grenadeCoroutine);
            grenadeCoroutine = null;
        }
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
