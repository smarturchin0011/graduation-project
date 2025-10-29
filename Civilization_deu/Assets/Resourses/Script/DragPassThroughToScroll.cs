using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragPassThroughToScroll : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    Transform scrollRoot; // ScrollRect 所在节点

    void Awake()
    {
        var sr = GetComponentInParent<ScrollRect>(true);
        if (sr) scrollRoot = sr.transform;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (scrollRoot)
            ExecuteEvents.ExecuteHierarchy(scrollRoot.gameObject, e, ExecuteEvents.beginDragHandler);
    }

    public void OnDrag(PointerEventData e)
    {
        if (scrollRoot)
            ExecuteEvents.ExecuteHierarchy(scrollRoot.gameObject, e, ExecuteEvents.dragHandler);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (scrollRoot)
            ExecuteEvents.ExecuteHierarchy(scrollRoot.gameObject, e, ExecuteEvents.endDragHandler);
    }
}