using System.Collections;
using UnityEngine;

/// <summary>
/// 枪弹新兵手部（纯 MonoBehaviour，不继承任何类、不实现任何接口）。
///
/// 初始间隔后，手臂绕固定点A(pivotPoint)在「初始角度(当前角度往下偏移)」与
/// 「末尾角度(左上方)」之间来回摆动；摆动开始后经 fireStartDelay 开始发射子弹：
/// 3个发射位置，每个位置每轮按 bulletPairInterval 间隔发射2发子弹（两种不同子弹），
/// 子弹方向 = 对准位置→发射位置，共3×2=6发。
/// 发射时暂停摆动，发射完毕后恢复。
/// 对准位置(aimPoints)与发射位置(firePoints)均由开发者在Inspector设置，一一对应。
/// </summary>
public class GunnerRecruitHand : MonoBehaviour
{
    [Header("摆动设置")]
    [SerializeField] private float startDelay = 2f;        // 初始间隔
    [SerializeField] private float startDownOffset = 30f;  // 初始角度(最低) = 当前角度往下偏移的度数
    [SerializeField] private float endAngle = 135f;        // 末尾角度（左上方）
    [SerializeField] private float swingSpeed = 1.5f;      // 摆动速度
    [SerializeField] private Transform pivotPoint;         // 固定点A，围绕此点旋转；空=绕自身位置

    [Header("发射位置（3个，建议为手臂子物体）")]
    [SerializeField] private Transform[] firePoints = new Transform[3];

    [Header("子弹")]
    [SerializeField] private GameObject bulletPrefab;    // 第1发子弹
    [SerializeField] private GameObject bulletPrefab2;   // 第2发子弹（另一种）
    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private int bulletDamage = 10;

    [Header("发射节奏")]
    [SerializeField] private float fireStartDelay = 0.5f;   // 摆动开始后多久开始发射
    [SerializeField] private float fireInterval = 1.2f;      // 轮与轮之间的间隔
    [SerializeField] private float bulletPairInterval = 0.15f; // 每位置2发子弹之间的间隔

    [Header("对准位置（3个，与发射位置一一对应）")]
    [SerializeField] private Transform[] aimPoints = new Transform[3];

    private float startAngle;
    private bool swinging;
    private bool firing;          // 发射时暂停摆动
    private float swingElapsed;   // 摆动累计时间（暂停时不增长）
    private float lastSwingAngle; // 上一帧摆动角度（用于 RotateAround 增量）
    private Coroutine mainRoutine;

    private void Awake()
    {
        startAngle = transform.localEulerAngles.z - startDownOffset;
    }

    private void OnEnable()
    {
        swinging = false;
        firing = false;
        swingElapsed = 0f;
        if (mainRoutine != null) StopCoroutine(mainRoutine);
        mainRoutine = StartCoroutine(MainRoutine());
    }

    private void OnDisable()
    {
        swinging = false;
        if (mainRoutine != null) { StopCoroutine(mainRoutine); mainRoutine = null; }
    }

    private IEnumerator MainRoutine()
    {
        yield return new WaitForSeconds(startDelay);
        swinging = true;
        lastSwingAngle = startAngle;
        if (fireStartDelay > 0f)
            yield return new WaitForSeconds(fireStartDelay);
        yield return FireLoop();
    }

    private void Update()
    {
        if (!swinging || firing) return;
        swingElapsed += Time.deltaTime;
        float t = (1f - Mathf.Cos(swingElapsed * swingSpeed)) * 0.5f;
        float angle = Mathf.LerpAngle(startAngle, endAngle, t);

        if (pivotPoint != null)
        {
            // 围绕固定点A旋转：用 RotateAround 施加增量角度
            float delta = Mathf.DeltaAngle(lastSwingAngle, angle);
            transform.RotateAround(pivotPoint.position, Vector3.forward, delta);
            lastSwingAngle = angle;
        }
        else
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private IEnumerator FireLoop()
    {
        while (swinging)
        {
            firing = true;  // 暂停摆动

            // 第1发：bulletPrefab
            FireAll(bulletPrefab);
            if (bulletPairInterval > 0f)
                yield return new WaitForSeconds(bulletPairInterval);
            // 第2发：bulletPrefab2
            FireAll(bulletPrefab2);

            firing = false;  // 恢复摆动

            if (fireInterval > 0f)
                yield return new WaitForSeconds(fireInterval);
            else
                yield return null;
        }
    }

    private void FireAll(GameObject prefab)
    {
        if (prefab == null || firePoints == null || aimPoints == null) return;
        for (int i = 0; i < firePoints.Length && i < aimPoints.Length; i++)
        {
            if (firePoints[i] != null && aimPoints[i] != null)
                FireOneBullet(prefab, firePoints[i], aimPoints[i]);
        }
    }

    private void FireOneBullet(GameObject prefab, Transform firePoint, Transform aimPoint)
    {
        if (prefab == null) return;

        // 子弹方向 = 对准位置 → 发射位置
        Vector2 dir = firePoint.position - aimPoint.position;
        if (dir.sqrMagnitude > 0.0001f)
            dir = dir.normalized;
        else
            dir = Vector2.left;

        // 精灵默认朝左，+180° 使其朝向运动方向
        float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, angleDeg + 180f);

        GameObject bullet = PoolManager.Release(prefab, firePoint.position, rot);
        if (bullet == null) return;
        EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.SetDamage(bulletDamage);
            eb.SetMoveDirection(dir);
            eb.SetMoveSpeed(bulletSpeed);
        }
    }
}
