using Unity.Netcode;
using UnityEngine;

public class ItemRalentizador : ItemBase
{
    [Header("Configuración del Efecto")]
    [Tooltip("0.2 = 20% de la velocidad (Súper lento). 0.5 = 50% (Mitad de velocidad).")]
    [SerializeField] private float multiplicadorRalentizacion = 0.2f;
    [SerializeField] private float duracionEfecto = 4f;

    protected override void AplicarEfecto(PlayerController jugadorQueAgarro)
    {
        // Recorremos a todos los clientes conectados a la partida
        foreach (var cliente in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (cliente.PlayerObject != null)
            {
                PlayerController otroJugador = cliente.PlayerObject.GetComponent<PlayerController>();

                // Si es un jugador válido y NO es el que agarró el ítem... ¡lo ralentizamos!
                if (otroJugador != null && otroJugador != jugadorQueAgarro)
                {
                    otroJugador.ActivarRalentizacion(multiplicadorRalentizacion, duracionEfecto);
                }
            }
        }

        Debug.Log($"[Item] ¡El jugador {jugadorQueAgarro.ColorId.Value} ralentizó a todos los demás!");
    }
}