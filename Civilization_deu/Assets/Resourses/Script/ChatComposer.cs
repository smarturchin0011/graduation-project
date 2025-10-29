using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class ChatComposer : MonoBehaviour
{
    [Header("调用api脚本")]
    [SerializeField] private ApiPost _apiPost;
    [SerializeField] private TencentApiManager _tencentApiManager;

    [Header("Refs")]
    [SerializeField] TMP_InputField inputField;
    [SerializeField] Button sendButton;
    [SerializeField] ScrollRect scrollRect;          // 指向的 Scroll View
    [SerializeField] RectTransform content;          // 指向 Scroll View/Viewport/Content
    [SerializeField] GameObject userBoxPrefab;       // userBox 预制体（含 TMP_Text）
    [SerializeField] GameObject speakerBoxPrefab;    // speakerBox 预制体（含 TMP_Text）

    [Header("Hooks (预留给 LLM)")]
    public UnityEvent<string> onUserMessage;         // 发送后把原文 invok 出去

    // 本轮“说书人”流式绑定的文本引用
    TMP_Text activeAssistantText = null;

    void Awake()
    {
        if (sendButton) sendButton.onClick.AddListener(Send);

        _apiPost = GetComponent<ApiPost>();
        _tencentApiManager = GetComponent<TencentApiManager>();
        
        // 现在：只把“完成”当作清理/收尾事件，不创建新气泡
        if (_apiPost != null)
            _apiPost.OnUIUpdate += OnAssistantFinished;

        // 订阅流式增量：把分片直接追加到当前说书人气泡
        if (_tencentApiManager != null)
            _tencentApiManager.OnStreamDelta.AddListener(AppendAssistantDelta);
    }

    void OnDestroy()
    {
        if (sendButton) sendButton.onClick.RemoveListener(Send);
        if (_tencentApiManager != null)
            _tencentApiManager.OnStreamDelta.RemoveListener(AppendAssistantDelta);
        if (_apiPost != null)
            _apiPost.OnUIUpdate -= OnAssistantFinished;
    }

    public void Send()
    {
        if (!inputField) return;
        var text = inputField.text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        // 1) 生成一条用户气泡
        CreateUserBubble(text);

        // 2) 立刻创建“说书人气泡”（空文本），作为本轮流的承接者
        CreateEmptySpeakerBubble();

        // 3) 清空输入框 & 失焦（可选）
        inputField.text = string.Empty;
        inputField.DeactivateInputField();

        // 4) 抛给 ApiPost → TencentApiManager.SendTextMessage（外部链路不变）
        onUserMessage?.Invoke(text);
    }

    public void SendTest(string contentText)
    {
        CreateUserBubble(contentText);
        CreateEmptySpeakerBubble();
    }

    void CreateUserBubble(string text)
    {
        if (!userBoxPrefab || !content) return;
        var go = Instantiate(userBoxPrefab, content);
        var tmp = go.GetComponentInChildren<TMP_Text>(true);
        if (tmp) tmp.text = text;
        Canvas.ForceUpdateCanvases();
        ScrollToBottom();
    }

    // 发送后只创建一次空的说书人气泡
    void CreateEmptySpeakerBubble()
    {
        if (!speakerBoxPrefab || !content) return;
        var go = Instantiate(speakerBoxPrefab, content);
        activeAssistantText = go.GetComponentInChildren<TMP_Text>(true);
        if (activeAssistantText) activeAssistantText.text = string.Empty;
        Canvas.ForceUpdateCanvases();
        ScrollToBottom();
    }

    // 流式增量写入当前气泡
    void AppendAssistantDelta(string piece)
    {
        if (activeAssistantText == null) return;
        activeAssistantText.text += piece;
        Canvas.ForceUpdateCanvases();
        ScrollToBottom();
    }

    // 流式结束：不再创建新气泡；可选择性校正文本，这里直接结束并清引用
    void OnAssistantFinished(string fullText)
    {
        // 最终校正
        if (activeAssistantText != null) activeAssistantText.text = fullText;

        activeAssistantText = null; // 本轮结束，等待下一次用户输入
    }

    void ScrollToBottom()
    {
        if (!scrollRect) return;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();
    }

    void Update()
    {
        if (inputField && inputField.isFocused && Input.GetKeyDown(KeyCode.Return))
            Send();
    }
}
