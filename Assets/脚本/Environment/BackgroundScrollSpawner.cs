using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>组的生成方式 —— 想加新玩法就在这里加一项，并在 BackgroundScrollSpawner.SpawnOnce 里补对应分支。</summary>
public enum GroupSpawnMode
{
    /// <summary>顺序铺开：每次触发把数组里的 prefab 按顺序依次生成，相邻两个间隔 innerSpacing。适合拼一整段背景。</summary>
    Sequential,
    /// <summary>随机单发：每次触发只从数组里随机挑一个 prefab 生成。适合随机散布的装饰物。</summary>
    Random,
}

/// <summary>
/// 一个背景组 —— 一组同类型的背景 prefab，加上它自己的生成节奏。
/// 每个组都是完全独立的一套参数，互不影响。
/// </summary>
[System.Serializable]
public class BackgroundGroup
{
    [Tooltip("组名，只用于在 Inspector 里辨认，不影响运行")]
    public string groupName = "新组";

    [Tooltip("取消勾选可临时关掉这一组，不用删配置")]
    public bool enabled = true;

    [Header("背景预制体")]
    [Tooltip("这一组要生成的 prefab（必须已注册到 PoolManager 的池里，且身上挂有 MoveLeft）")]
    public GameObject[] prefabs;

    [Header("生成方式")]
    public GroupSpawnMode mode = GroupSpawnMode.Sequential;

    [Tooltip("【顺序铺开】同一次触发里，相邻两个 prefab 之间的间隔（秒）")]
    public float innerSpacing = 0.3f;

    [Header("触发节奏")]
    [Tooltip("关卡开始后多久，这一组第一次生成（秒）。调这个可以把各组错开出现")]
    public float startDelay = 0f;

    [Tooltip("【顺序铺开】两次触发之间的固定间隔（秒）")]
    public float spawnInterval = 10f;

    [Tooltip("【随机单发】两次触发之间的随机间隔下限（秒）")]
    public float randomIntervalMin = 3f;

    [Tooltip("【随机单发】两次触发之间的随机间隔上限（秒）")]
    public float randomIntervalMax = 8f;

    [Header("生成位置")]
    [Tooltip("在摄像机右边界往右多远处生成，保证是在画面外出现")]
    public float spawnAheadDistance = 12f;

    [Tooltip("基准 Y 坐标（世界坐标）")]
    public float baseY = 0f;

    [Tooltip("Y 轴随机浮动范围：最终 Y = baseY + Random(-randomYRange, +randomYRange)，0 表示不浮动")]
    public float randomYRange = 0f;

    [Header("生成次数")]
    [Tooltip("勾选则无限循环生成")]
    public bool loop = true;

    [Tooltip("不循环时，这一组总共触发几次")]
    public int spawnTimes = 1;

    /// <summary>取本次触发之后要等多久再触发下一次</summary>
    public float GetNextInterval()
    {
        if (mode == GroupSpawnMode.Random)
        {
            float lo = Mathf.Min(randomIntervalMin, randomIntervalMax);
            float hi = Mathf.Max(randomIntervalMin, randomIntervalMax);
            return Random.Range(lo, hi);
        }
        return spawnInterval;
    }
}

/// <summary>
/// 通用固定背景卷动器 —— 把背景拆成若干「组」，每组独立循环生成。
///
/// 职责边界：
///   · 本脚本只负责「在摄像机右侧外面，按各组自己的节奏从对象池里生成背景」
///   · 生成出来的背景靠自身挂的 MoveLeft 向左移动，移出摄像机左边界后由 MoveLeft 自行禁用回收
///   · 本脚本不干预移动和回收，也不改 MoveLeft 的速度（每组要视差就在 prefab 上调 MoveLeft.speed）
///
/// 三件事都可以在 Inspector 里按组单独调：
///   1. 分组    —— groups 数组，每组一个 prefabs 数组
///   2. 节奏    —— startDelay（第几秒开始）+ spawnInterval / randomIntervalMin~Max（多久来一次）
///   3. 生成方式 —— mode 选「顺序铺开」或「随机单发」
///
/// 前置条件（缺一不可）：
///   · prefab 必须已注册到 PoolManager 的池配置里，否则生成不出来并会在 Console 报一次警告
///   · prefab 身上必须挂有 MoveLeft，否则生成后会一直停在右侧不动
/// </summary>
public class BackgroundScrollSpawner : MonoBehaviour
{
    [Header("摄像机")]
    [Tooltip("用于取右边界。留空则自动用 Camera.main")]
    [SerializeField] private Camera targetCamera;

