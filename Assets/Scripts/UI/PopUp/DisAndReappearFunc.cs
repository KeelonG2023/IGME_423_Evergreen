using System.Collections;
using System.Resources;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class DisAndReappearFunc : MonoBehaviour
{
    public Evergreen.Systems.ResourceManager resourceManager;
    public Button happinessButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        resourceManager = Evergreen.Systems.ResourceManager.Instance;
        happinessButton.onClick.AddListener(hideButton);
        happinessButton.onClick.AddListener(showButton);
        happinessButton.gameObject.SetActive(false);
        spawnRate();
    }

    public void spawnRate()
    {
        Debug.Log("Working");
        float curHappiness = resourceManager.Happiness;
        
        // Checks Happiness property from resourceManage then generates a random # from 0 to 1.0 to see if it less than set #
        // If both conditions meet then button appears
        if (curHappiness <= 10f)
        {
            if(Random.value < 0.5f)
            {
                showButton();
                print("Button Appears from a 5% chance");
            }
        }
        else if (curHappiness <= 20f)
        {
            if (Random.value < 0.1f)
            {
                showButton();
                print("Button Appears from a 10% chance");
            }
        }
        else if (curHappiness <= 30f)
        {
            if (Random.value < 0.15f)
            {
                showButton();
                print("Button Appears from a 15% chance");
            }
        }
        else if (curHappiness <= 40f)
        {
            if (Random.value < 0.20f)
            {
                showButton();
                print("Button Appears from a 20% chance");
            }
        }
        else if (curHappiness <= 50f)
        {
            if (Random.value < 0.25f)
            {
                showButton();
                print("Button Appears from a 25% chance");
            }
        }
        else if (curHappiness <= 60f)
        {
            if (Random.value < 0.30f)
            {
                showButton();
                print("Button Appears from a 30% chance");
            }
        }
        else if (curHappiness <= 70f)
        {
            if (Random.value < 0.35f)
            {
                showButton();
                print("Button Appears from a 35% chance");
            }
        }
        else if (curHappiness <= 80f)
        {
            if (Random.value < 0.40f)
            {
                showButton();
                print("Button Appears from a 40% chance");
            }
        }
        else if (curHappiness <= 90f)
        {
            if (Random.value < 0.45f)
            {
                showButton();
                print("Button Appears from a 45% chance");
            }
        }
        else if (curHappiness <= 100f)
        {
            if (Random.value < 0.50f)
            {
                showButton();
                print("Button Appears from a 5% chance");
            }
        }



    }

    // Makes button disappear
    private void hideButton()
    {
        happinessButton.gameObject.SetActive(false);
    }

    // Makes button appear
    private void showButton()
    {
        happinessButton.gameObject.SetActive(true);
    }


    // Update is called once per frame
    void Update()
    {
        spawnRate();
        //Debug.Log("Working");
        //print("Working");

        //if (happinessButton != null)
        //{
        //    happinessButton.gameObject.SetActive(false);
        //    print("false");
        //}
    }
}
