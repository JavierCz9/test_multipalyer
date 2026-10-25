using UnityEngine;

/// <summary>
/// Baldosa individual. NO es un NetworkObject.
/// El SueloController le asigna el índice al arrancar.
/// Guarda su color original para poder restaurarlo al resetear.
/// </summary>
public class Tile : MonoBehaviour
{
    [HideInInspector]
    public int Indice = -1;

    private Renderer render;
    private Material materialInstancia;

    // Color original del prefab, capturado en Awake.
    private Color colorOriginal;

    private void Awake()
    {
        render = GetComponent<Renderer>();
        if (render != null)
        {
            materialInstancia = render.material;
            colorOriginal = materialInstancia.color;
        }
    }

    public void EstablecerColor(Color32 color)
    {
        if (materialInstancia != null)
            materialInstancia.color = color;
    }

    /// <summary>
    /// Restaura el color original del prefab.
    /// </summary>
    public void RestaurarColorOriginal()
    {
        if (materialInstancia != null)
            materialInstancia.color = colorOriginal;
    }
}