using System.Collections;
using System.Resources;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class DisAndReappearFunc : MonoBehaviour
{
    // Declaring vars
    private Evergreen.Systems.ResourceManager resourceManager;
    public Button happinessButton;
    public GameObject targetObject;
    public float delayBySeconds;
    private float timer = 0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        resourceManager = Evergreen.Systems.ResourceManager.Instance; // setting local var to resource manager being used in scene

        happinessButton.gameObject.SetActive(true); // Makes the class run
        hideButton(); // hides button in the background until it is "spawn in" with the spawn()
        happinessButton.onClick.AddListener(hideButton); // Adds method to the button

        //Debug.Log("Bt position: " + happinessButton.transform.position); // Prints out buttonPos for debugging
        
        // setting the position of the button instance based on the object it is assigned to
        if (happinessButton != null && targetObject != null)
        {
            Vector3 objPos = targetObject.transform.position; // getting objPos
            objPos.y = objPos.y * 1.25f; // increasing y value by 25%

            happinessButton.transform.position = objPos; // setting modify objPos to buttonPos

            //Debug.Log("Bt position: " + happinessButton.transform.position); // Prints out buttonPos for debugging
        }

        InvokeRepeating("spawn", 1f, delayBySeconds); // Calls spawn() based on delayBySeconds


        // Debugging for testing
        //Debug.Log("Total Happiness: " + resourceManager.Happiness); // Prints out total
        //spawnRate();
    }


    // Spawns in button instance based on happiness and a random var
    public void spawn()
    {
        //Debug.Log("Triggered"); // For debugging
        float curHappiness = resourceManager.Happiness; // gets the value of happiness and setting to a local float
        float rolling = Random.Range(0f, 100f); // rng var

        
        if (happinessButton.interactable != true && happinessButton.image.enabled != true) // Checks if button already exist for this instance
        {
            // Debugging
            //Debug.Log("Bypass for checkpoint"); // For debugging
            //Debug.Log("Total Happiness: " + resourceManager.Happiness); // For debugging

            // Checks Happiness property from resourceManage then generates a random # from 0 to 1.0 to see if it less than set #
            // If both conditions meet then button appears
            if (curHappiness <= 10f && curHappiness > 0f)
            {
                if (rolling < 5f)
                {
                    showButton();
                    
                    //print("Button Appears from a 5% chance"); // For debugging
                }
            }
            else if (curHappiness <= 20f && curHappiness > 10f)
            {
                if (rolling < 1f)
                {
                    showButton();
                    
                    //print("Button Appears from a 10% chance"); // For debugging
                }
            }
            else if (curHappiness <= 30f && curHappiness > 20f)
            {
                if (rolling < 15f)
                {
                    showButton();
                    //print("Button Appears from a 15% chance"); // For debugging
                }
            }
            else if (curHappiness <= 40f && curHappiness > 30f)
            {
                if (rolling < 20f)
                {
                    showButton();
                    //print("Button Appears from a 20% chance"); // For debugging
                }
            }
            else if (curHappiness <= 50f && curHappiness > 40f)
            {
                if (rolling < 25f)
                {
                    showButton();
                    //print("Button Appears from a 25% chance"); // For debugging
                }
            }
            else if (curHappiness <= 60f && curHappiness > 50f)
            {
                Debug.Log("60 to 69 happiness");
                if (rolling < 30f)
                {
                    showButton();
                    //print("Button Appears from a 30% chance"); // For debugging
                }
            }
            else if (curHappiness <= 70f && curHappiness > 60f)
            {
                if (rolling < 35f)
                {
                    showButton();
                    //print("Button Appears from a 35% chance"); // For debugging
                }
            }
            else if (curHappiness <= 80f && curHappiness > 70f)
            {
                if (rolling <40f)
                {
                    showButton();
                    //print("Button Appears from a 40% chance"); // For debugging
                }
            }
            else if (curHappiness <= 90f && curHappiness > 80f)
            {
                if (rolling < 45f)
                {
                    showButton();
                    //print("Button Appears from a 45% chance"); // For debugging
                }
            }
            else if (curHappiness <= 100f && curHappiness > 90f)
            {
                if (rolling < 50f)
                {
                    showButton();
                    //print("Button Appears from a 50% chance"); // For debugging
                }
            }
        }
        
    }

    // Makes button disappear
    private void hideButton()
    {
        //Debug.Log("Hiding button"); // For debugging
        happinessButton.interactable = false;
        happinessButton.image.enabled = false;
    }

    // Makes button appear
    private void showButton()
    {
        //Debug.Log("Showing button"); // For debugging
        happinessButton.interactable = true;
        happinessButton.image.enabled = true;
    }

    // Update is called once per frame
    void Update()
    {
        //Debug.Log("Bt position: " + happinessButton.transform.position); // Prints out buttonPos for debugging

        if (happinessButton != null && targetObject != null)
        {
            Vector3 objPos = targetObject.transform.position; // getting objPos
            objPos.y = objPos.y * 1.25f; // increasing y value by 25%

            happinessButton.transform.position = objPos; // setting modify objPos to buttonPos

            //Debug.Log("Bt position: " + happinessButton.transform.position); // Prints out buttonPos for debugging
        }

        if(happinessButton.interactable == true && happinessButton.image.enabled == true)
        {
            timer += Time.deltaTime;
            
            if (timer >= 30f) // 30f is condition whether if the conditions are met
            {
                hideButton();

                timer = 0f; // resets timer
            }
        }

        

        // Debugging
        //print("Working"); // For debugging 
        //if (happinessButton != null)
        //{
        //    happinessButton.gameObject.SetActive(false);
        //    print("false");
        //}

    }
}
