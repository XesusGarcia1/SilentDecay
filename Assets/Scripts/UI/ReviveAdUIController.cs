using UnityEngine;
using UnityEngine.UI;
using SilentDecay.Core;

namespace SilentDecay.UI
{
    /// <summary>
    /// Controlador de UI independiente para manejar el botón "Revivir con Anuncio" en la pantalla de Muerte.
    /// </summary>
    public class ReviveAdUIController : MonoBehaviour
    {
        [Header("Referencias de UI")]
        [Tooltip("Botón de la interfaz de muerte para ver el anuncio y revivir")]
        public Button reviveAdButton;

        [Tooltip("GameObject opcional (ej: icono de cargando) si el anuncio aún no se ha descargado")]
        public GameObject loadingIndicator;

        [Header("Referencias de Jugador")]
        public PlayerHealth playerHealth;

        private void OnEnable()
        {
            if (reviveAdButton != null)
            {
                reviveAdButton.onClick.RemoveAllListeners();
                reviveAdButton.onClick.AddListener(OnReviveButtonPressed);
            }

            UpdateUIState();
        }

        private void Update()
        {
            UpdateUIState();
        }

        private void UpdateUIState()
        {
            if (AdManager.Instance == null || !AdManager.Instance.enableAds)
            {
                if (reviveAdButton != null) reviveAdButton.gameObject.SetActive(false);
                if (loadingIndicator != null) loadingIndicator.SetActive(false);
                return;
            }

            bool isReady = AdManager.Instance.IsReviveAdReady();

            if (reviveAdButton != null)
            {
                reviveAdButton.gameObject.SetActive(true);
                reviveAdButton.interactable = isReady;
            }

            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(!isReady);
            }
        }

        private void OnReviveButtonPressed()
        {
            if (AdManager.Instance == null) return;

            AdManager.Instance.ShowRewardedRevive(
                onRewardEarned: () =>
                {
                    Debug.Log("[ReviveAdUIController] ¡Recompensa otorgada! Reviviendo al jugador...");
                    if (playerHealth != null)
                    {
                        playerHealth.ReviveFromAd();
                    }
                    else
                    {
                        var player = GameObject.FindObjectOfType<PlayerHealth>();
                        if (player != null) player.ReviveFromAd();
                    }

                    gameObject.SetActive(false); // Ocultar pantalla de muerte
                },
                onAdClosedOrFailed: () =>
                {
                    Debug.LogWarning("[ReviveAdUIController] No se completó el anuncio.");
                }
            );
        }
    }
}
