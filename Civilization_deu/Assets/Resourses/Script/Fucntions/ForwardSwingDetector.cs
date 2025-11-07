// ForwardSwingDetector.cs
// 功能：采用 “Jerk + Pitch角度变化” 检测横屏下的“向前翻甩”意图（无需陀螺仪）。
// 触发条件：jerk 超过阈值 且 短时间内俯仰角(取 Y/Z 两种模型的最大值)变化超过阈值。
// 事件：OnForwardTriggered(无参) —— 相机推进组件订阅即可。
// 调试：OnDebugInfo(string) —— HUD 可显示 a/g/jerk/dPitch 等指标。
// 平台：仅在微信小游戏真机（或WebGL非编辑器）启用传感器监听；编辑器下只输出日志。

using UnityEngine;
using System;
using WeChatWASM;

public class ForwardSwingDetector : MonoBehaviour
{
    [Header("核心阈值（建议先用默认，按需微调）")]
    [Tooltip("jerk 阈值（m/s^3 近似）；26~32 较常用")]
    public float jerkThreshold = 30f;

    [Tooltip("短时俯仰角变化阈值（度）；12~18 较常用")]
    public float pitchDeltaDeg = 12f;

    [Tooltip("两次触发的最小间隔（秒），避免一甩多触")]
    public float triggerCooldown = 0.65f;

    [Header("重力估计与采样")]
    [Tooltip("重力低通滤波因子（0.08~0.18）")]
    public float gravityLerp = 0.12f;

    [Tooltip("用于限制采样时间步，提升 jerk 稳定性")]
    public float minDt = 0.02f, maxDt = 0.08f;
    
    public enum PitchModel { AutoPickMax, UseZModel, UseYModel }
    
    [Tooltip("AutoPickMax：同时计算 Z/Y 两种模型的角度变化，取较大者，更抗横屏朝向差异")]
    public PitchModel pitchModel = PitchModel.AutoPickMax;

    // ===== 触发事件（相机推进组件订阅此事件）=====
    public event Action OnForwardTriggered;

    // ===== HUD 调试（可选）=====
    public Action<string> OnDebugInfo;

    // ===== 运行态 =====
    private Vector3 accel;       // 实时加速度（含重力）
    private Vector3 accelPrev;   // 上一帧加速度
    private Vector3 gravity;     // 低通后的重力估计

    private float lastSampleTime;
    private float lastTriggerTime = -999f;

    // 上一帧俯仰角（按不同模型各记一份）
    private float prevPitchZDeg;
    private float prevPitchYDeg;

    private Action<OnAccelerometerChangeListenerResult> _accelListener;

    private void Awake()
    {
        _accelListener = OnAccel;
    }

    private void OnEnable()
    {
#if UNITY_WEIXINMINIGAME || (UNITY_WEBGL && !UNITY_EDITOR)
        // 注册监听并启动加速计
        WX.OnAccelerometerChange(_accelListener);
        WX.StartAccelerometer(new StartAccelerometerOption
        {
            interval = "game",
            success = (res) =>
            {
                Debug.Log("[Accel] StartAccelerometer success");
                //在微信端跳出debug标识
                WX.ShowToast(new ShowToastOption { title = "加速计已启动", icon = "success", duration = 800 });
            },
            fail = (res) =>
            {
                Debug.LogError("[Accel] StartAccelerometer FAIL: " + (res?.errMsg ?? "null"));
                //在微信端跳出debug标识
                WX.ShowToast(new ShowToastOption { title = "加速计启动失败", icon = "error", duration = 1500 });
            }
        });
#else
        Debug.Log("[Accel] 非小游戏真机环境（或编辑器），不会启用传感器监听。");
#endif
    }

    private void OnDisable()
    {
#if UNITY_WEIXINMINIGAME || (UNITY_WEBGL && !UNITY_EDITOR)
        WX.OffAccelerometerChange(_accelListener);
        WX.StopAccelerometer(new StopAccelerometerOption());
#endif
    }

