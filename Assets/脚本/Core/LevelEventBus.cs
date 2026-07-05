using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡级事件总线 —— 挂载到关卡场景中的 GameObject 上。
/// 不是全局单例，随关卡加载/卸载自动生灭。
///
/// 用法示例：
///   发事件：LevelEventBus.Trigger("BossDied");
///   带参：  LevelEventBus.Trigger("EnemyDied", enemyObj);
///   听事件：LevelEventBus.On("BossDied", () => { ... });
///   带参：  LevelEventBus.On("EnemyDied", data => { var e = data as GameObject; ... });
///   取消：  LevelEventBus.Off("BossDied", myCallback);
/// </summary>
public class LevelEventBus : MonoBehaviour
{
    // ==================== 单例 ====================
    public static LevelEventBus Instance { get; private set; }

    [Header("调试")]
    [SerializeField] private bool _enableDebugLog = false;

    // ==================== 内部存储 ====================
    // 有参事件
    private readonly Dictionary<string, Action<object>> _eventDict = new();
    // 无参事件
    private readonly Dictionary<string, Action> _eventDictNoParam = new();

    // ==================== 生命周期 ====================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[LevelEventBus] 场景中存在多个事件总线！旧:{Instance.name} → 新:{name}，已用新的顶掉旧的。请确保场景中只有一个。");
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        Instance = null;
        _eventDict.Clear();
        _eventDictNoParam.Clear();

        if (_enableDebugLog)
            Debug.Log("[LevelEventBus] 关卡事件总线已销毁，所有事件已清空。");
    }

    // ==================== 监听 ====================

    /// <summary>监听有参事件</summary>
    public static void On(string eventName, Action<object> callback)
    {
        if (callback == null)
        {
            Debug.LogError($"[LevelEventBus] On(\"{eventName}\") 传入的 callback 为 null！");
            return;
        }
        if (Instance == null) return;

        if (Instance._eventDict.TryGetValue(eventName, out var existing))
            Instance._eventDict[eventName] = existing + callback;
        else
            Instance._eventDict[eventName] = callback;

        if (Instance._enableDebugLog)
            Debug.Log($"[LevelEventBus] +订阅 \"{eventName}\"，当前监听数:{Instance._eventDict[eventName].GetInvocationList().Length}");
    }

    /// <summary>监听无参事件</summary>
    public static void On(string eventName, Action callback)
    {
        if (callback == null)
        {
            Debug.LogError($"[LevelEventBus] On(\"{eventName}\") 传入的 callback 为 null！");
            return;
        }
        if (Instance == null) return;

        if (Instance._eventDictNoParam.TryGetValue(eventName, out var existing))
            Instance._eventDictNoParam[eventName] = existing + callback;
        else
            Instance._eventDictNoParam[eventName] = callback;

        if (Instance._enableDebugLog)
            Debug.Log($"[LevelEventBus] +订阅 \"{eventName}\"，当前监听数:{Instance._eventDictNoParam[eventName].GetInvocationList().Length}");
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
            Debug.Log($"[LevelEventBus] -取消订阅 \"{eventName}\"，剩余监听数:{(existing != null ? existing.GetInvocationList().Length : 0)}");
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
            Debug.Log($"[LevelEventBus] -取消订阅 \"{eventName}\"，剩余监听数:{(existing != null ? existing.GetInvocationList().Length : 0)}");
    }

    // ==================== 触发 ====================

    /// <summary>触发有参事件 —— 由业务代码调用，事件总线代为广播</summary>
    public static void Trigger(string eventName, object data = null)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"[LevelEventBus] Trigger(\"{eventName}\") 失败：场景中没有 LevelEventBus 实例！");
            return;
        }

        if (Instance._eventDict.TryGetValue(eventName, out var action))
        {
            if (Instance._enableDebugLog)
                Debug.Log($"[LevelEventBus] >>> 触发 \"{eventName}\" data={data}，监听数:{action.GetInvocationList().Length}");

            // 拷贝一份再 invoke，防止回调里改订阅导致迭代异常
            var snapshot = (Action<object>)Delegate.Combine(action.GetInvocationList());
            snapshot?.Invoke(data);
        }
        else if (Instance._enableDebugLog)
        {
            Debug.Log($"[LevelEventBus] >>> 触发 \"{eventName}\"（无监听者）");
        }
    }

    /// <summary>触发无参事件 —— 由业务代码调用，事件总线代为广播</summary>
    public static void Trigger(string eventName)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"[LevelEventBus] Trigger(\"{eventName}\") 失败：场景中没有 LevelEventBus 实例！");
            return;
        }

        if (Instance._eventDictNoParam.TryGetValue(eventName, out var action))
        {
            if (Instance._enableDebugLog)
                Debug.Log($"[LevelEventBus] >>> 触发 \"{eventName}\"，监听数:{action.GetInvocationList().Length}");

            var snapshot = (Action)Delegate.Combine(action.GetInvocationList());
            snapshot?.Invoke();
        }
        else if (Instance._enableDebugLog)
        {
            Debug.Log($"[LevelEventBus] >>> 触发 \"{eventName}\"（无监听者）");
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

    /// <summary>清除所有事件（关卡重置等场景）</summary>
    public static void ClearAll()
    {
        if (Instance == null) return;
        Instance._eventDict.Clear();
        Instance._eventDictNoParam.Clear();

        if (Instance._enableDebugLog)
            Debug.Log("[LevelEventBus] 所有事件监听已清空。");
    }
}
