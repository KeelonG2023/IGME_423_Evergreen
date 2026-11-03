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
        
        print("Happiness Collected");
        //resourceManager.GainHappiness(1f);
        //Debug.Log("Total Happiness: " + resourceManager.Happiness);

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
