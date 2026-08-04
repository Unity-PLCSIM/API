//----------------------------------------------------------------------------------------------------------------------
// PLC TAG TABLE BUILDER
//
// Desc: Factoría estática que construye y devuelve un ListView configurado para mostrar
//       tags PLC. Responsabilidad única: crear y enlazar elementos visuales.
//       No sabe nada de ApiInterface ni de PlcTagDataService.
//
// Uso:  var list = PlcTagTableBuilder.Build(rows, onWriteRequested);
//       container.Add(PlcTagTableBuilder.BuildHeader());
//       PlcTagTableBuilder.SetFontSize(12f);   // escala toda la tabla
//
// Ubicación: Assets/Scripts/UI/PlcTagTableBuilder.cs
// Autor: Alex Asensio
// Date:  Agosto 2026
//----------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

public static class PlcTagTableBuilder
{
    // -- Paleta -----------------------------------------------------------------

    private static readonly Color ColBg       = new Color(0.08f, 0.09f, 0.10f, 1f);
    private static readonly Color ColRowEven  = new Color(0.11f, 0.12f, 0.13f, 1f);
    private static readonly Color ColRowOdd   = new Color(0.09f, 0.10f, 0.11f, 1f);
    private static readonly Color ColAccent   = new Color(0.25f, 0.85f, 0.55f, 1f);
    private static readonly Color ColText     = new Color(0.88f, 0.88f, 0.88f, 1f);
    private static readonly Color ColMuted    = new Color(0.50f, 0.52f, 0.54f, 1f);
    private static readonly Color ColBtnTrue  = new Color(0.10f, 0.60f, 0.25f, 1f);
    private static readonly Color ColBtnFalse = new Color(0.62f, 0.12f, 0.12f, 1f);
    private static readonly Color ColBtnOk    = new Color(0.15f, 0.40f, 0.75f, 1f);
    private static readonly Color ColTfBorder = new Color(0.30f, 0.32f, 0.35f, 1f);
    private static readonly Color ColTfBg     = new Color(0.18f, 0.19f, 0.21f, 1f);

    // -- Fuente escalable -------------------------------------------------------

    private static float _fontSize = 11f;

    /// <summary>
    /// Actualiza el tamaño de fuente global de la tabla.
    /// Llama a ListView.Rebuild() después para que se aplique.
    /// </summary>
    public static void SetFontSize(float size) => _fontSize = Mathf.Clamp(size, 9f, 16f);

    // -- Anchos de columna (proporcionales, se recalculan con el ancho) ---------
    // Las proporciones suman 1.0; se multiplican por el ancho útil del panel.

    // <-- AÑADIDO: Redistribución de porcentajes
    private const float PWMenu = 0.05f;
    private const float PWName = 0.22f; 
    private const float PWType = 0.18f;
    private const float PWVal  = 0.14f;
    private const float PWArea = 0.06f;
    private const float PWMod  = 0.35f; 

    // Ancho de referencia usado en Build(); se actualiza con SetPanelWidth
    private static float _panelWidth = 640f;

    private static float _pwNameDynamic = PWName;
    private static float _pwValDynamic  = PWVal;

    public static void SetPanelWidth(float w) => _panelWidth = Mathf.Max(w, 100f);

    public static void AdjustColumnWidths(List<RowData> items)
    {
        if (items == null || items.Count == 0) return;

        int maxChars = 0;
        foreach (var row in items)
            if (row.Name != null && row.Name.Length > maxChars)
                maxChars = row.Name.Length;

        float charWidth = _fontSize * 0.65f;
        float neededWidth = maxChars * charWidth + 16f;
        float neededProportion = neededWidth / _panelWidth;

        _pwNameDynamic = Mathf.Clamp(neededProportion, 0.18f, 0.45f); // <-- MODIFICADO
        float remaining = 1f - _pwNameDynamic - PWMenu - PWType - PWArea - PWMod; // <-- MODIFICADO (Incluye PWMenu)
        _pwValDynamic = Mathf.Max(0.08f, remaining);
    }

    private static float W(float proportion) => Mathf.Floor(_panelWidth * proportion);

    public static void RefreshHeader(VisualElement header)
    {
        if (header == null) { Debug.Log("HEADER ES NULL"); return; }
        var cells = header.Children().ToList();
        Debug.Log($"Celdas en header: {cells.Count} | _pwNameDynamic: {_pwNameDynamic} | W: {W(_pwNameDynamic)}");
        if (cells.Count < 6) return; // <-- MODIFICADO (de 5 a 6)
        
        cells[0].style.width = W(PWMenu); // <-- AÑADIDO
        cells[1].style.width = W(_pwNameDynamic);
        cells[2].style.width = W(PWType);
        cells[3].style.width = W(_pwValDynamic);
        cells[4].style.width = W(PWArea);
        cells[5].style.width = W(PWMod);
    }

