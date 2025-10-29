using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("0:首页 1:说书人 2:工坊 3:探索 4:时间轴（可空）")]
    [SerializeField] public GameObject[] panels;
    [SerializeField] public GameObject[] tabSelectedIcons; // 与 panels 对齐，可为空
    [SerializeField] public GameObject[] pageLogos;//每个page的页首
    int _current = 0;

    [SerializeField] private SwitchAnimatorControler _switch;

    void Awake()
    {
        ShowPanel(_current);
    }

    public void ShowPanel(int index)
    {
        
        if (index < 0 || index >= panels.Length) return;
        
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i])
            {
                panels[i].SetActive(i == index);
            }
        }
        _current = index;
        
        if (tabSelectedIcons != null && tabSelectedIcons.Length > 0)
        {
            for (int i = 0; i < tabSelectedIcons.Length; i++)
            {
                if (tabSelectedIcons[i]) tabSelectedIcons[i].SetActive(i == index);
            }
        }

        if (pageLogos != null && pageLogos.Length > 0)
        {
            for (int i = 0; i < pageLogos.Length; i++)
            {
                if (pageLogos[i]) pageLogos[i].SetActive(i == index);
            }
                
        }
            
    }
}