using Unity.Netcode;
using UnityEngine;

public enum GameState
{
    SalaEspera = 0,
    EnJuego = 1,
    Finalizado = 2
}

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    // ============================================================
    // REFERENCIAS UI
    // ============================================================
    // Asignar el Canvas del lobby desde el Inspector.
    // Evita usar GameObject.Find (que puede agarrar el Canvas equivocado).

    [Header("Referencias UI")]
    [SerializeField] private GameObject canvasLobby;

    // ============================================================
    // ESTADO DE PARTIDA
    // ============================================================

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

    private const float DURACION_PARTIDA = 15f;

    // ============================================================
    // CONTADORES POR COLORID
    // ============================================================

    private NetworkList<int> contadoresPorColor;

    public NetworkList<int> ContadoresPorColor => contadoresPorColor;

    // ============================================================
    // EVENTO DE GANADOR
    // ============================================================

    public static event System.Action<byte, int> OnGanadorAnunciado;

    // ============================================================
    // AWAKE / SPAWN
    // ============================================================

    private void Awake()
    {
        Instance = this;
        contadoresPorColor = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            contadoresPorColor.Clear();
            for (int i = 0; i <= PlayerPalette.CantidadJugadores; i++)
                contadoresPorColor.Add(0);
        }
    }

    // ============================================================
    // EMPEZAR PARTIDA
    // ============================================================

    public void EmpezarPartida()
    {
        if (!IsServer) return;
        if (EstadoActual.Value == GameState.EnJuego) return;

        Debug.Log("[GameManager] Empezando partida");

        for (int i = 0; i < contadoresPorColor.Count; i++)
            contadoresPorColor[i] = 0;

        TiempoRestante.Value = DURACION_PARTIDA;
        EstadoActual.Value = GameState.EnJuego;
    }

    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        // Mostrar u ocultar el Canvas del lobby según el estado.
        if(canvasLobby != null)
{
            // ✅ El Canvas del lobby SOLO se muestra en SalaEspera.
            // En EnJuego y en Finalizado, está oculto.
            bool deberiaEstarActivo = (EstadoActual.Value == GameState.SalaEspera);

            if (canvasLobby.activeSelf != deberiaEstarActivo)
                canvasLobby.SetActive(deberiaEstarActivo);
        }

        // Timer (solo servidor).
        if (!IsServer) return;
        if (EstadoActual.Value != GameState.EnJuego) return;

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
    // CONTADORES
    // ============================================================

    public void ActualizarContador(byte colorAnterior, byte colorNuevo)
    {
        if (!IsServer) return;
        if (colorAnterior == colorNuevo) return;

        if (colorAnterior > 0 && colorAnterior < contadoresPorColor.Count)
        {
            int valorActual = contadoresPorColor[colorAnterior];
            contadoresPorColor[colorAnterior] = Mathf.Max(0, valorActual - 1);
        }

        if (colorNuevo > 0 && colorNuevo < contadoresPorColor.Count)
        {
            contadoresPorColor[colorNuevo] = contadoresPorColor[colorNuevo] + 1;
        }
    }

    public int ObtenerBaldosas(byte colorId)
    {
        if (contadoresPorColor == null) return 0;
        if (colorId < contadoresPorColor.Count)
            return contadoresPorColor[colorId];
        return 0;
    }

    // ============================================================
    // TERMINAR PARTIDA
    // ============================================================

    private void TerminarPartida()
    {
        if (!IsServer) return;
        if (EstadoActual.Value == GameState.Finalizado) return;

        Debug.Log("[GameManager] TIEMPO TERMINADO");

        byte colorGanador = 0;
        int mayorCantidad = -1;

        for (int i = 1; i < contadoresPorColor.Count; i++)
        {
            Debug.Log($"[GameManager] Color {i}: {contadoresPorColor[i]} tiles");

            if (contadoresPorColor[i] > mayorCantidad)
            {
                mayorCantidad = contadoresPorColor[i];
                colorGanador = (byte)i;
            }
        }

        if (mayorCantidad <= 0)
        {
            colorGanador = 0;
            mayorCantidad = 0;
        }

        Debug.Log($"[GameManager] GANADOR: color {colorGanador} con {mayorCantidad} tiles");

        EstadoActual.Value = GameState.Finalizado;
        AnunciarGanadorRpc(colorGanador, mayorCantidad);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AnunciarGanadorRpc(byte colorGanador, int baldosas)
    {
        Debug.Log($"🏆 [GANADOR] Color {colorGanador} con {baldosas} tiles");
        OnGanadorAnunciado?.Invoke(colorGanador, baldosas);
    }

    // ============================================================
    // VOLVER AL LOBBY
    // ============================================================

    public void VolverAlLobby()
    {
        if (!IsServer) return;
        if (EstadoActual.Value != GameState.Finalizado) return;

        Debug.Log("[GameManager] Volviendo al lobby");

        for (int i = 0; i < contadoresPorColor.Count; i++)
            contadoresPorColor[i] = 0;

        if (SueloController.Instance != null)
            SueloController.Instance.ResetearTodasLasTiles();

        TiempoRestante.Value = DURACION_PARTIDA;
        EstadoActual.Value = GameState.SalaEspera;
    }
}