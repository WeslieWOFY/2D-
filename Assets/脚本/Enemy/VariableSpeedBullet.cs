using System.Collections;
using UnityEngine;

/// <summary>
/// 变速子弹：飞行一定时间后速度降为原来的2/3
/// </summary>
public class VariableSpeedBullet : EnemyBullet
{
    [Header("变速设置")]
    [SerializeField] private float slowDownDelay = 1.5f;  // 多少秒后开始减速

    private float prefabOriginalSpeed;  // 预制体原始速度，Awake时记录，整个生命周期不变
    private float originalSpeed;
    private bool hasSlowedDown;
    private Coroutine slowDownRoutine;

    protected override void OnEnable()
    {
        hasSlowedDown = false;
        originalSpeed = originalMoveSpeed;
        if (slowDownRoutine != null)
        StopCoroutine(slowDownRoutine);
        slowDownRoutine = StartCoroutine(SlowDownRoutine());
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        if (slowDownRoutine != null)
        {
            StopCoroutine(slowDownRoutine);
            slowDownRoutine = null;
        }
    }

    private IEnumerator SlowDownRoutine()
    {
        yield return new WaitForSeconds(slowDownDelay);

        if (!hasSlowedDown)
        {
            hasSlowedDown = true;
            SetMoveSpeed(originalSpeed * 2f / 3f);
        }
    }
}
