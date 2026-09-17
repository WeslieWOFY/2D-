using System.Collections;
using UnityEngine;

/// <summary>
/// 枪弹新兵头部 —— 继承 Enemy。
///
/// 行为循环：初始间隔(shakeDelay)后开始上下小幅旋转摇晃，之后反复执行
///   “3-5 次子弹攻击 → 1 次激光攻击”。
///   子弹攻击：摇晃 attackDelay 秒 → 发射一颗子弹（两种备弹等概率随机其一）→ 短暂保持不摆动(bulletAttackDuration)。
///   激光攻击：摇晃 attackDelay 秒 → 激活 LaserEmitter(自我发射) → 等激光发射完毕
///             → 再延迟 laserResumeDelay 秒 → 关闭 LaserEmitter，回到摇晃。
///   攻击时停止摇晃并摆正（旋转归位）。激光特效为本物体子物体，经 LaserEmitter.IsEffectActive 判断是否结束。
/// 额外独立射击：与上述循环独立，经 extraAttackDelay 后开始，按 extraAttackInterval 间隔
///   从 extraFirePoint 往前发射2种备弹之一（等概率）。
/// 死亡：不禁用自身，爆炸后通知父物体(GunnerRecruit)禁用整个对象。
/// </summary>
public class GunnerRecruitHead : Enemy
{
    [Header("摇晃")]
    [SerializeField] private float shakeDelay = 2f;        // 初始间隔：多久后开始摇晃
    [SerializeField] private float shakeAmplitude = 8f;    // 小幅度上下摇晃角度（度）
    [SerializeField] private float shakeSpeed = 6f;        // 摇晃速度

    [Header("攻击节奏")]
    [SerializeField] private float attackDelay = 3f;       // 每次攻击前摇晃的时长
    [SerializeField] private int bulletCountMin = 3;       // 一轮激光前发射子弹的次数（最小）
    [SerializeField] private int bulletCountMax = 5;       // 一轮激光前发射子弹的次数（最大）

