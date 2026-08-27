using UnityEngine;
using UnityEngine.UI;
using SilentDecay.Core;

namespace SilentDecay.UI
{
    /// <summary>
    /// Controlador de UI independiente para mostrar la opción de "Recargar Linterna con Anuncio" cuando la batería está baja o agotada.
    /// Diseñado como una burbuja flotante colocada estratégicamente debajo del icono del cuaderno.
    /// </summary>
    public class BatteryAdUIController : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (FindObjectOfType<BatteryAdUIController>() == null)
            {
                GameObject go = new GameObject("[BatteryAdUIController]");
                go.AddComponent<BatteryAdUIController>();
                DontDestroyOnLoad(go);
            }
        }

        [Header("Referencias de UI")]
        [Tooltip("Botón flotante en uGUI si se desea usar Canvas (opcional)")]
        public Button rechargeAdButton;

        [Tooltip("Porcentaje de batería (0.0 a 1.0) por debajo del cual aparece la burbuja de anuncio (ej: 0.35 = 35%)")]
        [Range(0.05f, 0.50f)]
        public float batteryThresholdToShow = 0.35f;

        [Header("Referencias de Jugador")]
        public FlashlightController flashlightController;

        private Texture2D _bubbleBgTex;
        private Texture2D _bubbleBorderTex;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            CreateProceduralTextures();
        }

        private void CreateProceduralTextures()
        {
            int w = 128, h = 48, r = 24;
            _bubbleBgTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _bubbleBgTex.wrapMode = TextureWrapMode.Clamp;

            _bubbleBorderTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _bubbleBorderTex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float cx = (x < r) ? r : ((x > w - r) ? w - r : x);
                    float cy = (y < r) ? r : ((y > h - r) ? h - r : y);
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                    if (dist <= r)
                    {
                        float alphaBg = Mathf.Clamp01((r - dist) / 1.5f) * 0.78f;
                        _bubbleBgTex.SetPixel(x, y, new Color(0.05f, 0.05f, 0.08f, alphaBg));

                        // Borde exterior delgado (1.5px)
                        float alphaBorder = (dist >= r - 2.5f) ? Mathf.Clamp01((r - dist) / 1.2f) : 0f;
                        _bubbleBorderTex.SetPixel(x, y, new Color(1f, 0.85f, 0.2f, alphaBorder));
                    }
                    else
                    {
                        _bubbleBgTex.SetPixel(x, y, Color.clear);
                        _bubbleBorderTex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            _bubbleBgTex.Apply();
            _bubbleBorderTex.Apply();
        }

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
            // Si hay un botón asignado en UGUI Canvas, este script no dibuja la burbuja OnGUI
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

            if (!isLowBattery || !isAdReady) return;

            // Ocultar la burbuja si el jugador está leyendo notas o en menú de pausa/guía
            if (NotepadUIManager.Instance != null && NotepadUIManager.IsOpen) return;
            if (ElevatorController.isNotepadOpen) return;

            // --- CÁLCULO DE POSICIÓN ALINEADA DEBAJO DEL CUADERNO ---
            float hudScale = PlayerPrefs.GetFloat("HUDScale", 1.25f);
            Matrix4x4 oldHudMat = GUI.matrix;
            if (hudScale != 1.0f)
            {
                Vector2 pivot = new Vector2(Screen.width - 25, 25);
                GUIUtility.ScaleAroundPivot(new Vector2(hudScale, hudScale), pivot);
            }

            // Obtener la posición inferior exacta del icono del cuaderno desde NotepadUIManager
            float notepadBottom = NotepadUIManager.GetNotebookBottomY();
            float bubbleY = notepadBottom + 12f;

            // --- ANIMACIÓN DE BURBUJA FLOTANTE Y PULSACIÓN ---
            float floatOffset = Mathf.Sin(Time.time * 3.5f) * 3.5f; // Flotación suave vertical
            float pulseGlow = 0.6f + Mathf.PingPong(Time.time * 2.5f, 0.4f); // Pulsación de brillo dorado

            float bubbleWidth = 140f;
            float bubbleHeight = 44f;
            float bubbleX = Screen.width - 25f - bubbleWidth;

            Rect bubbleRect = new Rect(bubbleX, bubbleY + floatOffset, bubbleWidth, bubbleHeight);

            // 1. Dibujar Fondo Oscuro de Burbuja
            if (_bubbleBgTex != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(bubbleRect, _bubbleBgTex);
            }

            // 2. Dibujar Borde Dorado Pulsante
            if (_bubbleBorderTex != null)
            {
                GUI.color = new Color(1f, 0.85f, 0.2f, pulseGlow);
                GUI.DrawTexture(bubbleRect, _bubbleBorderTex);
                GUI.color = Color.white;
            }

            // 3. Estilo del Texto de la Burbuja Flotante
            GUIStyle bubbleTextStyle = new GUIStyle(GUI.skin.button);
            bubbleTextStyle.normal.background = null;
            bubbleTextStyle.hover.background = null;
            bubbleTextStyle.active.background = null;
            bubbleTextStyle.fontSize = 13;
            bubbleTextStyle.fontStyle = FontStyle.Bold;
            bubbleTextStyle.normal.textColor = new Color(1f, 0.92f, 0.3f);
            bubbleTextStyle.alignment = TextAnchor.MiddleCenter;

            // 4. Interacción al pulsar la Burbuja Flotante
            string label = "🔋 RECARGAR 📺";
            if (GUI.Button(bubbleRect, label, bubbleTextStyle))
            {
                OnRechargeButtonPressed();
            }

            GUI.matrix = oldHudMat;
        }
    }
}
