using UnityEngine;

/// <summary>
/// 子弹变向组件 V2 —— 定时改向：行动一律朝左 + 表现瞬间朝左。
/// 素材默认朝上，变向后 transform.rotation.z = 90° 即朝左。
/// 挂载在带 EnemyBullet 的对象上。
/// </summary>
public class BulletDirectionChangerV2 : MonoBehaviour
{
    [Header("变向开关")]
    [SerializeField] private bool disableChange = false;

    [Header("变向时间")]
    [SerializeField] private float changeTime = 0.05f;

    private EnemyBullet enemyBullet;
    private SpriteRenderer spriteRenderer;
    private float timer;

    /// <summary>是否已经完成变向</summary>
    public bool HasChanged { get; private set; }

    private void Awake()
    {
        enemyBullet = GetComponent<EnemyBullet>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        timer = 0f;
        HasChanged = false;
    }

    private void Update()
    {
        if (disableChange || HasChanged || enemyBullet == null) return;

        timer += Time.deltaTime;
        if (timer >= changeTime)
            ApplyDirectionChange();
    }

    // ==================== 公开 API ====================

    /// <summary>运行时配置变向参数</summary>
    public void Configure(float newChangeTime, bool shouldDisableChange)
    {
        changeTime = newChangeTime;
        disableChange = shouldDisableChange;
    }

    /// <summary>初始化精灵朝向（子弹生成时调用）</summary>
    public void InitFacing(Vector2 direction)
    {
        if (spriteRenderer != null)
            FaceDirection(direction);
    }

    // ==================== 内部 ====================

    private void ApplyDirectionChange()
    {
        // 行动方向：一律朝左
        enemyBullet.SetMoveDirection(Vector2.left);
        HasChanged = true;

        // 表现方向：素材朝上 → 旋转 90° 朝左
        transform.rotation = Quaternion.Euler(0f, 0f, 90f);
    }

    /// <summary>精灵瞬间朝向指定方向（素材朝上 → rotZ = Atan2 - 90°）</summary>
    private void FaceDirection(Vector2 dir)
    {
        if (spriteRenderer == null) return;

        float rotZ = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, rotZ);
    }
}
