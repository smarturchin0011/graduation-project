using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using UnityEngine.Events;

#region Request Models
[System.Serializable]
public class YuanqiMessage
{
    public string role;
    public List<MessageContent> content = new List<MessageContent>();
}

[System.Serializable]
public class MessageContent
{
    public string type;
    public string text;
    public FileUrlContent file_url;
}

[System.Serializable]
public class FileUrlContent
{
    public string type;
    public string url;
}

[System.Serializable]
public class APIRequest
{
    public string assistant_id;
    public string user_id;
    public bool stream = false; // ← 流式开关
    public List<YuanqiMessage> messages = new List<YuanqiMessage>();
}
#endregion

/// <summary>
/// 调用腾讯元器API的主要代码，支持对话历史/记忆；SendTextMessage 内部改为“流式读取”
/// 外部调用方式保持不变：SendTextMessage(userText, onFinished, onError)
/// 若需要边下边显示，请在 Inspector 订阅 OnStreamDelta 事件。
/// </summary>
public class TencentApiManager : MonoBehaviour
{
    // ====== API 配置 ======
    private string apiUrl = "https://open.hunyuan.tencent.com/openapi/v1/agent/chat/completions";
    [SerializeField] public string assistantId;
    [SerializeField] public string apiToken;
    [SerializeField] public string userId;
    public string result;

    [Header("Timeout (s)")]
    [SerializeField] float requestTimeout = 60f;

    [Header("Streaming (可选订阅)")]
    public UnityEvent<string> OnStreamDelta;     // 每次接收到增量片段时触发（可不订阅）

    // ====== 对话历史 ======
    private static List<YuanqiMessage> conversationHistory = new List<YuanqiMessage>();
    [Tooltip("Maximum number of messages (user+assistant) to keep in history")]
    [SerializeField] private int historyLimit = 12; // 给个默认

    public void SetHistoryLimit(int limit)
    {
        historyLimit = Mathf.Max(4, limit);
        TrimHistory();
    }

    /// <summary>清空对话历史（页面超时/返回触发可调用）</summary>
    public void ClearHistory()
    {
        conversationHistory.Clear();
    }

    // ========== 外部接口：保持不变 ==========
    /// <summary>
    /// 发送文本消息（内部流式）：完成后一次性回调 callback，错误走 errorCallback
    /// 如需边下边显示，请订阅 OnStreamDelta
    /// </summary>
    public void SendTextMessage(string userMessage, Action<string> callback, Action<string> errorCallback)
    {
        // 1) 历史：先把用户消息入栈
        AddMessageToHistory("user", userMessage, null);

        // 2) 构建请求（含历史），并开启流式
        var requestData = CreateRequestFromHistory();
        requestData.stream = true; // ← 开启流式

        // 3) 启动流式协程：最终完成时再把助手完整回复入历史并回调 callback
        StartCoroutine(CoSendStream(requestData, fullText =>
        {
            AddMessageToHistory("assistant", fullText, null);
            callback?.Invoke(fullText);
        }, errorCallback));
    }

    /// <summary>
    /// 发送图文消息（保持同步/一次性返回，不改为流式）
    /// </summary>
    public void SendImageTextMessage(string imageUrl, string userMessage, Action<string> callback, Action<string> errorCallback)
    {
        var compositeMessage = new List<MessageContent>();
        if (!string.IsNullOrEmpty(userMessage))
        {
            compositeMessage.Add(new MessageContent { type = "text", text = userMessage });
        }
        compositeMessage.Add(new MessageContent
        {
            type = "file_url",
            file_url = new FileUrlContent { type = "image", url = imageUrl }
        });

        var imageMessage = new YuanqiMessage { role = "user", content = compositeMessage };

        var requestData = new APIRequest
        {
            assistant_id = assistantId,
            user_id = userId,
            stream = false, // 图文还是走一次性
            messages = new List<YuanqiMessage> { imageMessage }
        };

        StartCoroutine(SendRequestCoroutine(requestData, responseText =>
        {
            callback?.Invoke(responseText);
        }, errorCallback));
    }