    private void OnAccel(OnAccelerometerChangeListenerResult d)
    {
        // 1) 更新加速度、重力估计
        accelPrev = accel;
        accel = new Vector3((float)d.x, (float)d.y, (float)d.z);

        if (gravity == Vector3.zero)
        {
            gravity = accel; // 冷启动
            // 初始化上一帧 pitch，避免第一帧出现巨大的 dPitch
            InitPitchHistory(gravity);
        }

        gravity = Vector3.Lerp(gravity, accel, gravityLerp);

        // HUD：原始数值
        OnDebugInfo?.Invoke($"a=({accel.x:0.00},{accel.y:0.00},{accel.z:0.00})  g=({gravity.x:0.00},{gravity.y:0.00},{gravity.z:0.00})");

        // 2) 计算 jerk、dPitch，并进行判定
        TryDetectJerkAndPitch();
    }

    private void InitPitchHistory(Vector3 g)
    {
        // Z 模型：pitchZ = atan2(-gz, sqrt(gx^2+gy^2))
        float horizZ = Mathf.Sqrt(g.x * g.x + g.y * g.y);
        prevPitchZDeg = Mathf.Atan2(-g.z, horizZ) * Mathf.Rad2Deg;

        // Y 模型：pitchY = atan2(-gy, sqrt(gx^2+gz^2))
        float horizY = Mathf.Sqrt(g.x * g.x + g.z * g.z);
        prevPitchYDeg = Mathf.Atan2(-g.y, horizY) * Mathf.Rad2Deg;
    }

    private void TryDetectJerkAndPitch()
    {
        if (Time.time - lastTriggerTime < triggerCooldown) return;

        // 采样间隔（限制在 [minDt, maxDt] 内，稳定 jerk）
        float dt = Mathf.Max(Time.time - lastSampleTime, 0.016f);
        lastSampleTime = Time.time;
        dt = Mathf.Clamp(dt, minDt, maxDt);

        // 1) jerk（线性加速度变化率近似）
        float jerk = ((accel - accelPrev) / dt).magnitude;

        // 2) 俯仰角变化（两种模型，选择更大者或指定其一）
        float dPitchDeg = ComputePitchDeltaDegrees(gravity);

        // HUD 追加信息
        OnDebugInfo?.Invoke($"jerk={jerk:0.0}  dPitch={dPitchDeg:0.0}° (>{pitchDeltaDeg})");

        // 3) 复合触发（稳妥）：jerk 高 + 短时俯仰角变化大
        bool fire = (jerk > jerkThreshold) && (dPitchDeg > pitchDeltaDeg);

        if (fire)
        {
            lastTriggerTime = Time.time;
            //在微信端跳出debug标识
            WX.ShowToast(new ShowToastOption { title = "Forward!", icon = "success", duration = 500 });
            Debug.Log($"[Accel] TRIGGER: jerk={jerk:0.0}, dPitch={dPitchDeg:0.0}°");
            OnForwardTriggered?.Invoke();
        }
    }

    /// <summary>
    /// 计算当前帧相对上一帧的俯仰角增量（度）。
    /// - Z 模型：pitchZ = atan2(-gz, sqrt(gx^2+gy^2))，常见横屏下“前后”更体现在 Z。
    /// - Y 模型：pitchY = atan2(-gy, sqrt(gx^2+gz^2))，某些机型/握持下“前后”体现在 Y。
    /// - AutoPickMax：两者都算，取更大变化，以提升对横屏朝向/机型差异的鲁棒性。
    /// </summary>
    private float ComputePitchDeltaDegrees(in Vector3 g)
    {
        // Z 模型
        float horizZ = Mathf.Sqrt(g.x * g.x + g.y * g.y);
        float pitchZ = Mathf.Atan2(-g.z, horizZ) * Mathf.Rad2Deg;
        float dZ = Mathf.Abs(pitchZ - prevPitchZDeg);
        prevPitchZDeg = pitchZ;

        if (pitchModel == PitchModel.UseZModel)
            return dZ;

        // Y 模型
        float horizY = Mathf.Sqrt(g.x * g.x + g.z * g.z);
        float pitchY = Mathf.Atan2(-g.y, horizY) * Mathf.Rad2Deg;
        float dY = Mathf.Abs(pitchY - prevPitchYDeg);
        prevPitchYDeg = pitchY;

        if (pitchModel == PitchModel.UseYModel)
            return dY;

        // Auto：取更大值
        return Mathf.Max(dZ, dY);
    }

    // ========== 调试按钮（可绑 UI 验证后续链路） ==========
    public void DebugFireOnce()
    {
        //在微信端跳出debug标识
        WX.ShowToast(new ShowToastOption { title = "DebugFire", icon = "none", duration = 600 });
        OnForwardTriggered?.Invoke();
    }
}
