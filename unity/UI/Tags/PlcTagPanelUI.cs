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
using System.Linq; 
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

    // -- Elementos Custom Tables -- 
    private DropdownField _tableDropdown;
    private Button        _btnAddTable;
    private Button        _btnDelTable;
    private TextField     _newTableField;
    private string        _activeTableName = "Principal";
    private VisualElement _contextMenu;

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

        // --- Controles Custom Tables ---
        _tableDropdown = new DropdownField();
        _tableDropdown.style.width = 120f;
        _tableDropdown.style.height = 22f;
        StyleDropdownField(_tableDropdown); // Estilo corregido aplicado
        _tableDropdown.RegisterValueChangedCallback(e => {
            _activeTableName = e.newValue;
            _btnDelTable.style.display = _activeTableName == "Principal" ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshListFromService();
        });
        _titleBar.Add(_tableDropdown);

        _btnAddTable = new Button(ShowNewTableInput) { text = "+" };
        StyleTopButton(_btnAddTable, compact: true);
        _btnAddTable.style.marginLeft = 4f;
        _titleBar.Add(_btnAddTable);

        _btnDelTable = new Button(() => PlcTagDataService.Instance.DeleteCustomTable(_activeTableName)) { text = "-" };
        StyleTopButton(_btnDelTable, compact: true);
        _btnDelTable.style.marginLeft = 4f;
        _btnDelTable.style.display = DisplayStyle.None;
        _titleBar.Add(_btnDelTable);

        _newTableField = new TextField();
        _newTableField.style.width = 100f;
        _newTableField.style.height = 22f; 
        _newTableField.style.marginLeft = 4f;
        _newTableField.style.display = DisplayStyle.None;
        StyleInnerField(_newTableField, "unity-text-field__input"); 
        _newTableField.RegisterCallback<KeyDownEvent>(e => {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) {
                PlcTagDataService.Instance.CreateCustomTable(_newTableField.value);
                _newTableField.style.display = DisplayStyle.None;
                _activeTableName = _newTableField.value; 
            }
            if (e.keyCode == KeyCode.Escape) _newTableField.style.display = DisplayStyle.None;
        });
        _titleBar.Add(_newTableField);
        // -------------------------------- //

        _searchField = new TextField { name = "search-field" };
        _searchField.style.width       = 140f;
        _searchField.style.height      = 22f;
        _searchField.style.marginRight = 10f;
        
        StyleInnerField(_searchField, "unity-text-field__input");

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
        _listView = PlcTagTableBuilder.Build(_rows, OnWriteRequested, OnRowMenuClick);
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

        // --- Menú contextual flotante custom --- 
        _contextMenu = new VisualElement { name = "custom-context-menu" };
        _contextMenu.style.position = Position.Absolute;
        _contextMenu.style.display = DisplayStyle.None;
        _contextMenu.style.backgroundColor = ColTopBar;
        ApplyBorder(_contextMenu, ColBorder, 1f, 4f);
        _root.Add(_contextMenu);

        // Ocultar si hacemos clic fuera
        _root.RegisterCallback<PointerDownEvent>(e => {
            if (_contextMenu.style.display == DisplayStyle.Flex && e.target != _contextMenu && !_contextMenu.Contains(e.target as VisualElement))
                _contextMenu.style.display = DisplayStyle.None;
        });

        RegisterDragAndResize();
    }

    // -- Helpers Custom Tables -------------------------------------------------- 
    private void ShowNewTableInput()
    {
        _newTableField.style.display = DisplayStyle.Flex;
        _newTableField.value = "";
        _newTableField.Focus();
    }
    
    private void UpdateDropdownOptions()
    {
        var choices = new List<string> { "Principal" };
        choices.AddRange(PlcTagDataService.Instance.CustomTables.Select(t => t.Name));
        _tableDropdown.choices = choices;
        if (!choices.Contains(_activeTableName)) _activeTableName = "Principal";
        _tableDropdown.SetValueWithoutNotify(_activeTableName);
        if (_btnDelTable != null) 
            _btnDelTable.style.display = _activeTableName == "Principal" ? DisplayStyle.None : DisplayStyle.Flex;
    }

    // -- Altura del ListView ----------------------------------------------------

    // Se llama solo cuando el usuario SUELTA el resize (no en cada frame de drag)
    void OnResizeFinished()
    {
        float panelW = _panel.resolvedStyle.width;
        float t        = Mathf.InverseLerp(PanelWMin, PanelWMax, panelW);
        float fontSize = Mathf.Round(Mathf.Lerp(FontMin, FontMax, t));
        
        PlcTagTableBuilder.SetPanelWidth(panelW - 55f);
        PlcTagTableBuilder.SetFontSize(fontSize);
        PlcTagTableBuilder.AdjustColumnWidths(_rows);  // recalcular con nuevo ancho
        PlcTagTableBuilder.RefreshHeader(_header);      // actualizar header

        _listView.fixedItemHeight = Mathf.Max(24f, fontSize * 2.4f);
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
        else if (visible) 
            UpdateDropdownOptions(); 
    }

    void OnRefreshClicked() => PlcTagDataService.Instance.Load();
    void OnWriteRequested(string tagName, string newValue) =>
        PlcTagDataService.Instance.WriteInput(tagName, newValue);

    // --- Manejo del Clic del Menú del Botón --- 
    void OnRowMenuClick(string tagName, VisualElement anchor)
    {
        _contextMenu.Clear();
        _contextMenu.style.display = DisplayStyle.Flex;
        
        Vector2 localPos = _root.WorldToLocal(anchor.worldBound.position);
        _contextMenu.style.left = localPos.x;
        _contextMenu.style.top = localPos.y + anchor.worldBound.height + 2f; 

        var svc = PlcTagDataService.Instance;

        if (_activeTableName == "Principal")
        {
            if (svc.CustomTables.Count == 0)
            {
                _contextMenu.Add(new Label("Crea una tabla nueva") { style = { color = ColMuted, paddingBottom = 6, paddingTop = 6, paddingLeft = 10, paddingRight = 10, fontSize = 11f } });
                return;
            }
            foreach (var table in svc.CustomTables)
            {
                string tName = table.Name;
                bool alreadyIn = table.Tags.Contains(tagName);
                var btn = new Button(() => {
                    if (!alreadyIn) svc.AddTagToTable(tName, tagName);
                    _contextMenu.style.display = DisplayStyle.None;
                }) { text = alreadyIn ? $"✓ En {tName}" : $"Añadir a -> {tName}" };
                StyleTopButton(btn, compact:true);
                btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 0f;
                if (alreadyIn) btn.style.color = ColMuted;
                _contextMenu.Add(btn);
            }
        }
        else
        {
            string act = _activeTableName; 
            var btn = new Button(() => {
                svc.RemoveTagFromTable(act, tagName);
                _contextMenu.style.display = DisplayStyle.None;
            }) { text = $"Quitar de '{act}'" };
            StyleTopButton(btn, compact:true);
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 0f;
            _contextMenu.Add(btn);
        }
    }

    // -- Suscripción al servicio ------------------------------------------------

    void SubscribeToService()
    {
        var svc = PlcTagDataService.Instance;
        if (svc == null) { Debug.LogError("[PlcTagPanelUI] PlcTagDataService.Instance es null en Start()"); return; }
        svc.OnTagsLoaded    += HandleTagsLoaded;
        svc.OnTagUpdated    += HandleTagUpdated;
        svc.OnStatusChanged += HandleStatusChanged;
        svc.OnCustomTablesChanged += HandleCustomTablesChanged; 
    }

    void UnsubscribeFromService()
    {
        var svc = PlcTagDataService.Instance;
        if (svc == null) return;
        svc.OnTagsLoaded    -= HandleTagsLoaded;
        svc.OnTagUpdated    -= HandleTagUpdated;
        svc.OnStatusChanged -= HandleStatusChanged;
        svc.OnCustomTablesChanged -= HandleCustomTablesChanged; 
    }

    // -- Handlers del servicio --------------------------------------------------

    void HandleTagsLoaded(IReadOnlyList<string> order)
    {
        UpdateDropdownOptions(); 
        RefreshListFromService();
    }

    void HandleCustomTablesChanged() 
    {
        UpdateDropdownOptions();
        RefreshListFromService();
    }

    void RefreshListFromService()
    {
        var svc = PlcTagDataService.Instance;
        if (svc == null) return;

        _rows.Clear();
        
        // Decidir qué tags mostrar
        IEnumerable<string> tagsToDisplay = _activeTableName == "Principal" 
            ? svc.Order 
            : svc.CustomTables.FirstOrDefault(t => t.Name == _activeTableName)?.Tags ?? new List<string>();

        foreach (string name in tagsToDisplay)
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
        PlcTagTableBuilder.AdjustColumnWidths(_rows);
        PlcTagTableBuilder.RefreshHeader(_header);
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

    private static void StyleDropdownField(DropdownField dd) 
    {
        // Limpiamos los márgenes y bordes exteriores
        dd.style.backgroundColor = Color.clear;
        dd.style.borderTopWidth  = 0f; dd.style.borderBottomWidth = 0f;
        dd.style.borderLeftWidth = 0f; dd.style.borderRightWidth  = 0f;
        dd.style.marginTop       = 0f; dd.style.marginBottom      = 0f;
        dd.style.paddingTop      = 0f; dd.style.paddingBottom     = 0f;
        dd.style.paddingLeft     = 0f; dd.style.paddingRight      = 0f;

        dd.RegisterCallback<AttachToPanelEvent>(e => 
        {
            var input = dd.Q(className: "unity-base-popup-field__input");
            if (input == null) return;
            
            input.style.backgroundColor = new Color(0.18f, 0.19f, 0.21f, 1f);
            input.style.color           = ColText;
            input.style.borderTopLeftRadius     = 3f; input.style.borderTopRightRadius    = 3f;
            input.style.borderBottomLeftRadius  = 3f; input.style.borderBottomRightRadius = 3f;
            input.style.borderTopColor    = ColBorder; input.style.borderBottomColor = ColBorder;
            input.style.borderLeftColor   = ColBorder; input.style.borderRightColor  = ColBorder;
            input.style.borderTopWidth    = 1f; input.style.borderBottomWidth = 1f;
            input.style.borderLeftWidth   = 1f; input.style.borderRightWidth  = 1f;
            
            // Fix para evitar corte: limpiar márgenes y paddings del input
            input.style.marginTop = 0f; input.style.marginBottom = 0f;
            input.style.paddingTop = 0f; input.style.paddingBottom = 0f;
            
            // Buscar el texto (label interno) y alinearlo sin que se aplaste
            var textElement = input.Q(className: "unity-text-element");
            if (textElement != null)
            {
                textElement.style.unityTextAlign = TextAnchor.MiddleLeft;
                textElement.style.marginTop = 0f; textElement.style.marginBottom = 0f;
                textElement.style.paddingTop = 0f; textElement.style.paddingBottom = 0f;
            }
        });
    }

    private static void StyleInnerField(VisualElement field, string innerClassName) 
    {
        field.style.backgroundColor = Color.clear;
        field.style.borderTopWidth  = 0f; field.style.borderBottomWidth = 0f;
        field.style.borderLeftWidth = 0f; field.style.borderRightWidth  = 0f;

        field.RegisterCallback<AttachToPanelEvent>(e => 
        {
            var input = field.Q(className: innerClassName);
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
    }

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