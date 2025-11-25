// AccelDebugHUD.cs
using UnityEngine;
using TMPro; // 如果用 TextMeshPro，把这两行改成 using TMPro; 并把 Text 换成 TMP_Text

public class AccelDebugHUD : MonoBehaviour
{
    public ForwardSwingDetector detector;
    public TMP_Text label; // 或 TMP_Text label;

    private void Awake()
    {
        if (detector == null) detector = FindObjectOfType<ForwardSwingDetector>();
    }

    private void OnEnable()
    {
        if (detector != null)
            detector.OnDebugInfo += UpdateText;
    }

    private void OnDisable()
    {
        if (detector != null)
            detector.OnDebugInfo -= UpdateText;
    }

    private void UpdateText(string s)
    {
        if (label) label.text = s;
    }
}