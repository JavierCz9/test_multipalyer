using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Baldosa individual. Es un GameObject local, NO un NetworkObject.
/// El tilemap la instancia en la escena. El SueloController le asigna
/// el índice al arrancar y sincroniza su color por red.
/// </summary>
public class Tile : MonoBehaviour
{
    [HideInInspector] // No la toques a mano, la asigna el SueloController.
    public int Indice = -1;

    private Renderer render;
    private Material materialInstancia;

    private void Awake()
    {
        render = GetComponent<Renderer>();
        if (render != null) materialInstancia = render.material;

        // Aseguramos que el collider sea trigger.
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    public void EstablecerColor(Color32 color)
    {
        if (materialInstancia != null)
            materialInstancia.color = color;
    }

    private void OnTriggerEnter(Collider otro)
    {
        // Solo el servidor procesa. Los clientes ignoran.
        if (!NetworkManager.Singleton.IsServer) return;
        if (SueloController.Instance == null) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.EstadoActual.Value != GameState.EnJuego) return;

        var jugador = otro.GetComponent<PlayerController>();
        if (jugador == null) return;

        SueloController.Instance.EstablecerColorBaldosa(
            Indice,
            jugador.ColorId.Value,
            jugador.OwnerClientId
        );
    }
}