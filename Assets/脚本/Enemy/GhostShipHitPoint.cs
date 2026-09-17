using System.Collections;
using UnityEngine;

/// <summary>
/// 幽冥船受击部位：继承 Enemy，重写死亡机制 + 攻击系统。
/// 死亡时爆炸特效播放后，直接通知父物体 GhostShip 禁用自身。
/// </summary>
public class GhostShipHitPoint : Enemy
{
    [Header("攻击")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefabA;
    [SerializeField] private GameObject bulletPrefabB;
    [SerializeField] private GameObject bulletPrefabC;              // C子弹：扇形普通子弹
    [SerializeField] private float initialAttackDelay = 2f;
    [SerializeField] private float attackInterval = 1.5f;
    [SerializeField] private int bulletDamageA = 0;                 // A子弹伤害，0则使用 Enemy 的 damege
    [SerializeField] private int bulletDamageB = 0;                 // B子弹伤害，0则使用 Enemy 的 damege
    [SerializeField] private int bulletDamageC = 0;                 // C子弹伤害，0则使用 Enemy 的 damege
    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private Vector2 bulletDirection = Vector2.left;
    [SerializeField] private float splitDamageCoefficient = 1f;     // 子弹分裂伤害系数（设置到 BulletSplitter）

    [Header("扇形攻击")]
    [SerializeField] private int fanBulletCount = 6;                // 每轮子弹数
    [SerializeField] private int fanRounds = 4;                     // 发射轮数
    [SerializeField] private float fanRoundInterval = 0.2f;         // 每轮间隔（秒）
    [SerializeField] private float fanSpreadAngle = 60f;            // 扇形总角度（度）
    [SerializeField] [Range(0f, 1f)] private float fanProbability = 0.35f; // 扇形攻击触发概率

    private GhostShip parentShip;
    private Coroutine attackCoroutine;

    protected override void Awake()
    {
        base.Awake();
        parentShip = GetComponentInParent<GhostShip>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        StartAttack();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        StopAttack();
    }

    protected override void OnDeath()
    {
        isDie = true;
        moveSpeed = 0;
        if (animator != null)
            animator.speed = 0;
        StopAttack();
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

        if (parentShip != null)
            parentShip.DisableShip();
    }

    private void StartAttack()
    {
        StopAttack();
        attackCoroutine = StartCoroutine(AttackRoutine());
    }

    private void StopAttack()
    {
        if (attackCoroutine != null) { StopCoroutine(attackCoroutine); attackCoroutine = null; }
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(initialAttackDelay);

        while (true)
        {
            Fire();
            yield return new WaitForSeconds(attackInterval);
        }
    }

    private void Fire()
    {
        if (firePoint == null) return;

        // 随机选择攻击模式：扇形攻击 或 普通单发
        if (bulletPrefabC != null && Random.value < fanProbability)
        {
            StartCoroutine(FireFanRoutine());
            return;
        }

        // 原有单发逻辑（A/B 子弹）
        if (bulletPrefabA == null && bulletPrefabB == null) return;

        bool useA = Random.value < 0.5f;
        GameObject prefab;
        int dmg;
        if (useA && bulletPrefabA != null)
        {
            prefab = bulletPrefabA;
            dmg = bulletDamageA;
        }
        else if (bulletPrefabB != null)
        {
            prefab = bulletPrefabB;
            dmg = bulletDamageB;
        }
        else
        {
            prefab = bulletPrefabA;
            dmg = bulletDamageA;
        }

        FireSingleBullet(prefab, bulletDirection, dmg);
    }

    /// <summary>
    /// 发射单颗子弹，设置伤害、方向、速度、分裂系数
    /// </summary>
    private void FireSingleBullet(GameObject prefab, Vector2 direction, int bulletDamage)
    {
        if (prefab == null || firePoint == null) return;

        Vector3 pos = firePoint.position;
        GameObject bullet = PoolManager.Release(prefab, pos, Quaternion.identity);

        var enemyBullet = bullet.GetComponent<EnemyBullet>();
        if (enemyBullet != null)
        {
            int dmg = bulletDamage > 0 ? bulletDamage : damege;
            enemyBullet.SetDamage(dmg);
            enemyBullet.SetMoveDirection(direction);
            enemyBullet.SetMoveSpeed(bulletSpeed);
        }

        var splitter = bullet.GetComponent<BulletSplitter>();
        if (splitter != null)
        {
            splitter.SetDamageCoefficient(splitDamageCoefficient);
        }
    }

    /// <summary>
    /// 扇形攻击协程：发射 fanRounds 轮，每轮 fanBulletCount 发C子弹，扇形均匀分布
    /// </summary>
    private IEnumerator FireFanRoutine()
    {
        for (int round = 0; round < fanRounds; round++)
        {
            FireFanRound();
            yield return new WaitForSeconds(fanRoundInterval);
        }
    }

    /// <summary>
    /// 发射一轮扇形C子弹：以 bulletDirection 为中心，左右展开 fanSpreadAngle 度
    /// </summary>
    private void FireFanRound()
    {
        if (bulletPrefabC == null || firePoint == null) return;

        float baseAngle = Mathf.Atan2(bulletDirection.y, bulletDirection.x) * Mathf.Rad2Deg;
        float startAngle = baseAngle - fanSpreadAngle / 2f;
        float angleStep = fanBulletCount > 1 ? fanSpreadAngle / (fanBulletCount - 1) : 0f;

        for (int i = 0; i < fanBulletCount; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            FireSingleBullet(bulletPrefabC, dir, bulletDamageC);
        }
    }

    public override void OnMove() { }
    public override void OnAttack() { }
}
