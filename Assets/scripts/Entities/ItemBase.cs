using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Clase base para todos los power-ups. Se encarga de la detección,
/// la validación del servidor y la destrucción del ítem.
/// </summary>
public abstract class ItemBase : NetworkBehaviour
{
    protected bool utilizada = false;

    private void OnTriggerEnter(Collider other)
    {
        // 1. Solo el servidor procesa la colisión
        if (!IsServer) return;
        if (utilizada) return;

        // 2. ¿Es un jugador?
        PlayerController jugador = other.GetComponent<PlayerController>();
        if (jugador == null) return;

        // 3. ¿Estamos jugando?
        if (GameManager.Instance == null || GameManager.Instance.EstadoActual.Value != GameState.EnJuego) return;

        // 4. Marcar como usada y aplicar el efecto
        utilizada = true;
        AplicarEfecto(jugador);

        // 5. Destruir el ítem para todos
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);
    }

    /// <summary>
    /// Cada ítem hijo implementa qué pasa cuando el jugador lo toca.
    /// </summary>
    protected abstract void AplicarEfecto(PlayerController jugador);
}