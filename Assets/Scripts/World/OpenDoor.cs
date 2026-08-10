using UnityEngine;

public class OpenDoor : MonoBehaviour
{
    public Animator doorAnimator;
    public AudioSource audioSource;
    public AudioClip doorOpenSound;
    public AudioClip doorCloseSound;

    public bool isLocked = false;
    private bool isOpen = false;
    public bool playerNearby = false;

    private float lastInteractTime = 0f;

    void Update()
    {
        if (isLocked) return;

        // Autodetectar proximidad si el trigger fallara
        bool near = playerNearby;
        if (!near)
        {
            UnityEngine.CharacterController cc = FindObjectOfType<UnityEngine.CharacterController>();
            if (cc != null && Vector3.Distance(transform.position, cc.transform.position) <= 2.8f)
            {
                near = true;
            }
            else
            {
                GameObject pObj = GameObject.Find("NestedParent_Unpack");
                if (pObj == null) pObj = GameObject.FindGameObjectWithTag("Player");
                if (pObj != null && Vector3.Distance(transform.position, pObj.transform.position) <= 2.8f)
                {
                    near = true;
                }
            }
        }

        bool ePressed = Input.GetKeyDown(KeyCode.E) || MobileInput.GetKeyDown(KeyCode.E) || MobileInput.ePressedDown;

        if (near && ePressed)
        {
            if (Time.unscaledTime < lastInteractTime + 0.35f) return;
            lastInteractTime = Time.unscaledTime;
            MobileInput.ePressedDown = false;

            isOpen = !isOpen;

            if (doorAnimator != null)
            {
                doorAnimator.enabled = true;
                doorAnimator.SetBool("isOpen", isOpen);
            }

            if (audioSource != null)
            {
                AudioClip clipToPlay = isOpen ? doorOpenSound : doorCloseSound;
                if (clipToPlay != null)
                {
                    audioSource.PlayOneShot(clipToPlay);
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player") || other.GetComponentInParent<UnityEngine.CharacterController>() != null)
        {
            playerNearby = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.transform.root.CompareTag("Player") || other.GetComponentInParent<UnityEngine.CharacterController>() != null)
        {
            playerNearby = false;
        }
    }
}
