using UnityEngine;

public class HomeView : MonoBehaviour
{
    bool built;

    void OnEnable() { StartCoroutine(CoBuildNextFrame()); }

    System.Collections.IEnumerator CoBuildNextFrame()
    {
        yield return null;                 // 避开本帧的 UI 重建
        if (!built) { Build(); built = true; }
        else { RefreshLight(); }           // 可选：轻量刷新
    }

    void Build()
    {
        // 在这里实例化/销毁首页的卡片、模块、轮播项等
        // 批量操作很多的话，用下方第3条的“暂关Canvas再开”包裹
    }

    void RefreshLight() { /* 已构建后的小改动（替换文案/图片等） */ }
}