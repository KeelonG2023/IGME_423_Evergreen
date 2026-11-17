using System;
using System.Collections.Generic;
using Evergreen.Systems;
using Evergreen.UI;
using UnityEngine;
using UnityEngine.UI;

public class StatusMenu : MonoBehaviour
{
    [Header("Menu Objects")]
    [SerializeField] GameObject statusMenu;
    [SerializeField] Text titleText;
    [SerializeField] Text incomeRateText;
    [SerializeField] Text ecoRateText;
    [SerializeField] Text happinessRateText; 
    [SerializeField] Text popRateText;
    [SerializeField] Text incomeTotalText;
    [SerializeField] Text ecoTotalText;
    [SerializeField] Text happinessTotalText;
    [SerializeField] Text popTotalText;
    [SerializeField] Text ageText;


    [Header("Misc.")]
    [SerializeField] BuildingClickSelector selector;
    [SerializeField] GameObject alertObjectReference;
    List<StatusAlert> statusAlerts = new List<StatusAlert>();

    BuildingYield selected;
    private void Start()
    {
        if (statusMenu == null)
        {
            Debug.LogWarning("no statusMenu gameObject detected, disableing StatusMenu script");
            this.enabled = false;
        }
    }


    void OnEnable()
    {
        if (selector != null) selector.SelectionChanged += OnSelectionChanged;

        ResourceManager.Instance.ResourcesChanged += TryUpdateStatusMenu;
    }

    void OnDisable()
    {
        if (selector != null) selector.SelectionChanged -= OnSelectionChanged;
    }
    void OnSelectionChanged(BuildingYield building)
    {  
        if (building != null) OpenStatusMenu(building);
        else CloseStatusMenu();
    }

    void TryUpdateStatusMenu(ResourceManager manager)
    {
        if (selected != null)
        {
            UpdateStatusMenu(selected);
        }
    }
    void OpenStatusMenu(BuildingYield building)
    {
        selected = building;
        if (statusMenu != null)
        {
            UpdateStatusMenu(building);

            foreach (AlertInfo alert in building.statusAlerts)
            {
                GameObject newObject = Instantiate(alertObjectReference, alertObjectReference.transform.parent);

                StatusAlert statusAlert = newObject.GetComponent<StatusAlert>();

                statusAlert.text.text = alert.text;
                statusAlert.title.text = alert.title;
                newObject.SetActive(true);
            }
        }


    }

    void UpdateStatusMenu(BuildingYield building)
    {
        statusMenu.SetActive(true);
        titleText.text = building.DisplayName;

        UpdateText(incomeRateText, $"{(building.YearlyYield.money < 0 ? "-" : "+")}{Math.Abs(building.YearlyYield.money)}");
        UpdateText(ecoRateText, $"{(building.YearlyYield.eco < 0 ? "-" : "+")}{Math.Abs(building.YearlyYield.eco)}");
        UpdateText(happinessRateText, $"{(building.YearlyYield.happiness < 0 ? "-" : "+")}{Math.Abs(building.YearlyYield.happiness)}");
        UpdateText(popRateText, $"{(building.YearlyYield.population < 0 ? "-" : "+")}{Math.Abs(building.YearlyYield.population)}");

        UpdateText(incomeTotalText, $"{Math.Round(building.TotalYield.money, 1)}");
        UpdateText(ecoTotalText, $"{Math.Round(building.TotalYield.eco, 1)}");
        UpdateText(popTotalText, $"{Math.Round(building.TotalYield.population, 1)}");
        UpdateText(happinessTotalText, $"{Math.Round(building.TotalYield.happiness, 1)}");

        UpdateText(ageText, $"{Math.Round(building.LifespanYears - building.YearsRemaining, 1)}");
    }

    void CloseStatusMenu()
    {
        selected =null;
        if (statusMenu != null)
        {
            statusMenu.SetActive(false);

            Transform parent = alertObjectReference.transform.parent;

            foreach (Transform child in parent)
            {
                if (child.gameObject != null && child.gameObject != alertObjectReference)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }


    //------------HELPER FUNCTIONS-----------

    void UpdateText(Text textBox, string text)
    {
        if (textBox != null)
        {
            textBox.text = text;
        }
    }
}

public class AlertInfo
{
    public string text;
    public string title;

    public AlertInfo(string title, string text) 
    { 
        this.text = text;
        this.title = title;
    
    }
}
