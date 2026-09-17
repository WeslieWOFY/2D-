using UnityEngine;

/// <summary>
/// 派罗斯 —— 最终 BOSS。
/// 不继承 Enemy，独立 MonoBehaviour（同 MonkeySlateBoss / GhostShip 风格）。
/// 因为是最终 BOSS：只实现入场，不实现撤离（不挂 ISecretBoss，无 TriggerRetreat）。
///
/// 入场（与其他秘密关卡 BOSS 一致）：从屏幕右侧固定向左飞入，
///   到达目标 X 坐标 或 入场超时 → 进入战斗。
/// </summary>
public class Pyros : MonoBehaviour
{
    [Header("派罗斯-入场")]
    [SerializeField] private float enterSpeed = 3f;              // 入场移动速度（向左）
    [SerializeField] private float enterTargetX = -2f;           // 入场目标 X 坐标，到达即进入战斗
    [SerializeField] private float enterDuration = 3f;           // 入场最长持续时间（秒），超时强制进入战斗

    [Header("派罗斯-随机漂移")]
    [SerializeField] private float wanderRadius = 0.8f;          // 漂移半径（相对入场结束位置）
    [SerializeField] private float wanderSpeed = 0.5f;           // 漂移速度
    [SerializeField] private float changeDirectionInterval = 2f; // 换方向间隔（秒）

    // ── 状态机 ──
    private enum BossState
    {
        Entering,   // 入场
        Battle,     // 战斗
    }
    private BossState currentState;

    private float enterElapsed;
    private Vector2 initialPosition;    // 入场结束后的位置（漂移锚点）
    private Vector2 wanderTarget;       // 当前漂移目标
    private Coroutine wanderCoroutine;
    private WaitForSeconds changeDirectionWait;

    // ==================== 生命周期 ====================

    private void OnEnable()
    {
        currentState = BossState.Entering;
        enterElapsed = 0f;
        initialPosition = transform.position;
        wanderTarget = transform.position;
        changeDirectionWait = new WaitForSeconds(changeDirectionInterval);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        wanderCoroutine = null;
    }

    private void Update()
    {
        switch (currentState)
        {
            case BossState.Entering:   EnterTick();   break;
            case BossState.Battle:     BattleTick();  break;
        }
    }

    // ==================== 入场 ====================

    /// <summary>入场：固定向左移动，到达目标 X 或超时后进入战斗</summary>
    private void EnterTick()
    {
        enterElapsed += Time.deltaTime;

        if (transform.position.x <= enterTargetX || enterElapsed >= enterDuration)
        {
            StartBattle();
        }
        else
        {
            transform.Translate(enterSpeed * Time.deltaTime * Vector3.left);
        }
    }

    /// <summary>战斗移动：在入场结束位置周围随机小范围漂移</summary>
    private void BattleTick()
    {
        Vector2 newPos = Vector2.MoveTowards(
            transform.position,
            wanderTarget,
            wanderSpeed * Time.deltaTime
        );
        transform.position = newPos;
    }

    /// <summary>入场完成，进入战斗并启动随机漂移</summary>
    private void StartBattle()
    {
        currentState = BossState.Battle;

        // 入场结束，记录当前位置作为漂移锚点
        initialPosition = transform.position;
        wanderTarget = transform.position;

        if (wanderCoroutine != null) StopCoroutine(wanderCoroutine);
        wanderCoroutine = StartCoroutine(WanderRoutine());
    }

    /// <summary>随机漂移协程：每隔一段时间在锚点周围随机选一个新目标点</summary>
    private System.Collections.IEnumerator WanderRoutine()
    {
        while (true)
        {
            yield return changeDirectionWait;

            if (gameObject.activeSelf && currentState == BossState.Battle)
            {
                float offsetX = Random.Range(-wanderRadius, wanderRadius);
                float offsetY = Random.Range(-wanderRadius, wanderRadius);
                wanderTarget = initialPosition + new Vector2(offsetX, offsetY);
            }
        }
    }

    /// <summary>受击点死亡回调：派罗斯本体禁用（无撤离）</summary>
    public void OnHitPointDied()
    {
        gameObject.SetActive(false);
    }
}