    [Header("背景组")]
    [Tooltip("每一组独立循环生成，互不影响")]
    [SerializeField] private BackgroundGroup[] groups;

    // 正在跑的每组协程，OnDisable 时统一停掉
    private readonly List<Coroutine> runningCoroutines = new();

    // 已经警告过「不在对象池里」的 prefab，避免每次生成都刷屏
    private readonly HashSet<GameObject> warnedPrefabs = new();

    // 取值时再兜底找 Camera.main，找到就缓存下来
    private Camera Cam
    {
        get
        {
            if (targetCamera == null) targetCamera = Camera.main;
            return targetCamera;
        }
    }

    // ==================== 生命周期 ====================

    private void OnEnable()
    {
        StartAllGroups();
    }

    private void OnDisable()
    {
        StopAllGroups();
    }

    private void StartAllGroups()
    {
        StopAllGroups();
        if (groups == null) return;

        foreach (BackgroundGroup group in groups)
        {
            if (group == null || !group.enabled) continue;

            if (group.prefabs == null || group.prefabs.Length == 0)
            {
                Debug.LogWarning($"[BackgroundScrollSpawner] 组「{group.groupName}」没有配置任何 prefab，已跳过。");
                continue;
            }

            // 每组一个协程 —— 这是「各组独立」的关键，任何一组卡住都不会影响别的组
            runningCoroutines.Add(StartCoroutine(RunGroup(group)));
        }
    }

    private void StopAllGroups()
    {
        foreach (Coroutine routine in runningCoroutines)
        {
            if (routine != null) StopCoroutine(routine);
        }
        runningCoroutines.Clear();
    }

    // ==================== 每组的主循环 ====================

    /// <summary>一组一条循环：先等自己的 startDelay，再按自己的间隔反复触发</summary>
    private IEnumerator RunGroup(BackgroundGroup group)
    {
        if (group.startDelay > 0f)
            yield return new WaitForSeconds(group.startDelay);

        int firedTimes = 0;
        while (true)
        {
            yield return StartCoroutine(SpawnOnce(group));
            firedTimes++;

            if (!group.loop && firedTimes >= group.spawnTimes)
                yield break;

            yield return new WaitForSeconds(group.GetNextInterval());
        }
    }

    /// <summary>触发一次生成 —— 新增生成方式时在这里补分支即可</summary>
    private IEnumerator SpawnOnce(BackgroundGroup group)
    {
        switch (group.mode)
        {
            case GroupSpawnMode.Sequential:
                // 顺序铺开：按数组顺序全部生成，相邻之间隔 innerSpacing
                for (int i = 0; i < group.prefabs.Length; i++)
                {
                    SpawnOne(group, group.prefabs[i]);

                    if (i < group.prefabs.Length - 1 && group.innerSpacing > 0f)
                        yield return new WaitForSeconds(group.innerSpacing);
                }
                break;

            case GroupSpawnMode.Random:
                // 随机单发：只挑一个
                SpawnOne(group, group.prefabs[Random.Range(0, group.prefabs.Length)]);
                break;

            default:
                Debug.LogWarning($"[BackgroundScrollSpawner] 组「{group.groupName}」的生成方式 {group.mode} 还没实现，已跳过。");
                break;
        }
    }

    // ==================== 单个对象生成 ====================

    private void SpawnOne(BackgroundGroup group, GameObject prefab)
    {
        if (prefab == null) return;

        GameObject spawned = PoolManager.Release(prefab, GetSpawnPosition(group));

        if (spawned == null && warnedPrefabs.Add(prefab))
        {
            Debug.LogWarning($"[BackgroundScrollSpawner] 预制体「{prefab.name}」不在对象池里，无法生成。" +
                             $"请把它加到 PoolManager 的池配置数组中。");
        }
    }

    /// <summary>生成点：摄像机右边界再往右 spawnAheadDistance，保证在画面外</summary>
    private Vector3 GetSpawnPosition(BackgroundGroup group)
    {
        float rightEdge = 0f;
        Camera cam = Cam;
        if (cam != null)
            rightEdge = cam.ViewportToWorldPoint(new Vector3(1f, 0f, 0f)).x;

        float y = group.baseY;
        if (group.randomYRange > 0f)
            y += Random.Range(-group.randomYRange, group.randomYRange);

        return new Vector3(rightEdge + group.spawnAheadDistance, y, 0f);
    }
}
