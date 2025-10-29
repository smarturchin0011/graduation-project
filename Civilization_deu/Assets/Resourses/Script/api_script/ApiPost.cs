using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ApiPost : MonoBehaviour
{
    private string Url;
    [SerializeField] private string message;
    [SerializeField] private string content;

    [SerializeField] [Range(0,100)] private int MaxHistorayNum;
    private TencentApiManager TencentApiManager;
    [SerializeField] ChatComposer _chatComposer;
    [SerializeField] public Action<string> OnUIUpdate;
    
    

    private void Start()
    {
        content = "";
        
        TencentApiManager = GetComponent<TencentApiManager>();
        _chatComposer = GetComponent<ChatComposer>();
        TencentApiManager.SetHistoryLimit(MaxHistorayNum);

        _chatComposer.onUserMessage.AddListener(ClickOnPostText);

    }

    public void ClickOnPostText(string a)
    {
        message = a;
        TencentApiManager.SendTextMessage(message,
            response =>
            {
                UpdateUI(response);
                Debug.Log("收到回复：" + response);
            },
                

            error => Debug.LogError("错误：" + error));
        
    }

    public void UpdateUI(string resualt)
    {
        content = resualt;
        OnUIUpdate?.Invoke(content);
        
    }
    
}
