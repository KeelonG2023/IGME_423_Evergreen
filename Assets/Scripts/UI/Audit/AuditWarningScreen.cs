using Evergreen.Systems;
using UnityEngine;
using UnityEngine.UI;

public class AuditWarningScreen : MonoBehaviour
{
    public SimulationClock clock;
    private bool enable;

    [Tooltip("The panel that gets shown. Keep this component on an always-active object outside it.")]
    [SerializeField] GameObject root;

    [Header("Content")]
    [SerializeField] Text titleText;
    [SerializeField] Text subtitleText;

    [Header("Buttons")]
    [SerializeField] Button closeScreen;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (closeScreen != null)
        {
            closeScreen.gameObject.SetActive(true);
            closeScreen.interactable = true;
            closeScreen.onClick.AddListener(CloseScreen);
        }

        if (root != null)
        {
            root.SetActive(true);
        }

        hidePanel();
    }

    void Awake()
    {
        if (clock == null) clock = FindFirstObjectByType<SimulationClock>();
        if (root == null) root = gameObject;
    }

    public void CloseScreen()
    {
        //Debug.Log("Hiding panel"); // For debugging
        hidePanel();
        clock.Resume(); // Resumes the clock
    }

    public void warning()
    {
        enable = false;
        subtitleText.text = "The auidt is happpening in 3 years";
    }

    public void hidePanel()
    {

    }

    public void showPanel()
    {

    }


    // Update is called once per frame
    void Update()
    {
        if(clock.CurrentYear == 1 || clock.CurrentYear == 6 || clock.CurrentYear == 11)
        {
            enable = true;
        }

        if(enable == true && (clock.CurrentYear == 2 || clock.CurrentYear == 7 || clock.CurrentYear == 12) )
        {
            clock.Pause(); // pauses the clock
            showPanel();
        }
    }
}
