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
        return $"[{key}]"; // Si no se encuentra la traducción, devolvemos la clave como fallback
    }

    public string GetFormat(string key, params object[] args)
    {
        string raw = Get(key);
        try
        {
            return string.Format(raw, args);
        }
        catch
        {
            return raw;
        }
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

        // --- SISTEMA NARRATIVO: LORE, INTROS Y MONÓLOGOS ---
        Add("interact_lore_note", "[E]  Examinar Registro / Diario", "[E]  Examine Log / Journal", "[E]  Examinar Registro / Diário");

        // Monólogos iniciales
        Add("monologue_hospital_ethan", "Tengo que encontrar fusibles y baterías para activar el ascensor. Es mi especialidad, pero este lugar me da escalofríos...",
                                         "I need to find fuses and batteries to power the elevator. It's my specialty, but this place gives me the creeps...",
                                         "Tenho que encontrar fusíveis e baterias para ativar o elevador. É minha especialidade, mas este lugar me dá arrepios...");
        Add("monologue_hospital_nora", "Este lugar está contaminado... tengo que encontrar la tarjeta de acceso de la oficina y salir por el ascensor.",
                                        "This place is contaminated... I have to find the office keycard and get out through the elevator.",
                                        "Este lugar está contaminado... tenho que encontrar o cartão de acesso da sala e sair pelo elevador.");
        Add("monologue_tunnels_ethan", "La consola de bombeo está despresurizada... Necesito reactivar la energía de los generadores para drenar el sector.",
                                        "The pump console is depressurized... I need to reactivate the generators' power to drain the sector.",
                                        "O console de bombeamento está despressurizado... Preciso reativar a energia dos geradores para drenar o setor.");
        Add("monologue_tunnels_nora", "El aire es denso... la consola de bombeo de agua debe ser la clave para abrir la escotilla de escape.",
                                       "The air is dense... the water pump console must be the key to opening the escape hatch.",
                                       "O ar está denso... o console de bombeamento de água deve ser a chave para abrir a escotilha de escape.");

        // Monólogos al recoger notas de lore
        Add("monologue_lore_1", "¿Un informe experimental sobre BookHead...? ¿Entonces este lugar sabía lo que estaba pasando?",
                               "An experimental report on BookHead...? So this place knew what was going on?",
                               "Um relatório experimental sobre BookHead...? Então este lugar sabia o que estava acontecendo?");
        Add("monologue_lore_2", "Este registro habla de TheCreep... no soy el primero en estar atrapado aquí.",
                               "This log talks about TheCreep... I'm not the first one trapped here.",
                               "Este registro fala sobre o TheCreep... não sou o primeiro preso aqui.");
        Add("monologue_lore_4", "¿El Fenómeno...? Así llaman a esa cosa que se teletransporta. Tengo que evitar mirarlo directamente.",
                               "The Phenomenon...? That's what they call that teleporting thing. I must avoid looking directly at it.",
                               "O Fenômeno...? É assim que chamam aquela coisa que se teletransporta. Tenho que evitar olhar diretamente para ele.");
        Add("monologue_lore_5", "Las chispas eléctricas... dañarán la batería de mi camcorder si me acerco demasiado.",
                               "Electrical sparks... they will drain my camcorder battery if I get too close.",
                               "Faíscas elétricas... vão descarregar a bateria da minha filmadora se eu chegar perto demais.");

        // Intros de Nivel (VHS) - Hospital (Ethan)
        Add("intro_hospital_ethan", 
            "SISTEMA DE ARCHIVO DE ANOMALÍAS -- VHS-DECAY v0.98\n====================================================\nCINTA RECUPERADA #04: HOSPITAL CENTRAL ABANDONADO\nSUJETO: ETHAN CROSS (INSPECTOR DE INFRAESTRUCTURA)\nOBJETIVO: EVALUACIÓN DE SISTEMAS Y ESTRUCTURA DE ALIMENTACIÓN\n\n\"El hospital fue evacuado de emergencia. Los generadores de respaldo\nestán fallando y el ascensor principal de escape requiere una clave cifrada.\nHe detectado anomalías acústicas en la biblioteca... oigo pasar las páginas.\nDebo restaurar la energía de la subestación y salir rápido de aquí.\"\n====================================================",
            "ANOMALY ARCHIVE SYSTEM -- VHS-DECAY v0.98\n====================================================\nRECOVERED TAPE #04: ABANDONED CENTRAL HOSPITAL\nSUBJECT: ETHAN CROSS (INFRASTRUCTURE INSPECTOR)\nOBJECTIVE: POWER GRID AND STRUCTURAL ASSESSMENT\n\n\"The hospital was evacuated in an emergency. The backup generators are failing and the main escape elevator requires an encrypted key.\nI have detected acoustic anomalies in the library... I hear pages turning.\nI must restore the power at the substation and get out of here quickly.\"\n====================================================",
            "SISTEMA DE ARQUIVOS DE ANOMALIAS -- VHS-DECAY v0.98\n====================================================\nFITA RECUPERADA #04: HOSPITAL CENTRAL ABANDONADO\nSUJEITO: ETHAN CROSS (INSPETOR DE INFRAESTRUTURA)\nOBJETIVO: AVALIAÇÃO DA REDE ELÉTRICA E ESTRUTURA\n\n\"O hospital foi evacuado em emergência. Os geradores de reserva estão falhando e o elevador de fuga principal exige uma senha criptografada.\nDetectei anomalias acústicas na biblioteca... ouço páginas passando.\nPreciso restaurar a energia na subestação e sair daqui rapidamente.\"\n====================================================");

        // Intros de Nivel (VHS) - Hospital (Nora)
        Add("intro_hospital_nora", 
            "SISTEMA DE ARCHIVO DE ANOMALÍAS -- VHS-DECAY v0.98\n====================================================\nCINTA RECUPERADA #04: HOSPITAL CENTRAL ABANDONADO\nSUJETO: NORA HAYES (INVESTIGADORA AMBIENTAL)\nOBJETIVO: ANÁLISIS DE RESIDUOS Y EVALUACIÓN DE RIESGOS\n\n\"El hospital fue evacuado en absoluto silencio. Hay rastros de una severa\ncontaminación en los niveles inferiores y el ascensor está bloqueado.\nAlgo se arrastra por el suelo y vigila desde la oscuridad de la biblioteca principal...\nNo estoy sola aquí. Debo conseguir las pruebas y huir antes de que sea tarde.\"\n====================================================",
            "ANOMALY ARCHIVE SYSTEM -- VHS-DECAY v0.98\n====================================================\nRECOVERED TAPE #04: ABANDONED CENTRAL HOSPITAL\nSUBJECT: NORA HAYES (ENVIRONMENTAL INVESTIGATOR)\nOBJECTIVE: WASTE ANALYSIS AND RISK ASSESSMENT\n\n\"The hospital was evacuated in absolute silence. There are traces of severe contamination in the lower levels and the elevator is blocked.\nSomething is crawling on the floor and watching from the shadows of the main library... I'm not alone.\nI must gather the evidence and escape before it's too late.\"\n====================================================",
            "SISTEMA DE ARQUIVOS DE ANOMALIAS -- VHS-DECAY v0.98\n====================================================\nFITA RECUPERADA #04: HOSPITAL CENTRAL ABANDONADO\nSUJEITO: NORA HAYES (INVESTIGADORA AMBIENTAL)\nOBJETIVO: ANÁLISE DE RESÍDUOS E AVALIAÇÃO DE RISCOS\n\n\"O hospital foi evacuado em silêncio absoluto. Há vestígios de contaminação severa nos níveis inferiores e o elevador está bloqueado.\nAlgo está se arrastando pelo chão e vigiando da escuridão da biblioteca principal... Não estou sozinha.\nPreciso coletar as evidências e fugir antes que seja tarde.\"\n====================================================");

        // Intros de Nivel (VHS) - Túneles (Ethan)
        Add("intro_tunnels_ethan", 
            "SISTEMA DE ARCHIVO DE ANOMALÍAS -- VHS-DECAY v0.98\n====================================================\nCINTA RECUPERADA #07: SECTOR DE DRENAJES SUBTERRÁNEOS B-12\nSUJETO: ETHAN CROSS (INSPECTOR DE INFRAESTRUCTURA)\nOBJETIVO: INSPECCIÓN DE LA CONSOLA DE BOMBEO Y ENERGÍA SECUNDARIA\n\n\"La consola de bombeo principal ha fallado. El generador secundario\nestá apagado por sobrecarga. Hay una interferencia electromagnética\nsevera que distorsiona mi equipo. Debo presurizar los tres tanques\nde drenaje para abrir la compuerta de escape.\"\n====================================================",
            "ANOMALY ARCHIVE SYSTEM -- VHS-DECAY v0.98\n====================================================\nRECOVERED TAPE #07: B-12 SUBTERRANEAN DRAINAGE SECTOR\nSUBJECT: ETHAN CROSS (INFRASTRUCTURE INSPECTOR)\nOBJECTIVE: PUMP CONSOLE AND AUXILIARY POWER INSPECTION\n\n\"The main pump console has failed. The auxiliary generator is shut down due to overload.\nThere is severe electromagnetic interference distorting my equipment.\nI must pressurize the three drainage tanks to open the escape hatch.\"\n====================================================",
            "SISTEMA DE ARQUIVOS DE ANOMALIAS -- VHS-DECAY v0.98\n====================================================\nFITA RECUPERADA #07: SETOR DE DRENAGEM SUBTERRÂNEO B-12\nSUJEITO: ETHAN CROSS (INSPETOR DE INFRAESTRUTURA)\nOBJETIVO: INSPEÇÃO DO CONSOLE DE BOMBEO E ENERGIA AUXILIAR\n\n\"O console de bombeamento principal falhou. O gerador secundário está desligado por sobrecarga.\nHá uma interferência eletromagnética severa distorcendo meu equipamento.\nPreciso pressurizar os três tanques de drenagem para abrir a escotilha de fuga.\"\n====================================================");

        // Intros de Nivel (VHS) - Túneles (Nora)
        Add("intro_tunnels_nora", 
            "SISTEMA DE ARCHIVO DE ANOMALÍAS -- VHS-DECAY v0.98\n====================================================\nCINTA RECUPERADA #07: SECTOR DE DRENAJES SUBTERRÁNEOS B-12\nSUJETO: NORA HAYES (INVESTIGADORA AMBIENTAL)\nOBJETIVO: ANÁLISIS DE TOXICIDAD Y LOCALIZACIÓN DE ANOMALÍAS\n\n\"El agua de los drenajes muestra niveles críticos de toxicidad.\nUna densa niebla negra está llenando rápidamente los pasadizos.\nMi camcorder está captando parpadeos extraños y estática severa.\nDebo activar la consola de bombeo para drenar la zona y encontrar la salida.\"\n====================================================",
            "ANOMALY ARCHIVE SYSTEM -- VHS-DECAY v0.98\n====================================================\nRECOVERED TAPE #07: B-12 SUBTERRANEAN DRAINAGE SECTOR\nSUBJECT: NORA HAYES (ENVIRONMENTAL INVESTIGATOR)\nOBJECTIVE: TOXICITY ANALYSIS AND ANOMALY DETECTION\n\n\"The drainage water shows critical toxicity levels.\nA dense black fog is quickly filling the tunnels.\nMy camcorder is catching strange flickers and severe static.\nI must activate the pump console to drain the zone and find the exit.\"\n====================================================",
            "SISTEMA DE ARQUIVOS DE ANOMALIAS -- VHS-DECAY v0.98\n====================================================\nFITA RECUPERADA #07: SETOR DE DRENAGEM SUBTERRÂNEO B-12\nSUJEITO: NORA HAYES (INVESTIGADORA AMBIENTAL)\nOBJETIVO: ANÁLISE DE TOXICIDADE E DETECÇÃO DE ANOMALIAS\n\n\"A água da drenagem mostra níveis críticos de toxicidade.\nUma névoa negra densa está enchendo rapidamente os túneis.\nMinha filmadora está registrando piscadas estranhas e estática severa.\nPreciso ativar o console de bombeamento para drenar a área e encontrar a saída.\"\n====================================================");

        // --- CONTENIDO DE NOTAS DE LORE ---
        // Hospital Lore 1 (BookHead)
        Add("lore_hosp_title_1", "Diario del Bibliotecario (BookHead)", "Librarian's Diary (BookHead)", "Diário do Bibliotecário (BookHead)");
        Add("lore_hosp_body_1", 
            "<b>REGISTRO DEL DIARIO - 18 DE OCTUBRE:</b>\n\nEse maldito monstruo... la criatura con cabeza de libro que merodea la biblioteca principal.\nConfirmado: <i>TIENE UN OJO EN EL LIBRO</i>, por lo que no es totalmente ciego, pero su vista es muy limitada.\nSin embargo, su oído es increíblemente agudo.\nSi caminas despacio, te ignorará por completo. Pero si entras en pánico y corres <b>(sprint)</b>, sabrá exactamente dónde estás al instante y te perseguirá.\nGuarda silencio si quieres conservar la cabeza.",
            "<b>DIARY LOG - OCTOBER 18:</b>\n\nThat damn monster... the creature with a book for a head roaming the main library.\nConfirmed: <i>IT HAS AN EYE ON THE BOOK</i>, so it is not completely blind, but its sight is very limited.\nHowever, its hearing is incredibly sharp.\nIf you walk slowly, it will ignore you completely. But if you panic and run <b>(sprint)</b>, it will know exactly where you are instantly and chase you.\nKeep quiet if you want to keep your head.",
            "<b>REGISTRO DO DIÁRIO - 18 DE OUTUBRO:</b>\n\nAquele maldito monstro... a criatura com cabeça de livro que ronda a biblioteca principal.\nConfirmado: <i>TEM UM OLHO NO LIVRO</i>, por isso não é totalmente cego, mas sua visão é muito limitada.\nNo entanto, sua audição é incrivelmente aguçada.\nSe você caminhar devagar, ele o ignorará completamente. Mas se você entrar em pânico e correr <b>(sprint)</b>, ele saberá exatamente onde você está instantaneamente e o perseguirá.\nGuarde silêncio se quiser manter a cabeça.");

        // Hospital Lore 2 (TheCreep)
        Add("lore_hosp_title_2", "Informe de Psiquiatría (TheCreep)", "Psychiatry Report (TheCreep)", "Relatório Psiquiátrico (TheCreep)");
        Add("lore_hosp_body_2", 
            "<b>EXPEDIENTE ANÓMALO #09-B:</b>\n\nLos pacientes del Pabellón Este reportan avistamientos de un ser deforme en el suelo.\nSe arrastra como un insecto y lo llaman 'TheCreep' (El Rastrero).\nEl personal reporta que prefiere quedarse en las esquinas más oscuras del hospital.\nEs extremadamente agresivo. Si te encuentra, intentará acorralarte y atacarte.\nPara escapar de él, debes correr hacia el spawn o buscar zonas iluminadas.\nNunca te quedes quieto en los callejones oscuros.",
            "<b>ANOMALOUS FILE #09-B:</b>\n\nPatients in the East Ward report sightings of a deformed being on the floor.\nIt crawls like an insect and they call it 'TheCreep'.\nStaff report that it prefers to stay in the darkest corners of the hospital.\nIt is extremely aggressive. If it finds you, it will try to corner and attack you.\nTo escape from it, you must run towards the spawn or look for lighted areas.\nNever stand still in dark alleys.",
            "<b>ARQUIVO ANÔMALO #09-B:</b>\n\nOs pacientes da Ala Leste relatam avistamentos de um ser deformado no chão.\nEle se arrasta como um insecto e o chamam de 'TheCreep' (O Rastejante).\nA equipe relata que ele prefere ficar nos cantos mais escuros do hospital.\nÉ extremamente agressivo. Se ele te encontrar, tentará encurralar e atacar você.\nPara escapar dele, você deve correr em direção ao spawn ou procurar áreas iluminadas.\nNunca fique parado em becos escuros.");

        // Hospital Lore 3 (Evacuation)
        Add("lore_hosp_title_3", "Memorándum de Evacuación", "Evacuation Memorandum", "Memorando de Evacuação");
        Add("lore_hosp_body_3", 
            "<b>ORDEN DE EVACUACIÓN INTERNA:</b>\n\nA todo el personal administrativo:\nLa fuga biológica ha alcanzado los niveles subterráneos del ala oeste.\nEl ascensor de escape principal de la oficina del director ha sido bloqueado por el protocolo de cuarentena.\nSe requiere una contraseña cifrada de 7 dígitos para restablecerlo.\nLas hojas de códigos de seguridad se han esparcido por las habitaciones para evitar que los sujetos de prueba las encuentren.\nBusca los 7 dígitos y evacua inmediatamente.",
            "<b>INTERNAL EVACUATION ORDER:</b>\n\nTo all administrative staff:\nThe biological leak has reached the underground levels of the west wing.\nThe main escape elevator in the director's office has been locked by the quarantine protocol.\nA 7-digit encrypted passcode is required to reset it.\nSecurity code sheets have been scattered across the rooms to prevent test subjects from finding them.\nFind all 7 digits and evacuate immediately.",
            "<b>ORDEM DE EVACUAÇÃO INTERNA:</b>\n\nA todo o pessoal administrativo:\nA fuga biológica atingiu os níveis subterráneos da ala oeste.\nO elevador de fuga principal da sala do diretor foi bloqueado pelo protocolo de quarentena.\nÉ necessária uma senha criptografada de 7 dígitos para restaurá-lo.\nAs folhas de códigos de segurança foram espalhadas pelas salas para evitar que as cobaias as encontrem.\nProcure os 7 dígitos e evacue imediatamente.");

        // Tunnels Lore 1 (The Phenomenon / Lore 4)
        Add("lore_tunn_title_1", "Bitácora del Operario (The Phenomenon)", "Operator's Log (The Phenomenon)", "Diário do Operário (The Phenomenon)");
        Add("lore_tunn_body_1", 
            "<b>REGISTRO DE SEGURIDAD - SECTOR B-12:</b>\n\nHay algo más aquí abajo. No es una rata. No es una tubería rota.\nSe teletransporta por el rabillo del ojo. Cuando lo miras de frente, parece desvanecerse...\nPero si te quedas inmóvil mirándolo fijamente demasiado tiempo, su presencia te consume la mente.\nSi escuchas una estática aguda y la pantalla parpadea, ¡DATE LA VUELTA Y CORRE!\nNo dejes que se acerque.",
            "<b>SECURITY RECORD - SECTOR B-12:</b>\n\nThere is something else down here. It's not a rat. It's not a broken pipe.\nIt teleports in the corner of your eye. When you look at it head-on, it seems to fade away...\nBut if you stand still staring at it for too long, its presence consumes your mind.\nIf you hear sharp static and your screen flickers, TURN AROUND AND RUN!\nDon't let it get close.",
            "<b>REGISTRO DE SEGURANÇA - SETOR B-12:</b>\n\nHá algo mais aqui embaixo. Não é um rato. Não é um cano quebrado.\nEle se teletransporta pelo canto do olho. Quando você olha de frente, parece desaparecer...\nMas se você ficar parado olhando fixamente por muito tempo, sua presença consome sua mente.\nSe ouvir estática aguda e sua tela piscar, VIR-SE E CORRA!\nNão deixe ele se aproximar.");

        // Tunnels Lore 2 (Chemical / Lore 5)
        Add("lore_tunn_title_2", "Informe de Incidentes - Fuga Química", "Incident Report - Chemical Leak", "Relatório de Incidentes - Vazamento Químico");
        Add("lore_tunn_body_2", 
            "<b>EXPEDIENTE TÉCNICO DE INSTALACIONES:</b>\n\nEl sistema de drenaje principal ha sido contaminado por residuos del laboratorio de arriba.\nSe informa de ruidos de metal doblándose y crujidos en las pasarelas (catwalks).\nCuidado con las chispas eléctricas expuestas; pueden dañar la linterna del camcorder.\nSi el generador principal de los túneles se apaga por sobrecarga, usa los interruptores de los paneles eléctricos secundarios.",
            "<b>FACILITIES TECHNICAL FILE:</b>\n\nThe main drainage system has been contaminated by waste from the lab above.\nReports of metal bending and creaking noises on the catwalks.\nWatch out for exposed electrical sparks; they can damage your camcorder's flashlight.\nIf the main generator in the tunnels shuts down due to overload, use the secondary electrical panel switches.",
            "<b>ARQUIVO TÉCNICO DE INSTALAÇÕES:</b>\n\nO sistema de drenagem principal foi contaminado por resíduos do laboratório acima.\nRelatos de ruídos de metal se dobrando e estalos nas passarelas (catwalks).\nCuidado com faíscas elétricas expostas; elas podem danificar a lanterna da sua filmadora.\nSe o gerador principal dos túneis desligar por sobrecarga, use os interruptores dos painéis elétricos secundários.");

        // Tunnels Lore 3 (Supervisor arrugado / Lore 6)
        Add("lore_tunn_title_3", "Nota Arrugada de Supervisor", "Crumpled Supervisor Note", "Nota Amassada do Supervisor");
        Add("lore_tunn_body_3", 
            "<b>GARABATO APRESURADO:</b>\n\nLa escotilla de escape está sellada. La consola de bombeo requiere presurizar los tres tanques principales.\nNo hay energía. El interruptor principal está en la cabina del generador...\nPero hay una estática insoportable que se mueve por el pasillo central.\nSi estás leyendo esto, no intentes pelear contra lo que acecha en la niebla negra. Solo corre y reza.",
            "<b>HURRIED SCRIBBLE:</b>\n\nThe escape hatch is sealed. The pump console requires pressurizing the three main tanks.\nThere is no power. The main switch is in the generator cabin...\nBut there is unbearable static moving along the central hallway.\nIf you are reading this, don't try to fight what lurks in the black fog. Just run and pray.",
            "<b>RASCUNHO APRESSADO:</b>\n\nA escotilha de fuga está selada. O console de bombeamento exige pressurizar os três tanques principais.\nNão há energia. O interruptor principal está na cabine do gerador...\nMas há uma estática insuportável se movendo pelo corredor central.\nSe você estiver lendo isso, não tente lutar contra o que espreita na névoa negra. Apenas corra e reze.");

        // --- INTERFAZ LIBRETA (NOTEPAD) ---
        Add("notepad_director_code", "Código de la Oficina del Director:", "Director's Office Passcode:", "Código da Sala do Diretor:");
        Add("notepad_hospital_map", "MAPA DEL HOSPITAL", "HOSPITAL MAP", "MAPA DO HOSPITAL");
        Add("notepad_tunnels_map", "PLANO DE LOS TÚNELES", "TUNNELS SCHEMATIC", "PLANO DOS TÚNEIS");
        Add("notepad_lore_records", "REGISTROS:", "LOGS:", "REGISTROS:");
        Add("notepad_close", "Cerrar", "Close", "Fechar");
        Add("notepad_no_lore", "No has recopilado ningún informe ni documento de historia todavía.\n\nBusca papeles envejecidos y quemados en las mesas y consultas del hospital.",
                               "You haven't gathered any reports or history logs yet.\n\nLook for aged and burnt papers on tables and rooms.",
                               "Você ainda não coletou nenhum relatório ou documento de história.\n\nProcure por papéis envelhecidos e queimados nas mesas e salas.");
        
        Add("notepad_hint_tunnels", 
            "Pistas del Hospital:\n\n(Esta sección correspondía al Hospital. En los túneles no se requieren notas clave para avanzar).\n\n⚠️ Tu objetivo actual en el sector de túneles es localizar la consola de drenaje, accionar la palanca de bombeo y evacuar por la escotilla principal.",
            "Hospital Clues:\n\n(This section belonged to the Hospital. In the tunnels, no keycodes are required to advance).\n\n⚠️ Your current objective in the tunnels is to locate the drainage console, activate the pump lever, and escape through the main hatch.",
            "Pistas do Hospital:\n\n(Esta seção correspondia ao Hospital. Nos túneis não são necessárias notas de código para avançar).\n\n⚠️ Seu objetivo atual nos túneis é localizar o console de drenagem, acionar a alavanca de bombeamento e evacuar pela escotilha principal.");
        Add("notepad_hint_header", "Pistas encontradas en el laberinto:\n\n", "Clues found in the maze:\n\n", "Pistas encontradas no labirinto:\n\n");
        Add("notepad_hint_digit", "• Dígito {0} del código: {1}\n", "• Digit {0} of the code: {1}\n", "• Dígito {0} do código: {1}\n");
        Add("notepad_hint_none", "(Aún no has encontrado ninguna nota. Busca papeles blancos con números en las consultas y oficinas del hospital).",
                                 "(You haven't found any notes yet. Search for white papers with numbers in offices and hospital rooms).",
                                 "(Você ainda não encontrou nenhuma nota. Procure por papéis brancos com números nas salas e escritórios do hospital).");
        Add("notepad_hint_complete", "¡Código completo descubierto! Ve a la puerta de la Oficina del Director e ingresa los 7 números.",
                                     "Complete code discovered! Go to the Director's Office door and enter the 7 numbers.",
                                     "Código completo descoberto! Vá até a porta da Sala do Diretor e insira os 7 números.");
        Add("notepad_hint_progress", "\n({0} de 7 notas encontradas. Sigue explorando para rellenar los casilleros vacíos).",
                                     "\n({0} of 7 notes found. Keep exploring to fill in the empty slots).",
                                     "\n({0} de 7 notas encontradas. Continue explorando para preencher as lacunas).");

        // Pestañas de Libreta
        Add("notepad_tab_code", "📝 CLAVE", "📝 CODE", "📝 CÓDIGO");
        Add("notepad_tab_map", "🗺️ MAPA", "🗺️ MAP", "🗺️ MAPA");
        Add("notepad_tab_lore", "📜 REGISTROS", "📜 RECORDS", "📜 REGISTROS");
    }
}
