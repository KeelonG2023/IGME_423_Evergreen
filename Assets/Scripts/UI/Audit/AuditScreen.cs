using Evergreen.Systems;
using UnityEngine;
using UnityEngine.UI;

public class AuditScreen : MonoBehaviour
{
    public SimulationClock clock;
    public ResourceManager manager;
    private int years = 0;
    private int numOfWarnings = 0;

    [Tooltip("The panel that gets shown. Keep this component on an always-active object outside it.")]
    [SerializeField] GameObject root;

    [Header("Content")]
    [SerializeField] Text titleText;
    [SerializeField] Text subtitleText;

    [Header("Buttons")]
    [SerializeField] Button closeAuditScreen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(closeAuditScreen != null)
        {
            closeAuditScreen.gameObject.SetActive(true);
            closeAuditScreen.interactable = false;
            closeAuditScreen.onClick.AddListener(ClosedAuditScreen);
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
        //root
    }

    public void Auditted()
    {
        //if()
        //{ 

        //}
        //else
        //{

        //}
    }

    // Update is called once per frame
    void Update()
    {
        years = clock.YearsElapsed;
        if(years == 5)
        {
            Auditted();
        }
    }
}
