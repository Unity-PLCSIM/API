//----------------------------------------------------------------------------------------------------------------------
// PANEL TAGS PLC, TIA PORTAL
//
// Desc: Panel de interfaz gráfica para visualizar y modificar tags PLC.
//       Muestra nombre, tipo, valor, dirección (E/S) y permite editar entradas.
//       Las salidas se actualizan por WebSocket (push). Las entradas se modifican via PUT.
//
// Coms:  - Requiere ApiInterface en la escena (Singleton)
//        - Situar el botón junto a otros paneles en la barra superior
//
// Autor: Alex Asensio
// Date: Julio 2026
//----------------------------------------------------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlcTagPanel : MonoBehaviour
{
    // -- Estado interno ---------------------------------------------------------
    private bool _visible = false;

    private class TagData
    {
        public string Name;
        public string Type;
        public string Value;
        public string Area;
        public string EditBuffer;
    }

    private Dictionary<string, TagData> _tags  = new Dictionary<string, TagData>();
    private List<string>                _order = new List<string>();

    private string _status     = "Pulsa 'Tags PLC' para cargar";
    private bool   _loading    = false;

    // -- Estilos GUI ------------------------------------------------------------
    private GUIStyle _styleBox;
    private GUIStyle _styleLabel;
    private GUIStyle _styleHeader;
    private GUIStyle _styleStatus;
    private GUIStyle _styleStatusOk;
    private GUIStyle _styleStatusErr;
    private bool _stylesReady = false;

    // -- Layout -----------------------------------------------------------------
    private Vector2 _scroll;
    private const float RowH      = 24f;
    private const float MaxRows   = 16f;
    private const float HeaderH   = 55f;
    private const float FooterH   = 40f;
    private const float ColPad    = 20f;
    private const float ColType   = 110f;
    private const float ColVal    = 90f;
    private const float ColArea   = 30f;
    private const float ColMod    = 130f;
    private const float MinColName = 80f;

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

    // -- Update -----------------------------------------------------------------

    void Update() { } // [WS] El WebSocket de ApiInterface ya llama a DispatchMessageQueue()

    // -- Layout helpers ---------------------------------------------------------

    float CalcColName()
    {
        float max = MinColName;
        foreach (var kv in _tags)
        {
            float w = _styleLabel.CalcSize(new GUIContent(kv.Value.Name)).x;
            if (w > max) max = w;
        }
        float hw = _styleHeader.CalcSize(new GUIContent("Nombre")).x;
        if (hw > max) max = hw;
        return max + 10f;
    }

    // -- Llamadas API -----------------------------------------------------------

    void CargarTodos()
    {
        _status  = "Cargando...";
        _loading = true;

        ApiInterface.Instance.GetTagsWithValues(
            tags =>
            {
                _tags.Clear();
                _order.Clear();

                foreach (var t in tags)
                {
                    var td = new TagData
                    {
                        Name       = t.Name,
                        Type       = t.Type,
                        Value      = t.Value,
                        Area       = t.Area,
                        EditBuffer = t.Value
                    };
                    _tags[t.Name] = td;
                    _order.Add(t.Name);
                }

                _status  = $"{_tags.Count} tags · " + System.DateTime.Now.ToString("HH:mm:ss");
                _loading = false;
                SuscribirSalidas(); // [WS] sustituye al polling HTTP de salidas
            },
            err =>
            {
                _status  = "Error: " + err;
                _loading = false;
            }
        );
    }

    /// <summary>
    /// Suscribe por WebSocket cada tag de salida conocido.
    /// El servidor empujará el nuevo valor cada vez que cambie;
    /// aquí solo actualizamos el diccionario local para que OnGUI lo pinte.
    /// </summary>
    void SuscribirSalidas()                                        // [WS]
    {
        foreach (string name in _order)
        {
            if (!_tags.TryGetValue(name, out TagData td)) continue;
            if (td.Area != "S") continue;

            // Capturar referencia local para el closure
            TagData tdLocal = td;
            ApiInterface.Instance.SubscribeOutputTag(td.Name, value =>
            {
                tdLocal.Value = value;
            });
        }
    }

    void EscribirEntrada(TagData td, string nuevoValor)
    {
        ApiInterface.Instance.SetTag(
            td.Name, td.Type, nuevoValor,
            msg =>
            {
                td.Value      = nuevoValor;
                td.EditBuffer = nuevoValor;
                // [WS] El WebSocket notificará el cambio de salidas automáticamente
            },
            err => _status = "Error escritura: " + err
        );
    }

    // -- GUI --------------------------------------------------------------------

    void OnGUI()
    {
        InitStyles();

        float colName = _tags.Count > 0 ? CalcColName() : MinColName;
        float panW    = colName + ColType + ColVal + ColArea + ColMod + ColPad + 20f;
        float listH   = Mathf.Min(_tags.Count > 0 ? _tags.Count : 1, MaxRows) * RowH;
        float panH    = HeaderH + listH + FooterH;

        // Botón — a la derecha del panel de instancias (130 + 110 = 240)
        if (GUI.Button(new Rect(250, 10, 100, 24), _visible ? "Ocultar Tags" : "Tags PLC"))
        {
            _visible = !_visible;
            if (_visible && _tags.Count == 0)
                CargarTodos();
        }

        if (!_visible) return;

        float panX = 10f;
        float panY = 40f;

        GUI.Box(new Rect(panX, panY, panW, panH), GUIContent.none, _styleBox);
        GUILayout.BeginArea(new Rect(panX + 8, panY + 8, panW - 16, panH - 16));

        // -- Barra superior --
        GUILayout.BeginHorizontal();
        GUILayout.Label(_status, _styleStatus);
        if (GUILayout.Button("Actualizar", GUILayout.Width(90)))
            CargarTodos();
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // -- Cabecera --
        GUILayout.BeginHorizontal();
        GUILayout.Label("Nombre",    _styleHeader, GUILayout.Width(colName));
        GUILayout.Label("Tipo",      _styleHeader, GUILayout.Width(ColType));
        GUILayout.Label("Valor",     _styleHeader, GUILayout.Width(ColVal));
        GUILayout.Label("E/S",       _styleHeader, GUILayout.Width(ColArea));
        GUILayout.Label("Modificar", _styleHeader, GUILayout.Width(ColMod));
        GUILayout.EndHorizontal();

        // -- Filas --
        _scroll = GUILayout.BeginScrollView(_scroll, false, true,
            GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(listH));

        foreach (string name in _order)
        {
            if (!_tags.TryGetValue(name, out TagData td)) continue;

            GUILayout.BeginHorizontal();
            GUILayout.Label(td.Name,  _styleLabel, GUILayout.Width(colName));
            GUILayout.Label(td.Type,  _styleLabel, GUILayout.Width(ColType));
            GUILayout.Label(td.Value, _styleLabel, GUILayout.Width(ColVal));
            GUILayout.Label(td.Area,  _styleLabel, GUILayout.Width(ColArea));

            // -- Columna Modificar --
            if (td.Area == "E")
            {
                if (td.Type == "Bool")
                {
                    bool estaActivo = td.Value.ToLower() == "true" || td.Value == "1";
                    GUI.backgroundColor = estaActivo
                        ? new Color(0.1f, 0.7f, 0.2f)
                        : new Color(0.7f, 0.1f, 0.1f);

                    if (GUILayout.Button(estaActivo ? "TRUE" : "FALSE", GUILayout.Width(ColMod)))
                        EscribirEntrada(td, estaActivo ? "false" : "true");

                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.SetNextControlName("edit_" + name);
                    td.EditBuffer = GUILayout.TextField(td.EditBuffer, GUILayout.Width(ColMod - 50));

                    if (GUILayout.Button("OK", GUILayout.Width(40)))
                        EscribirEntrada(td, td.EditBuffer);
                }
            }
            else
            {
                GUILayout.Label("—", _styleLabel, GUILayout.Width(ColMod));
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        GUILayout.Space(4);
        GUILayout.Label(_loading ? "Actualizando..." : "", _styleStatus);

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