using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Caja que aparece en el mapa. Al ser tocada por un jugador, le roba
/// 5 tiles al enemigo con más baldosas y las pinta con el color del
/// jugador que la agarró. Después desaparece.
/// Usa OnTriggerStay para detectar de forma robusta, aunque el jugador
/// ya esté dentro del trigger cuando la caja se spawnea.
/// </summary>
public class ItemCaja : NetworkBehaviour
{
    [Header("Detección")]
    [Tooltip("Cada cuántos segundos se puede intentar agarrar la caja. " +
             "Evita spamear RPCs si el jugador se queda pegado al trigger.")]
    [SerializeField] private float cooldownDeteccion = 0.3f;

    // Solo el servidor controla si la caja fue usada.
    // El cliente NO toca esta variable.
    private bool utilizada = false;

    // Cooldown local del cliente para no spamear RPCs.
    private float ultimoIntento = 0f;

    private void OnTriggerStay(Collider other)
    {
        // Si la caja ya fue usada, no procesamos nada.
        if (utilizada) return;

        // Solo procesamos en partida.
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.EstadoActual.Value != GameState.EnJuego) return;

        // ¿Lo que entró es un jugador?
        PlayerController jugador = other.GetComponent<PlayerController>();
        if (jugador == null) return;

        // Solo el dueño del jugador que tocó la caja pide el robo.
        // Los demás clientes no hacen nada.
        if (!jugador.IsOwner) return;

        // Cooldown para no spamear RPCs mientras el jugador sigue dentro.
        if (Time.time - ultimoIntento < cooldownDeteccion) return;
        ultimoIntento = Time.time;

        // Mandar el RPC al servidor. NO marcamos `utilizada` acá.
        // El servidor decide.
        SolicitarRoboRpc(jugador.ColorId.Value);
    }

    [Rpc(SendTo.Server)]
    private void SolicitarRoboRpc(byte colorJugador)
    {
        // El servidor chequea su propia `utilizada`.
        if (utilizada) return;
        if (SueloController.Instance == null) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.EstadoActual.Value != GameState.EnJuego) return;

        // Marcar como usada SOLO en el servidor.
        utilizada = true;

        Debug.Log($"[Item] Robo solicitado por color {colorJugador}");

        // Buscar al enemigo con más tiles (excluyendo al propio jugador).
        byte colorEnemigo = BuscarEnemigoConMasTiles(colorJugador);

        if (colorEnemigo != 0)
            SueloController.Instance.RobarBaldosas(colorJugador, colorEnemigo);
        else
            Debug.Log("[Item] No hay enemigos con tiles que robar.");

        // Despawnear la caja para todos.
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);
    }

    private byte BuscarEnemigoConMasTiles(byte colorJugador)
    {
        byte mejorColor = 0;
        int mejorCantidad = 0;

        for (byte color = 1; color <= PlayerPalette.CantidadJugadores; color++)
        {
            if (color == colorJugador) continue;

            int cantidad = GameManager.Instance.ObtenerBaldosas(color);

            if (cantidad > mejorCantidad)
            {
                mejorCantidad = cantidad;
                mejorColor = color;
            }
        }

        return mejorColor;
    }
}