    // ========== 历史工具 ==========
    private void AddMessageToHistory(string role, string text, FileUrlContent fileUrl)
    {
        var message = new YuanqiMessage { role = role };
        var content = new MessageContent();

        if (!string.IsNullOrEmpty(text))
        {
            content.type = "text";
            content.text = text;
        }
        else if (fileUrl != null)
        {
            content.type = "file_url";
            content.file_url = fileUrl;
        }

        message.content.Add(content);
        conversationHistory.Add(message);
        TrimHistory();
    }

    private void TrimHistory()
    {
        int excess = conversationHistory.Count - historyLimit;
        if (excess > 0) conversationHistory.RemoveRange(0, excess);
    }

    private APIRequest CreateRequestFromHistory()
    {
        return new APIRequest
        {
            assistant_id = assistantId,
            user_id = userId,
            stream = false, // 具体开关由调用方或 SendTextMessage 再覆写
            messages = new List<YuanqiMessage>(conversationHistory),
        };
    }

    // ========== 旧版：一次性请求（仍用于图文 or 兜底） ==========
    private IEnumerator SendRequestCoroutine(APIRequest requestData, Action<string> callback, Action<string> errorCallback)
    {
        string jsonData = JsonUtility.ToJson(requestData);
        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Source", "openapi");
            request.SetRequestHeader("Authorization", $"Bearer {apiToken}");

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                errorCallback?.Invoke($"Request failed: {request.error}");
                yield break;
            }

