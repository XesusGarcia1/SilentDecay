using UnityEngine;
using StarterAssets;

public class LadderInteract : MonoBehaviour
{
    [Header("Reparación de Escalera")]
    [Tooltip("¿Esta escalera necesita ser reparada antes de poder subir?")]
    public bool isBroken = false;
    
    [Tooltip("Componentes desactivados que se activan al reparar (arrastra aquí LadderComponent_1 y LadderComponent_2)")]
    public GameObject[] ladderComponents;
    
    private int totalParts = 0;
    private int installedParts = 0;
    private bool isFullyRepaired = false;
    private bool playerInZone = false;

    void Start()
    {
        if (isBroken && ladderComponents != null)
        {
            totalParts = ladderComponents.Length;
            foreach (GameObject comp in ladderComponents)
            {
                if (comp != null) comp.SetActive(false);
            }
        }
        
        if (!isBroken) isFullyRepaired = true;
    }

    void Update()
    {
        if (!isBroken || isFullyRepaired) return;
        if (!playerInZone) return;

        bool ePressed = Input.GetKeyDown(KeyCode.E) || MobileInput.GetKeyDown(KeyCode.E) || MobileInput.ePressedDown;
        if (!ePressed) return;

        MobileInput.ePressedDown = false;
        TryInstallNextPart();
    }

    void TryInstallNextPart()
    {
        if (installedParts >= totalParts)
        {
            return;
        }

        // Verificar si el jugador carga alguna pieza de escalera
        if (LadderPartItem.collectedParts.Count == 0)
        {
            int remaining = totalParts - installedParts;
            if (remaining >= 2)
                PlayerMonologueManager.ShowDialogue("Mierda... la escalera está rota. Faltan piezas, necesito encontrarlas.", 4.5f);
            else
                PlayerMonologueManager.ShowDialogue("Aún me falta una pieza para poder repararla.", 3.5f);
            
            return;
        }

        // ¡Tiene una pieza! Instalarla
        // Consumir cualquier pieza del inventario
        var enumerator = LadderPartItem.collectedParts.GetEnumerator();
        enumerator.MoveNext();
        string usedPartID = enumerator.Current;
        LadderPartItem.collectedParts.Remove(usedPartID);

        // Liberar la carga pesada: el jugador recupera su velocidad
        LadderPartItem.isCarryingPart = false;
        FirstPersonController playerController = FindObjectOfType<FirstPersonController>();
        if (playerController != null)
        {
            playerController.isCarryingHeavy = false;
        }

        // Activar el componente visual correspondiente
        if (installedParts < ladderComponents.Length && ladderComponents[installedParts] != null)
        {
            ladderComponents[installedParts].SetActive(true);
        }

        installedParts++;

        // Sonido de reparación
        AudioClip repairSound = Resources.Load<AudioClip>("Audio/Compartido/Interruptor");
        if (repairSound != null)
            AudioSource.PlayClipAtPoint(repairSound, transform.position, 1.0f);

        if (installedParts >= totalParts)
        {
            isFullyRepaired = true;
            PlayerMonologueManager.ShowDialogue("Listo, la escalera está reparada. Ahora sí puedo subir.", 4.0f);
        }
        else
        {
            int remaining = totalParts - installedParts;
            PlayerMonologueManager.ShowDialogue("Bien, una pieza menos. Aún me falta " + remaining + " más.", 3.5f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = true;

            // Solo permitir escalar si NO está rota, o si ya fue reparada
            if (!isBroken || isFullyRepaired)
            {
                FirstPersonController player = other.GetComponent<FirstPersonController>();
                if (player == null) player = other.GetComponentInParent<FirstPersonController>();
                
                if (player != null)
                {
                    player.isClimbing = true;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = false;

            FirstPersonController player = other.GetComponent<FirstPersonController>();
            if (player == null) player = other.GetComponentInParent<FirstPersonController>();
            
            if (player != null)
            {
                player.isClimbing = false;
            }
        }
    }

    void OnGUI()
    {
        if (!playerInZone) return;
        if (!isBroken || isFullyRepaired) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontStyle = FontStyle.Bold;

        Rect rect = new Rect(Screen.width / 2 - 260, Screen.height - 120, 520, 50);

        bool hasAnyPart = LadderPartItem.collectedParts.Count > 0;
        string message;

        if (hasAnyPart)
        {
            // Puede reparar
            GUI.color = new Color(0f, 0.15f, 0.05f, 0.8f);
            GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            message = "[E] Instalar Pieza (" + installedParts + "/" + totalParts + ")";

            style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), message, style);

            style.normal.textColor = new Color(0.3f, 1f, 0.4f);
            GUI.Label(rect, message, style);
        }
        else
        {
            // No puede reparar
            GUI.color = new Color(0.2f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(rect.x - 10, rect.y - 5, rect.width + 20, rect.height + 10), Texture2D.whiteTexture);
            GUI.color = Color.white;

            int remaining = totalParts - installedParts;
            message = "Escalera Rota - Necesitas " + remaining + " pieza(s)";

            style.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), message, style);

            style.normal.textColor = new Color(1f, 0.4f, 0.3f);
            GUI.Label(rect, message, style);
        }
    }
}
