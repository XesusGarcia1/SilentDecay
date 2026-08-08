using UnityEngine;

namespace StarterAssets
{
    public class UICanvasControllerInput : MonoBehaviour
    {

        [Header("Output")]
        public StarterAssetsInputs starterAssetsInputs;

        private void Start()
        {
#if !UNITY_EDITOR
            // Ocultar los botones en pantalla si es una build final de PC/Consola
            if (!Application.isMobilePlatform)
            {
                gameObject.SetActive(false);
            }
#endif
            // Auto-conectar los botones Uso y Luz para evitar problemas de referencias en el Editor y builds móviles
            UIVirtualButton[] buttons = GetComponentsInChildren<UIVirtualButton>(true);
            foreach (var btn in buttons)
            {
                string lowerName = btn.gameObject.name.ToLower();
                if (lowerName.Contains("uso") || lowerName.Contains("interact") || lowerName.Contains("use") || lowerName.Contains("hand") || lowerName.Contains("mano") || lowerName.Contains("action") || lowerName.Contains("e_button") || lowerName.Contains("btn_e"))
                {
                    btn.buttonStateOutputEvent.RemoveListener(VirtualInteractInput);
                    btn.buttonStateOutputEvent.AddListener(VirtualInteractInput);
                    btn.buttonClickOutputEvent.RemoveListener(VirtualInteractClick);
                    btn.buttonClickOutputEvent.AddListener(VirtualInteractClick);
                    Debug.Log($"UICanvasControllerInput: Botón de INTERACCIÓN ('{btn.gameObject.name}') conectado con éxito.");
                }
                else if (lowerName.Contains("luz") || lowerName.Contains("flash") || lowerName.Contains("linterna") || lowerName.Contains("light") || lowerName.Contains("f_button"))
                {
                    btn.buttonStateOutputEvent.RemoveListener(VirtualFlashlightInput);
                    btn.buttonStateOutputEvent.AddListener(VirtualFlashlightInput);
                    btn.buttonClickOutputEvent.RemoveListener(VirtualFlashlightClick);
                    btn.buttonClickOutputEvent.AddListener(VirtualFlashlightClick);
                    Debug.Log($"UICanvasControllerInput: Botón de LINTERNA ('{btn.gameObject.name}') conectado con éxito.");
                }
            }
        }
        public void VirtualMoveInput(Vector2 virtualMoveDirection)
        {
            starterAssetsInputs.MoveInput(virtualMoveDirection);
        }

        public void VirtualLookInput(Vector2 virtualLookDirection)
        {
            starterAssetsInputs.LookInput(virtualLookDirection);
        }

        public void VirtualJumpInput(bool virtualJumpState)
        {
            starterAssetsInputs.JumpInput(virtualJumpState);
        }

        public void VirtualSprintInput(bool virtualSprintState)
        {
            starterAssetsInputs.SprintInput(virtualSprintState);
        }

        public void VirtualInteractInput(bool interactState)
        {
            MobileInput.ePressed = interactState;
            if (interactState) 
            {
                MobileInput.ePressedDown = true;
                MobileInput.lastFrameEPressed = Time.frameCount;
            }
        }

        public void VirtualInteractClick()
        {
            MobileInput.ePressedDown = true;
            MobileInput.lastFrameEPressed = Time.frameCount;
        }

        public void VirtualFlashlightInput(bool flashlightState)
        {
            MobileInput.fPressed = flashlightState;
            if (flashlightState) 
            {
                MobileInput.fPressedDown = true;
                MobileInput.lastFrameFPressed = Time.frameCount;
            }
        }

        public void VirtualFlashlightClick()
        {
            MobileInput.fPressedDown = true;
            MobileInput.lastFrameFPressed = Time.frameCount;
        }
        
    }

}
