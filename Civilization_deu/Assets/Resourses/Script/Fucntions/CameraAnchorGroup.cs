// CameraAnchorGroup.cs (在你原文件基础上，补一个可选排序字段)
using UnityEngine;
using System.Collections.Generic;

public class CameraAnchorGroup : MonoBehaviour
{
    [Tooltip("按顺序填：0=初始，其后依序")]
    public List<Transform> anchors = new List<Transform>();

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