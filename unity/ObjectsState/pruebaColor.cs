using UnityEngine;

public class PlcElementSync : MonoBehaviour
{
    [Header("Configuración de Tag")]
    public string tagName = "Motor";
    public string tagType = "Bool";

    [Header("Tiempo de actualización")]
    public float updateRate = 0.5f;

    private Renderer objRenderer;

    void Start()
    {
        objRenderer = GetComponent<Renderer>();ApiInterface.Instance.pollInterval = updateRate;
        ApiInterface.Instance.SubscribeTag(tagName, tagType, UpdateVisuals);
    }

    void OnDestroy()
    {
        ApiInterface.Instance.UnsubscribeTag(tagName);
    }

    private void UpdateVisuals(string rawValue)
    {
        if (string.IsNullOrEmpty(rawValue))
        {
            Debug.LogWarning("<b>[CUIDADO]</b> El valor recibido está vacío o es nulo.");
            return;
        }

        if (tagType == "Bool")
        {
            if (bool.TryParse(rawValue.Trim(), out bool isTrue))
            {
                objRenderer.material.color = isTrue ? Color.green : Color.red;
            }
            else
            {
                Debug.LogError($"<b>[ERROR DE CONVERSIÓN]</b> No se pudo convertir '{rawValue}' a bool.");
            }
        }
    }
}