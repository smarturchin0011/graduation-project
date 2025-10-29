using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SliderFunc : MonoBehaviour
{
    [SerializeField] public RectTransform slider;
    [SerializeField] public float speed = 0.3f;
    
    private Coroutine moveCoroutine;
    // Start is called before the first frame update
    void Awake()
    {
        slider = this.GetComponent<RectTransform>();
        
    }
    

    // 外部调用这个方法来切换按钮
    public void MoveToButton(RectTransform targetButton)
    {
        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);

        moveCoroutine = StartCoroutine(Move(targetButton));
    }



    IEnumerator Move(RectTransform target)
    {
        Vector3 startPos = slider.position;
        Vector3 targetPos = new Vector3(target.position.x, slider.position.y, slider.position.z);
        float elapsed = 0f;

        while (elapsed < speed)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / speed;
            // 使用 SmoothStep 提升丝滑感
            t = Mathf.SmoothStep(0, 1, t);
            slider.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        slider.position = targetPos; // 确保到位
    }
}
