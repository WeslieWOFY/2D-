using System.Collections;
using UnityEngine;

/// <summary>
/// 旋转齿轮：按固定轨迹运动，自旋由同物体的 SelfRotator 组件驱动。
/// 通过控制 SelfRotator.SetSpeed / ResetSpeed 实现阶段3的加速自旋与恢复。
///
/// 轨迹描述（相对于初始局部位置）：
///   1. 下落一段距离
///   2. 向左移动
///   3. 加速自旋向上
///   4. 朝右下角斜向移动
///   5. 向右回到原位置横坐标
///   6. 向上回到原位置纵坐标
///   7. 停顿一段时间后重复
///
/// 挂载到齿轮子物体上，该物体需同时挂载 SelfRotator。
/// </summary>
[RequireComponent(typeof(SelfRotator))]
public class RotatingGear : MonoBehaviour
{
    [Header("阶段1：下落")]
    [SerializeField] private float fallDistance = 2f;
    [SerializeField] private float fallDuration = 0.5f;

    [Header("阶段2：左移")]
    [SerializeField] private float leftDistance = 3f;
    [SerializeField] private float leftDuration = 0.5f;

    [Header("阶段3：加速自旋向上")]
    [SerializeField] private float upDistance = 2f;
    [SerializeField] private float upDuration = 0.5f;

    [Header("阶段4：右下斜移")]
    [SerializeField] private float diagonalRightAmount = 2f;
    [SerializeField] private float diagonalDownAmount = 3f;
    [SerializeField] private float diagonalDuration = 0.5f;

    [Header("阶段5：右移（回到原X）")]
    [SerializeField] private float returnRightDuration = 0.5f;

    [Header("阶段6：上移（回到原Y）")]
    [SerializeField] private float returnUpDuration = 0.5f;

    [Header("自旋速度（写入 SelfRotator）")]
    [SerializeField] private float baseSpinSpeed = 90f;
    [SerializeField] private float acceleratedSpinSpeed = 540f;

    [Header("节奏")]
    [SerializeField] private float initialDelay = 0f;
    [SerializeField] private float pauseDuration = 2f;

    [Header("伤害")]
    [SerializeField] private int colliderdamege = 20;
    [SerializeField] private float damageCooldown = 0.5f;

    private Vector3 originalLocalPos;
    private SelfRotator selfRotator;
    private Coroutine trajectoryCoroutine;
    private float lastDamageTime = -1f;

    private void Awake()
    {
        selfRotator = GetComponent<SelfRotator>();
    }

    private void OnEnable()
    {
        originalLocalPos = transform.localPosition;

        if (trajectoryCoroutine != null)
        {
            StopCoroutine(trajectoryCoroutine);
            trajectoryCoroutine = null;
        }

        trajectoryCoroutine = StartCoroutine(TrajectoryRoutine());
    }

    private void OnDisable()
    {
        selfRotator.ResetSpeed();

        if (trajectoryCoroutine != null)
        {
            StopCoroutine(trajectoryCoroutine);
            trajectoryCoroutine = null;
        }
    }

    private IEnumerator TrajectoryRoutine()
    {
        // ── 初始延迟：原地自旋，不移动 ──
        float deadline = Time.time + initialDelay;
        while (Time.time < deadline)
        {
            selfRotator.SetSpeed(baseSpinSpeed);
            transform.localPosition = originalLocalPos;
            yield return null;
        }

        Vector3 orig = originalLocalPos;

        while (true)
        {
            Vector3 p1 = orig + Vector3.down * fallDistance;
            yield return MovePhase(p1, fallDuration, baseSpinSpeed);

            Vector3 p2 = p1 + Vector3.left * leftDistance;
            yield return MovePhase(p2, leftDuration, baseSpinSpeed);

            Vector3 p3 = p2 + Vector3.up * upDistance;
            yield return MovePhase(p3, upDuration, acceleratedSpinSpeed);

            Vector3 p4 = p3 + new Vector3(diagonalRightAmount, -diagonalDownAmount, 0);
            yield return MovePhase(p4, diagonalDuration, baseSpinSpeed);

            Vector3 p5 = new Vector3(orig.x, p4.y, orig.z);
            yield return MovePhase(p5, returnRightDuration, baseSpinSpeed);

            yield return MovePhase(orig, returnUpDuration, baseSpinSpeed);

            // 停顿：每帧锁定位置，防止其他系统干扰
            selfRotator.SetSpeed(baseSpinSpeed);
            float pauseElapsed = 0f;
            while (pauseElapsed < pauseDuration)
            {
                pauseElapsed += Time.deltaTime;
                transform.localPosition = orig;
                yield return null;
            }
        }
    }

    private IEnumerator MovePhase(Vector3 targetLocalPos, float duration, float spinSpeed)
    {
        selfRotator.SetSpeed(spinSpeed);
        Vector3 start = transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 限制每帧最多走 1/10 进度，保证每个阶段至少 10 帧平滑过渡
            elapsed += Mathf.Min(Time.deltaTime, duration / 10f);
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localPosition = Vector3.Lerp(start, targetLocalPos, t);
            yield return null;
        }

        transform.localPosition = targetLocalPos;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        if (Time.time - lastDamageTime < damageCooldown) return;
        lastDamageTime = Time.time;
        PlayerKongzhi player = collision.gameObject.GetComponent<PlayerKongzhi>();
        player.ChangeRed();
        GameManager.Instance.PlayerTakeDamage(colliderdamege);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;
        if (Time.time - lastDamageTime < damageCooldown) return;
        lastDamageTime = Time.time;
        PlayerKongzhi player = collision.gameObject.GetComponent<PlayerKongzhi>();
        player.ChangeRed();
        GameManager.Instance.PlayerTakeDamage(colliderdamege);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !enabled) return;

        Vector3 orig = originalLocalPos;
        Vector3 parentPos = transform.parent != null ? transform.parent.position : Vector3.zero;

        Vector3 W(Vector3 lp) => parentPos + lp;

        Vector3 p1 = orig + Vector3.down * fallDistance;
        Vector3 p2 = p1 + Vector3.left * leftDistance;
        Vector3 p3 = p2 + Vector3.up * upDistance;
        Vector3 p4 = p3 + new Vector3(diagonalRightAmount, -diagonalDownAmount, 0);
        Vector3 p5 = new Vector3(orig.x, p4.y, orig.z);

        Gizmos.color = Color.green;
        DrawArrow(W(orig), W(p1));
        DrawArrow(W(p1), W(p2));
        DrawArrow(W(p2), W(p3));
        Gizmos.color = Color.yellow;
        DrawArrow(W(p3), W(p4));
        Gizmos.color = Color.green;
        DrawArrow(W(p4), W(p5));
        DrawArrow(W(p5), W(orig));

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(W(orig), 0.2f);
    }

    private void DrawArrow(Vector3 from, Vector3 to)
    {
        Gizmos.DrawLine(from, to);
        Vector3 dir = (to - from).normalized;
        Vector3 right = Quaternion.Euler(0, 0, 135) * dir * 0.3f;
        Vector3 left = Quaternion.Euler(0, 0, -135) * dir * 0.3f;
        Gizmos.DrawLine(to, to + right);
        Gizmos.DrawLine(to, to + left);
    }
#endif
}
