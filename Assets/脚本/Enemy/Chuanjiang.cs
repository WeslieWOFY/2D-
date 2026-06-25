using System.Collections;
using UnityEngine;

/// <summary>
/// 船桨敌人：每次攻击时精灵变大后复原，发射4发对称方向的子弹
/// </summary>
public class Chuanjiang : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private int damage = 10;

    [Header("船桨特有属性")]
    [SerializeField] private GameObject bulletPrefab;           // 子弹预制体
    [SerializeField] private Transform muzzle;                  // 发射口位置
    [SerializeField] private float shootInterval = 2.5f;        // 射击间隔（秒）
    [SerializeField] private float scaleUpAmount = 1.3f;        // 放大倍数
    [SerializeField] private float scaleDuration = 0.2f;        // 放大/复原时间

    [Header("子弹角度设置（相对于向左方向，正值向上，负值向下）")]
    [SerializeField] private float[] bulletAngles = { 60f, 20f, -20f, -40f };  // 子弹角度数组

    private Coroutine shootCoroutine;
    private Coroutine scaleCoroutine;
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
        shootCoroutine = StartCoroutine(ShootRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        shootCoroutine = null;
        scaleCoroutine = null;
    }

    private void Update()
    {
        OnMove();
    }

    /// <summary>
    /// 移动逻辑
    /// </summary>
    private void OnMove()
    {
        // 向左移动
        Vector2 movement = Vector2.left * moveSpeed * Time.deltaTime;
        transform.Translate(movement);

        // 检查是否超出屏幕
        CheckIfOutOfScreen();
    }

    private void CheckIfOutOfScreen()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);
        if (viewportPos.x < -0.2f)
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 射击协程
    /// </summary>
    private IEnumerator ShootRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(shootInterval);

            if (gameObject.activeSelf)
            {
                yield return StartCoroutine(AttackSequence());
            }
        }
    }

    /// <summary>
    /// 攻击序列：精灵变大 → 发射子弹 → 复原
    /// </summary>
    private IEnumerator AttackSequence()
    {
        // 1. 精灵变大
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(ScaleUp());

        yield return new WaitForSeconds(scaleDuration);

        // 2. 发射4发对称子弹
        FireSymmetricBullets();

        // 3. 精灵复原
        yield return StartCoroutine(ScaleDown());
    }

    /// <summary>
    /// 精灵放大动画
    /// </summary>
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

    /// <summary>
    /// 精灵复原动画
    /// </summary>
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

    /// <summary>
    /// 发射4发对称方向的子弹
    /// 方向：向左偏上、向左、向左偏下、向下（对称分布）
    /// </summary>
    private void FireSymmetricBullets()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("船桨：子弹预制体未设置！");
            return;
        }

        Vector3 shootPos = muzzle != null ? muzzle.position : transform.position;

        // 子弹的角度（相对于向左180°，正值向上偏移，负值向下偏移）
        float[] angles = new float[bulletAngles.Length];
        for (int i = 0; i < bulletAngles.Length; i++)
        {
            angles[i] = 180f - bulletAngles[i];
        }

        for (int i = 0; i < angles.Length; i++)
        {
            float rad = angles[i] * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

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
}
