using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Una baldosa individual. NO es un NetworkObject.
/// Solo existe localmente y el GameManager sincroniza su estado.
/// </summary>
public class Tile : MonoBehaviour
{
    // Índice dentro del grid. Lo asigna el GameManager al generar.
    public int Indice;

    private Renderer renderBaldosa;

    private void Awake()
    {
        renderBaldosa = GetComponent<Renderer>();
    }

    /// <summary>
    /// Cambia el color de la baldosa. Lo llama el GameManager
    /// cuando llega un evento de red.
    /// </summary>
    public void EstablecerColor(Color32 color)
    {
        renderBaldosa.material.color = color;
    }

    /// <summary>
    /// Detección de entrada del jugador. Solo el servidor procesa esto.
    /// </summary>
    private void OnTriggerEnter(Collider otro)
    {
        // Los clientes ignoran. Solo el servidor decide.
        if (!NetworkManager.Singleton.IsServer) return;

        // Solo tiene sentido pisar cuando la partida está en curso.
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.EstadoActual.Value != GameState.EnJuego) return;

        // Comprobamos que lo que entró es un jugador.
        // Preferimos componente antes que Tag para no depender de config.
        var jugador = otro.GetComponent<PlayerController>();
        if (jugador == null) return;

        // Avisamos al GameManager. Él escribirá en la NetworkList,
        // que se sincronizará automáticamente con todos los clientes.
        GameManager.Instance.EstablecerColorBaldosa(Indice, jugador.ColorId.Value);
    }
}