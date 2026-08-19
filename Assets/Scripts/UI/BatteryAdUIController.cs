using UnityEngine;
using UnityEngine.UI;
using SilentDecay.Core;

namespace SilentDecay.UI
{
    /// <summary>
    /// Controlador de UI independiente para mostrar la opción de "Recargar Linterna con Anuncio" cuando la batería está baja o agotada.
    /// </summary>
    public class BatteryAdUIController : MonoBehaviour
    {
        [Header("Referencias de UI")]
        [Tooltip("Botón flotante o en el HUD para ver anuncio y recargar la linterna")]
        public Button rechargeAdButton;

        [Tooltip("Porcentaje de batería (0.0 a 1.0) por debajo del cual aparece el botón de anuncio (ej: 0.20 = 20%)")]
        [Range(0.05f, 0.50f)]
        public float batteryThresholdToShow = 0.20f;

        [Header("Referencias de Jugador")]
        public FlashlightController flashlightController;

        private void Start()
        {
            if (flashlightController == null)
            {
                flashlightController = FindObjectOfType<FlashlightController>();
            }

            if (rechargeAdButton != null)
            {
                rechargeAdButton.onClick.RemoveAllListeners();
                rechargeAdButton.onClick.AddListener(OnRechargeButtonPressed);
            }
        }

        private void Update()
        {
            UpdateUIState();
        }

        private void UpdateUIState()
        {
            if (rechargeAdButton == null) return;

            if (AdManager.Instance == null || !AdManager.Instance.enableAds)
            {
                rechargeAdButton.gameObject.SetActive(false);
                return;
            }

            if (flashlightController == null)
            {
                flashlightController = FindObjectOfType<FlashlightController>();
                if (flashlightController == null)
                {
                    rechargeAdButton.gameObject.SetActive(false);
                    return;
                }
            }

            float currentRatio = flashlightController.currentBattery / flashlightController.maxBattery;
            bool isLowBattery = currentRatio <= batteryThresholdToShow;
            bool isAdReady = AdManager.Instance.IsBatteryAdReady();

            // Mostrar el botón solo si la batería está baja y el anuncio está disponible
            rechargeAdButton.gameObject.SetActive(isLowBattery && isAdReady);
        }

        private void OnRechargeButtonPressed()
        {
            if (AdManager.Instance == null) return;

            AdManager.Instance.ShowRewardedBattery(
                onRewardEarned: () =>
                {
                    Debug.Log("[BatteryAdUIController] ¡Recompensa otorgada! Recargando batería al 100%...");
                    if (flashlightController != null)
                    {
                        flashlightController.RechargeBatteryFull();
                    }
                },
                onAdClosedOrFailed: () =>
                {
                    Debug.LogWarning("[BatteryAdUIController] Anuncio no completado.");
                }
            );
        }

        private void OnGUI()
        {
            // Si hay un botón asignado en UGUI, ese botón maneja la UI
            if (rechargeAdButton != null) return;

            if (AdManager.Instance == null || !AdManager.Instance.enableAds) return;

            if (flashlightController == null)
            {
                flashlightController = FindObjectOfType<FlashlightController>();
                if (flashlightController == null) return;
            }

            float currentRatio = flashlightController.currentBattery / flashlightController.maxBattery;
            bool isLowBattery = currentRatio <= batteryThresholdToShow;
            bool isAdReady = AdManager.Instance.IsBatteryAdReady();

            // Dibujar botón en la esquina superior derecha si la batería está baja
            if (isLowBattery && isAdReady)
            {
                GUIStyle batButtonStyle = new GUIStyle(GUI.skin.button);
                batButtonStyle.fontSize = 16;
                batButtonStyle.fontStyle = FontStyle.Bold;
                batButtonStyle.normal.textColor = new Color(1f, 0.9f, 0.2f);
                batButtonStyle.alignment = TextAnchor.MiddleCenter;

                float btnW = 200f;
                float btnH = 45f;
                Rect rect = new Rect(Screen.width - btnW - 20, 20, btnW, btnH);

                if (GUI.Button(rect, "🔋 RECARGAR (ANUNCIO)", batButtonStyle))
                {
                    OnRechargeButtonPressed();
                }
            }
        }
    }
}
