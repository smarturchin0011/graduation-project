using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[System.Serializable] public class SlideData
{
    public Sprite sprite;
    public int targetPanelIndex;
}

public class SimpleCarousel : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Header("Refs")]
    [SerializeField] ScrollRect scroll;
    [SerializeField] RectTransform content;
    [SerializeField] RectTransform viewport;
    [SerializeField] GameObject slideItemPrefab;
    [SerializeField] Transform dotsParent;  // 可为空

    [Header("Config")]
    [SerializeField] float snapTime = 0.25f;

    [Header("Data")]
    public List<SlideData> slides = new();

    readonly List<RectTransform> items = new();
    float cellWidth;
    bool dragging;
    int page; // 逻辑页 0..n-1
    UIManager ui;

    void Awake()
    {
        ui = FindObjectOfType<UIManager>();
    }

    void Start() { StartCoroutine(CoSafeBuild()); }

    System.Collections.IEnumerator CoSafeBuild()
    {
        yield return null;   // 等布局尺寸就绪（避免重建期间再改UI）
        CoSafeBuild();
    }


    public void Build()
    {
        foreach (Transform c in content) Destroy(c.gameObject);
        items.Clear();
        if (slides == null || slides.Count == 0) return;

        // cloneLast at head
        items.Add(CreateItem(slides[^1]));
        // real
        for (int i = 0; i < slides.Count; i++) items.Add(CreateItem(slides[i]));
        // cloneFirst at tail
        items.Add(CreateItem(slides[0]));
        
        cellWidth = viewport.rect.width;
        SetPos(1, true); // 初始显示逻辑第0页=物理索引1
        UpdateDots();
    }

    RectTransform CreateItem(SlideData d)
    {
        var rt = Instantiate(slideItemPrefab, content).GetComponent<RectTransform>();
        var img = rt.GetComponentInChildren<Image>(true);
        if (img) img.sprite = d.sprite;
        var btn = rt.GetComponentInChildren<Button>(true);
        if (btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => { if (ui) ui.ShowPanel(d.targetPanelIndex); });
        }
        return rt;
    }

    public void OnBeginDrag(PointerEventData e) { dragging = true; }
    public void OnEndDrag(PointerEventData e) { dragging = false; SnapToNearest(); }

    void Update()
    {
        if (dragging) return;
        // 循环：滑到两端克隆时瞬移
        int phys = Mathf.RoundToInt(-content.anchoredPosition.x / cellWidth);
        if (phys <= 0) SetPos(slides.Count, true);
        else if (phys >= items.Count - 1) SetPos(1, true);
    }

    void SnapToNearest()
    {
        float x = content.anchoredPosition.x;
        int phys = Mathf.RoundToInt(-x / cellWidth);
        phys = Mathf.Clamp(phys, 0, items.Count - 1);
        int logic = (phys - 1 + slides.Count) % slides.Count;
        page = logic;
        SetPos(phys, false);
        UpdateDots();
    }

    void SetPos(int physIndex, bool instant)
    {
        Vector2 target = new(-physIndex * cellWidth, content.anchoredPosition.y);
        if (instant) content.anchoredPosition = target;
        else StartCoroutine(TweenTo(target));
    }

    System.Collections.IEnumerator TweenTo(Vector2 target)
    {
        Vector2 start = content.anchoredPosition; float t = 0;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / snapTime;
            content.anchoredPosition = Vector2.Lerp(start, target, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        content.anchoredPosition = target;
    }

    void UpdateDots()
    {
        if (!dotsParent) return;
        for (int i = 0; i < dotsParent.childCount; i++)
        {
            var dot = dotsParent.GetChild(i).gameObject;
            dot.SetActive(i < slides.Count);
            // 可在这里切换 dot 的选中态（例如更换图片或开启高亮子物体）
            if (i < slides.Count)
            {
                // 示例：有个名为 "On" 的子物体表示选中
                var on = dot.transform.Find("On");
                if (on) on.gameObject.SetActive(i == page);
            }
        }
    }
}

