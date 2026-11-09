using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ItemCaja : ItemBase
{
    [Header("Configuración del Robo")]
    [SerializeField] private int cantidadBaldosasARobar = 5;

    protected override void AplicarEfecto(PlayerController jugador)
    {
        // 1. El GameManager (que sabe los contadores) nos dice a quién robarle
        byte colorEnemigo = GameManager.Instance.ObtenerEnemigoConMasTiles(jugador.ColorId.Value);

        if (colorEnemigo != 0 && SueloController.Instance != null)
        {
            // 2. El SueloController nos da las baldosas de ese enemigo
            List<int> baldosasEnemigas = SueloController.Instance.ObtenerIndicesDeColor(colorEnemigo);

            if (baldosasEnemigas.Count == 0) return;

            // 3. El ItemCaja hace el cálculo de cuántas robar (mezclando)
            for (int i = 0; i < baldosasEnemigas.Count; i++)
            {
                int randomIndex = Random.Range(i, baldosasEnemigas.Count);
                int temporal = baldosasEnemigas[i];
                baldosasEnemigas[i] = baldosasEnemigas[randomIndex];
                baldosasEnemigas[randomIndex] = temporal;
            }

            int cantidadReal = Mathf.Min(cantidadBaldosasARobar, baldosasEnemigas.Count);

            // 4. El ItemCaja le pide al SueloController que pinte las elegidas
            for (int i = 0; i < cantidadReal; i++)
            {
                SueloController.Instance.EstablecerColorBaldosa(baldosasEnemigas[i], jugador.ColorId.Value);
            }

            Debug.Log($"[Item] La caja robó {cantidadReal} baldosas al color {colorEnemigo}.");
        }
    }
}