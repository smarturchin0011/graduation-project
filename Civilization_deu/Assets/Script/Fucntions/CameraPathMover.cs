// 功能：相机按锚点组平滑推进（无强吸附抖动），支持“最后锚点→下一章节→返回→切下组初始”。
// 运动实现：LateUpdate + 绝对时间推进 + Vector3.SmoothDamp(位置) + 指数式 RotateTowards(旋转)。
// 适配：微信小游戏端锁帧，减少帧抖引起的台阶感。
// 依赖：可选订阅 ForwardSwingDetector.OnForwardTriggered 触发前进；CameraAnchorGroup 提供锚点序列。

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class CameraPathMover : MonoBehaviour
{
    // ========== 组切换过渡模式 ==========
    public enum GroupTransitionMode
    {
        Teleport,           // 直接瞬移到下组初始
        SmoothMove,         // 从旧初始平滑移动到下组初始
        FadeAndTeleport,    // 黑场 → 切组瞬移 → 亮场
        FadeAndSmoothMove   // 黑场 → 切组 → 亮场后平滑移动到下组初始
    }

    [Header("目标相机（或相机Rig）")]
    public Transform cameraRig;

    [Header("推进时长（同组内相邻锚点）")]
    public float moveDuration = 1.20f;

    [Header("组间过渡")]
    public GroupTransitionMode transitionMode = GroupTransitionMode.FadeAndTeleport;
    public float groupMoveDuration = 1.00f;           // 仅 SmoothMove/FadeAndSmoothMove 用
    public CanvasGroup fadeCanvas;                    // 可选：用于淡入淡出（全屏Image+CanvasGroup）
    public float fadeDuration = 0.30f;

    [Header("触发器（为空则自动找）")]
    public ForwardSwingDetector detector;

    [Header("组管理（可留空自动搜集并按 chapterOrder 排序）")]
    public List<CameraAnchorGroup> groups;

    // ====== 平滑与防抖参数（核心：无强吸附）======
    [Header("插值曲线（0..1）")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("位置平滑（SmoothDamp）")]
    public bool useSmoothDamp = true;
    public float smoothTime = 0.12f;     // 越大越柔
    public float maxSpeed = 100f;

    [Header("旋转平滑（指数式 RotateTowards）")]
    public float rotSmoothTime = 0.10f;  // 越大越柔（指数时间常数）

    [Header("结束收敛阈值（不强行吸附）")]
    public float posEpsilon = 0.002f;    // 米
    public float velEpsilon = 0.002f;    // 米/秒（SmoothDamp 的速度向量模长）
    public float angEpsilon = 0.15f;     // 度
    public int   settleFrames = 2;       // 连续满足阈值的帧数才判结束

    // ====== 运行态 ======
    private CameraAnchorGroup _group;
    private int _groupIndex = 0;       // 当前组在 groups 中的索引
    private int _currentIndex = 0;     // 当前锚点索引（组内）
    private bool _isMoving = false;
    
    /// <summary>
    /// 当前正在使用的锚点组（只读）
    /// </summary>
    public CameraAnchorGroup CurrentGroup => _group;

    /// <summary>
    /// 当前组在 groups 列表中的索引（只读）
    /// </summary>
    public int CurrentGroupIndex => _groupIndex;


    // Tween 状态（LateUpdate 驱动）
    private bool tweenActive = false;
    private Vector3 fromPos, toPos, vel;    // vel 用于 SmoothDamp
    private Quaternion fromRot, toRot;
    private float tweenStart, tweenDuration;
    private int _pendingIndex = -1;
    private int settledCount = 0;

    // ====== 生命周期 ======
    private void Awake()
    {
        if (cameraRig == null)
        {
            var cam = Camera.main;
            if (cam) cameraRig = cam.transform;
        }
    }

    private void Start()
    {
        // 锁 60 帧（Unity + 微信端）
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 绑定检测器
        BindDetector(detector ?? FindObjectOfType<ForwardSwingDetector>());

        // 组初始化
        BootstrapGroupsInScene();
        // 放到当前组初始
        JumpToGroupInitial();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindDetector();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BootstrapGroupsInScene();
        JumpToGroupInitial();
    }

    // ====== 组管理 ======
    private void BootstrapGroupsInScene()
    {
        if (groups == null || groups.Count == 0)
        {
            groups = FindObjectsOfType<CameraAnchorGroup>()
                     .Where(g => g.IsValid)
                     .OrderBy(g => g.chapterOrder)
                     .ToList();
        }

        if (groups.Count > 0)
        {
            var def = groups.FirstOrDefault(g => g.isDefaultGroup) ?? groups[0];
            _groupIndex = Mathf.Clamp(groups.IndexOf(def), 0, groups.Count - 1);
            SetAnchorGroup(groups[_groupIndex]);
        }
        else
        {
            _group = null;
        }
    }

    private void SetAnchorGroup(CameraAnchorGroup group)
    {
        _group = group;
        _currentIndex = 0;
        tweenActive = false;
        _isMoving = false;
        settledCount = 0;
    }

    private void JumpToGroupInitial()
    {
        if (_group != null && _group.IsValid && cameraRig != null)
        {
            var a0 = _group.GetAnchor(0);
            cameraRig.position = a0.position;
            cameraRig.rotation = a0.rotation;
            _currentIndex = 0;
        }
    }

    // ====== 触发器绑定 ======
    private void BindDetector(ForwardSwingDetector d)
    {
        if (d == null) return;
        detector = d;
        detector.OnForwardTriggered += HandleForwardTriggered;
    }

    private void UnbindDetector()
    {
        if (detector != null)
            detector.OnForwardTriggered -= HandleForwardTriggered;
    }

    private void HandleForwardTriggered()
    {
        if (_isMoving || _group == null || !_group.IsValid || cameraRig == null) return;

        if (_currentIndex >= _group.Count - 1) return; // 已在最后一个锚点

        MoveToIndex(_currentIndex + 1);
    }

    // ====== 对外（UI）：下一章节 ======
    public void OnNextChapter()
    {
        if (_isMoving || _group == null || !_group.IsValid || cameraRig == null) return;
        if (_currentIndex != _group.Count - 1) return; // 仅在最后一个锚点有效

        StartCoroutine(NextChapterFlow());
    }

    private IEnumerator NextChapterFlow()
    {
        _isMoving = true;

        // Step1：按原路返回到当前组初始（可选）
        bool needReturn = (_group != null && _group.returnToInitialOnNextChapter);
        if (needReturn)
        {
            for (int i = _currentIndex - 1; i >= 0; --i)
            {
                var to = _group.GetAnchor(i);
                yield return MoveStep(to, moveDuration);
            }
            // 只有执行了“原路返回”时，才把索引重置到 0
            _currentIndex = 0;
        }

        // Step2：若无下一个组，流程结束（可在此触发真正的章节切换/场景切换）
        if (groups == null || groups.Count == 0 || _groupIndex >= groups.Count - 1)
        {
            _isMoving = false;
            yield break;
        }

        var oldInit = _group.GetAnchor(0);
        var nextGroup = groups[_groupIndex + 1];
        var newInit = nextGroup.GetAnchor(0);

        // Step3：组间过渡
        switch (transitionMode)
        {
            case GroupTransitionMode.Teleport:
                SetAnchorGroup(nextGroup);
                cameraRig.position = newInit.position;
                cameraRig.rotation = newInit.rotation;
                break;

            case GroupTransitionMode.SmoothMove:
                cameraRig.position = oldInit.position;
                cameraRig.rotation = oldInit.rotation;
                SetAnchorGroup(nextGroup);
                yield return MoveStep(newInit, groupMoveDuration);
                break;

            case GroupTransitionMode.FadeAndTeleport:
                yield return Fade(1f);
                SetAnchorGroup(nextGroup);
                cameraRig.position = newInit.position;
                cameraRig.rotation = newInit.rotation;
                yield return Fade(0f);
                break;

            case GroupTransitionMode.FadeAndSmoothMove:
                yield return Fade(1f);
                cameraRig.position = oldInit.position;
                cameraRig.rotation = oldInit.rotation;
                SetAnchorGroup(nextGroup);
                yield return Fade(0f);
                yield return MoveStep(newInit, groupMoveDuration);
                break;
        }

        _groupIndex++;
        _currentIndex = 0;
        _isMoving = false;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (fadeCanvas == null) yield break;

        fadeCanvas.blocksRaycasts = true;
        float start = fadeCanvas.alpha;
        float t = 0f;
        float dur = Mathf.Max(0.01f, fadeDuration);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime; // UI 淡入用不受缩放的时间
            float k = Mathf.Clamp01(t / dur);
            fadeCanvas.alpha = Mathf.Lerp(start, targetAlpha, k);
            yield return null;
        }
        fadeCanvas.alpha = targetAlpha;
        // 全亮时才放开点击
        fadeCanvas.blocksRaycasts = targetAlpha > 0.01f;
    }

    // ====== 移动（无强吸附版本）======
    // 发起到组内某索引的移动
    private void MoveToIndex(int targetIndex)
    {
        var to = _group.GetAnchor(targetIndex);
        BeginMoveTo(to, moveDuration, targetIndex);
    }

    // 单步移动（等待式，给回退/组间过渡用）
    private IEnumerator MoveStep(Transform to, float duration)
    {
        BeginMoveTo(to, duration, -1);
        while (tweenActive) yield return null;
    }

    // 启动一次移动
    private void BeginMoveTo(Transform target, float duration, int targetIndexAfter)
    {
        if (cameraRig == null || target == null) return;

        tweenActive = true;
        _isMoving = true;
        settledCount = 0;

        fromPos = cameraRig.position;
        fromRot = cameraRig.rotation;
        toPos   = target.position;
        toRot   = target.rotation;

        vel = Vector3.zero;
        tweenStart = Time.time;
        tweenDuration = Mathf.Max(0.0001f, duration);

        _pendingIndex = targetIndexAfter;
    }

    // LateUpdate 驱动补间（核心消抖）
    private void LateUpdate()
    {
        if (!tweenActive) return;

        // 绝对时间推进：避免掉帧“欠步”
        float t = Mathf.Clamp01((Time.time - tweenStart) / tweenDuration);
        float k = ease != null ? ease.Evaluate(t) : t;

        // 位置目标（曲线 Lerp），再用 SmoothDamp 追踪，弱化台阶
        Vector3 lerpTargetPos = Vector3.LerpUnclamped(fromPos, toPos, k);
        if (useSmoothDamp)
            cameraRig.position = Vector3.SmoothDamp(cameraRig.position, lerpTargetPos, ref vel, smoothTime, maxSpeed, Time.deltaTime);
        else
            cameraRig.position = lerpTargetPos;

        // 旋转：指数式步长收敛（不强吸附）
        float ang = Quaternion.Angle(cameraRig.rotation, toRot);
        float step = ang * (1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, rotSmoothTime)));
        cameraRig.rotation = Quaternion.RotateTowards(cameraRig.rotation, toRot, step);

        // 收敛判定：连续若干帧满足阈值才结束
        float dist = Vector3.Distance(cameraRig.position, toPos);
        float speed = vel.magnitude;
        float angLeft = Quaternion.Angle(cameraRig.rotation, toRot);
        bool settledThisFrame = (dist <= posEpsilon) && (speed <= velEpsilon) && (angLeft <= angEpsilon);
        settledCount = settledThisFrame ? (settledCount + 1) : 0;

// 结束：不强制赋 toPos/toRot，避免“吸附抖动”
        if (settledCount >= settleFrames || t >= 1f + 0.2f) // 兜底时限
        {
            tweenActive = false;
            _isMoving = false;

            if (_pendingIndex >= 0)
            {
                _currentIndex = _pendingIndex;
                _pendingIndex = -1;

                // ★ NEW：如果当前组要求“到达最后点位自动跳下一章”，就在这里触发
                if (_group != null &&
                    _group.IsValid &&
                    _group.autoNextChapterOnLastAnchor &&
                    _currentIndex == _group.Count - 1)
                {
                    OnNextChapter();   // 等价于你在外部手动点“下一章节”
                }
            }
        }
    }
}
