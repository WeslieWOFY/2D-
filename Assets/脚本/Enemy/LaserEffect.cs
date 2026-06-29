using UnityEngine;

/// <summary>
/// 激光特效类：挂载在特效物体上，动画事件激活激光，OnDisable关闭激光并回调发射器
/// </summary>
public class LaserEffect : MonoBehaviour
{
    [Header("激光")]
    [SerializeField] private GameObject[] lasers;             // 激光物体（特效的子物体）
    [SerializeField] private LaserEmitter emitter;            // 激光发射器（用于回调）
    [SerializeField] private float duration = 2f;             // 激光持续时间

    private Coroutine durationCoroutine;

    /// <summary>
    /// 由动画事件调用：激活所有激光，开始持续计时
    /// </summary>
    public void ActivateLaser()
    {
        foreach (GameObject laser in lasers)
        {
            if (laser != null)
                laser.SetActive(true);
        }

        if (durationCoroutine != null)
            StopCoroutine(durationCoroutine);
        durationCoroutine = StartCoroutine(DurationRoutine());
    }

    private System.Collections.IEnumerator DurationRoutine()
    {
        yield return new WaitForSeconds(duration);
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (durationCoroutine != null)
        {
            StopCoroutine(durationCoroutine);
            durationCoroutine = null;
        }

        foreach (GameObject laser in lasers)
        {
            if (laser != null)
                laser.SetActive(false);
        }

        if (emitter != null)
            emitter.OnEffectDisabled();
    }
}
