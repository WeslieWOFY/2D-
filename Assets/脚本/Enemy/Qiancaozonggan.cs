using System.Collections;
using UnityEngine;

/// <summary>
/// 前操纵杆：发射子弹，发射时自身变大后复原
/// </summary>
public class Qiancaozonggan : MonoBehaviour
{
    [Header("发射设置")]
    [SerializeField] private GameObject bulletPrefab;           // 子弹预制体
    [SerializeField] private Transform muzzle;                  // 发射点
    [SerializeField] private float startupDelay = 1f;          // 前摇时间
    [SerializeField] private float shootInterval = 2f;          // 发射间隔
    [SerializeField] private int damage = 10;                   // 子弹伤害
    [SerializeField] private float minAngle = -45f;             // 最小角度（相对于向左）
    [SerializeField] private float maxAngle = 45f;              // 最大角度（相对于向左）

    [Header("缩放设置")]
    [SerializeField] private float scaleUpAmount = 1.3f;        // 放大倍数
    [SerializeField] private float scaleDuration = 0.2f;        // 放大/复原时间

    private Coroutine shootCoroutine;
    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        transform.localScale = originalScale;

        if (shootCoroutine != null)
            StopCoroutine(shootCoroutine);
        shootCoroutine = StartCoroutine(StartupAndShoot());
    }

    private IEnumerator StartupAndShoot()
    {
        // 前摇等待
        yield return new WaitForSeconds(startupDelay);

        // 开始发射
        yield return ShootRoutine();
    }

    private void OnDisable()
    {
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }
    }

    private IEnumerator ShootRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(shootInterval);

            if (gameObject.activeSelf)
            {
                yield return StartCoroutine(ShootSequence());
            }
        }
    }

    private IEnumerator ShootSequence()
    {
        // 1. 放大
        yield return StartCoroutine(ScaleUp());

        // 2. 发射子弹
        FireBullet();

        // 3. 复原
        yield return StartCoroutine(ScaleDown());
    }

    private IEnumerator ScaleUp()
    {
        Vector3 targetScale = originalScale * scaleUpAmount;
        float elapsed = 0f;

        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scaleDuration);
            transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        transform.localScale = targetScale;
    }

    private IEnumerator ScaleDown()
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scaleDuration);
            transform.localScale = Vector3.Lerp(startScale, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
    }

    private void FireBullet()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("前操纵杆：子弹预制体未设置！");
            return;
        }

        // 随机角度
        float randomAngleOffset = Random.Range(minAngle, maxAngle);
        float finalAngle = 180f - randomAngleOffset;  // 基准向左180°

        // 转换为方向向量
        float rad = finalAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        // 发射位置
        Vector3 shootPos = muzzle != null ? muzzle.position : transform.position;

        // 生成子弹
        GameObject bullet = PoolManager.Release(bulletPrefab, shootPos);
        if (bullet != null)
        {
            EnemyBullet eb = bullet.GetComponent<EnemyBullet>();
            if (eb != null)
            {
                eb.SetMoveDirection(direction);
                eb.SetDamage(damage);
            }
        }
    }
}
