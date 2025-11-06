// CameraPathMover.cs —— 支持“回到原组初始 → 过渡到下一组初始 → 继续前进”
// 用法：同前。若同场景有多组，设置它们的 chapterOrder（或在 Inspector 手动把 groups 列表排好）
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

public class CameraPathMover : MonoBehaviour
{
    public enum GroupTransitionMode
    {
        Teleport,           // 直接瞬移
        SmoothMove,         // 从当前初始锚点平滑移动到下组初始锚点
        FadeAndTeleport,    // 淡入黑 → 切组瞬移 → 淡出黑
        FadeAndSmoothMove   // 淡入黑 → 切组并从旧初始到新初始平滑移动 → 淡出黑
    }

    [Header("要推进的相机（或相机Rig）")]
    public Transform cameraRig;

    [Header("推进插值")]
    public float moveDuration = 1.2f;

    [Header("组间过渡")]
    public GroupTransitionMode transitionMode = GroupTransitionMode.FadeAndTeleport;
    public float groupMoveDuration = 1.0f;           // SmoothMove用
    public CanvasGroup fadeCanvas;                   // 可选：全屏UI上放一层Image(黑)+CanvasGroup
    public float fadeDuration = 0.3f;

    [Header("检测器（为空则自动找）")]
    public ForwardSwingDetector detector;

    [Header("（可选）显式指定本场景组；留空则自动按 chapterOrder 查找/排序")]
    public List<CameraAnchorGroup> groups;

    // 运行态
    private CameraAnchorGroup _group;
    private int _currentIndex = 0;
    private bool _isMoving = false;
    private int _groupIndex = 0; // 当前组索引（在 groups 中）

    private void Awake()
    {
        if (cameraRig == null) cameraRig = Camera.main ? Camera.main.transform : null;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindDetector(detector ?? FindObjectOfType<ForwardSwingDetector>());
        BootstrapGroupsInScene();
        // 进入场景后，将相机放到当前组的初始点
        JumpToGroupInitial();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindDetector();
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        BootstrapGroupsInScene();
        JumpToGroupInitial();
    }

    private void BootstrapGroupsInScene()
    {
        // 若没手动指定 groups，则自动搜集并按 chapterOrder 排序
        if (groups == null || groups.Count == 0)
        {
            groups = FindObjectsOfType<CameraAnchorGroup>()
                     .Where(g => g.IsValid)
                     .OrderBy(g => g.chapterOrder)
                     .ToList();
        }

        // 选默认组：优先 isDefaultGroup=true；否则取排序后第一个
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
        if (_currentIndex >= _group.Count - 1) return; // 已是最后一个

        StartCoroutine(MoveToIndex(_currentIndex + 1));
    }

    private IEnumerator MoveToIndex(int targetIndex)
    {
        _isMoving = true;

        Transform to = _group.GetAnchor(targetIndex);
        Vector3 p0 = cameraRig.position;
        Quaternion r0 = cameraRig.rotation;
        Vector3 p1 = to.position;
        Quaternion r1 = to.rotation;

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / moveDuration);
            cameraRig.position = Vector3.Lerp(p0, p1, k);
            cameraRig.rotation = Quaternion.Slerp(r0, r1, k);
            yield return null;
        }

        cameraRig.position = p1;
        cameraRig.rotation = r1;
        _currentIndex = targetIndex;
        _isMoving = false;
    }

    // UI：下一章节
    public void OnNextChapter()
    {
        if (_isMoving || _group == null || !_group.IsValid || cameraRig == null) return;

        // 只有到达最后一个锚点才响应（与你的规则一致）
        if (_currentIndex == _group.Count - 1)
        {
            StartCoroutine(NextChapterFlow());
        }
    }

    private IEnumerator NextChapterFlow()
    {
        _isMoving = true;

        // Step1：原路返回到当前组初始
        for (int i = _currentIndex - 1; i >= 0; --i)
            yield return MoveStep(_group.GetAnchor(i), moveDuration);

        _currentIndex = 0;

        // Step2：如果没有下一个组，流程结束（可在此触发“真正换章节/切场景”的外部事件）
        if (groups == null || groups.Count == 0 || _groupIndex >= groups.Count - 1)
        {
            _isMoving = false;
            yield break;
        }

        // 记录旧组初始与新组初始（做组间过渡）
        var oldInit = _group.GetAnchor(0);
        var nextGroup = groups[_groupIndex + 1];
        var newInit = nextGroup.GetAnchor(0);

        // Step3：执行组间过渡
        switch (transitionMode)
        {
            case GroupTransitionMode.Teleport:
                SetAnchorGroup(nextGroup);
                cameraRig.position = newInit.position;
                cameraRig.rotation = newInit.rotation;
                break;

            case GroupTransitionMode.SmoothMove:
                // 切组前先保证相机在旧初始，再从旧初始平滑移动到新初始
                cameraRig.position = oldInit.position;
                cameraRig.rotation = oldInit.rotation;
                // 先切组再动（方便统一 currentIndex=0）
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
                // 放到旧初始，切组
                cameraRig.position = oldInit.position;
                cameraRig.rotation = oldInit.rotation;
                SetAnchorGroup(nextGroup);
                yield return Fade(0f);            // 先亮起来
                yield return MoveStep(newInit, groupMoveDuration); // 再平滑过去
                break;
        }

        _groupIndex++;
        _currentIndex = 0;
        _isMoving = false;
        // 现在继续等 ForwardSwingDetector 的触发即可推进下一组
    }

    private IEnumerator MoveStep(Transform to, float duration)
    {
        Vector3 p0 = cameraRig.position;
        Quaternion r0 = cameraRig.rotation;
        Vector3 p1 = to.position;
        Quaternion r1 = to.rotation;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            cameraRig.position = Vector3.Lerp(p0, p1, k);
            cameraRig.rotation = Quaternion.Slerp(r0, r1, k);
            yield return null;
        }
        cameraRig.position = p1;
        cameraRig.rotation = r1;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        if (fadeCanvas == null) yield break;
        fadeCanvas.blocksRaycasts = true;

        float start = fadeCanvas.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeDuration));
            fadeCanvas.alpha = Mathf.Lerp(start, targetAlpha, k);
            yield return null;
        }
        fadeCanvas.alpha = targetAlpha;

        // 只有全亮时才放开点击
        fadeCanvas.blocksRaycasts = targetAlpha > 0.01f;
    }
}
