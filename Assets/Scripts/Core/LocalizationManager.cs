using UnityEngine;
using System.Collections.Generic;

public class LocalizationManager : MonoBehaviour
{
    private static LocalizationManager _instance;
    public static LocalizationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // Buscar si ya existe en la escena
                _instance = FindFirstObjectByType<LocalizationManager>();
                if (_instance == null)
                {
                    // Si no existe, crearlo dinámicamente en tiempo de ejecución
                    GameObject go = new GameObject("LocalizationManager_AutoCreated");
                    _instance = go.AddComponent<LocalizationManager>();
                }
            }
            return _instance;
        }
    }

    public delegate void LanguageChangedHandler();
    public static event LanguageChangedHandler OnLanguageChanged;

    public enum Idioma
    {
        ESPAÑOL,
        ENGLISH,
        PORTUGUES
    }

    private Idioma idiomaActual = Idioma.ESPAÑOL;
    private Dictionary<string, Dictionary<Idioma, string>> database = new Dictionary<string, Dictionary<Idioma, string>>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Evitar doble inicialización si se autoinstanció antes del Awake de Unity
        if (database.Count == 0)
        {
            CargarBaseDeDatos();
            CargarIdiomaGuardado();
        }
    }

    private void CargarIdiomaGuardado()
    {
        string saved = PlayerPrefs.GetString("JuegoIdioma", "ES");
        if (saved == "EN") idiomaActual = Idioma.ENGLISH;
        else if (saved == "PT") idiomaActual = Idioma.PORTUGUES;
        else idiomaActual = Idioma.ESPAÑOL;
    }

    public void CambiarIdioma(Idioma nuevoIdioma)
    {
        idiomaActual = nuevoIdioma;
        string code = "ES";
        if (nuevoIdioma == Idioma.ENGLISH) code = "EN";
        else if (nuevoIdioma == Idioma.PORTUGUES) code = "PT";
        
        PlayerPrefs.SetString("JuegoIdioma", code);
        PlayerPrefs.Save();

        Debug.Log($"LocalizationManager: Idioma cambiado a {nuevoIdioma}");
        
        // Notificar a todos los scripts de texto que actualicen su visual
        OnLanguageChanged?.Invoke();
    }

    public Idioma GetIdiomaActual()
    {
        return idiomaActual;
    }

    public string Get(string key)
    {
        if (database.ContainsKey(key))
        {
            if (database[key].ContainsKey(idiomaActual))
            {
                return database[key][idiomaActual];
            }
        }
        
        // Si no se encuentra la traducción, devolvemos la clave como fallback
        return $"[{key}]";
    }

    private void Add(string key, string es, string en, string pt)
    {
        var dict = new Dictionary<Idioma, string>
        {
            { Idioma.ESPAÑOL, es },
            { Idioma.ENGLISH, en },
            { Idioma.PORTUGUES, pt }
        };
        database[key] = dict;
    }

    private void CargarBaseDeDatos()
    {
        // --- MENÚ PRINCIPAL Y AJUSTES ---
        Add("menu_jugar", "JUGAR", "PLAY", "JOGAR");
        Add("menu_ajustes", "AJUSTES", "SETTINGS", "CONFIGURAÇÕES");
        Add("menu_salir", "SALIR", "QUIT", "SAIR");
        Add("menu_volver", "VOLVER AL MENÚ", "BACK TO MENU", "VOLTAR AO MENU");
        Add("menu_dificultad", "DIFICULTAD", "DIFFICULTY", "DIFICULDADE");
        Add("menu_tamano_mapa", "TAMAÑO DE MAPA", "MAP SIZE", "TAMANHO DO MAPA");
        Add("menu_sensibilidad", "SENSIBILIDAD MOUSE", "MOUSE SENSITIVITY", "SENSIBILIDADE DO MOUSE");
        Add("menu_volumen", "VOLUMEN GENERAL", "MASTER VOLUME", "VOLUME GERAL");
        Add("menu_graficos", "CALIDAD GRÁFICOS", "GRAPHICS QUALITY", "QUALIDADE GRÁFICA");
        Add("menu_idioma", "IDIOMA", "LANGUAGE", "IDIOMA");
        Add("menu_ir_tunes", "  [ IR A LOS TÚNELES (NIVEL 2) ]", "  [ GO TO TUNNELS (LEVEL 2) ]", "  [ IR PARA OS TÚNEIS (NÍVEL 2) ]");

        // --- PANTALLAS DE CARGA ---
        Add("load_titulo", "CONSEJO DE SUPERVIVENCIA", "SURVIVAL TIP", "CONSELHO DE SOBREVIVÊNCIA");
        Add("load_status_cargando", "CARGANDO MAPA PROCEDURAL...", "LOADING PROCEDURAL MAP...", "CARREGANDO MAPA PROCEDURAL...");
        Add("load_status_iniciando", "INICIANDO ENCUENTRO...", "STARTING ENCOUNTER...", "INICIANDO ENCONTRO...");
        
        // --- TIPS DE CARGA ---
        Add("tip_1", "El fenómeno es atraído por el ruido de tus pasos. Evita correr cuando esté cerca para que no detecte tu posición.",
                     "The phenomenon is attracted by the noise of your steps. Avoid running when it's nearby so it doesn't detect you.",
                     "O fenômeno é atraído pelo barulho dos seus passos. Evite correr quando ele estiver por perto para não ser detectado.");
        Add("tip_2", "Si el monstruo te ve, corre hacia una cama y mantén pulsada la acción para esconderte debajo.",
                     "If the monster sees you, run to a bed and hold the action key to hide underneath.",
                     "Se o monstro te vir, corra para uma cama e segure a ação para se esconder embaixo.");
        Add("tip_3", "La linterna consume batería. Apágala [F] cuando estés en una habitación con luz estable.",
                     "The flashlight consumes battery. Turn it off [F] when inside a room with stable lighting.",
                     "A lanterna consome bateria. Desligue-a [F] quando estiver em uma sala com luz estável.");
        Add("tip_4", "Tu salud y tu cordura se regeneran gradualmente, pero únicamente si estás bajo una zona iluminada.",
                     "Your health and sanity regenerate gradually, but only when standing under a lighted area.",
                     "Sua saúde e sanidade regeneram gradualmente, mas apenas se você estiver sob uma área iluminada.");
        Add("tip_5", "Las notas de la pared contienen los dígitos de la Oficina del Director. Abre la puerta para conseguir la Tarjeta de Acceso.",
                     "Wall notes contain the Director's Office door digits. Open the door to get the Keycard.",
                     "As notas na parede contêm os dígitos da Sala do Diretor. Abra a porta para obter o Cartão de Acesso.");
        Add("tip_6", "Cuando el hospital sufre un apagón, debes activar todos los subgeneradores antes de rearmar la caja de fusibles.",
                     "When the hospital suffers a blackout, you must activate all subgenerators before resetting the main fuse box.",
                     "Quando o hospital sofrer um apagão, você deve ativar todos os subgeradores antes de rearmar a caixa de fusíveis.");
        Add("tip_7", "El monstruo puede teletransportarse a través de las sombras si corres de espaldas en zonas completamente oscuras.",
                     "The monster can teleport through shadows if you run backwards in completely dark zones.",
                     "O monstro pode se teletransportar pelas sombras se você correr de costas em áreas completamente escuras.");
        Add("tip_8", "Tienes intentos limitados por mapa. Si mueres, reaparecerás en el spawn y el monstruo se retirará lejos.",
                     "You have limited attempts per map. If you die, you will respawn and the monster will retreat far away.",
                     "Você tem tentativas limitadas por mapa. Se morrer, reaparecerá no spawn e o monstro se retirará para longe.");

        // --- EN JUEGO / HUDS ---
        Add("hud_intentos_ult", "Último intento", "Last attempt", "Última tentativa");
        Add("hud_dia_prefix", "Día ", "Day ", "Dia ");
        Add("hud_game_over", "GAME OVER", "GAME OVER", "GAME OVER");
        Add("hud_consumido", "La oscuridad te ha consumido...", "Darkness has consumed you...", "A escuridão consumiu você...");
        Add("hud_reintentar_inicio", "REINTENTAR", "RETRY", "TENTAR NOVAMENTE");
        Add("hud_ir_menu", "IR AL MENÚ", "MAIN MENU", "MENU PRINCIPAL");
        
        // --- ADVERTENCIAS Y MENSAJES DE PODER ---
        Add("msg_far_fuse", "Estás muy lejos para interactuar con la caja de fusibles.", "You are too far to interact with the fuse box.", "Você está muito longe para interagir com a caixa de fusíveis.");
        Add("msg_fuse_repaired", "Fusibles rearmados. ({0}/{1} reparaciones libres usadas)", "Fuses reset. ({0}/{1} free repairs used)", "Fusíveis rearmados. ({0}/{1} reparações livres usadas)");
        Add("msg_fuse_placed", "Fusible de repuesto colocado! Energía restablecida. (Quedan: {0})", "Spare fuse placed! Power restored. (Remaining: {0})", "Fusível reserva colocado! Energia restabelecida. (Restam: {0})");
        Add("msg_fuse_burned", "FUSIBLE QUEMADO PERMANENTEMENTE!\nEncuentra un fusible de repuesto en las habitaciones.", "FUSE BURNED PERMANENTLY!\nFind a spare fuse inside the rooms.", "FUSÍVEL QUEIMADO PERMANENTEMENTE!\nEncontre um fusível reserva nas salas.");
        Add("msg_stable_fuse", "Fusibles en estado estable. Inventario: {0} fusible(s).", "Fuses in stable state. Inventory: {0} fuse(s).", "Fusíveis estáveis. Inventário: {0} fusível(is).");
        Add("msg_no_network", "SIN RED ELÉCTRICA: Activa todos los Subgeneradores ({0}/{1}) en el hospital.", "NO POWER GRID: Activate all Subgenerators ({0}/{1}) in the hospital.", "SEM REDE ELÉTRICA: Ative todos os Subgeradores ({0}/{1}) no hospital.");
        
        // --- ITEMS E INTERACCIONES ---
        Add("interact_note", "[E]  Recoger Nota de Código", "[E]  Pick up Code Note", "[E]  Coletar Nota de Código");
        Add("interact_keycard", "[E]  Recoger Tarjeta del Director", "[E]  Pick up Director Keycard", "[E]  Coletar Cartão do Diretor");
        Add("msg_keycard_picked", "Tarjeta de Acceso del Director recogida!", "Director Keycard picked up!", "Cartão de Acesso do Diretor coletado!");
        Add("msg_subgen_active", "SUBGENERADOR {0} ACTIVADO!", "SUBGENERATOR {0} ACTIVATED!", "SUBGERADOR {0} ATIVADO!");
        Add("interact_subgen", "[E] Encender Subgenerador {0}", "[E] Turn on Subgenerator {0}", "[E] Ligar Subgerador {0}");
        Add("interact_bed", "Esconderse bajo la cama", "Hide under the bed", "Esconder-se debaixo da cama");
    }
}
