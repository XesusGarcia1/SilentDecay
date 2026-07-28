using UnityEngine;

namespace ModularHospital
{
    public enum ConnectorDirection
    {
        North,
        South,
        East,
        West
    }

    public class ModuleConnector : MonoBehaviour
    {
        [Header("Configuración del Conector")]
        public ConnectorDirection direction = ConnectorDirection.North;
        public bool isConnected = false;
        
        [HideInInspector]
        public ModuleConnector connectedTarget = null;
        
        [HideInInspector]
        public HospitalModule parentModule = null;

        [Header("Guías Visuales")]
        public bool showGizmos = false;

        private void Awake()
        {
            if (parentModule == null)
            {
                parentModule = GetComponentInParent<HospitalModule>();
            }
        }

        public Vector3 GetWorldPosition()
        {
            return transform.position;
        }

        public Vector3 GetWorldForward()
        {
            return transform.forward;
        }

        public static ConnectorDirection GetOppositeDirection(ConnectorDirection dir)
        {
            switch (dir)
            {
                case ConnectorDirection.North: return ConnectorDirection.South;
                case ConnectorDirection.South: return ConnectorDirection.North;
                case ConnectorDirection.East:  return ConnectorDirection.West;
                case ConnectorDirection.West:  return ConnectorDirection.East;
                default: return ConnectorDirection.South;
            }
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            if (isConnected)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawSphere(transform.position, 0.25f);
                return;
            }

            Color dirColor = Color.white;
            string dirLabel = "";

            switch (direction)
            {
                case ConnectorDirection.North: 
                    dirColor = new Color(0f, 0.75f, 1f); 
                    dirLabel = " [ NORTE ]"; 
                    break;
                case ConnectorDirection.South: 
                    dirColor = new Color(1f, 0.25f, 0.25f); 
                    dirLabel = " [ SUR ]"; 
                    break;
                case ConnectorDirection.East:  
                    dirColor = new Color(0.2f, 1f, 0.3f); 
                    dirLabel = " [ ESTE ]"; 
                    break;
                case ConnectorDirection.West:  
                    dirColor = new Color(1f, 0.85f, 0f); 
                    dirLabel = " [ OESTE ]"; 
                    break;
            }

            Gizmos.color = dirColor;
            Gizmos.DrawSphere(transform.position, 0.35f);

            // Flecha de dirección de salida hacia afuera
            Gizmos.color = Color.white;
            Vector3 targetPos = transform.position + transform.forward * 1.0f;
            Gizmos.DrawLine(transform.position, targetPos);
            Gizmos.DrawSphere(targetPos, 0.08f);

#if UNITY_EDITOR
            GUIStyle style = new GUIStyle();
            style.normal.textColor = dirColor;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 14;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, transform.name + dirLabel, style);
#endif
        }
    }
}
