// CameraAnchorGroup.cs
// 用来获取锚点位置，以及锚点顺序
// 控制是否需要按原路返回该组的初始锚点
using UnityEngine;
using System.Collections.Generic;

public class CameraAnchorGroup : MonoBehaviour
{
    [Tooltip("按顺序填：0=初始，其后依序")]
    public List<Transform> anchors = new List<Transform>();
    
    [Tooltip("本锚点组的交互点")]
    public List<Transform> hotSpots = new List<Transform>();
    
    [Tooltip("从本组最后锚点切到下一章节前，是否按原路退回到本组初始锚点")]
    public bool returnToInitialOnNextChapter = true;
    
    [Tooltip("到达本组最后一个锚点后，是否自动跳转到下一章节")]
    public bool autoNextChapterOnLastAnchor = false;

    [Tooltip("同场景存在多个分组时，可勾选一个为默认")]
    public bool isDefaultGroup = false;

    [Tooltip("同场景多分组的显示/切换顺序（可选）。越小越先。")]
    public int chapterOrder = 0;
    public bool IsValid => anchors != null && anchors.Count >= 1;
    public int Count => anchors?.Count ?? 0;

    public Transform GetAnchor(int index)
    {
        if (!IsValid) return null;
        index = Mathf.Clamp(index, 0, anchors.Count - 1);
        return anchors[index];
    }
}