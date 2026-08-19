using UnityEngine;
using GoogleMobileAds.Api;
using System;

namespace SilentDecay.Core
{
    /// <summary>
    /// Gestor centralizado e independiente de Google Mobile Ads (AdMob) para Silent Decay.
    /// Permite activar/desactivar anuncios globalmente mediante un toggle en el Inspector.
    /// </summary>
    public class AdManager : MonoBehaviour
    {
        public static AdManager Instance { get; private set; }

        [Header("Configuración Global de Anuncios")]
        [Tooltip("Si se desmarca, NO se inicializarán ni mostrarán anuncios en el juego.")]
        public bool enableAds = true;

        [Tooltip("Si está activo, usará automáticamente los IDs de prueba oficiales de Google para evitar sanciones en Editor/Dev Builds.")]
        public bool useTestAdsInEditor = true;

        [Header("IDs Reales de AdMob (Android)")]
        [Tooltip("ID del bloque de anuncio Recompensado para Revivir")]
        public string rewardedReviveAdUnitId = "ca-app-pub-5970961731703173/8187700485";

        [Tooltip("ID del bloque de anuncio Recompensado para Batería")]
        public string rewardedBatteryAdUnitId = "ca-app-pub-5970961731703173/4631598851";

        [Tooltip("ID del bloque de anuncio Intersticial para Transiciones de Nivel/Menú")]
        public string interstitialAdUnitId = "ca-app-pub-5970961731703173/3222419466";

        // IDs oficiales de Prueba de Google AdMob para Android
        private const string TEST_REWARDED_ID = "ca-app-pub-3940256099942544/5224354917";
        private const string TEST_INTERSTITIAL_ID = "ca-app-pub-3940256099942544/1033173712";

        private RewardedAd _rewardedReviveAd;
        private RewardedAd _rewardedBatteryAd;
        private InterstitialAd _interstitialAd;

        private bool _isInitialized = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Desactivar anuncios automáticamente en versiones de PC (Steam/PC Standalone)
            #if !UNITY_ANDROID && !UNITY_IOS
            enableAds = false;
            Debug.Log("[AdManager] Plataforma de PC/Consola detectada. Anuncios DESACTIVADOS automáticamente para la versión de pago.");
            return;
            #endif

            if (!enableAds)
            {
                Debug.Log("[AdManager] Anuncios DESACTIVADOS desde el Inspector. El sistema de publicidad está en reposo.");
                return;
            }

            InitializeAdMob();
        }

        /// <summary>
        /// Inicializa el SDK de Google Mobile Ads y comienza la precarga de bloques.
        /// </summary>
        public void InitializeAdMob()
        {
            if (_isInitialized) return;

            Debug.Log("[AdManager] Inicializando SDK de Google Mobile Ads...");
            MobileAds.Initialize(initStatus =>
            {
                Debug.Log("[AdManager] SDK de Google Mobile Ads Inicializado con éxito.");
                _isInitialized = true;

                // Precargar los 3 anuncios
                LoadRewardedReviveAd();
                LoadRewardedBatteryAd();
                LoadInterstitialAd();
            });
        }

        #region --- 1. ANUNCIO RECOMPENSADO: REVIVIR ---

        private string GetReviveAdUnitId()
        {
            if (useTestAdsInEditor || Application.isEditor || string.IsNullOrEmpty(rewardedReviveAdUnitId))
            {
                return TEST_REWARDED_ID;
            }
            return rewardedReviveAdUnitId;
        }

