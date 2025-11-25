// 文件名建议：MiniGameAccelerometer.cs
// 仅面向微信小游戏端：依赖 WeChatWASM SDK 的 WX.* 加速计 API
using UnityEngine;
using TMPro;
using WeChatWASM;
using System;

public class MiniGameAccelerometer : MonoBehaviour
{
    [Header("将你的 TMP 文本拖到这里")]
    [SerializeField] private TMP_Text output;

    private bool _running = false;

    // 记住同一个委托用于 On/Off 订阅与反订阅
    private Action<OnAccelerometerChangeListenerResult> _listener;

    private void Awake()
    {
        _listener = OnAccelerometerChanged;
    }

    /// <summary>
    /// 绑定到 Start 按钮 onClick
    /// </summary>
    public void StartAccelerometer()
    {
        if (_running) return;

        // 先订阅回调，再开始监听
        WX.OnAccelerometerChange(_listener);

        WX.StartAccelerometer(new StartAccelerometerOption
        {
            // 采样频率：'game'（较高频率），也可用 'ui' 或 'normal'
            interval = "game",
            success = _ => SetText("Accelerometer started."),
            fail = err => SetText($"Start failed: {err.errMsg}")
        });

        _running = true;
    }

    /// <summary>
    /// 绑定到 Stop 按钮 onClick
    /// </summary>
    public void StopAccelerometer()
    {
        if (!_running) return;

        // 停止监听并取消订阅
        WX.StopAccelerometer(new StopAccelerometerOption
        {
            success = _ => SetText("Accelerometer stopped."),
            fail = err => SetText($"Stop failed: {err.errMsg}")
        });

        WX.OffAccelerometerChange(_listener);
        _running = false;
    }

    private void OnAccelerometerChanged(OnAccelerometerChangeListenerResult data)
    {
        // data.x / data.y / data.z：单位 m/s^2（按微信小游戏规范）
        if (output != null)
        {
            output.text =
                $"Accel (m/s²)\n" +
                $"x: {data.x:F3}\n" +
                $"y: {data.y:F3}\n" +
                $"z: {data.z:F3}";
        }
    }

    private void OnDisable()
    {
        // 场景切换或对象失活时，安全清理
        if (_running)
        {
            WX.OffAccelerometerChange(_listener);
            WX.StopAccelerometer(new StopAccelerometerOption());
            _running = false;
        }
    }

    private void OnDestroy()
    {
        // 兜底清理
        if (_running)
        {
            WX.OffAccelerometerChange(_listener);
            WX.StopAccelerometer(new StopAccelerometerOption());
        }
    }

    private void SetText(string msg)
    {
        if (output != null) output.text = msg;
        else Debug.Log(msg);
    }
}
