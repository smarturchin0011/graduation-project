using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControlPC : MonoBehaviour
{
   public enum DragMode
    {
        PanX,   // 模式1：沿相机自身 X 轴左右平移
        RotateY // 模式2：绕相机自身 Y 轴旋转
    }

    [Header("当前模式")]
    public DragMode mode = DragMode.PanX;

    [Header("平移速度（单位：世界单位/像素）")]
    public float panSpeed = 0.02f;

    [Header("旋转速度（单位：度/像素）")]
    public float rotateSpeed = 0.2f;

    [Header("是否允许按下 Tab 切换模式")]
    public bool enableKeyboardToggle = true;

    [Header("是否反转左右拖拽方向")]
    public bool invert = false;

    // 记录上一帧鼠标位置，用于计算拖拽增量
    private Vector3 _lastMousePos;
    private bool _isRightDragging = false;

    void Update()
    {
        HandleModeToggleByKey();
        HandleRightMouseDrag();
    }

    /// <summary>
    /// Tab 键切换模式（可选）
    /// </summary>
    private void HandleModeToggleByKey()
    {
        if (!enableKeyboardToggle) return;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            mode = (mode == DragMode.PanX) ? DragMode.RotateY : DragMode.PanX;
            Debug.Log($"[CameraMouseDragController] Mode changed to: {mode}");
        }
    }

    /// <summary>
    /// 处理右键拖拽
    /// </summary>
    private void HandleRightMouseDrag()
    {
        // 按下右键那一帧
        if (Input.GetMouseButtonDown(1))
        {
            _isRightDragging = true;
            _lastMousePos = Input.mousePosition;
        }

        // 松开右键那一帧
        if (Input.GetMouseButtonUp(1))
        {
            _isRightDragging = false;
        }

        if (!_isRightDragging) return;

        Vector3 currentMousePos = Input.mousePosition;
        Vector3 delta = currentMousePos - _lastMousePos;
        _lastMousePos = currentMousePos;

        // 我们只关心水平方向的拖拽（X）
        float deltaX = delta.x;

        if (invert)
            deltaX = -deltaX;

        switch (mode)
        {
            case DragMode.PanX:
                DoPanX(deltaX);
                break;

            case DragMode.RotateY:
                DoRotateY(deltaX);
                break;
        }
    }

    /// <summary>
    /// 模式1：沿相机自身 X 轴左右平移（角度不变）
    /// </summary>
    private void DoPanX(float deltaX)
    {
        // deltaX 越大，平移越多
        // transform.right 就是相机自身 X 轴的方向
        Vector3 move = transform.right * (deltaX * panSpeed);

        // 仅改变位置，不改 rotation
        transform.position += move;
    }

    /// <summary>
    /// 模式2：绕相机自身 Y 轴旋转（位置不变）
    /// </summary>
    private void DoRotateY(float deltaX)
    {
        // deltaX 越大，旋转角度越大
        float angle = deltaX * rotateSpeed;

        // 只绕自身 Y 轴旋转，位置不动
        transform.Rotate(0f, angle, 0f, Space.Self);
    }

    /// <summary>
    /// 如果你想从别的脚本控制模式，可以调用这个方法
    /// </summary>
    public void SetMode(DragMode newMode)
    {
        mode = newMode;
    }
}
