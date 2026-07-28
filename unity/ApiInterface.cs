//----------------------------------------------------------------------------------------------------------------------
// INTERFAZ API PLC SIMULATION, TIA PORTAL
//
// Desc: Interfaz que abstrae la comunicacion con la API a métodos que pueden ser llamados por el resto
// de elementos de la simulación, sin que estos conozcan la estructura de dicha API.
//
// Coms:  - Se debe poner la opción 'Always Allowed' en Edit->Project Settings->Player->Allow downloads over HTTP
//        - Se debe ajustar 'baseUrl' a la dirección IP y PUERTO donde corra la API
//
// Uso:
//   // Leer un tag
//   ApiInterface.Instance.GetTag("Motor", "Bool", (value) => Debug.Log("Motor: " + value));
//
//   // Escribir un tag
//   ApiInterface.Instance.SetTag("Marcha", "Bool", "true", (msg) => Debug.Log(msg));
//
//   // Obtener todos los tags disponibles
//   ApiInterface.Instance.GetAllTags((tags) => { foreach (var t in tags) Debug.Log(t.Name); });
//
// Autor: Alex Asensio
// Date: Julio 2026
//----------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ApiInterface : MonoBehaviour
{
    // -- Singleton --------------------------------------------------------------
    public static ApiInterface Instance { get; private set; }

    // -- Configuración ----------------------------------------------------------
    [Header("Configuración API")]
    public string baseUrl = "http://192.168.49.128:9000/api/plc";

    [Tooltip("Segundos entre cada ciclo de polling automático (0 = desactivado)")]
    public float pollInterval = 0.5f;

    // -- Modelos de datos -------------------------------------------------------

    [Serializable]
    public class TagInfo
    {
        public string Name;
        public string Type;
    }

    [Serializable]
    public class TagValue
    {
        public string Tag;    // usado en GET /tags/{name}
        public string Value;
    }

    [Serializable]
    public class TagWithValue
    {
        public string Name;
        public string Type;
        public string Value;
        public string Area;
    }

    [Serializable]
    public class TagWritePayload
    {
        public string Value;
        public string Type;
    }

    [Serializable]
    public class ApiMessage
    {
        public string Message;
    }

    [Serializable]
    public class PlcInstance
    {
        public int ID;
        public string Name;
    }

    // Wrappers necesarios para JsonUtility con arrays
    [Serializable]
    private class TagInfoList     { public List<TagInfo>     items; }

    [Serializable]
    private class TagWithValueList { public List<TagWithValue> items; }

    [Serializable]
    private class PlcInstanceList { public List<PlcInstance> items; }

    // -- Eventos globales (opcionales) ------------------------------------------
    /// <summary>Se dispara cada vez que una petición falla.</summary>
    public event Action<string> OnApiError;

    // -- Ciclo de vida Unity ----------------------------------------------------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (pollInterval > 0f)
            StartCoroutine(AutoPollCoroutine());
    }

    // -- API PÚBLICA ------------------------------------------------------------

    /// <summary>
    /// Obtiene la lista completa de tags disponibles en el PLC.
    /// </summary>
    /// <param name="onSuccess">Recibe la lista de TagInfo con Name y Type.</param>
    /// <param name="onError">Recibe el mensaje de error (opcional).</param>
    public void GetAllTags(Action<List<TagInfo>> onSuccess, Action<string> onError = null)
    {
        string url = $"{baseUrl}/tags";
        StartCoroutine(GetRequest(url, (json) =>
        {
            // JsonUtility no parsea arrays directamente; usamos el wrapper
            string wrapped = "{\"items\":" + json + "}";
            TagInfoList result = JsonUtility.FromJson<TagInfoList>(wrapped);
            onSuccess?.Invoke(result.items);
        }, onError));
    }

    /// <summary>
    /// Obtiene todos los tags con su tipo y valor en una sola petición.
    /// </summary>
    public void GetTagsWithValues(Action<List<TagWithValue>> onSuccess, Action<string> onError = null)
    {
        string url = $"{baseUrl}/tags-with-values";
        StartCoroutine(GetRequest(url, (json) =>
        {
            string wrapped = "{\"items\":" + json + "}";
            TagWithValueList result = JsonUtility.FromJson<TagWithValueList>(wrapped);
            onSuccess?.Invoke(result.items);
        }, onError));
    }

    /// <summary>
    /// Obtiene todos los tags tipo SALIDA con su tipo y valor en una sola petición.
    /// </summary>
    public void GetOutputTagsWithValues(Action<List<TagWithValue>> onSuccess, Action<string> onError = null)
    {
        string url = $"{baseUrl}/output-tags-with-values";
        StartCoroutine(GetRequest(url, (json) =>
        {
            string wrapped = "{\"items\":" + json + "}";
            TagWithValueList result = JsonUtility.FromJson<TagWithValueList>(wrapped);
            onSuccess?.Invoke(result.items);
        }, onError));
    }

    /// <summary>
    /// Lee el valor actual de un tag del PLC.
    /// </summary>
    /// <param name="tagName">Nombre exacto del tag (ej: "Motor").</param>
    /// <param name="tagType">Tipo del tag (ej: "Bool", "DInt (Int32)").</param>
    /// <param name="onSuccess">Recibe el valor como string (ej: "True", "42").</param>
    /// <param name="onError">Recibe el mensaje de error (opcional).</param>
    public void GetTag(string tagName, string tagType, Action<string> onSuccess, Action<string> onError = null)
    {
        string url = $"{baseUrl}/tags/{UnityWebRequest.EscapeURL(tagName)}?type={UnityWebRequest.EscapeURL(tagType)}";
        StartCoroutine(GetRequest(url, (json) =>
        {
            TagValue result = JsonUtility.FromJson<TagValue>(json);
            onSuccess?.Invoke(result.Value);
        }, onError));
    }

    /// <summary>
    /// Lee el valor de un tag y lo devuelve ya convertido a bool.
    /// Solo para tags de tipo Bool.
    /// </summary>
    public void GetTagBool(string tagName, Action<bool> onSuccess, Action<string> onError = null)
    {
        GetTag(tagName, "Bool", (value) =>
        {
            onSuccess?.Invoke(value.Equals("True", StringComparison.OrdinalIgnoreCase));
        }, onError);
    }

    /// <summary>
    /// Lee el valor de un tag y lo devuelve ya convertido a int.
    /// Para tags de tipo DInt, Int, UDInt, UInt, etc.
    /// </summary>
    public void GetTagInt(string tagName, string tagType, Action<int> onSuccess, Action<string> onError = null)
    {
        GetTag(tagName, tagType, (value) =>
        {
            if (int.TryParse(value, out int intValue))
                onSuccess?.Invoke(intValue);
            else
                HandleError($"No se pudo convertir '{value}' a int para el tag '{tagName}'", onError);
        }, onError);
    }

    /// <summary>
    /// Escribe un valor en un tag del PLC.
    /// </summary>
    /// <param name="tagName">Nombre exacto del tag (ej: "Marcha").</param>
    /// <param name="tagType">Tipo del tag (ej: "Bool").</param>
    /// <param name="value">Valor a escribir como string (ej: "true", "42").</param>
    /// <param name="onSuccess">Recibe el mensaje de confirmación de la API.</param>
    /// <param name="onError">Recibe el mensaje de error (opcional).</param>
    public void SetTag(string tagName, string tagType, string value, Action<string> onSuccess = null, Action<string> onError = null)
    {
        string url = $"{baseUrl}/tags/{UnityWebRequest.EscapeURL(tagName)}";
        TagWritePayload payload = new TagWritePayload { Value = value, Type = tagType };
        string json = JsonUtility.ToJson(payload);

        StartCoroutine(PutRequest(url, json, (responseJson) =>
        {
            ApiMessage result = JsonUtility.FromJson<ApiMessage>(responseJson);
            onSuccess?.Invoke(result.Message);
        }, onError));
    }

    /// <summary>
    /// Escribe un valor bool en un tag del PLC.
    /// Atajo para SetTag con tipo Bool.
    /// </summary>
    public void SetTagBool(string tagName, bool value, Action<string> onSuccess = null, Action<string> onError = null)
    {
        SetTag(tagName, "Bool", value.ToString().ToLower(), onSuccess, onError);
    }

    /// <summary>
    /// Escribe un valor entero en un tag del PLC.
    /// </summary>
    public void SetTagInt(string tagName, string tagType, int value, Action<string> onSuccess = null, Action<string> onError = null)
    {
        SetTag(tagName, tagType, value.ToString(), onSuccess, onError);
    }

    /// <summary>
    /// Obtiene la lista de instancias PLC disponibles.
    /// </summary>
    /// <param name="onSuccess">Recibe la lista de PlcInstance.</param>
    /// <param name="onError">Recibe el mensaje de error (opcional).</param>
    public void GetInstances(Action<List<PlcInstance>> onSuccess, Action<string> onError = null)
    {
        string url = $"{baseUrl}/instances";
        StartCoroutine(GetRequest(url, (json) =>
        {
            string wrapped = "{\"items\":" + json + "}";
            PlcInstanceList result = JsonUtility.FromJson<PlcInstanceList>(wrapped);
            onSuccess?.Invoke(result.items);
        }, onError));
    }

    /// <summary>
    /// Conecta a una instancia PLC por su ID.
    /// Equivale a POST /instances/{id}/connect
    /// </summary>
    /// <param name="instanceId">ID de la instancia a conectar.</param>
    /// <param name="onSuccess">Recibe el mensaje de confirmación de la API.</param>
    /// <param name="onError">Recibe el mensaje de error (opcional).</param>
    public void ConnectInstance(string instanceId, Action<string> onSuccess = null, Action<string> onError = null)
    {
        string url = $"{baseUrl}/instances/{UnityWebRequest.EscapeURL(instanceId)}/connect";
        StartCoroutine(PostRequest(url, "", (responseJson) =>
        {
            ApiMessage result = JsonUtility.FromJson<ApiMessage>(responseJson);
            onSuccess?.Invoke(result.Message);
        }, onError));
    }

    // -- POLLING AUTOMÁTICO -----------------------------------------------------

    // Lista de tags suscritos para polling automático
    private readonly List<(string name, string type, Action<string> callback)> _polledTags = new();

    /// <summary>
    /// Suscribe un tag para ser leído automáticamente cada 'pollInterval' segundos.
    /// Requiere que pollInterval > 0.
    /// </summary>
    public void SubscribeTag(string tagName, string tagType, Action<string> onValue)
    {
        _polledTags.Add((tagName, tagType, onValue));
    }

    /// <summary>
    /// Elimina la suscripción de polling de un tag.
    /// </summary>
    public void UnsubscribeTag(string tagName)
    {
        _polledTags.RemoveAll(t => t.name == tagName);
    }

    private IEnumerator AutoPollCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(pollInterval);
            foreach (var (name, type, callback) in _polledTags)
                GetTag(name, type, callback);
        }
    }

    // -- CORRUTINAS INTERNAS ----------------------------------------------------

    private IEnumerator GetRequest(string url, Action<string> onSuccess, Action<string> onError)
    {
        using UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("Accept", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke(request.downloadHandler.text);
        }
        else
        {
            string error = $"GET {url} → {request.responseCode} {request.error}";
            HandleError(error, onError);
        }
    }

    private IEnumerator PutRequest(string url, string jsonBody, Action<string> onSuccess, Action<string> onError)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        using UnityWebRequest request = new UnityWebRequest(url, "PUT");
        request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept",       "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke(request.downloadHandler.text);
        }
        else
        {
            string error = $"PUT {url} → {request.responseCode} {request.error}";
            HandleError(error, onError);
        }
    }

    private void HandleError(string message, Action<string> localHandler)
    {
        Debug.LogError($"[ApiInterface] {message}");
        localHandler?.Invoke(message);
        OnApiError?.Invoke(message);
    }

    private IEnumerator PostRequest(string url, string jsonBody, Action<string> onSuccess, Action<string> onError)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Accept",       "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke(request.downloadHandler.text);
        }
        else
        {
            string error = $"POST {url} → {request.responseCode} {request.error}";
            HandleError(error, onError);
        }
    }
}