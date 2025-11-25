using UnityEngine;
using TMPro;
using WeChatWASM;
using System;
using System.Collections;
using UnityEngine.UI;

public class MiniGameCameraYawFollow : MonoBehaviour
{
    [Header("要跟随旋转的摄像机（通常拖本脚本所在的 Transform）")]
    [SerializeField] private Transform cameraTarget;

    [Header("可选：TMP 文本用于调试显示")]
    [SerializeField] private TMP_Text debugText;

    [Header("增益（放大因子，越大转得越多）")]
    [SerializeField] private float yawGain = 20f;   // 你可以从 60~150 之间试手感

    [Header("增益滑块，实时调整")] 
    [SerializeField] private Slider gainSlider;

    [Header("增益值text")] [SerializeField] private TMP_Text gainValue;

    [Header("平滑 (0=无, 1=强)")]
    [Range(0f, 1f)] [SerializeField] private float smooth = 0.06f;

    [Header("单帧最大旋转（度），防尖峰")]
    [SerializeField] private float maxDeltaPerFrame = 20f;

    [Header("水平轴方向（需要反向时把它改为 -1）")]
    [SerializeField] private int yawSign = 1;

    [Header("是否用世界空间旋转（否则 self 空间）")]
    [SerializeField] private bool worldSpace = true;

    private bool _running = false;
    private bool _calibrating = false;

    // 原始角速度（deg/s）
    private Vector3 _gyro;
    // 平滑后角速度
    private Vector3 _gyroSmoothed;
    // 零偏
    private Vector3 _bias;
    // 可选：积分偏置，让 Recenter 把当前当作 0
    private float _yawOffset = 0f;

    private Action<OnGyroscopeChangeListenerResult> _listener;

    private void Awake()
    {
        if (cameraTarget == null) cameraTarget = transform;
        _listener = OnGyroChanged;
    }

    public void SetGainValue()
    {
        yawGain = gainSlider.value;
        gainValue.text = yawGain.ToString();
    }

    public void StartFollow()
    {
        if (_running) return;

        if (!WXMiniGameInitializer.IsReady)
        {
            SetDebug("等待 WX.InitSDK 完成...");
            WXMiniGameInitializer.OnReady += () => { if (!_running) StartFollow(); };
            return;
        }

        // 订阅回调再启动
        WX.OnGyroscopeChange(_listener);
        WX.StartGyroscope(new StartGyroscopeOption
        {
            interval = "game",
            success = _ =>
            {
                SetDebug("Gyro started. Calibrating...");
                _running = true;
                StartCoroutine(CalibrateBias(0.4f)); // 取 0.4s 做零偏标定
            },
            fail = err => SetDebug($"Start failed: {err.errMsg}")
        });
    }

    public void StopFollow()
    {
        if (!_running) return;

        WX.StopGyroscope(new StopGyroscopeOption
        {
            success = _ => SetDebug("Gyro stopped."),
            fail = err => SetDebug($"Stop failed: {err.errMsg}")
        });

        WX.OffGyroscopeChange(_listener);
        _running = false;
    }

    /// <summary>
    /// 把当前朝向当作“正前方”（把积分偏置清零）
    /// </summary>
    public void RecenterYaw()
    {
        _yawOffset = 0f;
        SetDebug("Recenter yaw.");
    }

    private IEnumerator CalibrateBias(float seconds)
    {
        _calibrating = true;
        _bias = Vector3.zero;
        int count = 0;

        float tEnd = Time.time + seconds;
        while (Time.time < tEnd)
        {
            _bias += _gyro;
            count++;
            yield return null;
        }

        if (count > 0) _bias /= count;
        _calibrating = false;
        SetDebug("Gyro ready.");
    }

    private void OnGyroChanged(OnGyroscopeChangeListenerResult data)
    {
        _gyro = new Vector3((float)data.x, (float)data.y, (float)data.z);

        if (debugText != null)
        {
            debugText.text =
                $"Gyro (deg/s)\n" +
                $"x:{data.x:F3}  y:{data.y:F3}  z:{data.z:F3}\n" +
                $"bias: {_bias.x:F3},{_bias.y:F3},{_bias.z:F3}";
        }
    }

    private void Update()
    {
        if (!_running || _calibrating || cameraTarget == null) return;

        // 去偏 + 平滑
        Vector3 g = _gyro - _bias;
        _gyroSmoothed = Vector3.Lerp(_gyroSmoothed, g, 1f - Mathf.Clamp01(smooth));

        // —— 核心：只取“水平旋转（Yaw）”分量 ——
        // 在竖屏持握的微信小游戏里，常见手感：手机沿垂直轴的水平转动 ≈ 使用 -Z 作为 yaw（如与你现象相反，改 yawSign 或把 -Z 换成 +Z）。
        float yawVel = (-_gyroSmoothed.z) * yawSign;

        // 时间积分（角速度 * 增益 * dt = 本帧旋转角度）
        float deltaYaw = yawVel * yawGain * Time.deltaTime;

        // 防尖峰
        deltaYaw = Mathf.Clamp(deltaYaw, -maxDeltaPerFrame, maxDeltaPerFrame);

        // 应用旋转（只绕 Y）
        if (worldSpace)
            cameraTarget.Rotate(0f, deltaYaw + _yawOffset, 0f, Space.World);
        else
            cameraTarget.Rotate(0f, deltaYaw + _yawOffset, 0f, Space.Self);

        // 用后清空 offset（只对本帧生效，实现“瞬时归零”）
        if (_yawOffset != 0f) _yawOffset = 0f;
    }

    private void OnDisable()
    {
        if (_running)
        {
            WX.OffGyroscopeChange(_listener);
            WX.StopGyroscope(new StopGyroscopeOption());
            _running = false;
        }
    }

    private void OnDestroy()
    {
        if (_running)
        {
            WX.OffGyroscopeChange(_listener);
            WX.StopGyroscope(new StopGyroscopeOption());
        }
    }

    private void SetDebug(string msg)
    {
        if (debugText != null) debugText.text = msg;
        else Debug.Log(msg);
    }
}
