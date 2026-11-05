using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Caja de power-up. El servidor es el único que detecta la colisión,
/// valida y ejecuta el robo. Cero RPCs involucrados.
/// </summary>
public class ItemCaja : NetworkBehaviour
{
    // Bandera de protección contra la race condition.
    private bool utilizada = false;

    private void OnTriggerEnter(Collider other)
    {
        // REGLA DE ORO: Solo el servidor procesa la física.
        if (!IsServer) return;

        // Si ya se usó, ignorar.
        if (utilizada) return;

        // ¿Entró un jugador?
        PlayerController jugador = other.GetComponent<PlayerController>();
        if (jugador == null) return;

        // Validaciones de estado.
        if (GameManager.Instance == null ||
            GameManager.Instance.EstadoActual.Value != GameState.EnJuego) return;

        // Bloquear la caja inmediatamente.
        utilizada = true;

        // Ejecutar la lógica.
        byte colorEnemigo = BuscarEnemigoConMasTiles(jugador.ColorId.Value);

        if (colorEnemigo != 0)
        {
            SueloController.Instance.RobarBaldosas(jugador.ColorId.Value, colorEnemigo);
        }

        // Destruir para todos.
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }
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