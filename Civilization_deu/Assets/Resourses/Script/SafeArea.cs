using UnityEngine;

[ExecuteAlways]
public class SafeArea : MonoBehaviour
{
    Rect lastSafe = new Rect(0,0,0,0);
    RectTransform panel;

    void OnEnable()
    {
        panel = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void Update()
    {
        // 编辑器或运行时尺寸变化时自动刷新
        if (Screen.safeArea != lastSafe) ApplySafeArea();
    }

    void ApplySafeArea()
    {
        var sa = Screen.safeArea;     // 物理像素的安全区
        lastSafe = sa;

        // 把物理像素换算成 0~1 归一化锚点
        Vector2 anchorMin = sa.position;
        Vector2 anchorMax = sa.position + sa.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        panel.anchorMin = anchorMin;
        panel.anchorMax = anchorMax;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
    }
}