    // -- Modelo de fila ---------------------------------------------------------

    public class RowData
    {
        public string Name;
        public string Type;
        public string Value;
        public string Area;
        public string EditBuffer;
    }

    // -- API pública ------------------------------------------------------------

    public static VisualElement BuildHeader()
    {
        var row = MakeRow(ColBg);
        row.name = "plc-header";
        row.style.borderBottomWidth = 1f;
        row.style.borderBottomColor = ColAccent;
        row.style.paddingBottom     = 4f;

        row.Add(HeaderCell("", PWMenu)); // <-- AÑADIDO
        row.Add(HeaderCell("Nombre",    _pwNameDynamic));
        row.Add(HeaderCell("Tipo",      PWType));
        row.Add(HeaderCell("Valor",     _pwValDynamic));
        row.Add(HeaderCell("E/S",       PWArea));
        row.Add(HeaderCell("Modificar", PWMod));

        return row;
    }

    public static ListView Build(
        List<RowData>          items,
        Action<string, string> onWriteRequested,
        Action<string, VisualElement> onMenuClick) // <-- MODIFICADO
    {
        // Altura de fila escala con la fuente: fuente * 2.4 aprox, mínimo 24px
        float itemHeight = Mathf.Max(24f, _fontSize * 2.4f);

        var lv = new ListView(
            items,
            itemHeight,
            MakeItem,
            (el, i) => BindItem(el, i, items, onWriteRequested, onMenuClick)) // <-- MODIFICADO
        {
            selectionType = SelectionType.None,
            showAlternatingRowBackgrounds = AlternatingRowBackground.None,
            style =
            {
                backgroundColor = ColBg,
                // --- ARREGLO SCROLL: Flexbox completo para forzar la barra ---
                flexGrow        = 1f, 
                flexShrink      = 1f,
                minHeight       = 0f, 
            }
        };

        return lv;
    }

    // -- MakeItem / BindItem ----------------------------------------------------

    private static VisualElement MakeItem()
    {
        var row = MakeRow(Color.clear);
        row.name = "plc-row";

        // --- Celda del Menú --- // <-- AÑADIDO (BLOQUE)
        var menuCell = new VisualElement { name = "cell-menu" };
        menuCell.style.width = W(PWMenu); menuCell.style.flexShrink = 0f; menuCell.style.alignItems = Align.Center; menuCell.style.justifyContent = Justify.Center;
        var btnMenu = new Button { name = "btn-menu", text = "⋮" };
        btnMenu.style.width = 18f; btnMenu.style.height = 18f; btnMenu.style.paddingLeft = 0f; btnMenu.style.paddingRight = 0f; btnMenu.style.paddingTop = 0f; btnMenu.style.paddingBottom = 0f; btnMenu.style.fontSize = 13f;
        btnMenu.style.backgroundColor = new Color(0.15f, 0.16f, 0.18f, 1f); btnMenu.style.color = ColText; btnMenu.style.borderTopWidth = 0f; btnMenu.style.borderBottomWidth = 0f; btnMenu.style.borderLeftWidth = 0f; btnMenu.style.borderRightWidth = 0f;
        menuCell.Add(btnMenu);
        row.Add(menuCell);
        // ----------------------

        row.Add(DataCell("", _pwNameDynamic, "cell-name"));
        row.Add(DataCell("", PWType, "cell-type"));
        row.Add(DataCell("", _pwValDynamic,  "cell-val"));
        row.Add(DataCell("", PWArea, "cell-area"));

        // Celda Modificar
        var modCell = new VisualElement { name = "cell-mod" };
        modCell.style.width         = W(PWMod);
        modCell.style.flexShrink    = 0f;
        modCell.style.flexDirection = FlexDirection.Row;
        modCell.style.alignItems    = Align.Center;

        // Guion (salidas)
        var dash = new Label("—") { name = "mod-dash" };
        dash.style.color          = ColMuted;
        dash.style.fontSize       = _fontSize;
        dash.style.unityTextAlign = TextAnchor.MiddleLeft;
        modCell.Add(dash);

        // Botón bool
        var btnBool = new Button { name = "mod-bool" };
        StyleButton(btnBool, ColBtnFalse, W(PWMod) - 4f);
        modCell.Add(btnBool);

        // TextField + OK
        var tf = new TextField { name = "mod-tf" };
        tf.style.width       = W(PWMod) - 50f;
        tf.style.flexShrink  = 0f;
        tf.style.marginRight = 4f;
        StyleTextField(tf);
        modCell.Add(tf);

        var btnOk = new Button { text = "OK", name = "mod-ok" };
        StyleButton(btnOk, ColBtnOk, 42f);
        modCell.Add(btnOk);

        row.Add(modCell);
        return row;
    }

