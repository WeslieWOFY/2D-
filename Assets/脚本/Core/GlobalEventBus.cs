using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局事件总线 —— 处理跨关卡、跨场景的游戏生命周期事件。
/// 泛型单例，DontDestroyOnLoad，随游戏启动而生，随进程退出而灭。
///
/// 与 LevelEventBus 的分工：
///   LevelEventBus → 关卡内事件（BossDied、ClearAllBullets），随场景卸载销毁
///   GlobalEventBus → 全局事件（GamePaused、PlayerDied、StageCleared），全局唯一
///
/// 用法：
///   GlobalEventBus.Trigger("GamePaused");
///   GlobalEventBus.On("GamePaused", () => { ... });
///   GlobalEventBus.Off("GamePaused", myCallback);
/// </summary>
public class GlobalEventBus : PersistentSingleton<GlobalEventBus>
{
    [Header("调试")]
    [SerializeField] private bool _enableDebugLog = false;

    // ==================== 内部存储 ====================
    private readonly Dictionary<string, Action<object>> _eventDict = new();
    private readonly Dictionary<string, Action> _eventDictNoParam = new();

    protected override void Awake()
    {
        base.Awake();

        if (_enableDebugLog)
            Debug.Log("[GlobalEventBus] 全局事件总线已就绪。");
    }

    protected override void Start()
    {
        base.Start();
    }

    private void OnDestroy()
    {
        if (_enableDebugLog)
            Debug.Log("[GlobalEventBus] 全局事件总线已销毁，所有事件已清空。");

        _eventDict.Clear();
        _eventDictNoParam.Clear();
    }

    // ==================== 监听 ====================

    /// <summary>监听有参事件</summary>
    public static void On(string eventName, Action<object> callback)
    {
        if (callback == null)
        {
            Debug.LogError($"[GlobalEventBus] On(\"{eventName}\") 传入的 callback 为 null！");
            return;
        }
        if (Instance == null) return;

        if (Instance._eventDict.TryGetValue(eventName, out var existing))
            Instance._eventDict[eventName] = existing + callback;
        else
            Instance._eventDict[eventName] = callback;

        if (Instance._enableDebugLog)
            Debug.Log($"[GlobalEventBus] +订阅 \"{eventName}\"，当前监听数:{Instance._eventDict[eventName].GetInvocationList().Length}");
    }

    /// <summary>监听无参事件</summary>
    public static void On(string eventName, Action callback)
    {
        if (callback == null)
        {
            Debug.LogError($"[GlobalEventBus] On(\"{eventName}\") 传入的 callback 为 null！");
            return;
        }
        if (Instance == null) return;

        if (Instance._eventDictNoParam.TryGetValue(eventName, out var existing))
            Instance._eventDictNoParam[eventName] = existing + callback;
        else
            Instance._eventDictNoParam[eventName] = callback;

        if (Instance._enableDebugLog)
            Debug.Log($"[GlobalEventBus] +订阅 \"{eventName}\"，当前监听数:{Instance._eventDictNoParam[eventName].GetInvocationList().Length}");
    }

    // ==================== 取消监听 ====================

    /// <summary>取消监听有参事件</summary>
    public static void Off(string eventName, Action<object> callback)
    {
        if (callback == null) return;
        if (Instance == null) return;
        if (!Instance._eventDict.TryGetValue(eventName, out var existing)) return;

        existing -= callback;
        if (existing == null)
            Instance._eventDict.Remove(eventName);
        else
            Instance._eventDict[eventName] = existing;

        if (Instance._enableDebugLog)
            Debug.Log($"[GlobalEventBus] -取消订阅 \"{eventName}\"，剩余监听数:{(existing != null ? existing.GetInvocationList().Length : 0)}");
    }

    /// <summary>取消监听无参事件</summary>
    public static void Off(string eventName, Action callback)
    {
        if (callback == null) return;
        if (Instance == null) return;
        if (!Instance._eventDictNoParam.TryGetValue(eventName, out var existing)) return;

        existing -= callback;
        if (existing == null)
            Instance._eventDictNoParam.Remove(eventName);
        else
            Instance._eventDictNoParam[eventName] = existing;

        if (Instance._enableDebugLog)
            Debug.Log($"[GlobalEventBus] -取消订阅 \"{eventName}\"，剩余监听数:{(existing != null ? existing.GetInvocationList().Length : 0)}");
    }

    // ==================== 触发 ====================

    /// <summary>触发有参事件</summary>
    public static void Trigger(string eventName, object data = null)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"[GlobalEventBus] Trigger(\"{eventName}\") 失败：GlobalEventBus 实例不存在！");
            return;
        }

        if (Instance._eventDict.TryGetValue(eventName, out var action))
        {
            if (Instance._enableDebugLog)
                Debug.Log($"[GlobalEventBus] >>> 触发 \"{eventName}\" data={data}，监听数:{action.GetInvocationList().Length}");

            // 拷贝后再 invoke，防止回调里修改订阅导致迭代异常
            var snapshot = (Action<object>)Delegate.Combine(action.GetInvocationList());
            snapshot?.Invoke(data);
        }
        else if (Instance._enableDebugLog)
        {
            Debug.Log($"[GlobalEventBus] >>> 触发 \"{eventName}\"（无监听者）");
        }
    }

    /// <summary>触发无参事件</summary>
    public static void Trigger(string eventName)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"[GlobalEventBus] Trigger(\"{eventName}\") 失败：GlobalEventBus 实例不存在！");
            return;
        }

        if (Instance._eventDictNoParam.TryGetValue(eventName, out var action))
        {
            if (Instance._enableDebugLog)
                Debug.Log($"[GlobalEventBus] >>> 触发 \"{eventName}\"，监听数:{action.GetInvocationList().Length}");

            var snapshot = (Action)Delegate.Combine(action.GetInvocationList());
            snapshot?.Invoke();
        }
        else if (Instance._enableDebugLog)
        {
            Debug.Log($"[GlobalEventBus] >>> 触发 \"{eventName}\"（无监听者）");
        }
    }

    // ==================== 查询 ====================

    /// <summary>检查某个事件是否有监听者</summary>
    public static bool HasListener(string eventName)
    {
        if (Instance == null) return false;
        return Instance._eventDict.ContainsKey(eventName) || Instance._eventDictNoParam.ContainsKey(eventName);
    }

    // ==================== 清理 ====================

    /// <summary>清除某个事件的所有监听者</summary>
    public static void ClearEvent(string eventName)
    {
        if (Instance == null) return;
        Instance._eventDict.Remove(eventName);
        Instance._eventDictNoParam.Remove(eventName);
    }

    /// <summary>
    /// 清除所有事件监听（慎用！会清空全局所有订阅）。
    /// 通常只在游戏完全重置（返回标题画面等）时调用。
    /// </summary>
    public static void ClearAll()
    {
        if (Instance == null) return;
        Instance._eventDict.Clear();
        Instance._eventDictNoParam.Clear();

        if (Instance._enableDebugLog)
            Debug.Log("[GlobalEventBus] 所有全局事件监听已清空。");
    }
}
