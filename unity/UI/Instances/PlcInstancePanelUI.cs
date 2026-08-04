//----------------------------------------------------------------------------------------------------------------------
// PLC INSTANCE PANEL UI
//
// Desc: Panel flotante arrastrable y redimensionable para Instancias PLC.
//       Clon exacto de la estética del panel de Tags.
//----------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class PlcInstancePanelUI : MonoBehaviour
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

    // -- Drag & Resize ----------------------------------------------------------
    private bool    _dragging;
    private bool    _resizing;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartPos;
    private Vector2 _resizeStartMouse;
    private Vector2 _resizeStartSize;

    private const float MinW = 350f;
    private const float MinH = 150f;
    private const float InitW = 400f;
    private const float InitH = 300f;

    // Fuente y escalado
    private const float FontMin   = 9f;
    private const float FontMax   = 16f;
    private const float PanelWMin = 350f;
    private const float PanelWMax = 800f;
    private float _fontSize       = 12f;
    private float _panelWidth     = InitW;

    // Proporciones de columnas
    private const float PWName   = 0.70f;
    private const float PWAction = 0.30f;

    // -- Paleta Exacta (Copiada del PlcTagTableBuilder y PlcTagPanelUI) ---------
    private static readonly Color ColBg       = new Color(0.08f, 0.09f, 0.10f, 1f);
    private static readonly Color ColRowEven  = new Color(0.11f, 0.12f, 0.13f, 1f);
    private static readonly Color ColRowOdd   = new Color(0.09f, 0.10f, 0.11f, 1f);
    private static readonly Color ColBorder   = new Color(0.20f, 0.22f, 0.24f, 1f);
    private static readonly Color ColAccent   = new Color(0.25f, 0.85f, 0.55f, 1f);
    private static readonly Color ColText     = new Color(0.88f, 0.88f, 0.88f, 1f);
    private static readonly Color ColErr      = new Color(0.90f, 0.30f, 0.30f, 1f);
    private static readonly Color ColMuted    = new Color(0.50f, 0.52f, 0.54f, 1f);
    private static readonly Color ColTopBar   = new Color(0.11f, 0.12f, 0.14f, 1f);
    private static readonly Color ColBtnOk    = new Color(0.15f, 0.40f, 0.75f, 1f);

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
        _root.style.paddingLeft   = 130f; // Offset para que no pise al botón de Tags

        // Botón toggle
        _btnToggle = new Button(OnToggleClicked) { text = "Instancias PLC" };
        StyleTopButton(_btnToggle);
        _root.Add(_btnToggle);

        // Panel flotante
        _panel = new VisualElement();
        _panel.style.display         = DisplayStyle.None;
        _panel.style.position        = Position.Absolute;
        _panel.style.left            = 130f;
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

        var titleLabel = new Label("Panel Instancias");
        titleLabel.style.color                   = ColAccent;
        titleLabel.style.fontSize                = 13f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.flexGrow                = 1f;
        _titleBar.Add(titleLabel);

        _lblStatus = new Label(PlcInstanceDataService.Instance?.StatusMessage ?? "");
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

        _panelWidth = InitW - 40f; 

        // Cabecera columnas
        _header = BuildHeader();
        _header.style.paddingLeft  = 10f;
        _header.style.paddingRight = 10f;
        _header.style.flexShrink   = 0f;
        _panel.Add(_header);

        // ListView (Estética calcada del TableBuilder)
        _listView = new ListView();
        _listView.itemsSource = PlcInstanceDataService.Instance?.Instances;
        _listView.makeItem = MakeItem;
        _listView.bindItem = BindItem;
        _listView.selectionType = SelectionType.None;
        _listView.showAlternatingRowBackgrounds = AlternatingRowBackground.None;
        _listView.fixedItemHeight = Mathf.Max(24f, _fontSize * 2.4f);
        _listView.style.backgroundColor = ColBg;
        _listView.style.flexGrow = 1f;
        _listView.style.flexShrink = 1f;
        _listView.style.minHeight = 0f;
        _listView.style.paddingLeft = 2f;
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

    // -- Componentes de la Tabla ------------------------------------------------

    private float W(float proportion) => Mathf.Floor(_panelWidth * proportion);

    private VisualElement BuildHeader()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 8f; row.style.paddingRight = 8f;
        row.style.backgroundColor = ColBg;
        row.style.borderBottomWidth = 1f;
        row.style.borderBottomColor = ColAccent;
        row.style.paddingBottom = 4f;

        row.Add(HeaderCell("Nombre de Instancia", PWName));
        row.Add(HeaderCell("Acción", PWAction));

        return row;
    }

    private Label HeaderCell(string text, float proportion)
    {
        var l = new Label(text);
        l.style.width = W(proportion);
        l.style.flexShrink = 0f;
        l.style.color = ColAccent;
        l.style.fontSize = _fontSize;
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.unityTextAlign = TextAnchor.MiddleLeft;
        return l;
    }

    private VisualElement MakeItem()
    {
        var row = new VisualElement { name = "inst-row" };
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 8f; row.style.paddingRight = 8f;

        var lblName = new Label { name = "cell-name" };
        lblName.style.width = W(PWName);
        lblName.style.flexShrink = 0f;
        lblName.style.color = ColText;
        lblName.style.fontSize = _fontSize;
        lblName.style.unityTextAlign = TextAnchor.MiddleLeft;
        lblName.style.overflow = Overflow.Hidden;
        lblName.style.textOverflow = TextOverflow.Ellipsis;
        lblName.style.whiteSpace = WhiteSpace.NoWrap;
        row.Add(lblName);

        var actCell = new VisualElement { name = "cell-act" };
        actCell.style.width = W(PWAction);
        actCell.style.flexShrink = 0f;
        actCell.style.flexDirection = FlexDirection.Row;
        actCell.style.alignItems = Align.Center;

        var btnConnect = new Button { name = "btn-connect", text = "Conectar" };
        float btnH = Mathf.Max(20f, _fontSize * 1.8f);
        btnConnect.style.width = W(PWAction) - 10f;
        btnConnect.style.height = btnH;
        btnConnect.style.flexShrink = 0f;
        btnConnect.style.backgroundColor = ColBtnOk;
        btnConnect.style.color = Color.white;
        btnConnect.style.fontSize = _fontSize;
        btnConnect.style.borderTopLeftRadius = 3f; btnConnect.style.borderTopRightRadius = 3f;
        btnConnect.style.borderBottomLeftRadius = 3f; btnConnect.style.borderBottomRightRadius = 3f;
        btnConnect.style.borderTopWidth = 0f; btnConnect.style.borderBottomWidth = 0f;
        btnConnect.style.borderLeftWidth = 0f; btnConnect.style.borderRightWidth = 0f;
        actCell.Add(btnConnect);

        row.Add(actCell);
        return row;
    }

    private void BindItem(VisualElement el, int index)
    {
        var service = PlcInstanceDataService.Instance;
        if (service == null || index < 0 || index >= service.Instances.Count) return;

        var inst = service.Instances[index];

        // Fondo alternante
        el.style.backgroundColor = index % 2 == 0 ? ColRowEven : ColRowOdd;

        // Actualizar anchos
        var nameCell = el.Q<Label>("cell-name");
        if (nameCell != null) 
        {
            nameCell.style.width = W(PWName);
            nameCell.text = inst.Name;
            nameCell.style.fontSize = _fontSize;
        }

        var actCell = el.Q<VisualElement>("cell-act");
        if (actCell != null) actCell.style.width = W(PWAction);

        var btnConnect = el.Q<Button>("btn-connect");
        if (btnConnect != null)
        {
            btnConnect.style.width = W(PWAction) - 10f;
            btnConnect.style.height = Mathf.Max(20f, _fontSize * 1.8f);
            btnConnect.style.fontSize = _fontSize;

            if (btnConnect.userData is Action oldCb)
                btnConnect.clicked -= oldCb;

            Action connectAction = () => service.ConnectToInstance(inst.ID.ToString());
            btnConnect.userData = connectAction;
            btnConnect.clicked += connectAction;
        }

        el.style.height = Mathf.Max(24f, _fontSize * 2.4f);
    }

    // -- Drag & Resize ----------------------------------------------------------

    void OnResizeFinished()
    {
        float panelW = _panel.resolvedStyle.width;
        float t = Mathf.InverseLerp(PanelWMin, PanelWMax, panelW);
        _fontSize = Mathf.Round(Mathf.Lerp(FontMin, FontMax, t));
        _panelWidth = panelW - 40f; 

        // Refrescar cabecera
        var hName = _header.ElementAt(0); hName.style.width = W(PWName); hName.style.fontSize = _fontSize;
        var hAct  = _header.ElementAt(1); hAct.style.width = W(PWAction); hAct.style.fontSize = _fontSize;

        _listView.fixedItemHeight = Mathf.Max(24f, _fontSize * 2.4f);
        _listView.Rebuild();
    }

    void RegisterDragAndResize()
    {
        _titleBar.RegisterCallback<MouseDownEvent>(e =>
        {
            if (e.button != 0) return;
            _dragging = true;
            _dragStartMouse = e.mousePosition;
            _dragStartPos = new Vector2(_panel.style.left.value.value, _panel.style.top.value.value);
            _titleBar.CaptureMouse();
            e.StopPropagation();
        });
        _titleBar.RegisterCallback<MouseMoveEvent>(e =>
        {
            if (!_dragging) return;
            Vector2 d = (Vector2)e.mousePosition - _dragStartMouse;
            _panel.style.left = Mathf.Max(0, _dragStartPos.x + d.x);
            _panel.style.top = Mathf.Max(0, _dragStartPos.y + d.y);
            e.StopPropagation();
        });
        _titleBar.RegisterCallback<MouseUpEvent>(e =>
        {
            if (!_dragging) return;
            _dragging = false;
            _titleBar.ReleaseMouse();
            e.StopPropagation();
        });

        _resizeHandle.RegisterCallback<MouseDownEvent>(e =>
        {
            if (e.button != 0) return;
            _resizing = true;
            _resizeStartMouse = e.mousePosition;
            _resizeStartSize = new Vector2(_panel.style.width.value.value, _panel.style.height.value.value);
            _resizeHandle.CaptureMouse();
            e.StopPropagation();
        });
        _resizeHandle.RegisterCallback<MouseMoveEvent>(e =>
        {
            if (!_resizing) return;
            Vector2 d = (Vector2)e.mousePosition - _resizeStartMouse;
            _panel.style.width = Mathf.Max(MinW, _resizeStartSize.x + d.x);
            _panel.style.height = Mathf.Max(MinH, _resizeStartSize.y + d.y);
            e.StopPropagation();
        });
        _resizeHandle.RegisterCallback<MouseUpEvent>(e =>
        {
            if (!_resizing) return;
            _resizing = false;
            _resizeHandle.ReleaseMouse();
            OnResizeFinished();
            e.StopPropagation();
        });
    }

    // -- Eventos de UI ----------------------------------------------------------
    void OnToggleClicked()
    {
        bool visible = _panel.style.display == DisplayStyle.Flex;
        _panel.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
        _btnToggle.text = visible ? "Instancias PLC" : "Ocultar Inst.";
        
        if (!visible && PlcInstanceDataService.Instance != null && PlcInstanceDataService.Instance.Instances.Count == 0)
            PlcInstanceDataService.Instance.LoadInstances();
    }

    void OnRefreshClicked() => PlcInstanceDataService.Instance?.LoadInstances();

    // -- Suscripción al servicio ------------------------------------------------
    void SubscribeToService()
    {
        var svc = PlcInstanceDataService.Instance;
        if (svc == null) return;
        svc.OnInstancesLoaded += HandleInstancesLoaded;
        svc.OnConnectionStatusChanged += HandleStatusChanged;
    }

    void UnsubscribeFromService()
    {
        var svc = PlcInstanceDataService.Instance;
        if (svc == null) return;
        svc.OnInstancesLoaded -= HandleInstancesLoaded;
        svc.OnConnectionStatusChanged -= HandleStatusChanged;
    }

    void HandleInstancesLoaded()
    {
        if (_lblStatus != null) _lblStatus.text = PlcInstanceDataService.Instance.StatusMessage;
        if (_listView != null)
        {
            _listView.itemsSource = PlcInstanceDataService.Instance.Instances;
            _listView.Rebuild();
        }
    }

    void HandleStatusChanged()
    {
        if (_lblStatus == null) return;
        string msg = PlcInstanceDataService.Instance.ConnectMessage;
        _lblStatus.text = string.IsNullOrEmpty(msg) ? PlcInstanceDataService.Instance.StatusMessage : msg;
        _lblStatus.style.color = msg.StartsWith("Error") ? ColErr : ColMuted;
    }

    // -- Helpers de estilo ------------------------------------------------------
    private static void ApplyBorder(VisualElement el, Color color, float width, float radius)
    {
        el.style.borderTopColor = color; el.style.borderBottomColor = color;
        el.style.borderLeftColor = color; el.style.borderRightColor = color;
        el.style.borderTopWidth = width; el.style.borderBottomWidth = width;
        el.style.borderLeftWidth = width; el.style.borderRightWidth = width;
        el.style.borderTopLeftRadius = radius; el.style.borderTopRightRadius = radius;
        el.style.borderBottomLeftRadius = radius; el.style.borderBottomRightRadius = radius;
    }

    private static void StyleTopButton(Button b, bool compact = false)
    {
        b.style.height = compact ? 22f : 26f;
        b.style.paddingLeft = compact ? 8f : 12f;
        b.style.paddingRight = compact ? 8f : 12f;
        b.style.backgroundColor = new Color(0.15f, 0.16f, 0.18f, 1f);
        b.style.color = ColText;
        b.style.fontSize = compact ? 11f : 12f;
        ApplyBorder(b, new Color(0.28f, 0.30f, 0.33f, 1f), 1f, 4f);
    }
}