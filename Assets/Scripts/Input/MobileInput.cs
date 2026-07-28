using UnityEngine;

public static class MobileInput
{
    public static bool ePressedDown = false; // Solo para GetKeyDown (Taps de interacción)
    public static bool ePressed = false;     // Para GetKey (Mantener presionado para escapes)
    public static bool fPressedDown = false; // Solo para GetKeyDown (Taps de linterna)
    public static bool fPressed = false;

    public static bool GetKeyDown(KeyCode key)
    {
        if (key == KeyCode.E && ePressedDown)
        {
            return true;
        }
        if (key == KeyCode.F && fPressedDown)
        {
            return true;
        }
        return Input.GetKeyDown(key);
    }

    public static bool GetKey(KeyCode key)
    {
        if (key == KeyCode.E && ePressed)
        {
            return true;
        }
        if (key == KeyCode.F && fPressed)
        {
            return true;
        }
        return Input.GetKey(key);
    }

    public static void SetCursorState(bool locked)
    {
        #if UNITY_ANDROID || UNITY_IOS
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        #else
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
        #endif
    }
}
