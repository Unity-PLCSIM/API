//----------------------------------------------------------------------------------------------------------------------
// PANEL INSTANCIAS PLC, TIA PORTAL
//
// Desc: Panel de interfaz gráfica para listar y conectarse a instancias PLC disponibles
//       a través de ApiInterface. Muestra un botón en la esquina superior izquierda que
//       despliega el panel con la lista de instancias y permite conectarse a cada una.
//
// Coms:  - Requiere ApiInterface en la escena (Singleton)
//        - Situar el botón junto a otros paneles en la barra superior
//
// Uso:
//   Añadir este script a cualquier GameObject activo en la escena.
//   El botón "Instancias" aparecerá en la esquina superior izquierda de la pantalla.
//
// Autor:
// Date:
//----------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

public class PlcInstancePanel : MonoBehaviour
{
    // -- Estado interno ---------------------------------------------------------
    private List<ApiInterface.PlcInstance> _instances = new();
    private string _status        = "Pulsa 'Instancias' para cargar";
    private string _connectStatus = "";
    private Vector2 _scroll;
    private bool _visible = false;

    // -- Estilos GUI ------------------------------------------------------------
    private GUIStyle _styleBox;
    private GUIStyle _styleLabel;
    private GUIStyle _styleHeader;
    private GUIStyle _styleStatus;
    private GUIStyle _styleStatusOk;
    private GUIStyle _styleStatusErr;
    private bool _stylesReady = false;

    // -- Layout -----------------------------------------------------------------
    private const float RowH      = 26f;   // altura de cada fila de instancia
    private const float MaxRows   = 8f;    // máximo de filas antes de hacer scroll
    private const float HeaderH   = 60f;   // barra superior + cabecera + espacios
    private const float FooterH   = 40f;   // estado de conexión + espacios
    private const float ColBtn    = 80f;   // anchura del botón conectar
    private const float ColPad    = 32f;   // padding horizontal interno del panel
    private const float MinColName = 80f;  // anchura mínima de la columna nombre

    // -- Estilos ----------------------------------------------------------------

    void InitStyles()
    {
        if (_stylesReady) return;

        _styleBox = new GUIStyle(GUI.skin.box);
        _styleBox.normal.background = MakeTexture(Color.black);
        _styleBox.padding = new RectOffset(8, 8, 8, 8);

        _styleLabel = new GUIStyle(GUI.skin.label);
        _styleLabel.normal.textColor = Color.white;
        _styleLabel.fontSize = 12;

        _styleHeader = new GUIStyle(_styleLabel);
        _styleHeader.fontStyle = FontStyle.Bold;
        _styleHeader.normal.textColor = new Color(0.4f, 1f, 0.7f);

        _styleStatus = new GUIStyle(_styleLabel);
        _styleStatus.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

        _styleStatusOk = new GUIStyle(_styleLabel);
        _styleStatusOk.normal.textColor = new Color(0.2f, 0.9f, 0.4f);
        _styleStatusOk.fontStyle = FontStyle.Bold;

        _styleStatusErr = new GUIStyle(_styleLabel);
        _styleStatusErr.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
        _styleStatusErr.fontStyle = FontStyle.Bold;

        _stylesReady = true;
    }

    // -- Utilidades de layout ---------------------------------------------------

    /// <summary>
    /// Calcula la anchura de la columna nombre ajustada al texto más largo de la lista.
    /// </summary>
    float CalcColName()
    {
        float max = MinColName;
        foreach (var inst in _instances)
        {
            float w = _styleLabel.CalcSize(new GUIContent(inst.Name)).x;
            if (w > max) max = w;
        }
        // Comparar también con la cabecera
        float headerW = _styleHeader.CalcSize(new GUIContent("Nombre")).x;
        if (headerW > max) max = headerW;
        return max + 10f; // margen
    }

    // -- Llamadas API -----------------------------------------------------------

    /// <summary>
    /// Solicita a ApiInterface la lista de instancias PLC disponibles y actualiza el panel.
    /// </summary>
    void CargarInstancias()
    {
        _status = "Cargando...";
        _connectStatus = "";
        ApiInterface.Instance.GetInstances(
            instances =>
            {
                _instances = instances;
                _status = $"{instances.Count} instancias · " + System.DateTime.Now.ToString("HH:mm:ss");
                foreach (var i in instances) Debug.Log(i.ID + " - " + i.Name);
            },
            err =>
            {
                _status = "Error: " + err;
                Debug.LogError(err);
            }
        );
    }

    /// <summary>
    /// Solicita a ApiInterface conectarse a la instancia PLC con el ID indicado.
    /// </summary>
    /// <param name="instanceId">ID de la instancia a conectar.</param>
    void Conectar(string instanceId)
    {
        _connectStatus = $"Conectando a '{instanceId}'...";
        ApiInterface.Instance.ConnectInstance(
            instanceId,
            msg => { _connectStatus = "OK · " + msg;   Debug.Log("Conectado: " + msg); },
            err => { _connectStatus = "Error: " + err; Debug.LogError(err); }
        );
    }

    // -- GUI --------------------------------------------------------------------

    void OnGUI()
    {
        InitStyles();

        // Anchura dinámica ajustada al nombre más largo
        float colName = CalcColName();
        float panW    = colName + ColBtn + ColPad;

        // Altura dinámica según número de instancias, con máximo en MaxRows
        float listH = Mathf.Min(_instances.Count, MaxRows) * RowH;
        float panH  = HeaderH + listH + FooterH;

        // Botón superior, a la derecha del botón de PlcTagTable
        if (GUI.Button(new Rect(130, 10, 110, 24), _visible ? "Ocultar panel" : "Instancias"))
        {
            _visible = !_visible;
            if (_visible && _instances.Count == 0)
                CargarInstancias();
        }

        if (!_visible) return;

        // Caja de fondo del panel, tamaño ajustado al contenido
        GUI.Box(new Rect(10, 40, panW, panH), GUIContent.none, _styleBox);

        GUILayout.BeginArea(new Rect(18, 48, panW - 16, panH - 8));

        // -- Barra superior: estado + botón actualizar --
        GUILayout.BeginHorizontal();
        GUILayout.Label(_status, _styleStatus);
        if (GUILayout.Button("Actualizar", GUILayout.Width(90)))
            CargarInstancias();
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // -- Cabecera de columnas (sin ID) --
        GUILayout.BeginHorizontal();
        GUILayout.Label("Nombre", _styleHeader, GUILayout.Width(colName));
        GUILayout.Label("",                     GUILayout.Width(ColBtn));
        GUILayout.EndHorizontal();

        // -- Lista de instancias con scroll vertical únicamente --
        _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(listH));
        foreach (var inst in _instances)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(inst.Name, _styleLabel, GUILayout.Width(colName));  // ID oculto, usado solo en Conectar
            if (GUILayout.Button("Conectar", GUILayout.Width(ColBtn)))
                Conectar(inst.ID.ToString());
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        GUILayout.Space(6);

        // -- Estado de la última conexión --
        if (!string.IsNullOrEmpty(_connectStatus))
        {
            bool esError = _connectStatus.StartsWith("Error");
            GUILayout.Label(_connectStatus, esError ? _styleStatusErr : _styleStatusOk);
        }

        GUILayout.EndArea();
    }

    // -- Utilidades -------------------------------------------------------------

    Texture2D MakeTexture(Color col)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, col);
        tex.Apply();
        return tex;
    }
}