    private static void BindItem(
        VisualElement          el,
        int                    index,
        List<RowData>          items,
        Action<string, string> onWrite,
        Action<string, VisualElement> onMenuClick) // <-- MODIFICADO
    {
        if (index < 0 || index >= items.Count) return;
        RowData rd = items[index];

        // Fondo alternante
        el.style.backgroundColor = index % 2 == 0 ? ColRowEven : ColRowOdd;

        // Actualizar anchos proporcionales (pueden haber cambiado si el panel se redimensionó)
        UpdateCellWidth(el, "cell-menu", PWMenu); // <-- AÑADIDO
        UpdateCellWidth(el, "cell-name", _pwNameDynamic);
        UpdateCellWidth(el, "cell-type", PWType);
        UpdateCellWidth(el, "cell-val",  _pwValDynamic);
        UpdateCellWidth(el, "cell-area", PWArea);
        UpdateModCellWidths(el);

        // Textos y fuente
        SetLabel(el, "cell-name", rd.Name);
        SetLabel(el, "cell-type", rd.Type);
        SetLabel(el, "cell-val",  rd.Value);
        SetLabel(el, "cell-area", rd.Area, rd.Area == "E" ? ColAccent : ColMuted);

        // Altura de fila acorde a la fuente
        float rowH = Mathf.Max(24f, _fontSize * 2.4f);
        el.style.height = rowH;

        // --- Lógica Botón Menú --- // <-- AÑADIDO (BLOQUE)
        var btnMenu = el.Q<Button>("btn-menu");
        if (btnMenu.userData is Action oldMenu) btnMenu.clicked -= oldMenu;
        Action openMenu = () => onMenuClick?.Invoke(rd.Name, btnMenu);
        btnMenu.userData = openMenu;
        btnMenu.clicked += openMenu;
        // -------------------------

        // Celda Modificar
        var dash    = el.Q<Label>("mod-dash");
        var btnBool = el.Q<Button>("mod-bool");
        var tf      = el.Q<TextField>("mod-tf");
        var btnOk   = el.Q<Button>("mod-ok");

        dash.style.display    = DisplayStyle.None;
        btnBool.style.display = DisplayStyle.None;
        tf.style.display      = DisplayStyle.None;
        btnOk.style.display   = DisplayStyle.None;

        if (rd.Area == "S")
        {
            dash.style.display  = DisplayStyle.Flex;
            dash.style.fontSize = _fontSize;
        }
        else if (rd.Type == "Bool")
        {
            btnBool.style.display = DisplayStyle.Flex;
            bool active = rd.Value.Equals("true", StringComparison.OrdinalIgnoreCase) || rd.Value == "1";
            btnBool.text                  = active ? "TRUE" : "FALSE";
            btnBool.style.backgroundColor = active ? ColBtnTrue : ColBtnFalse;
            btnBool.style.fontSize        = _fontSize;
            btnBool.style.width           = W(PWMod) - 4f;
            btnBool.style.height          = Mathf.Max(20f, _fontSize * 1.8f);

            // COMPROBACIÓN 1: Evitar el null en el botón Bool
            if (btnBool.userData is Action oldBoolCb)
            {
                btnBool.clicked -= oldBoolCb;
            }
            Action toggle = () => onWrite(rd.Name, active ? "false" : "true");
            btnBool.userData = toggle;
            btnBool.clicked += toggle;
        }
        else
        {
            tf.style.display    = DisplayStyle.Flex;
            btnOk.style.display = DisplayStyle.Flex;
            tf.SetValueWithoutNotify(rd.EditBuffer);
            tf.style.width    = W(PWMod) - 50f;
            tf.style.fontSize = _fontSize;
            tf.style.height   = Mathf.Max(20f, _fontSize * 1.8f);
            btnOk.style.fontSize = _fontSize;
            btnOk.style.height   = Mathf.Max(20f, _fontSize * 1.8f);

            // COMPROBACIÓN 2: Evitar la ArgumentException en el TextField
            if (tf.userData is EventCallback<ChangeEvent<string>> oldTfCb)
            {
                tf.UnregisterValueChangedCallback(oldTfCb);
            }
            EventCallback<ChangeEvent<string>> onTfChange = evt => rd.EditBuffer = evt.newValue;
            tf.userData = onTfChange;
            tf.RegisterValueChangedCallback(onTfChange);

            // COMPROBACIÓN 3: Evitar el null en el botón OK
            if (btnOk.userData is Action oldOkCb)
            {
                btnOk.clicked -= oldOkCb;
            }
            Action confirm = () => onWrite(rd.Name, rd.EditBuffer);
            btnOk.userData = confirm;
            btnOk.clicked += confirm;
        }
    }

