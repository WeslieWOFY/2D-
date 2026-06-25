using System.Collections;
using UnityEngine;

/// <summary>
/// 榴弹炮：启用后直线上升飞出屏幕 → 完全出屏后随机Z角度+随机X位置 → 纯直线下落。
/// </summary>
public class Liudanpao : EnemyBullet
{
    [Header("榴弹设置")]
    [SerializeField] private float riseSpeed = 4f;
    [SerializeField] private float fallSpeed = 5f;
    [SerializeField] private float waitBeforeFall = 1f;
    [SerializeField] private float riseHeightOffset = 2f;

    private enum BulletState { Rising, Waiting, Falling }
    private BulletState currentState;
    private Coroutine waitRoutine;

    protected override void OnEnable()
    {
        base.OnEnable();

        isrotate = false;  // 上升阶段不旋转

        currentState = BulletState.Rising;
        SetMoveDirection(Vector2.up);
        SetMoveSpeed(riseSpeed);
    }

    private void OnDisable()
    {
        if (waitRoutine != null)
        {
            StopCoroutine(waitRoutine);
            waitRoutine = null;
        }
    }

    protected override void Update()
    {
        switch (currentState)
        {
            case BulletState.Rising:
                Move();
                if (transform.position.y > topBoundary + riseHeightOffset)
                {
                    // 完全出屏，锁定一个随机Z角度（只改这一次）
                    float randomZ = Random.Range(0f, 360f);
                    transform.rotation = Quaternion.Euler(0, 0, randomZ);

                    currentState = BulletState.Waiting;
                    waitRoutine = StartCoroutine(WaitThenFall());
                }
                break;

            case BulletState.Waiting:
                break;

            case BulletState.Falling:
                // 世界空间直线下落，不受Z旋转影响
                transform.Translate(Vector2.down * fallSpeed * Time.deltaTime, Space.World);
                if (transform.position.y < bottomBoundary - boundsOffset)
                {
                    DisableBullet();
                }
                break;
        }
    }

    private IEnumerator WaitThenFall()
    {
        yield return new WaitForSeconds(waitBeforeFall);
        currentState = BulletState.Falling;
        // 在屏幕X范围内随机选位置
        float randomX = Random.Range(leftBoundary + boundsOffset, rightBoundary - boundsOffset);
        transform.position = new Vector3(randomX, topBoundary + riseHeightOffset, transform.position.z);
    }
}
