using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public enum GameState
{
    SalaEspera = 0,
    EnJuego = 1
}

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public NetworkVariable<GameState> EstadoActual =
        new NetworkVariable<GameState>(
            GameState.SalaEspera,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // ============================================================
    // TIEMPO
    // ============================================================

    public NetworkVariable<float> TiempoRestante =
        new NetworkVariable<float>(
            180f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private const float DURACION_PARTIDA = 120f;

    // ============================================================
    // BALDOSAS
    // ============================================================

    private Dictionary<ulong, int> contadoresBaldosas =
        new Dictionary<ulong, int>();

    private void Awake()
    {
        Instance = this;
    }

    // ============================================================
    // EMPEZAR PARTIDA
    // ============================================================

    public void EmpezarPartida()
    {
        if (!IsServer)
            return;

        if (EstadoActual.Value == GameState.EnJuego)
            return;

        Debug.Log("[GameManager] Empezando partida");

        TiempoRestante.Value = DURACION_PARTIDA;

        EstadoActual.Value = GameState.EnJuego;
    }

    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (EstadoActual.Value == GameState.EnJuego)
        {
            // Ocultar el menú cuando empieza la partida.
            GameObject canvas = GameObject.Find("Canvas");

            if (canvas != null)
                canvas.SetActive(false);
        }

        // El timer solamente lo controla el servidor.
        if (!IsServer)
            return;

        if (EstadoActual.Value != GameState.EnJuego)
            return;

        if (TiempoRestante.Value > 0f)
        {
            TiempoRestante.Value -= Time.deltaTime;

            if (TiempoRestante.Value <= 0f)
            {
                TiempoRestante.Value = 0f;
                TerminarPartida();
            }
        }
    }

    // ============================================================
    // TERMINAR PARTIDA
    // ============================================================

    private void TerminarPartida()
    {
        if (!IsServer)
            return;

        Debug.Log("[GameManager] TIEMPO TERMINADO");

        ulong ganador = ulong.MaxValue;
        int mayorCantidad = -1;

        foreach (var jugador in contadoresBaldosas)
        {
            Debug.Log(
                $"[GameManager] Player {jugador.Key}: " +
                $"{jugador.Value} baldosas"
            );

            if (jugador.Value > mayorCantidad)
            {
                mayorCantidad = jugador.Value;
                ganador = jugador.Key;
            }
        }

        if (ganador != ulong.MaxValue)
        {
            Debug.Log(
                $"[GameManager] GANADOR: Player {ganador} " +
                $"con {mayorCantidad} baldosas."
            );
        }

        EstadoActual.Value = GameState.SalaEspera;
    }

    // ============================================================
    // CONTADOR DE BALDOSAS
    // ============================================================

    public void SumarBaldosa(ulong clientId)
    {
        if (!IsServer)
            return;

        if (!contadoresBaldosas.ContainsKey(clientId))
            contadoresBaldosas[clientId] = 0;

        contadoresBaldosas[clientId]++;

        Debug.Log(
            $"[GameManager] Player {clientId} tiene " +
            $"{contadoresBaldosas[clientId]} baldosas."
        );
    }

    public void RestarBaldosa(ulong clientId)
    {
        if (!IsServer)
            return;

        if (!contadoresBaldosas.ContainsKey(clientId))
            contadoresBaldosas[clientId] = 0;

        contadoresBaldosas[clientId]--;

        if (contadoresBaldosas[clientId] < 0)
            contadoresBaldosas[clientId] = 0;

        Debug.Log(
            $"[GameManager] Player {clientId} tiene " +
            $"{contadoresBaldosas[clientId]} baldosas."
        );
    }

    public int ObtenerBaldosas(ulong clientId)
    {
        if (contadoresBaldosas.ContainsKey(clientId))
            return contadoresBaldosas[clientId];

        return 0;
    }
}