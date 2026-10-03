using Unity.Netcode;
using UnityEngine;

public enum GameState { SalaEspera = 0, EnJuego = 1 }

/// <summary>
/// Solo maneja el estado global de la partida.

/// La sincronización de colores la maneja SueloController.
/// </summary>
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public NetworkVariable<GameState> EstadoActual = new NetworkVariable<GameState>(
        GameState.SalaEspera,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Awake() => Instance = this;

    /// <summary>
    /// Llamado por el Host al pulsar P.
    /// </summary>
    public void EmpezarPartida()
    {
        if (!IsServer) return;
        if (EstadoActual.Value == GameState.EnJuego) return;

        Debug.Log("[GameManager] Empezando partida");
        EstadoActual.Value = GameState.EnJuego;
    }

    private void Update()
    {
        if (EstadoActual.Value == GameState.EnJuego)
        {
            GameObject canvas = GameObject.Find("Canvas");

            if (canvas != null)
            canvas.SetActive(false);
        }
    }
}