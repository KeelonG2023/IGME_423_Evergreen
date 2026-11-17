using Evergreen.Systems;
using UnityEngine;
using UnityEngine.UI;

public class AuditWarningScreen : MonoBehaviour
{
    public SimulationClock clock;
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
        
    }

    public void AuditWarning()
    {

    }

    // Update is called once per frame
    void Update()
    {
        years = clock.YearsElapsed;
    }
}
