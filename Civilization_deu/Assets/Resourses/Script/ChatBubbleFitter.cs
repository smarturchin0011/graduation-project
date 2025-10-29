using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatBubbleFitter : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text text;                 
    public RectTransform bubble;          
    public RectTransform container;       

    [Header("Layout")]
    public float maxWidth = 600f;         
    public Vector4 padding = new Vector4(24,16,24,16); // L,T,R,B

    string lastText;   // ← 用来判断是否需要重算

    void Reset(){
        text = GetComponentInChildren<TMP_Text>();
        bubble = transform as RectTransform;
        container = transform.parent as RectTransform;
    }

    void OnEnable(){
        lastText = null;   // 强制首帧刷新
    }

    public void SetText(string s){
        if (text) text.text = s;
        // 不必额外置脏，LateUpdate 会检测到 lastText != text.text
    }

    void LateUpdate(){
        if (!text || !bubble) return;

        // 文本没变就不重算，避免多余开销
        if (lastText == text.text) return;
        lastText = text.text;

        text.enableWordWrapping = true;

        float innerMax = Mathf.Max(0, maxWidth - (padding.x + padding.z));
        Vector2 pref = text.GetPreferredValues(lastText, innerMax, 0f);
        float w = Mathf.Min(pref.x, innerMax);
        float h = Mathf.Max(pref.y, 1f);

        var tr = text.rectTransform;
        tr.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        tr.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   h);

        float bw = w + padding.x + padding.z;
        float bh = h + padding.y + padding.w;

        bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bw);
        bubble.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   bh);

        if (container) LayoutRebuilder.MarkLayoutForRebuild(container);
    }
}