using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwitchAnimatorControler : MonoBehaviour
{
    [SerializeField] public Animator switchAnimator;
    [SerializeField] public bool isSwitch;
    private Coroutine switchcoroutine;


    public void Switch()
    {
        if ( switchcoroutine!=null)
        {
            StopCoroutine(switchcoroutine);
        }

        switchcoroutine = StartCoroutine(Onswitch());
    }

    IEnumerator Onswitch()
    {
        switchAnimator.SetBool("IsSwitch",true);
        isSwitch = true;
        yield return new WaitForSeconds(0.5f);
        switchAnimator.SetBool("IsSwitch",false);
        isSwitch = false;
        yield return new WaitForSeconds(0.5f);
    }

}
