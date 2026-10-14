using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class SplashTouchTarget : MonoBehaviour
{
    public SplashWorkshopController controller;

    void Awake()
    {
        if (!controller)
            controller = FindFirstObjectByType<SplashWorkshopController>();
    }

    void OnMouseEnter()
    {
        if (controller)
            controller.SetTouchHover(true);
    }

    void OnMouseExit()
    {
        if (controller)
            controller.SetTouchHover(false);
    }

    void OnMouseDown()
    {
        if (controller)
            controller.ActivateWorkshop();
    }

    void OnTriggerEnter(Collider other)
    {
        if (controller && (other.CompareTag("Player") || other.name.Contains("Hand") || other.name.Contains("Controller")))
            controller.ActivateWorkshop();
    }
}
