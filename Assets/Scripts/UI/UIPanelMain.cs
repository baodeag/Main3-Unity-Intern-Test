using UnityEngine;
using UnityEngine.UI;

public class UIPanelMain : MonoBehaviour, IMenu
{
    [SerializeField] private Button btnMoves;
    [SerializeField] private Button btnAutoplay;
    [SerializeField] private Button btnAutoLose;
    [SerializeField] private Button btnTimeAttack;

    private UIMainManager m_mngr;

    private void Awake()
    {
        btnMoves.onClick.AddListener(OnClickPlay);
        btnAutoplay.onClick.AddListener(OnClickAutoplay);
        btnAutoLose.onClick.AddListener(OnClickAutoLose);
        btnTimeAttack.onClick.AddListener(OnClickTimeAttack);
    }

    private void OnDestroy()
    {
        if (btnMoves) btnMoves.onClick.RemoveAllListeners();
        if (btnAutoplay) btnAutoplay.onClick.RemoveAllListeners();
        if (btnAutoLose) btnAutoLose.onClick.RemoveAllListeners();
        if (btnTimeAttack) btnTimeAttack.onClick.RemoveAllListeners();
    }

    public void Setup(UIMainManager mngr)
    {
        m_mngr = mngr;
    }

    private void OnClickPlay()
    {
        m_mngr.LoadLevelMoves();
    }

    private void OnClickAutoplay()
    {
        m_mngr.LoadLevelAutoplay();
    }

    private void OnClickAutoLose()
    {
        m_mngr.LoadLevelAutoLose();
    }

    private void OnClickTimeAttack()
    {
        m_mngr.LoadLevelTimer();
    }

    public void Show()
    {
        this.gameObject.SetActive(true);
    }

    public void Hide()
    {
        this.gameObject.SetActive(false);
    }
}
