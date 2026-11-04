using UnityEngine;

public class HappinessCollect : MonoBehaviour
{
    private Evergreen.Systems.ResourceManager resourceManager;

    void Start()
    {
        resourceManager = Evergreen.Systems.ResourceManager.Instance;
    }

    public void Printing()
    {
        //Debug.Log("Total Happiness: " + resourceManager.Happiness); // prints out the happiness value from resource manager
        print("Happiness Collected"); // prints out to see if buttons works
        //resourceManager.GainHappiness(1f); // adds happiness when button is pressed
        //Debug.Log("Total Happiness: " + resourceManager.Happiness); // prints out the happiness value from resource manager

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
