using Evergreen.Systems;
using Evergreen.UI;
using UnityEngine;
using UnityEngine.UI;

public class AuditScreen : MonoBehaviour
{
    public SimulationClock clock;
    public ResourceManager manager;
    //public GameObject panel;

    private bool enable = false;
    private bool moneyFail;
    private bool ecoFail;
    private int numOfChances = 2;
    private int numOfAudit = 0;

    // lines 13 to 28 is from GameOverScreen script
    [Tooltip("The panel that gets shown. Keep this component on an always-active object outside it.")]
    [SerializeField] GameObject root;

    [Header("Content")]
    [SerializeField] Text titleText;
    [SerializeField] Text subtitleText;
    [SerializeField] Text passOrFail;
    [SerializeField] Text chancesRemaining;
    [SerializeField] GameOverMeterRow moneyRow;
    [SerializeField] GameOverMeterRow ecoRow;

    [Header("Bar scale (value that fills a bar)")]
    [SerializeField] float moneyFullAt = 10000f;
    [SerializeField] float ecoFullAt = 100f;

    [Header("Buttons")]
    [SerializeField] Button closeScreen;

    void Start()
    {
        hidePanel();

        if(closeScreen != null)
        {
            closeScreen.gameObject.SetActive(true);
            closeScreen.interactable = true;
            closeScreen.onClick.AddListener(ClosedAuditScreen);
        }

        if(root != null)
        {
            root.SetActive(true);
        }

    }

    void Awake()
    {
        if (clock == null) clock = FindFirstObjectByType<SimulationClock>();
        if (manager == null) manager = FindFirstObjectByType<ResourceManager>();
        if (root == null) root = gameObject;
    }

    public void ClosedAuditScreen()
    {
        //Debug.Log("Hiding panel"); // For debugging
        hidePanel();
        clock.Resume();
    }

    public void hidePanel()
    {
        //panel.GetComponent<CanvasGroup>().alpha = 0f;
        //panel.GetComponent<CanvasGroup>().interactable = false;
    }

    public void showPanel()
    {
        //panel.GetComponent<CanvasGroup>().alpha = 1f;
        //panel.GetComponent<CanvasGroup>().interactable = true;
    }

    public void Audited()
    {
        //Debug.Log("Auditted method is triggered");
        enable = false;
        
        showPanel();

        subtitleText.text = "This is audit " + numOfAudit;
        chancesRemaining.text = "Chances remaining " + numOfChances;

        if (manager.Money >= 200 && manager.Eco >= 0)
        {
            passOrFail.text = "You passed this audit";
            moneyFail = false;
            ecoFail = false;
            FillMeterRows(moneyFail, ecoFail);
        }
        else
        {
            passOrFail.text = "You failed this audit";

            numOfChances--;
            moneyFail = true;
            ecoFail = true;

            FillMeterRows(moneyFail, ecoFail);
        }
    }

    void FillMeterRows(bool moneyFail, bool ecoFail)
    { 
        if (manager == null) return;

        if (moneyRow != null)
            moneyRow.Set($"${Mathf.FloorToInt(manager.Money):N0}", manager.Money / moneyFullAt, moneyFail);
        
        if (ecoRow != null)
            ecoRow.Set($"{Mathf.FloorToInt(manager.Eco):N0}", manager.Eco / ecoFullAt, ecoFail);
    }

    void Update()
    {
        if (clock.CurrentYear == 4 || clock.CurrentYear == 9 || clock.CurrentYear == 14)
        {
            enable = true;
        }

        if (enable == true && (clock.CurrentYear == 5 || clock.CurrentYear == 10 || clock.CurrentYear == 15) )
        {
            Debug.Log("Passed first check");
            clock.Pause();
            Audited();
        }
    }
}
