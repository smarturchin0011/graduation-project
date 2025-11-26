using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class HotSpotClickFunc : MonoBehaviour
{
    [SerializeField]
    public UnityEvent OnClick;

    [Header("点击后自动失活自身")]
    public bool disableSelfOnClick = true;

    private void OnMouseDown()
    {
        //播放点击音效
        SoundManager.Instence.PlaySoundEffect(Globals.SE_ClickPoint,0.1f);
        
        if ( OnClick!= null)
        {
            Click();
        }
    }

    public void Click()
    {
        OnClick?.Invoke();
        // 标记为“已交互”：把自己失活
        if (disableSelfOnClick)
        {
            gameObject.SetActive(false);
        }
    }
}