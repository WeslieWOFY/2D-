using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 关卡切换管理器 —— 泛型单例、跨场景持久化对象（DontDestroyOnLoad）。
///
/// 职责：
///   1. 监听全局事件总线里的"请求切换场景"事件，收到后按场景名加载新场景；
///   2. 也可被任意脚本直接调用 SwitchScene("场景名") 切换。
///
/// 事件约定（与 GlobalEventBus 配套使用）：
///   事件名：SceneChangerManager.RequestSceneChangeEvent（即 "RequestSceneChange"）
///   参数  ：string 目标场景名
///   发送方：任何场景的脚本调用
///           GlobalEventBus.Trigger(SceneChangerManager.RequestSceneChangeEvent, "M4-3");
///
/// 说明：本管理器常驻场景之间，配合 MainMenuManager 使用。
/// 若某个关卡场景里也放置了本管理器，会自动被单例去重销毁。
/// </summary>
public class SceneChangerManager : PersistentSingleton<SceneChangerManager>
{
    /// <summary>请求切换场景的全局事件名</summary>
    public const string RequestSceneChangeEvent = "RequestSceneChange";

    [Header("过渡动画（可选）")]
    [Tooltip("切场景前的淡出画布（CanvasGroup），留空则直接切换场景")]
    [SerializeField] private CanvasGroup _fadeCanvasGroup;
    [SerializeField] private float _fadeOutDuration = 0.5f;

    [Header("调试")]
    [SerializeField] private bool _enableDebugLog = false;

    /// <summary>是否正在切换场景，防止重复触发</summary>
    private bool _isSwitching;

    /// <summary>异步加载中的场景操作（已加载未激活），由 CompleteSceneLoad 激活</summary>
    private AsyncOperation _pendingLoad;

    /// <summary>运行时创建的全屏黑幕（淡入淡出过渡用，常驻）</summary>
    private CanvasGroup _runtimeFadeGroup;

    protected override void Awake()
    {
        base.Awake();

        if (_enableDebugLog)
            Debug.Log("[SceneChangerManager] 关卡切换管理器已就绪。");
    }

    protected override void Start()
    {
        base.Start();

        // 在 Start 里订阅事件：此时所有场景对象的 Awake 都已执行完，
        // 保证 GlobalEventBus.Instance 一定存在，避免订阅静默失败。
        GlobalEventBus.On(RequestSceneChangeEvent, OnSceneChangeRequested);
    }

    private void OnDestroy()
    {
        // 只在自己是单例本体时取消订阅，防止重复销毁的对象把别人的订阅撤掉
        if (Instance == this)
            GlobalEventBus.Off(RequestSceneChangeEvent, OnSceneChangeRequested);
    }

    // ==================== 对外接口 ====================

