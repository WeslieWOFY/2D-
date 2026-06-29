using UnityEngine;
using System.Collections;

/// <summary>
/// 激光发射组件：自主发射特效，挂上即用
/// 第一次发射间隔5秒（从OnEnable计时），之后每次间隔8秒（从激光禁用后计时）
/// </summary>
public class LaserEmitter : MonoBehaviour
{
    [Header("特效")]
    [SerializeField] private GameObject effectObject;         // 特效物体

    [Header("发射间隔")]
    [SerializeField] private float firstInterval = 5f;        // 第一次发射间隔
    [SerializeField] private float interval = 8f;             // 之后每次发射间隔

    private Coroutine fireCoroutine;
    private bool stopped;

    private void OnEnable()
    {
        stopped = false;
        if (fireCoroutine != null)
            StopCoroutine(fireCoroutine);
        fireCoroutine = StartCoroutine(FireRoutine(firstInterval));
    }

    private void OnDisable()
    {
        if (fireCoroutine != null)
        {
            StopCoroutine(fireCoroutine);
            fireCoroutine = null;
        }
    }

    /// <summary>
    /// 彻底停止：关协程、关特效、关激光
    /// </summary>
    public void Stop()
    {
        stopped = true;

        if (fireCoroutine != null)
        {
            StopCoroutine(fireCoroutine);
            fireCoroutine = null;
        }

        if (effectObject != null)
            effectObject.SetActive(false);
    }

    private IEnumerator FireRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (effectObject != null)
            effectObject.SetActive(true);
    }

    /// <summary>
    /// 由LaserEffect.OnDisable回调：效果结束后等待interval秒再次发射
    /// </summary>
    public void OnEffectDisabled()
    {
        if (!gameObject.activeInHierarchy || stopped) return;

        if (fireCoroutine != null)
            StopCoroutine(fireCoroutine);
        fireCoroutine = StartCoroutine(FireRoutine(interval));
    }
}
