using UnityEngine;
/// <summary>
/// 检测当前 CameraAnchorGroup 中未交互的 Hotspot，
/// 如果不在屏幕里，则判断在主相机左侧还是右侧，
/// 并打开对应的提示 UI。
/// </summary>
public class HotspotDirectionHint : MonoBehaviour
{ 
    [Header("路径移动控制器（CameraPathMover）")]
    public CameraPathMover pathMover;

    [Header("用于计算视口坐标的相机（默认主相机）")]
    public Camera targetCamera;

    [Header("左右提示 UI 对象")]
    public GameObject leftHintUI;
    public GameObject rightHintUI;

    // 记录上一次使用的组，用于检测组切换
    private CameraAnchorGroup _lastGroup;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (pathMover == null)
        {
            pathMover = FindObjectOfType<CameraPathMover>();
        }

        // 初始先关掉提示
        SetHints(false, false);
    }

    private void OnEnable()
    {
        _lastGroup = null;
        SetHints(false, false);
    }

    private void Update()
    {
        if (pathMover == null || targetCamera == null)
        {
            SetHints(false, false);
            return;
        }

        CameraAnchorGroup group = pathMover.CurrentGroup;
        if (group == null || !group.IsValid)
        {
            // 当前没有有效组，直接关闭提示
            SetHints(false, false);
            _lastGroup = null;
            return;
        }

        // 如果组切换了，重置一次提示状态
        if (group != _lastGroup)
        {
            _lastGroup = group;
            SetHints(false, false);
        }

        bool hasLeft = false;
        bool hasRight = false;

        // 没有配置热点列表就直接关提示
        if (group.hotSpots == null || group.hotSpots.Count == 0)
        {
            SetHints(false, false);
            return;
        }

        // 遍历当前组的所有热点
        for (int i = 0; i < group.hotSpots.Count; i++)
        {
            Transform hs = group.hotSpots[i];
            if (hs == null) continue;

            GameObject go = hs.gameObject;

            // 约定：被点击后会 SetActive(false)，此时视为“已交互”，不再参与检测
            if (!go.activeInHierarchy) continue;

            Vector3 viewPos = targetCamera.WorldToViewportPoint(hs.position);

            // 在相机背后（z <= 0）就忽略
            if (viewPos.z <= 0f) continue;

            bool insideViewport =
                viewPos.x >= 0f && viewPos.x <= 1f &&
                viewPos.y >= 0f && viewPos.y <= 1f;

            // 只对“不在屏幕里的热点”做方向提示
            if (insideViewport) continue;

            // 判断左右：
            // x < 0 -> 在左侧； x > 1 -> 在右侧。
            if (viewPos.x < 0f)
            {
                hasLeft = true;
            }
            else if (viewPos.x > 1f)
            {
                hasRight = true;
            }

            // 两边都已经有了，就可以提前结束循环
            if (hasLeft && hasRight)
                break;
        }

        SetHints(hasLeft, hasRight);
    }

    private void SetHints(bool showLeft, bool showRight)
    {
        if (leftHintUI != null)
            leftHintUI.SetActive(showLeft);

        if (rightHintUI != null)
            rightHintUI.SetActive(showRight);
    }
}