            try
            {
                var response = JsonUtility.FromJson<YuanqiResponse>(request.downloadHandler.text);
                if (response != null && response.choices != null && response.choices.Count > 0)
                {
                    var assistantReply = response.choices[0].message.content;
                    result = assistantReply;
                    callback?.Invoke(assistantReply);
                }
                else
                {
                    errorCallback?.Invoke("Empty response.");
                }
            }
            catch (Exception e)
            {
                errorCallback?.Invoke($"Parse error: {e.Message}");
            }
        }
    }

    // ========== 新版：流式读取（SSE / 分片） ==========
    private IEnumerator CoSendStream(APIRequest requestData, Action<string> onFinished, Action<string> onError)
    {
        // 1) 构造请求（流式）
        requestData.stream = true;
        string jsonData = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Source", "openapi");
            request.SetRequestHeader("Authorization", $"Bearer {apiToken}");
            // SSE 常用
            request.SetRequestHeader("Accept", "text/event-stream");

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            var op = request.SendWebRequest();

            float startT = Time.realtimeSinceStartup;
            var sb = new StringBuilder();
            int lastLen = 0; // 已处理到的 text 长度

            while (!op.isDone)
            {
                // 超时控制
                if (Time.realtimeSinceStartup - startT > requestTimeout)
                {
                    request.Abort();
                    break;
                }

                // 2) 增量解析（SSE/分块普适写法）
                string all = request.downloadHandler.text;
                if (!string.IsNullOrEmpty(all) && all.Length > lastLen)
                {
                    string delta = all.Substring(lastLen);
                    lastLen = all.Length;

                    // SSE 一般以空行分包：data: {...}\n\n
                    // 为了兼容性，这里按双换行拆，逐包取 content 片段
                    var packets = delta.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var packet in packets)
                    {
                        var line = packet.Trim();

                        // SSE 行通常以 "data:" 开头；不是 data 行就略过
                        if (!line.StartsWith("data:")) continue;

                        var data = line.Substring(5).Trim(); // 去掉 "data:"

                        // 结束标记（部分服务会发 [DONE]）
                        if (data == "[DONE]" || data == "\"[DONE]\"")
                        {
                            onFinished?.Invoke(sb.ToString());
                            yield break;
                        }
                        
                        string snapshot = ExtractSnapshot(data);
                        if (!string.IsNullOrEmpty(snapshot))
                        {
                            // 将“快照”转为“真正增量”
                            string deltaToAppend = snapshot;
                            var sofar = sb.ToString();
                            if (snapshot.StartsWith(sofar))            // <-- 关键：去掉已输出的前缀
                                deltaToAppend = snapshot.Substring(sofar.Length);

                            if (deltaToAppend.Length > 0)
                            {
                                sb.Append(deltaToAppend);
                                OnStreamDelta?.Invoke(deltaToAppend);  // UI 只收到纯增量
                            }
                        }
                    }
                }

                yield return null; // 下一帧继续
            }

            // 3) 结束：成功/失败处理
            if (request.result == UnityWebRequest.Result.Success)
            {
                // 若后端没发 [DONE]，直接以累计文本作为最终
                onFinished?.Invoke(sb.ToString());
            }
            else
            {
                onError?.Invoke($"Request failed: {request.error}");
            }
        }
    }

    /// <summary>
    /// 从流式 data JSON 中尽量稳妥地抽取“增量内容”
    /// 说明：不同厂商字段可能是 choices[0].delta.content / choices[0].message.content / choices[0].delta 等
    /// 这里用一个较通用的查找：“找第一个 content 字段的字符串值”
    /// 如需精确对齐你的返回结构，可把本方法替换为强类型 JSON 解析。
    /// </summary>
    // 取“当前快照”——兼容多种流格式：delta / steps / message.content
    string ExtractSnapshot(string jsonLine)
    {
        try
        {
            // 1) 先粗查 delta.content
            int di = jsonLine.IndexOf("\"delta\"", StringComparison.OrdinalIgnoreCase);
            if (di >= 0)
            {
                string s = ExtractFirstStringAfter(jsonLine, "\"content\"");
                if (!string.IsNullOrEmpty(s)) return s;
            }

            // 2) 再查 steps[last].content
            int si = jsonLine.IndexOf("\"steps\"", StringComparison.OrdinalIgnoreCase);
            if (si >= 0)
            {
                // 找最后一个 "content":"..."
                int last = jsonLine.LastIndexOf("\"content\"", StringComparison.OrdinalIgnoreCase);
                if (last > si)
                {
                    string s = ExtractFirstStringAfter(jsonLine.Substring(last), "\"content\"");
                    if (!string.IsNullOrEmpty(s)) return s;
                }
            }

            // 3) 最后兜底：message.content
            int mi = jsonLine.IndexOf("\"message\"", StringComparison.OrdinalIgnoreCase);
            if (mi >= 0)
            {
                string s = ExtractFirstStringAfter(jsonLine.Substring(mi), "\"content\"");
                if (!string.IsNullOrEmpty(s)) return s;
            }
        }
        catch { /* 忽略，走不到这里 */ }

        // 退化：直接搜第一个 "content":"..."
        return ExtractFirstStringAfter(jsonLine, "\"content\"");
    }

// 从 json 文本里，在 key 之后提取第一个 string 值，容错转义
    string ExtractFirstStringAfter(string json, string key)
    {
        int i = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        int q1 = json.IndexOf('"', i + key.Length);
        if (q1 < 0) return null;

        var sbLocal = new System.Text.StringBuilder();
        bool esc = false;
        for (int j = q1 + 1; j < json.Length; j++)
        {
            char c = json[j];
            if (esc) { sbLocal.Append(c); esc = false; continue; }
            if (c == '\\') { esc = true; continue; }
            if (c == '"') break;
            sbLocal.Append(c);
        }
        return sbLocal.ToString();
    }

}

#region Response Models (保持原样)
[System.Serializable]
public class YuanqiResponse
{
    public string id;
    public string created;
    public List<Choice> choices = new List<Choice>();
    public Usage usage;
}

[System.Serializable]
public class Choice
{
    public int index;
    public string finish_reason;
    public MessageData message;
}

[System.Serializable]
public class MessageData
{
    public string role;
    public string content;
    public List<Step> steps;
}

[System.Serializable]
public class Step
{
    public string role;
    public string content;
}

[System.Serializable]
public class Usage
{
    public int prompt_tokens;
    public int completion_tokens;
    public int total_tokens;
}
#endregion
