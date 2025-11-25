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
    }
}