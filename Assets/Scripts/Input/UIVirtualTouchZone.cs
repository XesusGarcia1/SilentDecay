using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class UIVirtualTouchZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [System.Serializable]
    public class Event : UnityEvent<Vector2> { }

    [Header("Rect References")]
    public RectTransform containerRect;
    public RectTransform handleRect;

    [Header("Settings")]
    public bool clampToMagnitude;
    public float magnitudeMultiplier = 1f;
    public bool invertXOutputValue;
    public bool invertYOutputValue;

    [Header("Output")]
    public Event touchZoneOutputEvent;

    // Variables internas para el comportamiento de trackpad/delta de arrastre
    private Vector2 lastPointerPosition;
    private Vector2 currentDelta;
    private bool isDragging;

    // Variables para la detección de Tap (interacción en móviles)
    private float pressTime;
    private Vector2 pressPos;
    private static bool isSprintActive = false;

    void Start()
    {
        isSprintActive = false;
        SetupHandle();
        SetupMobileUIOverride();
    }

    private void SetupMobileUIOverride()
    {
        // 1. Configurar esta zona táctil (trackpad) para cubrir la pantalla completa si no es de movimiento
        if (!gameObject.name.ToLower().Contains("move"))
        {
            RectTransform tzRt = GetComponent<RectTransform>();
            if (tzRt != null)
            {
                tzRt.anchorMin = Vector2.zero;
                tzRt.anchorMax = Vector2.one;
                tzRt.offsetMin = Vector2.zero;
                tzRt.offsetMax = Vector2.zero;
            }
            transform.SetAsFirstSibling(); // Mandar al fondo de la jerarquía

            // Hacer invisible el fondo de esta zona táctil
            UnityEngine.UI.Image thisImg = GetComponent<UnityEngine.UI.Image>();
            if (thisImg != null)
            {
                thisImg.color = Color.clear;
            }

            // DESTRUIR TODOS LOS HIJOS (el handle del cuadro blanco, el fondo visual, etc.)
            // para que no se renderice NINGÚN residuo en medio de la pantalla
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        // 2. Encontrar el Canvas principal
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // Colores de la estética de horror: Negro/Gris muy oscuro semitransparente con detalles en blanco atenuado
        Color darkBgColor = new Color(0.08f, 0.08f, 0.08f, 0.45f); // Gris carbón con 45% alfa
        Color activeColor = new Color(0.4f, 0.04f, 0.04f, 0.55f);  // Rojo óxido / sangre seca sutil y atmosférico
        Color whiteIconColor = new Color(0.9f, 0.9f, 0.9f, 0.65f); // Blanco/gris fantasmagórico para iconos

        // Buscar todas las zonas táctiles (Touch Zones) de mirar y destruir sus elementos visuales
        UIVirtualTouchZone[] touchZones = canvas.GetComponentsInChildren<UIVirtualTouchZone>(true);
        foreach (var tz in touchZones)
        {
            if (tz.gameObject.name.ToLower().Contains("move")) continue;

            RectTransform localTzRt = tz.GetComponent<RectTransform>();
            if (localTzRt != null)
            {
                localTzRt.anchorMin = Vector2.zero;
                localTzRt.anchorMax = Vector2.one;
                localTzRt.offsetMin = Vector2.zero;
                localTzRt.offsetMax = Vector2.zero;
            }
            tz.transform.SetAsFirstSibling();

            UnityEngine.UI.Image tzImg = tz.GetComponent<UnityEngine.UI.Image>();
            if (tzImg != null) tzImg.color = Color.clear;

            for (int i = tz.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(tz.transform.GetChild(i).gameObject);
            }
        }

        // Estilizar los Joysticks directamente (solo color de fondo y handle) sin alterar sus iconos
        UIVirtualJoystick[] joysticks = canvas.GetComponentsInChildren<UIVirtualJoystick>(true);
        foreach (var js in joysticks)
        {
            UnityEngine.UI.Image bgImg = js.GetComponent<UnityEngine.UI.Image>();
            if (bgImg != null) bgImg.color = darkBgColor;

            if (js.handleRect != null)
            {
                UnityEngine.UI.Image handleImg = js.handleRect.GetComponent<UnityEngine.UI.Image>();
                if (handleImg != null) handleImg.color = darkBgColor; // Gris oscuro a juego con los botones

                // Poner el icono de las flechas interiores en blanco atenuado
                UnityEngine.UI.Image[] childImgs = js.handleRect.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                foreach (var child in childImgs)
                {
                    if (child != handleImg)
                    {
                        child.color = whiteIconColor;
                    }
                }
            }
        }

        // 3. Buscar el botón de correr original para configurar su lógica de toggle
        UIVirtualButton[] virtualButtons = canvas.GetComponentsInChildren<UIVirtualButton>(true);
        UIVirtualButton sprintButton = null;
        foreach (var vb in virtualButtons)
        {
            string vbName = vb.gameObject.name.ToLower();
            if (vbName.Contains("sprint") || vbName.Contains("run"))
            {
                sprintButton = vb;
            }
            else if (vbName.Contains("jump"))
            {
                vb.gameObject.SetActive(false); // Desactivar el botón de salto
            }
        }

        // Hacer que el botón de correr sea alternable (toggle: presionar una vez para correr, otra para caminar)
        if (sprintButton != null)
        {
            UIVirtualButton oldSprintVb = sprintButton.GetComponent<UIVirtualButton>();
            if (oldSprintVb != null) oldSprintVb.enabled = false; // Desactivar en lugar de destruir

            UnityEngine.UI.Button sprintBtn = sprintButton.GetComponent<UnityEngine.UI.Button>();
            if (sprintBtn == null) sprintBtn = sprintButton.gameObject.AddComponent<UnityEngine.UI.Button>();

            sprintBtn.onClick.RemoveAllListeners();

            // Encontrar el componente UICanvasControllerInput para enviar el input
            StarterAssets.UICanvasControllerInput canvasInput = canvas.GetComponent<StarterAssets.UICanvasControllerInput>();
            if (canvasInput == null) canvasInput = canvas.GetComponentInChildren<StarterAssets.UICanvasControllerInput>();

            sprintBtn.onClick.AddListener(() => {
                isSprintActive = !isSprintActive;
                if (canvasInput != null)
                {
                    canvasInput.VirtualSprintInput(isSprintActive);
                }
                
                // Color de fondo al hacer click (Gris destacado sutil si está activo, Gris oscuro si está inactivo)
                UnityEngine.UI.Image img = sprintButton.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    Color sprintActiveColor = new Color(0.18f, 0.18f, 0.18f, 0.65f);
                    img.color = isSprintActive ? sprintActiveColor : darkBgColor;
                }
            });

            // Color de fondo inicial del botón de correr (Gris oscuro por defecto)
            UnityEngine.UI.Image sprintImg = sprintButton.GetComponent<UnityEngine.UI.Image>();
            if (sprintImg != null)
            {
                Color sprintActiveColor = new Color(0.18f, 0.18f, 0.18f, 0.65f);
                sprintImg.color = isSprintActive ? sprintActiveColor : darkBgColor;
            }
        }

        // 3. Crear el botón de Linterna a la izquierda del botón de correr si no existe
        if (sprintButton != null && canvas.transform.Find("UI_Virtual_Button_Flashlight") == null)
        {
            GameObject flashlightBtnObj = Instantiate(sprintButton.gameObject, sprintButton.transform.parent);
            flashlightBtnObj.name = "UI_Virtual_Button_Flashlight";

            RectTransform sprintRt = sprintButton.GetComponent<RectTransform>();
            RectTransform flashRt = flashlightBtnObj.GetComponent<RectTransform>();

            // Posicionar a la izquierda del botón de correr
            flashRt.anchoredPosition = sprintRt.anchoredPosition + new Vector2(-155f, 0f);
            flashRt.localScale = sprintRt.localScale;

            // Reemplazar el componente UIVirtualButton por un click alternable directo
            UIVirtualButton oldVb = flashlightBtnObj.GetComponent<UIVirtualButton>();
            if (oldVb != null) oldVb.enabled = false; // Desactivar en lugar de destruir

            UnityEngine.UI.Button btn = flashlightBtnObj.GetComponent<UnityEngine.UI.Button>();
            if (btn == null) btn = flashlightBtnObj.AddComponent<UnityEngine.UI.Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => {
                var flashlight = FindFirstObjectByType<FlashlightController>();
                if (flashlight != null)
                {
                    flashlight.ToggleFlashlight();
                    
                    // Actualizar color de fondo del botón para reflejar si está encendido (Rojo) o apagado (Gris)
                    UnityEngine.UI.Image img = flashlightBtnObj.GetComponent<UnityEngine.UI.Image>();
                    if (img != null)
                    {
                        img.color = flashlight.flashlightLight.enabled ? activeColor : darkBgColor;
                    }
                }
            });

            // Sincronizar el color inicial según el estado actual de la linterna
            var initialFlashlight = FindFirstObjectByType<FlashlightController>();
            if (initialFlashlight != null && initialFlashlight.flashlightLight != null)
            {
                UnityEngine.UI.Image img = flashlightBtnObj.GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                {
                    img.color = initialFlashlight.flashlightLight.enabled ? activeColor : darkBgColor;
                }
            }

            // Configurar icono de Linterna
            if (flashlightBtnObj.transform.childCount > 0)
            {
                Transform iconTrans = flashlightBtnObj.transform.GetChild(0);
                UnityEngine.UI.Image iconImg = iconTrans.GetComponent<UnityEngine.UI.Image>();
                if (iconImg != null)
                {
                    Sprite flashlightIcon = CreateFlashlightSprite();
                    if (flashlightIcon != null)
                    {
                        iconImg.sprite = flashlightIcon;
                        iconImg.color = Color.white; // Usar color original blanco del sprite procesado
                        iconImg.enabled = true;

                        // Ajustar escala y rotación del icono para que se vea limpio
                        RectTransform iconRt = iconTrans.GetComponent<RectTransform>();
                        if (iconRt != null)
                        {
                            iconRt.localScale = new Vector3(1.3f, 1.3f, 1.3f); // Escala legible en cualquier pantalla
                            iconRt.localRotation = Quaternion.Euler(0f, 0f, 0f); // Sin rotación diagonal
                        }
                    }
                    else
                    {
                        // Fallback visual limpio: desactivamos el icono de correr y creamos un texto de linterna
                        iconImg.enabled = false;
                        
                        GameObject textObj = new GameObject("Text_Icon");
                        textObj.transform.SetParent(flashlightBtnObj.transform, false);
                        var tmpText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
                        tmpText.text = "Luz";
                        tmpText.fontSize = 32;
                        tmpText.alignment = TMPro.TextAlignmentOptions.Center;
                        tmpText.color = whiteIconColor;
                    }
                }
            }
        }

        // 4. Crear el botón de interactuar (E) arriba del botón de correr si no existe
        if (sprintButton != null && canvas.transform.Find("UI_Virtual_Button_Interact") == null)
        {
            GameObject interactBtnObj = Instantiate(sprintButton.gameObject, sprintButton.transform.parent);
            interactBtnObj.name = "UI_Virtual_Button_Interact";

            RectTransform sprintRt = sprintButton.GetComponent<RectTransform>();
            RectTransform interactRt = interactBtnObj.GetComponent<RectTransform>();

            // Posicionar arriba del botón de correr
            interactRt.anchoredPosition = sprintRt.anchoredPosition + new Vector2(0f, 155f);
            interactRt.localScale = sprintRt.localScale;

            // Estilizar el botón con el color de fondo inicial (gris oscuro)
            UnityEngine.UI.Image interactImg = interactBtnObj.GetComponent<UnityEngine.UI.Image>();
            if (interactImg != null)
            {
                interactImg.color = darkBgColor;
            }

            // Reemplazar el componente UIVirtualButton por un EventTrigger de presionar/mantener directo
            UIVirtualButton oldVb = interactBtnObj.GetComponent<UIVirtualButton>();
            if (oldVb != null) oldVb.enabled = false;

            UnityEngine.UI.Button oldBtn = interactBtnObj.GetComponent<UnityEngine.UI.Button>();
            if (oldBtn != null) oldBtn.enabled = false;

            var trigger = interactBtnObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            
            // Evento: Al presionar (PointerDown)
            var pointerDown = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerDown.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            pointerDown.callback.AddListener((data) => {
                MobileInput.ePressed = true;
                MobileInput.ePressedDown = true;
                
                UnityEngine.UI.Image img = interactBtnObj.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.color = activeColor;
            });
            trigger.triggers.Add(pointerDown);

            // Evento: Al soltar (PointerUp)
            var pointerUp = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerUp.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
            pointerUp.callback.AddListener((data) => {
                MobileInput.ePressed = false;
                MobileInput.ePressedDown = false;
                
                UnityEngine.UI.Image img = interactBtnObj.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.color = darkBgColor;
            });
            trigger.triggers.Add(pointerUp);

            // Configurar icono de Interact (Mano)
            if (interactBtnObj.transform.childCount > 0)
            {
                Transform iconTrans = interactBtnObj.transform.GetChild(0);
                UnityEngine.UI.Image iconImg = iconTrans.GetComponent<UnityEngine.UI.Image>();
                if (iconImg != null)
                {
                    Sprite handIcon = CreateHandSprite();
                    if (handIcon != null)
                    {
                        iconImg.sprite = handIcon;
                        iconImg.color = Color.white;
                        iconImg.enabled = true;

                        // Ajustar escala y rotación del icono para que se vea limpio
                        RectTransform iconRt = iconTrans.GetComponent<RectTransform>();
                        if (iconRt != null)
                        {
                            iconRt.localScale = new Vector3(1.3f, 1.3f, 1.3f); // Escala legible
                            iconRt.localRotation = Quaternion.identity;
                        }
                    }
                    else
                    {
                        iconImg.enabled = false; // Desactivar el icono original del muñeco
                        
                        GameObject textObj = new GameObject("Text_Icon");
                        textObj.transform.SetParent(interactBtnObj.transform, false);
                        var tmpText = textObj.AddComponent<TMPro.TextMeshProUGUI>();
                        tmpText.text = "Uso";
                        tmpText.fontSize = 32;
                        tmpText.alignment = TMPro.TextAlignmentOptions.Center;
                        tmpText.color = whiteIconColor;
                    }
                }
            }
        }
    }

    private void SetupHandle()
    {
        if(handleRect)
        {
            SetObjectActiveState(handleRect.gameObject, false); 
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        // Registrar la posición inicial de pantalla del toque
        lastPointerPosition = eventData.position;
        currentDelta = Vector2.zero;

        // Registrar tiempo y posición inicial para detección de Tap
        pressTime = Time.time;
        pressPos = eventData.position;

        if(handleRect)
        {
            SetObjectActiveState(handleRect.gameObject, true);
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(containerRect, eventData.position, eventData.pressEventCamera, out localPos);
            UpdateHandleRectPosition(localPos);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Calcular el delta real de movimiento de pantalla respecto al frame anterior
        Vector2 delta = eventData.position - lastPointerPosition;
        lastPointerPosition = eventData.position;

        if (invertXOutputValue) delta.x = -delta.x;
        if (invertYOutputValue) delta.y = -delta.y;

        // Escala del delta (0.15f da una sensibilidad táctil fluida y responsiva)
        currentDelta = delta * 0.15f * magnitudeMultiplier;
        OutputPointerEventValue(currentDelta);

        if(handleRect)
        {
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(containerRect, eventData.position, eventData.pressEventCamera, out localPos);
            UpdateHandleRectPosition(localPos);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        currentDelta = Vector2.zero;
        OutputPointerEventValue(Vector2.zero);

        if(handleRect)
        {
            SetObjectActiveState(handleRect.gameObject, false);
            UpdateHandleRectPosition(Vector2.zero);
        }

        // Detectar si fue un tap rápido y estático (menos de 0.22 segundos y moviendo el dedo menos de 15 píxeles)
        float duration = Time.time - pressTime;
        float distance = Vector2.Distance(eventData.position, pressPos);
        if (duration < 0.22f && distance < 15f)
        {
            TriggerVirtualInteraction();
        }
    }

    private void TriggerVirtualInteraction()
    {
        Debug.Log("[TouchZone] ¡Tap de interacción detectado! Activando MobileInput.ePressedDown.");
        MobileInput.ePressedDown = true;
        MobileInput.lastFrameEPressed = Time.frameCount;
    }

    private void TriggerVirtualFlashlight()
    {
        Debug.Log("[TouchZone] ¡Doble Tap detectado! Activando MobileInput.fPressedDown.");
        MobileInput.fPressedDown = true;
        MobileInput.lastFrameFPressed = Time.frameCount;
    }

    void Update()
    {
        // Si el usuario mantiene el dedo apoyado pero quieto (sin llamar a OnDrag), 
        // reseteamos el delta a cero en el siguiente frame para detener la rotación de cámara.
        if (isDragging)
        {
            OutputPointerEventValue(currentDelta);
            currentDelta = Vector2.zero;
        }
    }

    // LateUpdate eliminado para que MobileInput controle su propio consumo de inputs

    void OutputPointerEventValue(Vector2 pointerPosition)
    {
        touchZoneOutputEvent.Invoke(pointerPosition);
    }

    void UpdateHandleRectPosition(Vector2 newPosition)
    {
        if (handleRect != null)
        {
            handleRect.anchoredPosition = newPosition;
        }
    }

    void SetObjectActiveState(GameObject targetObject, bool newState)
    {
        if (targetObject != null)
        {
            targetObject.SetActive(newState);
        }
    }

    private Sprite CreateFlashlightSprite()
    {
        Texture2D originalTex = Resources.Load<Texture2D>("flashlight_icon");
        if (originalTex != null)
        {
            try
            {
                // Crear una copia editable para poder cambiar pixeles (RGBA32)
                Texture2D editableTex = new Texture2D(originalTex.width, originalTex.height, TextureFormat.RGBA32, false);
                
                // Asegurar que el filtro y modo de ajuste se vean nítidos
                editableTex.filterMode = FilterMode.Bilinear;
                editableTex.wrapMode = TextureWrapMode.Clamp;

                Color[] pixels = originalTex.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    // Obtener brillo/luminancia del pixel
                    float brightness = (pixels[i].r + pixels[i].g + pixels[i].b) / 3f;
                    if (brightness < 0.22f)
                    {
                        pixels[i] = Color.clear; // Fondo negro a completamente transparente
                    }
                    else
                    {
                        // Pintar la silueta de la linterna de color blanco/gris semitransparente para estilo horror
                        pixels[i] = new Color(0.9f, 0.9f, 0.9f, 0.65f);
                    }
                }
                editableTex.SetPixels(pixels);
                editableTex.Apply();

                return Sprite.Create(editableTex, new Rect(0f, 0f, editableTex.width, editableTex.height), new Vector2(0.5f, 0.5f));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[FlashlightIcon] Error procesando textura: " + ex.Message);
            }
        }
        else
        {
            Debug.LogWarning("[FlashlightIcon] No se encontró 'flashlight_icon' en Resources.");
        }
        return null;
    }

    private Sprite CreateHandSprite()
    {
        Texture2D originalTex = Resources.Load<Texture2D>("hand_icon");
        if (originalTex != null)
        {
            try
            {
                // Crear una copia editable para poder cambiar pixeles (RGBA32)
                Texture2D editableTex = new Texture2D(originalTex.width, originalTex.height, TextureFormat.RGBA32, false);
                
                // Asegurar que el filtro y modo de ajuste se vean nítidos
                editableTex.filterMode = FilterMode.Bilinear;
                editableTex.wrapMode = TextureWrapMode.Clamp;

                Color[] pixels = originalTex.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    // Obtener brillo/luminancia del pixel
                    float brightness = (pixels[i].r + pixels[i].g + pixels[i].b) / 3f;
                    if (brightness < 0.22f)
                    {
                        pixels[i] = Color.clear; // Fondo negro a completamente transparente
                    }
                    else
                    {
                        // Pintar la silueta de la mano de color blanco/gris semitransparente para estilo horror
                        pixels[i] = new Color(0.9f, 0.9f, 0.9f, 0.65f);
                    }
                }
                editableTex.SetPixels(pixels);
                editableTex.Apply();

                return Sprite.Create(editableTex, new Rect(0f, 0f, editableTex.width, editableTex.height), new Vector2(0.5f, 0.5f));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[HandIcon] Error procesando textura: " + ex.Message);
            }
        }
        else
        {
            Debug.LogWarning("[HandIcon] No se encontró 'hand_icon' en Resources.");
        }
        return null;
    }
}
