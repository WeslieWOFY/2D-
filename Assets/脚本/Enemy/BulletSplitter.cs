using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 子弹分裂组件：挂载到子弹对象上（不是EnemyBullet的子类，作为额外组件使用）。
/// 激活后先正常飞行，到达指定时间后强制停止移动，再按配置的多个波次向四周发射子子弹。
/// 每波可独立配置半径、初始角度、间隔角度、发射数量。
/// </summary>
[RequireComponent(typeof(EnemyBullet))]
public class BulletSplitter : MonoBehaviour
{
    [Header("激活延迟")]
    [SerializeField] private float activationDelay = 0.8f;   // 激活后多久开始发射子子弹（总时长）

    [Header("分裂前停顿")]
    [SerializeField] private float stopBeforeSplit = 0.5f;   // 分裂前多少秒强制停止移动（必须 <= activationDelay）

    [Header("波次设置")]
    [SerializeField] private WaveConfig[] waves;             // 每一波的配置（所有波同时发射）

    [Header("子弹设置")]
    [SerializeField] private GameObject childBulletPrefab;   // 要发射的子子弹预制体
    [SerializeField] private int childDamage = 10;           // 子子弹伤害
    [SerializeField] private float childMoveSpeed = 4f;      // 子子弹移速（0则保持预制体默认值）

    [Header("特效")]
    [SerializeField] private GameObject splitEffect;         // 分裂时播放的特效（仅播放一次）

    private bool hasSplit;
    private Coroutine splitCoroutine;
    private EnemyBullet parentBullet;
    private float originalSpeed;

    /// <summary>
    /// 单波配置
    /// </summary>
    [System.Serializable]
    public class WaveConfig
    {
        [Tooltip("子子弹离发射点的距离")]
        public float radius = 0.6f;
        [Tooltip("该波第一个子弹的角度（度），0=左，90=上，-90=下，180=右")]
        public float startAngle = 0f;
        [Tooltip("相邻子弹之间的角度间隔（度）")]
        public float angleStep = 30f;
        [Tooltip("该波发射的子弹数量")]
        public int bulletCount = 8;
    }

    private void Awake()
    {
        parentBullet = GetComponent<EnemyBullet>();

        // 如果没有配置波次，给一个默认波次避免空
        if (waves == null || waves.Length == 0)
        {
            waves = new WaveConfig[]
            {
                new WaveConfig
                {
                    radius = 0.6f,
                    startAngle = 0f,
                    angleStep = 45f,
                    bulletCount = 8
                }
            };
        }
    }

    private void OnEnable()
    {
        hasSplit = false;
        if (splitCoroutine != null)
            StopCoroutine(splitCoroutine);
        splitCoroutine = StartCoroutine(SplitRoutine());
    }

    private void OnDisable()
    {
        if (splitCoroutine != null)
        {
            StopCoroutine(splitCoroutine);
            splitCoroutine = null;
        }
    }

    /// <summary>
    /// 分裂主协程：正常飞行 → 强制停止 → 逐波发射
    /// </summary>
    private IEnumerator SplitRoutine()
    {
        // 阶段1：正常飞行（activationDelay - stopBeforeSplit 秒）
        float moveDuration = Mathf.Max(0f, activationDelay - stopBeforeSplit);
        if (moveDuration > 0f)
            yield return new WaitForSeconds(moveDuration);

        // 阶段2：强制停止移动
        if (parentBullet != null)
        {
            originalSpeed = parentBullet.GetMoveSpeed();
            parentBullet.SetMoveSpeed(0f);
        }

        yield return new WaitForSeconds(stopBeforeSplit);

        if (hasSplit) yield break;
        hasSplit = true;

        // 阶段3：一次性发射所有波次
        // 播放分裂特效（仅一次）
        if (splitEffect != null)
        {
            PoolManager.Release(splitEffect, transform.position);
        }

        for (int w = 0; w < waves.Length; w++)
        {
            if (waves[w] == null) continue;
            SpawnWave(waves[w]);
        }

        // 自身子弹结束
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 发射一波子弹
    /// </summary>
    private void SpawnWave(WaveConfig wave)
    {
        if (childBulletPrefab == null) return;

        for (int i = 0; i < wave.bulletCount; i++)
        {
            float angle = wave.startAngle + wave.angleStep * i;
            float rad = angle * Mathf.Deg2Rad;

            // 远离中心点的方向
            Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            // 计算子子弹生成位置（在父子弹周围，偏移 radius）
            Vector3 spawnPos = transform.position + (Vector3)(direction * wave.radius);

            // 旋转：精灵默认朝左(Vector2.left)，+180° 使其朝向远离中心点的方向
            // rot * Vector2.left = direction，因此 rot = Quaternion.Euler(0,0, angle+180)
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle + 180f);

            GameObject child = PoolManager.Release(childBulletPrefab, spawnPos, rotation);
            if (child != null)
            {
                EnemyBullet eb = child.GetComponent<EnemyBullet>();
                if (eb != null)
                {
                    eb.SetDamage(childDamage);
                    eb.SetMoveDirection(direction);
                    if (childMoveSpeed > 0f)
                    {
                        eb.SetMoveSpeed(childMoveSpeed);
                    }
                }
            }
        }
    }
}
