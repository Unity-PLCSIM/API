//----------------------------------------------------------------------------------------------------------------------
// PLC TAG DATA SERVICE
//
// Desc: Singleton de datos para tags PLC. Gestiona toda la comunicación con ApiInterface:
//       carga inicial (GET), escritura de entradas (PUT) y suscripción de salidas (WebSocket).
//       Completamente independiente de la UI - cualquier vista puede suscribirse a sus eventos.
//
// Uso:  PlcTagDataService.Instance.Load();
//       PlcTagDataService.Instance.OnTagsLoaded += ...;
//       PlcTagDataService.Instance.OnTagUpdated  += ...;
//
// Ubicación: Assets/Scripts/UI/PlcTagDataService.cs
// Autor: Alex Asensio
// Date:  Agosto 2026
//----------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq; // <-- AÑADIDO
using UnityEngine;

public class PlcTagDataService : MonoBehaviour
{
    // -- Singleton --------------------------------------------------------------

    public static PlcTagDataService Instance { get; private set; }

    void Awake()
    {
        // Comprobación de Singleton segura para Builds
        if (Instance != null && Instance != this) 
        { 
            if (Application.isPlaying)
                Destroy(gameObject); 
            else
                DestroyImmediate(gameObject);
                
            return; 
        }
        
        Instance = this;
        
        // DontDestroyOnLoad también da error si se llama fuera del Play Mode
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
            LoadCustomTablesFromPrefs(); // <-- AÑADIDO
        }
    }

    // -- Clases para Persistencia (Tablas Custom) ------------------------------- // <-- AÑADIDO (BLOQUE)
    [Serializable]
    public class CustomTableDef
    {
        public string Name;
        public List<string> Tags = new();
    }

    [Serializable]
    private class CustomTablesWrapper
    {
        public List<CustomTableDef> Tables = new();
    }

    // -- Modelo de datos público ------------------------------------------------

    /// <summary>
    /// Representación inmutable de un tag PLC.
    /// La UI crea sus propias copias defensivas; aquí solo existe el estado canónico.
    /// </summary>
    public class TagEntry
    {
        public string Name;
        public string Type;
        public string Value;
        public string Area;   // "E" = entrada (escribible), "S" = salida (solo lectura)
    }

    // -- Estado interno ---------------------------------------------------------

    private readonly Dictionary<string, TagEntry> _tags  = new();
    private readonly List<string>                 _order = new();
    private List<CustomTableDef>                  _customTables = new(); // <-- AÑADIDO

    public IReadOnlyList<string>                        Order => _order;
    public IReadOnlyDictionary<string, TagEntry>        Tags  => _tags;
    public IReadOnlyList<CustomTableDef>                CustomTables => _customTables; // <-- AÑADIDO

    public bool   IsLoading { get; private set; }
    public string StatusMessage { get; private set; } = "Sin datos — pulsa 'Cargar'";

    // -- Eventos ----------------------------------------------------------------

    /// <summary>Disparado tras una carga completa. Devuelve la lista ordenada de nombres.</summary>
    public event Action<IReadOnlyList<string>> OnTagsLoaded;

    /// <summary>Disparado cuando el valor de un tag cambia (PUT propio o push WS).</summary>
    public event Action<string, string> OnTagUpdated;   // (tagName, newValue)

    /// <summary>Disparado cuando el estado/loading cambia para que la UI actualice indicadores.</summary>
    public event Action<string, bool> OnStatusChanged;  // (message, isError)

    public event Action OnCustomTablesChanged; // <-- AÑADIDO

    // -- API de Tablas Custom --------------------------------------------------- // <-- AÑADIDO (BLOQUE)

    public void CreateCustomTable(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName) || _customTables.Any(t => t.Name == tableName)) return;
        _customTables.Add(new CustomTableDef { Name = tableName });
        SaveCustomTablesToPrefs();
        OnCustomTablesChanged?.Invoke();
    }

    public void DeleteCustomTable(string tableName)
    {
        _customTables.RemoveAll(t => t.Name == tableName);
        SaveCustomTablesToPrefs();
        OnCustomTablesChanged?.Invoke();
    }

    public void AddTagToTable(string tableName, string tagName)
    {
        var table = _customTables.FirstOrDefault(t => t.Name == tableName);
        if (table != null && !table.Tags.Contains(tagName))
        {
            table.Tags.Add(tagName);
            SaveCustomTablesToPrefs();
            OnCustomTablesChanged?.Invoke();
        }
    }

    public void RemoveTagFromTable(string tableName, string tagName)
    {
        var table = _customTables.FirstOrDefault(t => t.Name == tableName);
        if (table != null && table.Tags.Contains(tagName))
        {
            table.Tags.Remove(tagName);
            SaveCustomTablesToPrefs();
            OnCustomTablesChanged?.Invoke();
        }
    }

    private void SaveCustomTablesToPrefs()
    {
        var wrapper = new CustomTablesWrapper { Tables = _customTables };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString("PlcCustomTables", json);
        PlayerPrefs.Save();
    }

    private void LoadCustomTablesFromPrefs()
    {
        string json = PlayerPrefs.GetString("PlcCustomTables", "");
        if (!string.IsNullOrEmpty(json))
        {
            var wrapper = JsonUtility.FromJson<CustomTablesWrapper>(json);
            if (wrapper != null && wrapper.Tables != null)
                _customTables = wrapper.Tables;
        }
    }

    // -- API pública ------------------------------------------------------------

    /// <summary>
    /// Carga todos los tags con sus valores actuales.
    /// Suscribe automáticamente las salidas por WebSocket.
    /// </summary>
    public void Load()
    {
        if (IsLoading) return;

        IsLoading = true;
        NotifyStatus("Cargando tags...", isError: false);

        ApiInterface.Instance.GetTagsWithValues(
            onSuccess: tags =>
            {
                _tags.Clear();
                _order.Clear();

                foreach (var t in tags)
                {
                    _tags[t.Name] = new TagEntry
                    {
                        Name  = t.Name,
                        Type  = t.Type,
                        Value = t.Value,
                        Area  = t.Area,
                    };
                    _order.Add(t.Name);
                }

                IsLoading = false;
                string ts = DateTime.Now.ToString("HH:mm:ss");
                NotifyStatus($"{_tags.Count} tags · {ts}", isError: false);

                Debug.Log($"[PlcTagDataService] OnTagsLoaded suscriptores: {OnTagsLoaded?.GetInvocationList().Length ?? 0}");
                OnTagsLoaded?.Invoke(_order);
                SubscribeOutputsViaWebSocket();
            },
            onError: err =>
            {
                IsLoading = false;
                NotifyStatus($"Error al cargar: {err}", isError: true);
            }
        );
    }

    /// <summary>
    /// Escribe un nuevo valor en una entrada (Area == "E").
    /// Actualiza el estado local y notifica OnTagUpdated si tiene éxito.
    /// </summary>
    public void WriteInput(string tagName, string newValue)
    {
        if (!_tags.TryGetValue(tagName, out TagEntry td)) return;
        if (td.Area != "E")
        {
            Debug.LogWarning($"[PlcTagDataService] Intentando escribir en salida: {tagName}");
            return;
        }

        ApiInterface.Instance.SetTag(
            td.Name, td.Type, newValue,
            onSuccess: _ =>
            {
                td.Value = newValue;
                OnTagUpdated?.Invoke(tagName, newValue);
            },
            onError: err => NotifyStatus($"Error escritura '{tagName}': {err}", isError: true)
        );
    }

    // -- Internos ---------------------------------------------------------------

    private void SubscribeOutputsViaWebSocket()
    {
        foreach (string name in _order)
        {
            if (!_tags.TryGetValue(name, out TagEntry td)) continue;
            if (td.Area != "S") continue;

            string captured = name;                          // closure-safe
            ApiInterface.Instance.SubscribeOutputTag(captured, value =>
            {
                if (_tags.TryGetValue(captured, out TagEntry entry))
                {
                    entry.Value = value;
                    OnTagUpdated?.Invoke(captured, value);
                }
            });
        }
    }

    private void NotifyStatus(string message, bool isError)
    {
        StatusMessage = message;
        OnStatusChanged?.Invoke(message, isError);
    }
}