    // -- Helpers de layout ------------------------------------------------------

    private static void UpdateCellWidth(VisualElement row, string name, float proportion)
    {
        var el = row.Q(name);
        if (el != null) el.style.width = W(proportion);
    }

    private static void UpdateModCellWidths(VisualElement row)
    {
        var modCell = row.Q("cell-mod");
        if (modCell != null) modCell.style.width = W(PWMod);
    }

    // -- Helpers de estilo ------------------------------------------------------

    private static VisualElement MakeRow(Color bg)
    {
        var row = new VisualElement();
        row.style.flexDirection   = FlexDirection.Row;
        row.style.alignItems      = Align.Center;
        row.style.paddingLeft     = 8f;
        row.style.paddingRight    = 8f;
        row.style.backgroundColor = bg;
        return row;
    }

    private static Label HeaderCell(string text, float proportion)
    {
        var l = new Label(text);
        l.style.width                   = W(proportion);
        l.style.flexShrink              = 0f;
        l.style.color                   = ColAccent;
        l.style.fontSize                = _fontSize;
        l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.unityTextAlign          = TextAnchor.MiddleLeft;
        return l;
    }

    private static Label DataCell(string text, float proportion, string name)
    {
        var l = new Label(text) { name = name };
        l.style.width          = W(proportion);
        l.style.flexShrink     = 0f;
        l.style.color          = ColText;
        l.style.fontSize       = _fontSize;
        l.style.unityTextAlign = TextAnchor.MiddleLeft;
        
        // --- AÑADIR ESTAS 3 LÍNEAS ---
        l.style.overflow       = Overflow.Hidden;
        l.style.textOverflow   = TextOverflow.Ellipsis;
        l.style.whiteSpace     = WhiteSpace.NoWrap;
        // -----------------------------

        return l;
    }

    private static void StyleButton(Button b, Color bg, float width)
    {
        float h = Mathf.Max(20f, _fontSize * 1.8f);
        b.style.width           = width;
        b.style.height          = h;
        b.style.flexShrink      = 0f;
        b.style.backgroundColor = bg;
        b.style.color           = Color.white;
        b.style.fontSize        = _fontSize;
        b.style.borderTopLeftRadius     = 3f; b.style.borderTopRightRadius    = 3f;
        b.style.borderBottomLeftRadius  = 3f; b.style.borderBottomRightRadius = 3f;
        b.style.borderTopWidth    = 0f; b.style.borderBottomWidth = 0f;
        b.style.borderLeftWidth   = 0f; b.style.borderRightWidth  = 0f;
    }

    private static void StyleTextField(TextField tf)
    {
        // 1. Limpiamos el estilo del contenedor exterior para que no estorbe
        tf.style.backgroundColor = Color.clear;
        tf.style.borderTopWidth    = 0f; tf.style.borderBottomWidth = 0f;
        tf.style.borderLeftWidth   = 0f; tf.style.borderRightWidth  = 0f;

        // 2. Esperamos a que se añada a la pantalla para buscar el input real (el hijo)
        tf.RegisterCallback<AttachToPanelEvent>(e => 
        {
            // "unity-text-field__input" es la clase interna estándar de Unity
            var input = tf.Q(className: "unity-text-field__input");
            if (input == null) return;

            // Le aplicamos nuestros colores de fondo y texto
            input.style.backgroundColor = ColTfBg;
            input.style.color           = ColText;

            input.style.paddingTop    = 0f;
            input.style.paddingBottom = 0f;
            input.style.marginTop     = 0f;
            input.style.marginBottom  = 0f;
            
            // Le aplicamos los bordes redondeados
            input.style.borderTopLeftRadius     = 3f; input.style.borderTopRightRadius    = 3f;
            input.style.borderBottomLeftRadius  = 3f; input.style.borderBottomRightRadius = 3f;
            
            // Le aplicamos el color y grosor del borde
            input.style.borderTopColor    = ColTfBorder; input.style.borderBottomColor = ColTfBorder;
            input.style.borderLeftColor   = ColTfBorder; input.style.borderRightColor  = ColTfBorder;
            input.style.borderTopWidth    = 1f; input.style.borderBottomWidth = 1f;
            input.style.borderLeftWidth   = 1f; input.style.borderRightWidth  = 1f;
        });
    }

    private static void SetLabel(VisualElement root, string name, string text, Color? color = null)
    {
        var l = root.Q<Label>(name);
        if (l == null) return;
        l.text       = text;
        l.style.color    = color ?? ColText;
        l.style.fontSize = _fontSize;
    }
}