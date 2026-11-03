using Evergreen.UI;
using UnityEngine;

public class OpenSkillTree : MonoBehaviour
{
    [SerializeField] private GameObject treeMenu;
    [SerializeField] private GameObject treeButton;
    [SerializeField] private GameObject buildMenu;

    private void Start()
    {
        treeMenu.SetActive(false);
    }

    public void OpenTree()
    {
        buildMenu.SetActive(false);
        treeButton.SetActive(false);
        treeMenu.SetActive(true);
    }

    public void CloseTree()
    {
        buildMenu.SetActive(true);
        treeButton.SetActive(true);
        treeMenu.SetActive(false);
    }
}
