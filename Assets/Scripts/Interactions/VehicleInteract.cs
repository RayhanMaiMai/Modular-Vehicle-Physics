using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(BoxCollider))]
public class VehicleInteract : MonoBehaviour
{
    public CarController carController;
    public Transform getOutPosition; // Titik Child lokal untuk spawn (Menjawab EC-02)

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }
}