    [Header("子弹攻击")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private GameObject bulletPrefab2;   // 第二种备弹：与 bulletPrefab 等概率随机发射其一
    [SerializeField] private Transform firePoint;
    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private int bulletDamage = 10;
    [SerializeField] private float bulletAttackDuration = 0.3f;   // 发射一颗子弹后保持不摆动的时间

    [Header("激光攻击")]
    [SerializeField] private LaserEmitter laserEmitter;           // 激光发射器：激活后自我发射
    [SerializeField] private float laserResumeDelay = 0.3f;      // 激光消失后延迟恢复摇晃的时间
    [SerializeField] private float laserMaxWait = 15f;            // 激光等待超时（防卡死）

    [Header("额外独立射击")]
    [SerializeField] private GameObject extraBulletPrefab;
    [SerializeField] private GameObject extraBulletPrefab2;   // 第二种备弹
    [SerializeField] private Transform extraFirePoint;
    [SerializeField] private float extraBulletSpeed = 5f;
    [SerializeField] private int extraBulletDamage = 10;
    [SerializeField] private float extraAttackDelay = 2f;      // 初始间隔
    [SerializeField] private float extraAttackInterval = 1f;   // 每发间隔

    private enum HeadState { Waiting, Shaking, Attacking }

    private HeadState currentState;
    private Quaternion baseLocalRotation;
    private Coroutine cycleCoroutine;
    private GunnerRecruit parentBoss;

    protected override void Awake()
    {
        base.Awake();
        baseLocalRotation = transform.localRotation;
        if (firePoint == null) firePoint = transform;
        parentBoss = GetComponentInParent<GunnerRecruit>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        currentState = HeadState.Waiting;
        transform.localRotation = baseLocalRotation;
        if (laserEmitter != null) laserEmitter.enabled = false;  // 初始确保激光未发射
        cycleCoroutine = StartCoroutine(CycleRoutine());
        StartCoroutine(ExtraAttackRoutine());  // 独立于主循环的额外射击
    }

    protected override void OnDisable()
    {
        base.OnDisable();  // 基类 StopAllCoroutines
        cycleCoroutine = null;
    }

    public override void OnMove()
    {
        if (isDie) return;
        bool shaking = currentState == HeadState.Shaking;
        transform.localRotation = shaking
            ? baseLocalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * shakeSpeed) * shakeAmplitude)
            : baseLocalRotation;
    }

    public override void OnAttack() { }

    // ==================== 行为循环 ====================

    private IEnumerator CycleRoutine()
    {
        // 初始间隔后开始摇晃
        yield return new WaitForSeconds(shakeDelay);

        while (!isDie)
        {
            // 3-5 次子弹攻击
            int bulletCount = Random.Range(bulletCountMin, bulletCountMax + 1);
            for (int i = 0; i < bulletCount && !isDie; i++)
            {
                currentState = HeadState.Shaking;
                yield return new WaitForSeconds(attackDelay);
                currentState = HeadState.Attacking;
                transform.localRotation = baseLocalRotation;  // 摆正
                yield return BulletAttackRoutine();
            }

            if (isDie) break;

            // 1 次激光攻击
            currentState = HeadState.Shaking;
            yield return new WaitForSeconds(attackDelay);
            currentState = HeadState.Attacking;
            transform.localRotation = baseLocalRotation;  // 摆正
            yield return LaserAttackRoutine();
        }
    }

    // ==================== 子弹攻击 ====================

    private IEnumerator BulletAttackRoutine()
    {
        FireOneBullet();
        // 发射一颗后短暂保持不摆动
        yield return new WaitForSeconds(bulletAttackDuration);
    }

    private void FireOneBullet()
    {
        // 两种备弹，等概率随机发射其一
        GameObject prefab = PickBulletPrefab();
        if (prefab == null) return;

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        GameObject bullet = PoolManager.Release(prefab, origin, Quaternion.identity);
        if (bullet == null) return;
        EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(bulletDamage);
            eb.SetMoveDirection(Vector2.left);
            eb.SetMoveSpeed(bulletSpeed);
        }
    }

    /// <summary>
    /// 两种备弹等概率随机选其一：
    ///   都配置 → 各 50%；只配置一个 → 用那个；都没配置 → 返回 null。
    /// </summary>
    private GameObject PickBulletPrefab()
    {
        bool hasA = bulletPrefab != null;
        bool hasB = bulletPrefab2 != null;
        if (hasA && hasB)
            return Random.Range(0, 2) == 0 ? bulletPrefab : bulletPrefab2;
        if (hasA) return bulletPrefab;
        if (hasB) return bulletPrefab2;
        return null;
    }

    // ==================== 激光攻击 ====================

    private IEnumerator LaserAttackRoutine()
    {
        if (laserEmitter == null) yield break;

        // 激活 LaserEmitter，使其自我发射（内部按 firstInterval 计时后激活特效）
        laserEmitter.enabled = true;

        float elapsed = 0f;
        // 等待激光出现
        while (!isDie && !laserEmitter.IsEffectActive && elapsed < laserMaxWait)
        { elapsed += Time.deltaTime; yield return null; }
        // 等待激光发射完毕
        while (!isDie && laserEmitter.IsEffectActive && elapsed < laserMaxWait)
        { elapsed += Time.deltaTime; yield return null; }
        // 激光消失后再延迟一会儿才恢复摇晃
        if (!isDie)
            yield return new WaitForSeconds(laserResumeDelay);

        // 关闭 LaserEmitter，停止其自我循环（下次激光攻击再激活）
        laserEmitter.enabled = false;
    }

    // ==================== 额外独立射击 ====================

    private IEnumerator ExtraAttackRoutine()
    {
        if (extraAttackDelay > 0f)
            yield return new WaitForSeconds(extraAttackDelay);
        while (!isDie)
        {
            FireExtraBullet();
            if (extraAttackInterval > 0f)
                yield return new WaitForSeconds(extraAttackInterval);
            else
                yield return null;
        }
    }

    private void FireExtraBullet()
    {
        // 两种备弹等概率随机发射其一
        bool hasA = extraBulletPrefab != null;
        bool hasB = extraBulletPrefab2 != null;
        GameObject prefab = null;
        if (hasA && hasB)
            prefab = Random.Range(0, 2) == 0 ? extraBulletPrefab : extraBulletPrefab2;
        else if (hasA) prefab = extraBulletPrefab;
        else if (hasB) prefab = extraBulletPrefab2;
        if (prefab == null) return;

        Vector3 origin = extraFirePoint != null ? extraFirePoint.position : transform.position;
        GameObject bullet = PoolManager.Release(prefab, origin, Quaternion.identity);
        if (bullet == null) return;
        EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(extraBulletDamage);
            eb.SetMoveDirection(Vector2.left);
            eb.SetMoveSpeed(extraBulletSpeed);
        }
    }

    // ==================== 死亡 ====================

    protected override void OnDeath()
    {
        isDie = true;
        moveSpeed = 0;
        if (animator != null) animator.speed = 0;

        if (laserEmitter != null) laserEmitter.Stop();  // 立即停掉激光

        // 不禁用自身，爆炸后通知父物体禁用整个对象
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        AudioManager.Instance.PlaySFX(baozhaSFX);
        foreach (Transform t in boomTransform)
        {
            PoolManager.Release(baozha, t.position);
            yield return waitbaozha;
        }

        // 爆炸播完后通知父物体禁用整个 BOSS 对象
        if (parentBoss != null)
            parentBoss.DisableBoss();
    }
}
