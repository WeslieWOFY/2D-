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

    private void Awake()
    {
        // 只在首次实例化时记录一次，后续对象池复用不会再次调用
        prefabOriginalSpeed = GetMoveSpeed();
    }

    protected override void OnEnable()
    {
        hasSlowedDown = false;
        // 恢复为预制体的原始速度，避免对象池复用导致速度逐轮递减
        SetMoveSpeed(prefabOriginalSpeed);
        originalSpeed = prefabOriginalSpeed;

        if (slowDownRoutine != null)
        StopCoroutine(slowDownRoutine);
        slowDownRoutine = StartCoroutine(SlowDownRoutine());
        base.OnEnable();
    }

    private void OnDisable()
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
