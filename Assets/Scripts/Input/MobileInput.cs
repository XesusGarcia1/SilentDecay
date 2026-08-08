using UnityEngine;

public static class MobileInput
{
    public static bool ePressedDown = false; // Solo para GetKeyDown (Taps de interacción)
    public static bool ePressed = false;     // Para GetKey (Mantener presionado para escapes)
    public static bool fPressedDown = false; // Solo para GetKeyDown (Taps de linterna)
    public static bool fPressed = false;

    public static int lastFrameEPressed = -1;
    public static int lastFrameFPressed = -1;

    public static bool GetKeyDown(KeyCode key)
    {
        if (key == KeyCode.E && ePressedDown)
        {
            if (Time.frameCount > lastFrameEPressed + 1) 
            { 
                ePressedDown = false; // Expirado después del siguiente frame
                return false; 
            }
            return true; // No lo consumimos aquí, para que otros scripts en el mismo frame puedan leerlo
        }
        if (key == KeyCode.F && fPressedDown)
        {
            if (Time.frameCount > lastFrameFPressed + 1) 
            { 
                fPressedDown = false; 
                return false; 
            }
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
