using Unity.Netcode;
using UnityEngine;

public class ItemCaja : NetworkBehaviour
{
    private bool utilizada = false;

    private void OnTriggerEnter(Collider other)
    {
        if (utilizada) return;

        PlayerController jugador = other.GetComponent<PlayerController>();

        if (jugador == null) return;

        // Solo el jugador que tocó la caja solicita el robo.
        if (!jugador.IsOwner) return;

        SolicitarRoboServerRpc(jugador.ColorId.Value);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SolicitarRoboServerRpc(byte colorJugador)
    {
        Debug.Log($"[ITEM] Robo recibido. Jugador color: {colorJugador}");

        if (utilizada) return;

        utilizada = true;

        // Buscar al otro jugador.
        foreach (var cliente in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (cliente.PlayerObject == null) continue;

            PlayerController otroJugador =
                cliente.PlayerObject.GetComponent<PlayerController>();

            if (otroJugador == null) continue;

            byte colorEnemigo = otroJugador.ColorId.Value;

            if (colorEnemigo == colorJugador) continue;

            // Robar 5 baldosas.
            if (SueloController.Instance != null)
            {
                SueloController.Instance.RobarBaldosas(
                    colorJugador,
                    colorEnemigo
                );
            }

            break;
        }

        // Desaparecer para todos.
        GetComponent<NetworkObject>().Despawn();
    }
}