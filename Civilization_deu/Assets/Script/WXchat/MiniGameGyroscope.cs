using UnityEngine;
using TMPro;
using WeChatWASM;
using System;
using System.Collections;
using UnityEngine.UI;

public class MiniGameGyroscope : MonoBehaviour
{
    [Header("TMP 文本（打印 x/y/z）")]
    [SerializeField] private TMP_Text output;

    [Header("需要被旋转的 Cube（其 Transform）")]
    [SerializeField] private Transform cube;

    [Header("增益值")]
    [SerializeField] private float gain = 20f;
    [Header("增益值滑块")]
    [SerializeField] private Slider gainSlider;
    [Header("增益值文本")]
    [SerializeField] private TMP_Text gainText;

    [Header("平滑 (0=无, 1=强)")]
    [Range(0f, 1f)] [SerializeField] private float smooth = 0.05f;

    [Header("轴向微调（必要时改成 -1 以反向）")]
    [SerializeField] private Vector3 axisSign = new Vector3(1f, 1f, 1f);

    [Header("单帧最大旋转（度），防尖峰")]
    [SerializeField] private float maxDeltaPerFrame = 10f;

    private bool _running = false;
    private bool _calibrating = false;

    private Vector3 _gyro;          // 原始角速度（deg/s）
    private Vector3 _gyroSmoothed;  // 平滑后角速度
    private Vector3 _bias;          // 零偏

    private Action<OnGyroscopeChangeListenerResult> _listener;

    private void Awake()
    {
        _listener = OnGyroChanged;
    }

    public void SetGainValue()//外部滑块设置增益值
    {
        gain = gainSlider.value;
        gainText.text = gain.ToString("F0");
    }

    public void StartGyro()
    {
        if (_running) return;

        if (!WXMiniGameInitializer.IsReady)
        {
            SetText("微信SDK尚未就绪，等待初始化...");
            WXMiniGameInitializer.OnReady += () => { if (!_running) StartGyro(); };
            return;
        }

        // （如果你接了隐私门，放到这里）
        // WXPrivacyGuard.AskThen(() => { ... 真正启动 ... });

        WX.OnGyroscopeChange(_listener);
        WX.StartGyroscope(new StartGyroscopeOption
        {
            interval = "game",
            success = _ =>
            {
                SetText("Gyroscope started. Calibrating...");
                _running = true;
                StartCoroutine(CalibrateBias(0.4f)); // 取 0.4 秒做零偏标定
            },
            fail = err => SetText($"Start failed: {err.errMsg}")
        });
    }

    public void StopGyro()
    {
        if (!_running) return;

        WX.StopGyroscope(new StopGyroscopeOption
        {
            success = _ => SetText("Gyroscope stopped."),
            fail = err => SetText($"Stop failed: {err.errMsg}")
        });

        WX.OffGyroscopeChange(_listener);
        _running = false;
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
        SetText("Gyroscope started.");
    }

    private void OnGyroChanged(OnGyroscopeChangeListenerResult data)
    {
        _gyro = new Vector3((float)data.x, (float)data.y, (float)data.z);

        if (output != null)
        {
            output.text =
                $"Gyro (deg/s)\n" +
                $"x: {data.x:F3}\n" +
                $"y: {data.y:F3}\n" +
                $"z: {data.z:F3}\n" +
                $"bias: {_bias.x:F3},{_bias.y:F3},{_bias.z:F3}";
        }
    }

    private void Update()
    {
        if (!_running || cube == null || _calibrating) return;

        // 去偏 + 平滑
        Vector3 g = _gyro - _bias;
        _gyroSmoothed = Vector3.Lerp(_gyroSmoothed, g, 1f - Mathf.Clamp01(smooth));

        // 轴向修正
        g = new Vector3(
            _gyroSmoothed.x * axisSign.x,
            _gyroSmoothed.y * axisSign.y,
            _gyroSmoothed.z * axisSign.z
        );

        // 放大 & 时间积分：deltaEuler = ω(deg/s) * gain * dt
        Vector3 deltaEuler = g * gain * Time.deltaTime;

        // 防尖峰：限制单帧最大旋转
        deltaEuler.x = Mathf.Clamp(deltaEuler.x, -maxDeltaPerFrame, maxDeltaPerFrame);
        deltaEuler.y = Mathf.Clamp(deltaEuler.y, -maxDeltaPerFrame, maxDeltaPerFrame);
        deltaEuler.z = Mathf.Clamp(deltaEuler.z, -maxDeltaPerFrame, maxDeltaPerFrame);

        // 经验映射 - 根据你期望的手感调整符号或轴
        cube.Rotate(new Vector3(-deltaEuler.y, -deltaEuler.z, -deltaEuler.x), Space.Self);
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

    private void SetText(string msg)
    {
        if (output != null) output.text = msg;
        else Debug.Log(msg);
    }
}
