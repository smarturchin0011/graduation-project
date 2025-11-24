using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class HotSpotClickFunc : MonoBehaviour
{
    [SerializeField]
    public UnityEvent OnClick;

    private void OnMouseDown()
    {
        if ( OnClick!= null)
        {
            Click();
        }
    }

    public void Click()
    {
        OnClick?.Invoke();
    }
}