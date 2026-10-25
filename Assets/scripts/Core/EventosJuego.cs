using System;
using UnityEngine;

/// <summary>
/// Bus de eventos central estático.
/// El juego publica lo que pasa, la UI escucha y reacciona.
/// Guarda Snapshots (el último valor) para que las pantallas que se 
/// abren tarde puedan leer el estado actual sin perderse el evento.
/// </summary>
public static class EventosJuego
{
    // ============================================================
    // 1. ESTADO DE LA PARTIDA
    // ============================================================
    public static GameState EstadoActualSnapshot { get; private set; } = GameState.SalaEspera;
    public static event Action<GameState> OnEstadoCambiado;

    public static void PublicarEstado(GameState nuevoEstado)
    {
        EstadoActualSnapshot = nuevoEstado;
        OnEstadoCambiado?.Invoke(nuevoEstado);
    }

    // ============================================================
    // 2. GANADOR DE LA PARTIDA
    // ============================================================
    public static byte ColorGanadorSnapshot { get; private set; } = 0;
    public static int BaldosasGanadorSnapshot { get; private set; } = 0;
    public static bool HayGanadorSnapshot { get; private set; } = false;
    public static event Action<byte, int> OnGanadorAnunciado;

    public static void PublicarGanador(byte color, int baldosas)
    {
        ColorGanadorSnapshot = color;
        BaldosasGanadorSnapshot = baldosas;
        HayGanadorSnapshot = true;
        OnGanadorAnunciado?.Invoke(color, baldosas);
    }

    public static void LimpiarGanador()
    {
        ColorGanadorSnapshot = 0;
        BaldosasGanadorSnapshot = 0;
        HayGanadorSnapshot = false;
    }

    // ============================================================
    // 3. EVENTOS DEL LOBBY 
    // ============================================================
    // Avisa a la UI que la lista de jugadores o sus estados cambiaron.
    public static event Action OnSalaActualizada;

    public static void PublicarSalaActualizada()
    {
        OnSalaActualizada?.Invoke();
    }

    // Avisa a la UI que la partida arrancó y hay que ocultar el lobby.
    public static event Action OnPartidaIniciada;

    public static void PublicarPartidaIniciada()
    {
        OnPartidaIniciada?.Invoke();
    }
}