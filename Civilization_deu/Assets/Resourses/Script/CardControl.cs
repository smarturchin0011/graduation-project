using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardControl : MonoBehaviour
{
    [SerializeField] TMP_Text title;
    [SerializeField] Button button;
    [SerializeField] UIManager ui;
    [SerializeField] int targetIndex;

    public void Bind(string t, UIManager manager, int idx)
    {
        title.text = t;
        ui = manager; targetIndex = idx;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ui.ShowPanel(targetIndex));
    }

    void Reset() { button = GetComponent<Button>(); }
}