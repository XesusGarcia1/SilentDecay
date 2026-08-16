using UnityEngine;

public class GuieMapItem : MonoBehaviour
{
    [Header("Ajustes de Interacción")]
    public float interactDistance = 4.5f;

    private Transform player;
    private bool playerNear = false;
    private Light glowLight;

    void Start()
    {
        FindPlayer();
        ApplyPaperMaterial();

        // Asegurar colisionador de interacción
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = Vector3.zero;
            box.size = new Vector3(0.5f, 0.2f, 0.5f);
        }

        // Luz de guía ámbar cálida para destacar en la oscuridad al inicio del mapa
        GameObject lightObj = new GameObject("GuieMap_GlowLight");
        lightObj.transform.SetParent(this.transform);
        lightObj.transform.localPosition = new Vector3(0f, 0.15f, 0f);

        glowLight = lightObj.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = new Color(0.95f, 0.75f, 0.4f); // Tinte dorado/pergamino cálido
        glowLight.range = 3.0f * Mathf.Max(0.5f, transform.lossyScale.x);
        glowLight.intensity = 0.8f;
        glowLight.shadows = LightShadows.None;
    }

    private void ApplyPaperMaterial()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) rend = GetComponentInChildren<Renderer>();

        if (rend != null && rend.material != null)
        {
            rend.material.color = new Color(0.85f, 0.72f, 0.48f, 1.0f);
        }
    }

    void FindPlayer()
    {
        CharacterController cc = FindObjectOfType<CharacterController>();
        if (cc != null) { player = cc.transform; return; }

        GameObject pObj = GameObject.Find("NestedParent_Unpack");
        if (pObj != null) { player = pObj.transform; return; }

        GameObject playerTagObj = GameObject.FindGameObjectWithTag("Player");
        if (playerTagObj != null) { player = playerTagObj.transform; return; }

        if (Camera.main != null) player = Camera.main.transform;
    }

    void Update()
    {
        // Hacer parpadear la luz de guía cálida
        if (glowLight != null)
        {
            glowLight.intensity = 0.4f + Mathf.PingPong(Time.unscaledTime * 0.8f, 0.6f);
        }

        if (player == null) FindPlayer();

        float dist = player != null ? Vector3.Distance(transform.position, player.position) : 999f;
        if (dist > interactDistance)
        {
            playerNear = false;
            return;
        }

        // Raycast / Focus check
        playerNear = InteractionFocusManager.IsFocused(gameObject, interactDistance);
    }

    void LateUpdate()
    {
        bool isTarget = playerNear && InteractionFocusManager.IsFocused(gameObject, interactDistance);
        if (isTarget && (Input.GetKeyDown(KeyCode.E) || MobileInput.GetKeyDown(KeyCode.E) || MobileInput.ePressedDown))
        {
            MobileInput.ePressedDown = false;
            CollectAndOpenMap();
        }
    }

    private void CollectAndOpenMap()
    {
        GuideMapUI.hasGuideMap = true;

        // Monólogo del personaje al encontrar el mapa
        PlayerMonologueManager.ShowDialogue(LocalizationManager.Instance.Get("monologue_found_guide"), 5.0f);

        // Abrir la interfaz de la guía inmediatamente
        if (GuideMapUI.Instance != null)
        {
            GuideMapUI.Instance.OpenMap();
        }

        Destroy(gameObject);
    }

    void OnGUI()
    {
        bool isTarget = playerNear && InteractionFocusManager.IsFocused(gameObject, interactDistance);
        if (!isTarget) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        Rect rect = new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 50);

        GUI.color = new Color(0f, 0.1f, 0.2f, 0.75f);
        GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
        GUI.color = Color.white;

        style.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), LocalizationManager.Instance.Get("interact_survival_guide"), style);

        style.normal.textColor = new Color(0.95f, 0.85f, 0.4f);
        GUI.Label(rect, LocalizationManager.Instance.Get("interact_survival_guide"), style);
    }
}
