using UnityEngine;
using UnityEngine.InputSystem;

public class CursorLock : MonoBehaviour
{
    void Start() => Confine();

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (Cursor.lockState == CursorLockMode.Confined) Release();
            else Confine();
        }

        // click back in to re-confine after releasing
        if (Cursor.lockState == CursorLockMode.None
            && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Confine();
    }

    void Confine()
    {
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    }

    void Release()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}