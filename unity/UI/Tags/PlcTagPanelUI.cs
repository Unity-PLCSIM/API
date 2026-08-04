//----------------------------------------------------------------------------------------------------------------------
// PLC TAG PANEL UI
//
// Desc: Panel flotante arrastrable y redimensionable para tags PLC.
//       - Altura del ListView: calculada una sola vez tras GeometryChangedEvent
//         cuando el tamaño estabiliza (sin Rebuild dentro del evento).
//       - Fuente escalable: se recalcula solo al soltar el resize (MouseUp),
//         no en cada frame de drag.
//
// Ubicación: Assets/Scripts/UI/PlcTagPanelUI.cs
// Autor: Alex Asensio
// Date:  Agosto 2026
//----------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class PlcTagPanelUI : MonoBehaviour
{
    // -- Referencias ------------------------------------------------------------

    private UIDocument    _doc;
    private VisualElement _root;

    // -- Elementos --------------------------------------------------------------

    private Button        _btnToggle;
    private Button        _btnRefresh;
    private Label         _lblStatus;
    private VisualElement _panel;
    private VisualElement _titleBar;
    private VisualElement _header;
    private ListView      _listView;
    private VisualElement _resizeHandle;
    private TextField _searchField;
    private string    _currentSearch = "";

    // -- Drag & Resize ----------------------------------------------------------

    private bool    _dragging;
    private bool    _resizing;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartPos;
    private Vector2 _resizeStartMouse;
    private Vector2 _resizeStartSize;

    private const float MinW = 400f;
    private const float MinH = 200f;
    private const float InitW = 640f;
    private const float InitH = 420f;

    // Fuente
    private const float FontMin   = 9f;
    private const float FontMax   = 16f;
    private const float PanelWMin = 400f;
    private const float PanelWMax = 1200f;

    // -- Datos ListView ---------------------------------------------------------

    private readonly List<PlcTagTableBuilder.RowData> _rows = new();

    // -- Paleta -----------------------------------------------------------------

    private static readonly Color ColBg     = new Color(0.08f, 0.09f, 0.10f, 1f);
    private static readonly Color ColBorder = new Color(0.20f, 0.22f, 0.24f, 1f);
    private static readonly Color ColAccent = new Color(0.25f, 0.85f, 0.55f, 1f);
    private static readonly Color ColText   = new Color(0.88f, 0.88f, 0.88f, 1f);
    private static readonly Color ColErr    = new Color(0.90f, 0.30f, 0.30f, 1f);
    private static readonly Color ColMuted  = new Color(0.50f, 0.52f, 0.54f, 1f);
    private static readonly Color ColTopBar = new Color(0.11f, 0.12f, 0.14f, 1f);

    // -- Unity lifecycle --------------------------------------------------------

    void Awake()    => _doc = GetComponent<UIDocument>();
    void OnEnable() => BuildUI();
    void Start()    => SubscribeToService();
    void OnDisable() => UnsubscribeFromService();

    // -- Construcción de UI -----------------------------------------------------

    void BuildUI()
    {
        _root = _doc.rootVisualElement;
        _root.Clear();
        _root.style.flexDirection = FlexDirection.Column;
        _root.style.alignItems    = Align.FlexStart;
        _root.style.paddingTop    = 8f;
        _root.style.paddingLeft   = 8f;

        // Botón toggle
        _btnToggle = new Button(OnToggleClicked) { text = "Tags PLC" };
        StyleTopButton(_btnToggle);
        _root.Add(_btnToggle);

        // Panel flotante
        _panel = new VisualElement();
        _panel.style.display         = DisplayStyle.None;
        _panel.style.position        = Position.Absolute;
        _panel.style.left            = 8f;
        _panel.style.top             = 40f;
        _panel.style.width           = InitW;
        _panel.style.height          = InitH;
        _panel.style.backgroundColor = ColBg;
        _panel.style.flexDirection   = FlexDirection.Column;
        _panel.style.overflow        = Overflow.Hidden;
        ApplyBorder(_panel, ColBorder, 1f, 4f);

        // Title bar
        _titleBar = new VisualElement { name = "title-bar" };
        _titleBar.style.flexDirection     = FlexDirection.Row;
        _titleBar.style.alignItems        = Align.Center;
        _titleBar.style.paddingLeft       = 10f;
        _titleBar.style.paddingRight      = 10f;
        _titleBar.style.paddingTop        = 7f;
        _titleBar.style.paddingBottom     = 7f;
        _titleBar.style.backgroundColor   = ColTopBar;
        _titleBar.style.borderBottomWidth = 1f;
        _titleBar.style.borderBottomColor = ColBorder;
        _titleBar.style.borderTopLeftRadius  = 4f;
        _titleBar.style.borderTopRightRadius = 4f;
        _titleBar.style.flexShrink        = 0f;

        var titleLabel = new Label("Panel Tags PLC");
        titleLabel.style.color                   = ColAccent;
        titleLabel.style.fontSize                = 13f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.flexGrow                = 1f;
        _titleBar.Add(titleLabel);

        _searchField = new TextField { name = "search-field" };
        _searchField.style.width       = 140f;
        _searchField.style.height      = 22f;
        _searchField.style.marginRight = 10f;
        
        // Limpiar el contenedor exterior de Unity
        _searchField.style.backgroundColor = Color.clear;
        _searchField.style.borderTopWidth  = 0f; _searchField.style.borderBottomWidth = 0f;
        _searchField.style.borderLeftWidth = 0f; _searchField.style.borderRightWidth  = 0f;

        // Estilar el input interno cuando se adjunta al panel
        _searchField.RegisterCallback<AttachToPanelEvent>(e => 
        {
            var input = _searchField.Q(className: "unity-text-field__input");
            if (input == null) return;
            input.style.backgroundColor = new Color(0.18f, 0.19f, 0.21f, 1f);
            input.style.color           = ColText;
            input.style.borderTopLeftRadius     = 3f; input.style.borderTopRightRadius    = 3f;
            input.style.borderBottomLeftRadius  = 3f; input.style.borderBottomRightRadius = 3f;
            input.style.borderTopColor    = ColBorder; input.style.borderBottomColor = ColBorder;
            input.style.borderLeftColor   = ColBorder; input.style.borderRightColor  = ColBorder;
            input.style.borderTopWidth    = 1f; input.style.borderBottomWidth = 1f;
            input.style.borderLeftWidth   = 1f; input.style.borderRightWidth  = 1f;
            input.style.paddingTop        = 0f; input.style.paddingBottom = 0f;
        });

        // Evento: Filtrar cada vez que el usuario teclea algo
        _searchField.RegisterValueChangedCallback(evt => {
            _currentSearch = evt.newValue?.ToLowerInvariant() ?? "";
            RefreshListFromService();
        });
        
        _titleBar.Add(_searchField);

        _lblStatus = new Label(PlcTagDataService.Instance?.StatusMessage ?? "");
        _lblStatus.style.color          = ColMuted;
        _lblStatus.style.fontSize       = 10f;
        _lblStatus.style.flexGrow       = 1f;
        _lblStatus.style.unityTextAlign = TextAnchor.MiddleRight;
        _titleBar.Add(_lblStatus);

        _btnRefresh = new Button(OnRefreshClicked) { text = "↺  Actualizar" };
        StyleTopButton(_btnRefresh, compact: true);
        _btnRefresh.style.marginLeft = 10f;
        _titleBar.Add(_btnRefresh);
        _panel.Add(_titleBar);

        // --- ARREGLO BOTÓN OK (Inicio): Restamos 36f para scroll y padding ---
        PlcTagTableBuilder.SetPanelWidth(InitW - 55f);

        // Cabecera columnas
        _header = PlcTagTableBuilder.BuildHeader();
        _header.style.paddingLeft  = 10f;
        _header.style.paddingRight = 10f;
        _header.style.flexShrink   = 0f;
        _panel.Add(_header);

        // ListView — La altura ahora la gestiona Flexbox
        _listView = PlcTagTableBuilder.Build(_rows, OnWriteRequested);
        _listView.style.paddingLeft  = 2f;
        _listView.style.paddingRight = 2f;
        _panel.Add(_listView);

        // Resize handle
        _resizeHandle = new VisualElement { name = "resize-handle" };
        _resizeHandle.style.position = Position.Absolute;
        _resizeHandle.style.right    = 0f;
        _resizeHandle.style.bottom   = 0f;
        _resizeHandle.style.width    = 20f;
        _resizeHandle.style.height   = 20f;
        var resizeLabel = new Label("⇲");
        resizeLabel.style.color          = ColMuted;
        resizeLabel.style.fontSize       = 14f;
        resizeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        resizeLabel.style.width          = 20f;
        resizeLabel.style.height         = 20f;
        _resizeHandle.Add(resizeLabel);
        _panel.Add(_resizeHandle);

        _root.Add(_panel);

        RegisterDragAndResize();
    }

    // -- Altura del ListView ----------------------------------------------------

    // Se llama solo cuando el usuario SUELTA el resize (no en cada frame de drag)
    void OnResizeFinished()
    {
        float panelW = _panel.resolvedStyle.width;

        // Fuente proporcional al ancho
        float t        = Mathf.InverseLerp(PanelWMin, PanelWMax, panelW);
        float fontSize = Mathf.Round(Mathf.Lerp(FontMin, FontMax, t));
        
        // --- ARREGLO BOTÓN OK (Resize): Restamos 36f para scroll y padding ---
        PlcTagTableBuilder.SetPanelWidth(panelW - 55f);
        PlcTagTableBuilder.SetFontSize(fontSize);

        // --- ARREGLO REBUILD: Actualizamos el itemHeight del ListView ---
        _listView.fixedItemHeight = Mathf.Max(24f, fontSize * 2.4f);

        // Rebuild solo aquí, una vez por resize completo
        _listView.Rebuild();
    }

    // -- Drag & Resize ----------------------------------------------------------

    void RegisterDragAndResize()
    {
        // DRAG — solo mueve, no recalcula nada
        _titleBar.RegisterCallback<MouseDownEvent>(e =>
        {
            if (e.button != 0) return;
            _dragging       = true;
            _dragStartMouse = e.mousePosition;
            _dragStartPos   = new Vector2(_panel.style.left.value.value, _panel.style.top.value.value);
            _titleBar.CaptureMouse();
            e.StopPropagation();
        });
        _titleBar.RegisterCallback<MouseMoveEvent>(e =>
        {
            if (!_dragging) return;
            Vector2 d         = (Vector2)e.mousePosition - _dragStartMouse;
            _panel.style.left = Mathf.Max(0, _dragStartPos.x + d.x);
            _panel.style.top  = Mathf.Max(0, _dragStartPos.y + d.y);
            e.StopPropagation();
        });
        _titleBar.RegisterCallback<MouseUpEvent>(e =>
        {
            if (!_dragging) return;
            _dragging = false;
            _titleBar.ReleaseMouse();
            e.StopPropagation();
        });

        // RESIZE — cambia tamaño en drag, recalcula layout solo en MouseUp
        _resizeHandle.RegisterCallback<MouseDownEvent>(e =>
        {
            if (e.button != 0) return;
            _resizing         = true;
            _resizeStartMouse = e.mousePosition;
            _resizeStartSize  = new Vector2(_panel.style.width.value.value, _panel.style.height.value.value);
            _resizeHandle.CaptureMouse();
            e.StopPropagation();
        });
        _resizeHandle.RegisterCallback<MouseMoveEvent>(e =>
        {
            if (!_resizing) return;
            Vector2 d           = (Vector2)e.mousePosition - _resizeStartMouse;
            _panel.style.width  = Mathf.Max(MinW, _resizeStartSize.x + d.x);
            _panel.style.height = Mathf.Max(MinH, _resizeStartSize.y + d.y);
            // Solo actualizar altura del listview durante drag (barato, sin Rebuild)
            e.StopPropagation();
        });
        _resizeHandle.RegisterCallback<MouseUpEvent>(e =>
        {
            if (!_resizing) return;
            _resizing = false;
            _resizeHandle.ReleaseMouse();
            // Rebuild con fuente actualizada solo al soltar
            OnResizeFinished();
            e.StopPropagation();
        });
    }

    // -- Eventos de UI ----------------------------------------------------------

    void OnToggleClicked()
    {
        bool visible         = _panel.style.display == DisplayStyle.Flex;
        _panel.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
        _btnToggle.text      = visible ? "Tags PLC" : "Ocultar Tags";
        if (!visible && _rows.Count == 0)
            PlcTagDataService.Instance.Load();
    }

    void OnRefreshClicked() => PlcTagDataService.Instance.Load();
    void OnWriteRequested(string tagName, string newValue) =>
        PlcTagDataService.Instance.WriteInput(tagName, newValue);

    // -- Suscripción al servicio ------------------------------------------------

    void SubscribeToService()
    {
        var svc = PlcTagDataService.Instance;
        if (svc == null) { Debug.LogError("[PlcTagPanelUI] PlcTagDataService.Instance es null en Start()"); return; }
        svc.OnTagsLoaded    += HandleTagsLoaded;
        svc.OnTagUpdated    += HandleTagUpdated;
        svc.OnStatusChanged += HandleStatusChanged;
    }

    void UnsubscribeFromService()
    {
        var svc = PlcTagDataService.Instance;
        if (svc == null) return;
        svc.OnTagsLoaded    -= HandleTagsLoaded;
        svc.OnTagUpdated    -= HandleTagUpdated;
        svc.OnStatusChanged -= HandleStatusChanged;
    }

    // -- Handlers del servicio --------------------------------------------------

    void HandleTagsLoaded(IReadOnlyList<string> order)
    {
        RefreshListFromService();
    }

    void RefreshListFromService()
    {
        var svc = PlcTagDataService.Instance;
        if (svc == null) return;

        _rows.Clear();
        foreach (string name in svc.Order)
        {
            if (!svc.Tags.TryGetValue(name, out var td)) continue;
            
            // Si hay algo escrito en el buscador, comprobamos si el nombre coincide
            if (!string.IsNullOrEmpty(_currentSearch) && 
                !name.ToLowerInvariant().Contains(_currentSearch))
            {
                continue; // No coincide, nos lo saltamos
            }

            _rows.Add(new PlcTagTableBuilder.RowData
            {
                Name       = td.Name,
                Type       = td.Type,
                Value      = td.Value,
                Area       = td.Area,
                EditBuffer = td.Value,
            });
        }
        _listView?.Rebuild();
    }

    void HandleTagUpdated(string tagName, string newValue)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Name != tagName) continue;
            _rows[i].Value = newValue;
            if (_rows[i].Area == "S" || _rows[i].Type == "Bool")
                _rows[i].EditBuffer = newValue;
            _listView?.RefreshItem(i);
            break;
        }
    }

    void HandleStatusChanged(string message, bool isError)
    {
        if (_lblStatus == null) return;
        _lblStatus.text        = message;
        _lblStatus.style.color = isError ? ColErr : ColMuted;
    }

    // -- Helpers de estilo ------------------------------------------------------

    private static void ApplyBorder(VisualElement el, Color color, float width, float radius)
    {
        el.style.borderTopColor    = color; el.style.borderBottomColor = color;
        el.style.borderLeftColor   = color; el.style.borderRightColor  = color;
        el.style.borderTopWidth    = width; el.style.borderBottomWidth = width;
        el.style.borderLeftWidth   = width; el.style.borderRightWidth  = width;
        el.style.borderTopLeftRadius     = radius; el.style.borderTopRightRadius    = radius;
        el.style.borderBottomLeftRadius  = radius; el.style.borderBottomRightRadius = radius;
    }

    private static void StyleTopButton(Button b, bool compact = false)
    {
        b.style.height       = compact ? 22f : 26f;
        b.style.paddingLeft  = compact ? 8f  : 12f;
        b.style.paddingRight = compact ? 8f  : 12f;
        b.style.backgroundColor = new Color(0.15f, 0.16f, 0.18f, 1f);
        b.style.color           = ColText;
        b.style.fontSize        = compact ? 11f : 12f;
        ApplyBorder(b, new Color(0.28f, 0.30f, 0.33f, 1f), 1f, 4f);
    }
}