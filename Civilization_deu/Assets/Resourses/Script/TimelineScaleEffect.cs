using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(ScrollRect))]   // ← 和 ScrollRect 放同一个GO，才能收到 OnBegin/EndDrag
public class TimelineScaleEffect : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Header("Refs")]
    public ScrollRect scroll;              // 挂到 Scroll View 同物体
    public RectTransform viewport;         // Scroll View 的 Viewport
    public RectTransform content;          // Viewport/Content

    [Header("Scale Settings")]
    [Range(0.5f, 1.5f)] public float maxScale = 1.0f;
    [Range(0.2f, 1.2f)] public float minScale = 0.75f;
    [Range(0f, 1f)]     public float minAlpha = 0.35f;
    public bool alsoFade = true;
    public AnimationCurve falloff = AnimationCurve.EaseInOut(0,0,1,1);
    [Range(0.2f, 1.5f)] public float rangeByViewportHeight = 0.6f;

    [Header("Snap Settings")]
    public bool  enableAutoSnap = true;
    public float snapVelocityThreshold = 80f;     // 速度小于此值视为停下
    public float snapDuration = 0.25f;
    public AnimationCurve snapEase = AnimationCurve.EaseInOut(0,0,1,1);
    public float snapSettleMaxWait = 0.75f;       // 松手后最多等待多长时间再吸附

    bool dragging, snapping;
    Coroutine snapCo;

    void Reset()
    {
        scroll   = GetComponent<ScrollRect>();
        viewport = scroll ? scroll.viewport : null;
        content  = scroll ? scroll.content  : null;
    }

    void OnEnable()
    {
        if (!scroll) Reset();
        if (scroll) scroll.onValueChanged.AddListener(OnScroll);

        UpdateScales();
        // 首帧对齐（无动画），避免一进场就被持续吸附
        StartCoroutine(CoInitialSnapNextFrame());
    }

    void OnDisable()
    {
        if (scroll) scroll.onValueChanged.RemoveListener(OnScroll);
        if (snapCo != null) StopCoroutine(snapCo);
        snapCo = null; snapping = false; dragging = false;
    }

    IEnumerator CoInitialSnapNextFrame()
    {
        yield return null; // 等布局
        if (enableAutoSnap && content && viewport && content.rect.height > viewport.rect.height + 1f)
            SnapToNearestImmediate();
    }

    void LateUpdate() => UpdateScales();
    void OnScroll(Vector2 _) => UpdateScales();

    void UpdateScales()
    {
        if (!viewport || !content) return;

        Vector2 vpCenterLocal = viewport.rect.center;
        float influenceRange  = Mathf.Max(1f, viewport.rect.height * rangeByViewportHeight);

        for (int i = 0; i < content.childCount; i++)
        {
            var card = content.GetChild(i) as RectTransform;
            if (!card) continue;

            Vector3 cardCenterWorld = card.TransformPoint(card.rect.center);
            Vector3 cardInVpLocal   = viewport.InverseTransformPoint(cardCenterWorld);

            float dist = Mathf.Abs(cardInVpLocal.y - vpCenterLocal.y);
            float t    = Mathf.Clamp01(1f - dist / influenceRange);
            float k    = falloff.Evaluate(t);

            float s = Mathf.Lerp(minScale, maxScale, k);
            card.localScale = new Vector3(s, s, 1f);

            if (alsoFade)
            {
                float a = Mathf.Lerp(minAlpha, 1f, k);
                var cg = card.GetComponent<CanvasGroup>();
                if (!cg) cg = card.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = a;
            }
        }
    }

    // ==== 拖拽生命周期 ====
    public void OnBeginDrag(PointerEventData e)
    {
        dragging = true;
        if (snapCo != null) StopCoroutine(snapCo);
        snapCo = null; snapping = false;
    }

    public void OnEndDrag(PointerEventData e)
    {
        dragging = false;
        if (!enableAutoSnap || !gameObject.activeInHierarchy) return;
        if (snapCo != null) StopCoroutine(snapCo);
        snapCo = StartCoroutine(CoSnapWhenSettled());
    }

    IEnumerator CoSnapWhenSettled()
    {
        // 让 ScrollRect 先更新一次 velocity（EndDrag 当帧常常还没衰减）
        yield return null;

        float waited = 0f;
        Vector2 lastPos = content.anchoredPosition;

        // 等待“速度足够小”或“内容位移几乎不变”
        while (waited < snapSettleMaxWait)
        {
            // 惯性关闭时 velocity 为 0，我们用位移变化判断
            bool almostStop = Mathf.Abs(scroll.velocity.y) <= snapVelocityThreshold;
            bool almostNoMove = Vector2.Distance(content.anchoredPosition, lastPos) < 0.5f;

            if (almostStop || almostNoMove) break;

            lastPos = content.anchoredPosition;
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        SnapToNearestAnimated();
    }

    // ==== 吸附实现 ====
    void SnapToNearestAnimated()
    {
        if (!viewport || !content) return;

        RectTransform best = FindNearestCard(out float deltaY);
        if (!best) return;

        if (snapCo != null) StopCoroutine(snapCo);
        snapCo = StartCoroutine(CoSnapByDelta(-deltaY)); // 内容朝反方向移
    }

    void SnapToNearestImmediate()
    {
        if (!viewport || !content) return;

        RectTransform best = FindNearestCard(out float deltaY);
        if (!best) return;

        content.anchoredPosition += new Vector2(0f, -deltaY);
    }

    RectTransform FindNearestCard(out float deltaY)
    {
        deltaY = 0f;
        Vector2 vpCenterLocal = viewport.rect.center;
        RectTransform best = null; float bestDist = float.MaxValue;

        for (int i = 0; i < content.childCount; i++)
        {
            var card = content.GetChild(i) as RectTransform;
            if (!card || !card.gameObject.activeInHierarchy) continue;

            Vector3 centerWorld = card.TransformPoint(card.rect.center);
            Vector3 inVpLocal   = viewport.InverseTransformPoint(centerWorld);

            float dist = Mathf.Abs(inVpLocal.y - vpCenterLocal.y);
            if (dist < bestDist)
            {
                bestDist = dist; best = card;
                deltaY   = inVpLocal.y - vpCenterLocal.y;
            }
        }
        return best;
    }

    IEnumerator CoSnapByDelta(float deltaY)
    {
        snapping = true;
        if (scroll) scroll.velocity = Vector2.zero;   // 停掉惯性

        Vector2 from = content.anchoredPosition;
        Vector2 to   = from + new Vector2(0f, deltaY);

        float t = 0f, dur = Mathf.Max(0.0001f, snapDuration);
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float e = snapEase.Evaluate(Mathf.Clamp01(t));
            content.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
            yield return null;
        }
        content.anchoredPosition = to;

        snapping = false;
        snapCo = null;
    }
}