        public void LoadRewardedReviveAd()
        {
            if (!enableAds || !_isInitialized) return;

            if (_rewardedReviveAd != null)
            {
                _rewardedReviveAd.Destroy();
                _rewardedReviveAd = null;
            }

            string adUnitId = GetReviveAdUnitId();
            Debug.Log($"[AdManager] Cargando Anuncio Recompensado (Revivir): {adUnitId}");
            var adRequest = new AdRequest();

            RewardedAd.Load(adUnitId, adRequest, (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[AdManager] Error cargando Anuncio de Revivir: {error?.GetMessage()}");
                    return;
                }

                _rewardedReviveAd = ad;
                Debug.Log("[AdManager] Anuncio Recompensado (Revivir) LISTO.");

                _rewardedReviveAd.OnAdFullScreenContentClosed += () =>
                {
                    Debug.Log("[AdManager] Anuncio de Revivir cerrado. Precargando nuevo anuncio...");
                    LoadRewardedReviveAd();
                };
            });
        }

        public bool IsReviveAdReady()
        {
            return enableAds && _rewardedReviveAd != null && _rewardedReviveAd.CanShowAd();
        }

        public void ShowRewardedRevive(Action onRewardEarned, Action onAdClosedOrFailed = null)
        {
            if (!enableAds)
            {
                Debug.LogWarning("[AdManager] Anuncios desactivados en Inspector. Otorgando recompensa en modo Debug/Desarrollo.");
                onRewardEarned?.Invoke();
                return;
            }

            if (IsReviveAdReady())
            {
                _rewardedReviveAd.Show((Reward reward) =>
                {
                    Debug.Log("[AdManager] Recompensa de Revivir Otorgada al Jugador.");
                    onRewardEarned?.Invoke();
                });
            }
            else
            {
                Debug.LogWarning("[AdManager] Anuncio de Revivir no disponible.");
                onAdClosedOrFailed?.Invoke();
                LoadRewardedReviveAd();
            }
        }

        #endregion

        #region --- 2. ANUNCIO RECOMPENSADO: BATERÍA ---

        private string GetBatteryAdUnitId()
        {
            if (useTestAdsInEditor || Application.isEditor || string.IsNullOrEmpty(rewardedBatteryAdUnitId))
            {
                return TEST_REWARDED_ID;
            }
            return rewardedBatteryAdUnitId;
        }

        public void LoadRewardedBatteryAd()
        {
            if (!enableAds || !_isInitialized) return;

            if (_rewardedBatteryAd != null)
            {
                _rewardedBatteryAd.Destroy();
                _rewardedBatteryAd = null;
            }

            string adUnitId = GetBatteryAdUnitId();
            Debug.Log($"[AdManager] Cargando Anuncio Recompensado (Batería): {adUnitId}");
            var adRequest = new AdRequest();

            RewardedAd.Load(adUnitId, adRequest, (RewardedAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[AdManager] Error cargando Anuncio de Batería: {error?.GetMessage()}");
                    return;
                }

                _rewardedBatteryAd = ad;
                Debug.Log("[AdManager] Anuncio Recompensado (Batería) LISTO.");

                _rewardedBatteryAd.OnAdFullScreenContentClosed += () =>
                {
                    Debug.Log("[AdManager] Anuncio de Batería cerrado. Precargando nuevo anuncio...");
                    LoadRewardedBatteryAd();
                };
            });
        }

        public bool IsBatteryAdReady()
        {
            return enableAds && _rewardedBatteryAd != null && _rewardedBatteryAd.CanShowAd();
        }

        public void ShowRewardedBattery(Action onRewardEarned, Action onAdClosedOrFailed = null)
        {
            if (!enableAds)
            {
                Debug.LogWarning("[AdManager] Anuncios desactivados en Inspector. Otorgando recarga de batería en modo Debug.");
                onRewardEarned?.Invoke();
                return;
            }

            if (IsBatteryAdReady())
            {
                _rewardedBatteryAd.Show((Reward reward) =>
                {
                    Debug.Log("[AdManager] Recompensa de Batería Otorgada al Jugador.");
                    onRewardEarned?.Invoke();
                });
            }
            else
            {
                Debug.LogWarning("[AdManager] Anuncio de Batería no disponible.");
                onAdClosedOrFailed?.Invoke();
                LoadRewardedBatteryAd();
            }
        }

        #endregion

        #region --- 3. ANUNCIO INTERSTICIAL: TRANSICIÓN DE NIVEL / MENÚ ---

        private string GetInterstitialAdUnitId()
        {
            if (useTestAdsInEditor || Application.isEditor || string.IsNullOrEmpty(interstitialAdUnitId))
            {
                return TEST_INTERSTITIAL_ID;
            }
            return interstitialAdUnitId;
        }

        public void LoadInterstitialAd()
        {
            if (!enableAds || !_isInitialized) return;

            if (_interstitialAd != null)
            {
                _interstitialAd.Destroy();
                _interstitialAd = null;
            }

            string adUnitId = GetInterstitialAdUnitId();
            Debug.Log($"[AdManager] Cargando Anuncio Intersticial (Transición): {adUnitId}");
            var adRequest = new AdRequest();

            InterstitialAd.Load(adUnitId, adRequest, (InterstitialAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[AdManager] Error cargando Anuncio Intersticial: {error?.GetMessage()}");
                    return;
                }

                _interstitialAd = ad;
                Debug.Log("[AdManager] Anuncio Intersticial (Transición) LISTO.");
            });
        }

        public bool IsInterstitialReady()
        {
            return enableAds && _interstitialAd != null && _interstitialAd.CanShowAd();
        }

        public void ShowInterstitialTransition(Action onAdFinished = null)
        {
            if (!enableAds)
            {
                onAdFinished?.Invoke();
                return;
            }

            if (IsInterstitialReady())
            {
                _interstitialAd.OnAdFullScreenContentClosed += () =>
                {
                    onAdFinished?.Invoke();
                    LoadInterstitialAd();
                };
                _interstitialAd.Show();
            }
            else
            {
                Debug.LogWarning("[AdManager] Anuncio Intersticial no listo. Continuando transición inmediatamente.");
                onAdFinished?.Invoke();
                LoadInterstitialAd();
            }
        }

        #endregion
    }
}
