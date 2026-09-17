using UnityEngine;

/// <summary>
/// 独立子弹变向组件：挂到任意 GameObject 上，若干秒后自动改变移动方向。
/// 不继承 EnemyBullet，自带移动逻辑，不依赖任何子弹基类。
/// 发射者通过 SetChangeAngle / SetMoveDirection 控制变向角度和初始方向。
/// </summary>
public class IndependentBulletRedirector : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Vector2 initialDirection = Vector2.left;

    [Header("变向")]
    [SerializeField] private float changeTime = 1f;
    [SerializeField] private float changeAngle = 90f;

    private Vector2 currentDirection;
    private float elapsed;
    private bool hasChanged;

    private float originalChangeTime;
    private float originalChangeAngle;
    private float originalMoveSpeed;

    private void Awake()
    {
        originalChangeTime = changeTime;
        originalChangeAngle = changeAngle;
        originalMoveSpeed = moveSpeed;
    }

    private void OnEnable()
    {
        currentDirection = initialDirection.normalized;
        elapsed = 0f;
        hasChanged = false;
        changeTime = originalChangeTime;
        changeAngle = originalChangeAngle;
        moveSpeed = originalMoveSpeed;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        if (!hasChanged && elapsed >= changeTime)
        {
            currentDirection = Quaternion.Euler(0, 0, changeAngle) * currentDirection;
            hasChanged = true;
        }

        transform.Translate(currentDirection * (moveSpeed * Time.deltaTime), Space.World);
    }

    /// <summary>发射者调用，设置变向角度（相对当前方向旋转）</summary>
    public void SetChangeAngle(float angle)
    {
        changeAngle = angle;
    }

    /// <summary>发射者调用，设置初始移动方向</summary>
    public void SetMoveDirection(Vector2 dir)
    {
        currentDirection = dir.normalized;
    }

    /// <summary>发射者调用，设置变向时间</summary>
    public void SetChangeTime(float time)
    {
        changeTime = time;
    }

    /// <summary>发射者调用，设置移动速度</summary>
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }
}
