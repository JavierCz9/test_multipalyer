using UnityEngine;

public class ItemVelocidad : ItemBase
{
    [Header("Configuración del Buff")]
    [SerializeField] private float multiplicadorVelocidad = 2f; // x2 de velocidad
    [SerializeField] private float duracionBuff = 5f;            // 5 segundos

    protected override void AplicarEfecto(PlayerController jugador)
    {
        // ✅ El ítem solo pasa los datos. El jugador hace el cálculo.
        jugador.ActivarBuffVelocidad(multiplicadorVelocidad, duracionBuff);
    }
}