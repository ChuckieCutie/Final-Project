using UnityEngine;

public class TestNpcInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string interactMessage = "NPC test: da bam E";

    public void Interact()
    {
        Debug.Log(interactMessage, this);
    }
}