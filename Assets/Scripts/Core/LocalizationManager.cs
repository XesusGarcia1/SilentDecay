using System.Text.RegularExpressions;
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
                _instance = FindFirstObjectByType<LocalizationManager>();
                if (_instance == null)
                {
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
        PORTUGUES,
        РУССКИЙ
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
        else if (saved == "RU") idiomaActual = Idioma.РУССКИЙ;
        else idiomaActual = Idioma.ESPAÑOL;
    }

    public void CambiarIdioma(Idioma nuevoIdioma)
    {
        idiomaActual = nuevoIdioma;
        string code = "ES";
        if (nuevoIdioma == Idioma.ENGLISH) code = "EN";
        else if (nuevoIdioma == Idioma.PORTUGUES) code = "PT";
        else if (nuevoIdioma == Idioma.РУССКИЙ) code = "RU";
        
        PlayerPrefs.SetString("JuegoIdioma", code);
        PlayerPrefs.Save();

        Debug.Log($"LocalizationManager: Idioma cambiado a {nuevoIdioma}");
        
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
        return $"[{key}]"; 
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

    private void Add(string key, string es, string en, string pt, string ru)
    {
        var dict = new Dictionary<Idioma, string>
        {
            { Idioma.ESPAÑOL, es },
            { Idioma.ENGLISH, en },
            { Idioma.PORTUGUES, pt },
            { Idioma.РУССКИЙ, ru }
        };
        database[key] = dict;
    }

    private void CargarBaseDeDatos()
    {
        database.Clear();
        
        TextAsset csvData = Resources.Load<TextAsset>("Localization");
        if (csvData == null)
        {
            Debug.LogError("No se encontró Localization.csv en la carpeta Resources!");
            return;
        }

        string[] lineas = csvData.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        
        // Regex para separar por coma ignorando las comillas
        Regex csvParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

        for (int i = 0; i < lineas.Length; i++)
        {
            string linea = lineas[i];
            
            // Ignorar la cabecera o líneas vacías
            if (string.IsNullOrWhiteSpace(linea) || linea.StartsWith("sep=") || linea.Contains("ESPAÑOL"))
                continue;

            string[] columnas = csvParser.Split(linea);
            if (columnas.Length >= 5)
            {
                // Limpiar BOM y comillas
                string key = columnas[0].Trim('\uFEFF', '\"', ' ');
                string es = columnas[1].Trim('\"').Replace("\\n", "\n");
                string en = columnas[2].Trim('\"').Replace("\\n", "\n");
                string pt = columnas[3].Trim('\"').Replace("\\n", "\n");
                string ru = columnas[4].Trim('\"').Replace("\\n", "\n");

                Add(key, es, en, pt, ru);
            }
        }
    }
}
