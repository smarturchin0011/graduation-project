// WXMiniGameInitializer.cs
// 作用：在微信小游戏端，最早时机一次性调用 WX.InitSDK，并提供 IsReady 标志。
// 放到项目任何位置即可，无需手动挂载。
// 要求：已导入 WeChatWASM SDK。

using System;
using UnityEngine;
using WeChatWASM;

public static class WXMiniGameInitializer
{
    /// <summary>SDK 是否已完成初始化</summary>
    public static bool IsReady { get; private set; }

    /// <summary>当 SDK 初始化完成时触发（先订阅后等待）</summary>
    public static event Action OnReady;

    // 在首个场景加载前执行
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
#if UNITY_WEIXINMINIGAME || UNITY_WEBGL && !UNITY_EDITOR
        if (IsReady) return;

        Debug.unityLogger.logEnabled = true; // 方便在微信开发者工具里看日志

        // 调用初始化（多数版本无需参数，有回调即可）
        WX.InitSDK(result =>
        {
            IsReady = true;
            Debug.Log("[WX] InitSDK done.");
            OnReady?.Invoke();
            OnReady = null; // 释放一次性事件
        });
#endif
    }
}
