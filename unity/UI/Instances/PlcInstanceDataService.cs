using System;
using System.Collections.Generic;
using UnityEngine;

public class PlcInstanceDataService : MonoBehaviour
{
    public static PlcInstanceDataService Instance { get; private set; }

    // -- Estado interno --
    public List<ApiInterface.PlcInstance> Instances { get; private set; } = new();
    public string StatusMessage { get; private set; } = "Pulsa actualizar para cargar";
    public string ConnectMessage { get; private set; } = "";

    // -- Eventos para avisar a la interfaz --
    public event Action OnInstancesLoaded;
    public event Action OnConnectionStatusChanged;
    public event Action DisconnectionStatusChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }
        Instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Pide la lista de instancias a la API.
    /// </summary>
    public void LoadInstances()
    {
        StatusMessage = "Cargando instancias...";
        OnInstancesLoaded?.Invoke(); 

        ApiInterface.Instance.GetInstances(
            instances =>
            {
                Instances = instances;
                StatusMessage = $"{instances.Count} instancias · {DateTime.Now:HH:mm:ss}";
                OnInstancesLoaded?.Invoke();
            },
            err =>
            {
                StatusMessage = "Error: " + err;
                OnInstancesLoaded?.Invoke();
            }
        );
    }

    /// <summary>
    /// Conecta a una instancia específica.
    /// </summary>
    public void ConnectToInstance(string instanceId)
    {
        ConnectMessage = $"Conectando a '{instanceId}'...";
        OnConnectionStatusChanged?.Invoke();

        ApiInterface.Instance.ConnectInstance(
            instanceId,
            msg => 
            { 
                ConnectMessage = "OK · " + msg;
                OnConnectionStatusChanged?.Invoke();
            },
            err => 
            { 
                ConnectMessage = "Error: " + err;
                OnConnectionStatusChanged?.Invoke();
            }
        );
    }

    /// <summary>
    /// Desconecta la instancia
    /// </summary>
    public void DisconnectInstance()
    {
        ConnectMessage = $"Desconectando de instancia ...";
        DisconnectionStatusChanged?.Invoke();

        ApiInterface.Instance.DisconnectInstance(
            msg => 
            { 
                ConnectMessage = "OK · " + msg;
                DisconnectionStatusChanged?.Invoke();
            },
            err => 
            { 
                ConnectMessage = "Error: " + err;
                DisconnectionStatusChanged?.Invoke();
            }
        );
    }

    /// <summary>
    /// Pone en marcha la instancia
    /// </summary>
    public void Run()
    {
        ConnectMessage = "Poniendo en RUN...";
        OnConnectionStatusChanged?.Invoke();

        ApiInterface.Instance.RunInstance(
            msg =>
            {
                ConnectMessage = "OK · " + msg;
                OnConnectionStatusChanged?.Invoke();
            },
            err =>
            {
                ConnectMessage = "Error: " + err;
                OnConnectionStatusChanged?.Invoke();
            }
        );
    }

    /// <summary>
    /// Para la instancia
    /// </summary>
    public void Stop()
    {
        ConnectMessage = "Poniendo en STOP...";
        OnConnectionStatusChanged?.Invoke();

        ApiInterface.Instance.StopInstance(
            msg =>
            {
                ConnectMessage = "OK · " + msg;
                OnConnectionStatusChanged?.Invoke();
            },
            err =>
            {
                ConnectMessage = "Error: " + err;
                OnConnectionStatusChanged?.Invoke();
            }
        );
    }
}