    /// <summary>
    /// 直接调用接口：切换到指定场景。
    /// </summary>
    /// <param name="sceneName">目标场景名（需在 Build Settings 中注册）</param>
    public void SwitchScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneChangerManager] 切换失败：场景名为空！");
            return;
        }

        if (_isSwitching)
        {
            if (_enableDebugLog)
                Debug.Log($"[SceneChangerManager] 正在切换中，忽略重复请求 -> \"{sceneName}\"");
            return;
        }

        StartCoroutine(SwitchSceneRoutine(sceneName));
    }

    // ==================== 异步加载（真实进度） ====================

    /// <summary>
    /// 开始异步加载场景（先不激活，等进度条满后再调用 <see cref="CompleteSceneLoad"/> 进入新场景）。
    /// </summary>
    /// <param name="sceneName">目标场景名（需在 Build Settings 中注册）</param>
    /// <param name="onProgress">加载进度回调（0~1，1 表示已加载完成、等待激活）</param>
    /// <returns>是否成功开始加载（false 表示场景名为空 / 未注册 / 已在切换中）</returns>
    public bool LoadSceneAsync(string sceneName, Action<float> onProgress)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneChangerManager] 异步加载失败：场景名为空！");
            onProgress?.Invoke(0f);
            return false;
        }

        if (_isSwitching)
        {
            if (_enableDebugLog)
                Debug.Log("[SceneChangerManager] 正在切换中，忽略新的异步加载请求。");
            return false;
        }

        if (!IsSceneInBuildSettings(sceneName))
        {
            Debug.LogError($"[SceneChangerManager] 场景 \"{sceneName}\" 未添加到 Build Settings，无法加载！" +
                           $"请在菜单 File → Build Settings 中把该场景加入列表后重试。");
            return false;
        }

        _isSwitching = true;
        StartCoroutine(LoadSceneAsyncRoutine(sceneName, onProgress));
        return true;
    }

    private IEnumerator LoadSceneAsyncRoutine(string sceneName, Action<float> onProgress)
    {
        if (_enableDebugLog)
            Debug.Log($"[SceneChangerManager] 开始异步加载场景 \"{sceneName}\"");

        // allowSceneActivation=false：只加载不激活，进度最高停留在 0.9，等 CompleteSceneLoad 才进入场景
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        _pendingLoad = op;

        // 加载阶段真实进度：op.progress（0~0.9）映射成 0~1
        while (op.progress < 0.9f)
        {
            onProgress?.Invoke(Mathf.Clamp01(op.progress / 0.9f));
            yield return null;
        }

        // 已加载完成（未激活）
        onProgress?.Invoke(1f);

        if (_enableDebugLog)
            Debug.Log($"[SceneChangerManager] 场景 \"{sceneName}\" 已加载完成，等待激活。");
    }

    /// <summary>
    /// 进度条满后调用：激活已加载好的场景，真正进入下一场景。
    /// </summary>
    public void CompleteSceneLoad()
    {
        if (_pendingLoad != null)
        {
            if (_enableDebugLog)
                Debug.Log("[SceneChangerManager] 激活新场景。");

            _pendingLoad.allowSceneActivation = true;
            _pendingLoad = null;
        }

        _isSwitching = false;
    }

    // ==================== 异步加载 + 黑幕淡入淡出 ====================

    /// <summary>
    /// 异步加载场景，并带全屏黑幕淡入淡出过渡（用于"再玩一次"重载关卡等）。
    /// 流程：黑幕淡入 → 异步加载（不激活）→ 激活新场景 → 黑幕淡出。
    /// </summary>
    /// <param name="sceneName">目标场景名（需在 Build Settings 中注册）</param>
    /// <param name="fadeDuration">黑幕淡入/淡出时长（秒）</param>
    public void LoadSceneAsyncFade(string sceneName, float fadeDuration = 0.5f)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneChangerManager] 异步淡入淡出加载失败：场景名为空！");
            return;
        }

        if (_isSwitching)
        {
            if (_enableDebugLog)
                Debug.Log("[SceneChangerManager] 正在切换中，忽略新的加载请求。");
            return;
        }

        if (!IsSceneInBuildSettings(sceneName))
        {
            Debug.LogError($"[SceneChangerManager] 场景 \"{sceneName}\" 未添加到 Build Settings，无法加载！" +
                           $"请在菜单 File → Build Settings 中把该场景加入列表后重试。");
            return;
        }

        _isSwitching = true;
        StartCoroutine(LoadSceneAsyncFadeRoutine(sceneName, fadeDuration));
    }

    private IEnumerator LoadSceneAsyncFadeRoutine(string sceneName, float fadeDuration)
    {
        if (_enableDebugLog)
            Debug.Log($"[SceneChangerManager] 黑幕淡入，异步加载场景 \"{sceneName}\"");

        // 1. 黑幕淡入，盖住当前画面
        CanvasGroup fade = GetFadeOverlay();
        fade.blocksRaycasts = true;
        yield return FadeCanvasGroup(fade, 1f, fadeDuration);

        // 2. 异步加载（不激活），等加载完成
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        _pendingLoad = op;

        while (op.progress < 0.9f)
            yield return null;

        // 3. 激活新场景
        op.allowSceneActivation = true;
        _pendingLoad = null;
        _isSwitching = false;

        // 4. 等新场景渲染几帧，再黑幕淡出
        yield return null;
        yield return null;
        yield return FadeCanvasGroup(fade, 0f, fadeDuration);
        fade.blocksRaycasts = false;
    }

    /// <summary>获取黑幕：优先用序列化的 _fadeCanvasGroup，否则运行时创建并常驻</summary>
    private CanvasGroup GetFadeOverlay()
    {
        if (_fadeCanvasGroup != null) return _fadeCanvasGroup;
        if (_runtimeFadeGroup != null) return _runtimeFadeGroup;

        var go = new GameObject("SceneFadeOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(Image));

        DontDestroyOnLoad(go); // 随管理器常驻，跨场景不销毁

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // 盖在最上层

        Image image = go.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _runtimeFadeGroup = go.GetComponent<CanvasGroup>();
        _runtimeFadeGroup.alpha = 0f;
        _runtimeFadeGroup.blocksRaycasts = false;

        if (_enableDebugLog)
            Debug.Log("[SceneChangerManager] 运行时创建了全屏黑幕（SceneFadeOverlay）。");

        return _runtimeFadeGroup;
    }

    // ==================== 事件回调 ====================

    /// <summary>GlobalEventBus 事件回调：收到场景名后发起切换</summary>
    private void OnSceneChangeRequested(object data)
    {
        string sceneName = data as string;
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"[SceneChangerManager] 收到切换请求但场景名无效（data={data}）");
            return;
        }

        if (_enableDebugLog)
            Debug.Log($"[SceneChangerManager] 收到全局事件请求，切换场景 -> \"{sceneName}\"");

        SwitchScene(sceneName);
    }

    // ==================== 内部实现 ====================

    private IEnumerator SwitchSceneRoutine(string sceneName)
    {
        _isSwitching = true;

        // 先校验场景是否已注册到 Build Settings，避免 LoadScene 抛错
        if (!IsSceneInBuildSettings(sceneName))
        {
            Debug.LogError($"[SceneChangerManager] 场景 \"{sceneName}\" 未添加到 Build Settings，无法加载！" +
                           $"请在菜单 File → Build Settings 中把该场景加入列表后重试。");
            _isSwitching = false;
            yield break;
        }

        // 可选：淡出到黑屏再加载
        if (_fadeCanvasGroup != null && _fadeOutDuration > 0f)
            yield return FadeCanvasGroup(_fadeCanvasGroup, 1f, _fadeOutDuration);

        if (_enableDebugLog)
            Debug.Log($"[SceneChangerManager] 开始加载场景 \"{sceneName}\"");

        // 同步加载新场景；本管理器由 DontDestroyOnLoad 保留，不会随场景卸载
        SceneManager.LoadScene(sceneName);

        _isSwitching = false;
    }

    /// <summary>按场景名判断目标场景是否已在 Build Settings 中注册</summary>
    private static bool IsSceneInBuildSettings(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;

        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            // 只比对文件名（去路径和扩展名），容错输入的纯场景名
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                return true;
        }
        return false;
    }

    /// <summary>把 CanvasGroup 透明度平滑插值到目标值</summary>
    private IEnumerator FadeCanvasGroup(CanvasGroup group, float target, float duration)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        group.alpha = target;
    }
}
