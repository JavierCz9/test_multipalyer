
using UnityEngine;

public class Tile : MonoBehaviour
{
    [HideInInspector]
    public int Indice = -1;

    private Renderer render;
    private Material materialInstancia;

    private void Awake()
    {
        render = GetComponent<Renderer>();

        if (render != null)
        {
            materialInstancia = render.material;
            RestaurarColorOriginal();
        }
    }

    // Cuando un jugador pinta la baldosa
    public void EstablecerColor(Color32 color)
    {
        if (materialInstancia == null) return;

        Color nuevoColor = color;
        nuevoColor.a = 1f;

        materialInstancia.color = nuevoColor;
    }

    // Cuando la baldosa está sin pintar
    public void RestaurarColorOriginal()
    {
        if (materialInstancia == null) return;

        Color transparente = materialInstancia.color;
        transparente.a = 0f;

        materialInstancia.color = transparente;
    }
}
