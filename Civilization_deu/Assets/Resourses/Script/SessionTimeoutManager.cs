using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SessionTimeoutManager : MonoBehaviour
{
    [Header("必填")]
    public UIManager ui;                    // 你的面板切换管理器（已有）
    [Tooltip("离开某页面超过该秒数，返回时重置该页面")]
    public float timeoutSeconds = 60f;

    [Header("说书人页面（可按你的索引修改）")]
    public int storytellerPanelIndex = 1;   // 说书人面板在 UIManager.panels[] 的索引
    public Transform storytellerContent;    // 说书人 ScrollView 的 Content（用于清空气泡）
    public TencentApiManager tencentApi;    // 你已有的 API 管理器（含清除历史的方法）

    // ——— 内部状态 ———
    int _currentIndex = -1;
    readonly Dictionary<int, float> _lastLeaveTime = new();  // 每个面板上次“离开”的时间（秒）

    void Reset()
    {
        if (!ui) ui = FindObjectOfType<UIManager>();
        if (!tencentApi) tencentApi = FindObjectOfType<TencentApiManager>();
    }

    void Start()
    {
        _currentIndex = DetectActivePanelIndex();
    }

    void Update()
    {
        int index = DetectActivePanelIndex();
        if (index == -1) return;

        if (index != _currentIndex)
        {
            // 记录离开旧页的时间
            if (_currentIndex >= 0) _lastLeaveTime[_currentIndex] = Time.realtimeSinceStartup;

            // 检查新页是否超时，需要重置
            if (_lastLeaveTime.TryGetValue(index, out var lastLeave))
            {
                float away = Time.realtimeSinceStartup - lastLeave;
                if (away >= timeoutSeconds)
                    ResetPanel(index);
            }

            _currentIndex = index;
        }
    }

    int DetectActivePanelIndex()
    {
        if (ui == null || ui.panels == null || ui.panels.Length == 0) return -1;
        for (int i = 0; i < ui.panels.Length; i++)
            if (ui.panels[i] && ui.panels[i].activeInHierarchy) return i;
        return -1;
    }

    void ResetPanel(int index)
    {
        var panel = ui.panels[index];
        if (!panel) return;

        // 1) 所有页面：回到顶部（如有 ScrollRect）
        var scrolls = panel.GetComponentsInChildren<ScrollRect>(includeInactive: true);
        foreach (var sr in scrolls)
        {
            Canvas.ForceUpdateCanvases();
            if (sr.vertical) sr.verticalNormalizedPosition = 1f; // 顶部
            if (sr.horizontal) sr.horizontalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
        }

        // 2) 说书人：清空 UI + 后端历史
        if (index == storytellerPanelIndex)
        {
            // 清空对话气泡
            if (storytellerContent)
            {
                for (int i = storytellerContent.childCount - 1; i >= 0; i--)
                    Destroy(storytellerContent.GetChild(i).gameObject);
                Canvas.ForceUpdateCanvases();
            }

            // 清空后端对话历史（你的腾讯 API 管理器里已有）
            if (tencentApi)
            {
                // 假设方法名为 ClearHistory()；若你的类里方法名不同，直接替换下面这一行即可
                tencentApi.ClearHistory();
            }
        }
    }

    // 可选：手动立即重置所有页面（比如从设置页点击“清理缓存”）
    public void ResetAll()
    {
        for (int i = 0; i < ui.panels.Length; i++) ResetPanel(i);
    }
}
