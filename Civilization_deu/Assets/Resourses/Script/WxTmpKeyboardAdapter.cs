using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

#if UNITY_WEBGL && (WEIXINMINIGAME || PLATFORM_WEIXINMINIGAME || WECHAT)
using WeChatWASM; // 仅在小游戏构建里编译
#endif

/// <summary>
/// 点击/选中 TMP_InputField 时，调起微信键盘；输入 -> 回填 TMP；带日志与Toast调试。
/// 挂到 Canvas/spekerPage/InputField (TMP) 上即可。
/// </summary>
public class WxTmpKeyboardAdapter : MonoBehaviour,
    ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerClickHandler
{
    [Header("Refs")]
    public TMP_InputField tmp;

    [Header("Debug")]
    public bool toastDebug = true;   // 在微信里弹Toast做可视化调试
    public bool logDebug = true;     // 在控制台打印 [WXKeyboard] 日志

    bool listening = false;

    void Awake()
    {
        if (!tmp) tmp = GetComponent<TMP_InputField>();
    }

    public void OnPointerDown(PointerEventData e)
    {
        tmp?.ActivateInputField();
    }

    public void OnPointerClick(PointerEventData e)
    {
        tmp?.ActivateInputField();
        OpenWxKeyboard();
    }

    public void OnSelect(BaseEventData e)
    {
        OpenWxKeyboard();
    }

    public void OnDeselect(BaseEventData e)
    {
        CloseWxKeyboard();
    }

    void OpenWxKeyboard()
    {
#if UNITY_WEBGL && !UNITY_EDITOR && (WEIXINMINIGAME || PLATFORM_WEIXINMINIGAME || WECHAT)
        if (listening) return;
        listening = true;

        int maxLen   = (tmp && tmp.characterLimit > 0) ? tmp.characterLimit : 140;           // 注意：开发者工具里不传 maxLength 可能不能输入
        bool multi   = (tmp && tmp.lineType != TMP_InputField.LineType.SingleLine);
        string defV  = tmp ? (tmp.text ?? "") : "";

        Log($"ShowKeyboard maxLen={maxLen} multi={multi} default='{defV}'");

        WX.ShowKeyboard(new ShowKeyboardOption {
            defaultValue = defV,
            maxLength    = maxLen,
            multiple     = multi,
            confirmType  = multi ? "done" : "send",
            confirmHold  = false
        });

        if (toastDebug) WX.ShowToast(new ShowToastOption { title = "ShowKeyboard()", duration = 600 });

        // —— 输入中：实时回填
        WX.OnKeyboardInput(res => {
            if (tmp)
            {
                tmp.text = res.value;
                tmp.MoveTextEnd(false);
            }
            Log("OnKeyboardInput: " + res.value);
        });

        // —— 点击“完成/发送”
        WX.OnKeyboardConfirm(res => {
            if (tmp)
            {
                tmp.text = res.value;
                tmp.onEndEdit?.Invoke(tmp.text); // 需要时可在 TMP 的 On End Edit 里再挂发送逻辑
            }
            Log("OnKeyboardConfirm: " + res.value);
            if (toastDebug) WX.ShowToast(new ShowToastOption { title = "Confirm", duration = 500 });
        });

        // —— 键盘收起（包含确认/取消等情形）
        WX.OnKeyboardComplete(res => {
            if (tmp) tmp.DeactivateInputField();
            Log("OnKeyboardComplete");
            if (toastDebug) WX.ShowToast(new ShowToastOption { title = "Complete", duration = 500 });
            CloseWxKeyboard(); // 统一清理监听
        });
#else
        Log("OpenWxKeyboard() skipped —— 非微信小游戏运行环境。");
#endif
    }

    void CloseWxKeyboard()
    {
#if UNITY_WEBGL && !UNITY_EDITOR && (WEIXINMINIGAME || PLATFORM_WEIXINMINIGAME || WECHAT)
        Log("HideKeyboard()");
        WX.HideKeyboard(new HideKeyboardOption { });
        WX.OffKeyboardInput();
        WX.OffKeyboardConfirm();
        WX.OffKeyboardComplete();
        if (toastDebug) WX.ShowToast(new ShowToastOption { title = "HideKeyboard()", duration = 500 });
#endif
        listening = false;
    }

    void OnDisable()  => CloseWxKeyboard();
    void OnDestroy()  => CloseWxKeyboard();

    void Log(string msg)
    {
        if (logDebug) Debug.Log("[WXKeyboard] " + msg);
    }
}