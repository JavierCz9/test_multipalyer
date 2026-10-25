using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    // ESTADO DE PARTIDA
    // ============================================================
    public NetworkVariable<GameState> EstadoActual = new NetworkVariable<GameState>(
        GameState.SalaEspera,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ============================================================
    // TIMER
    // ============================================================
    [Header("Timer")]
    [SerializeField] private float duracionPartida = 120f;
    public float DuracionPartida => duracionPartida;

    public NetworkVariable<double> TiempoInicioPartida = new NetworkVariable<double>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ============================================================
    // CONTADORES
    // ============================================================
    private NetworkList<int> contadoresPorColor;
    public NetworkList<int> ContadoresPorColor => contadoresPorColor;

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

            // Arrancamos la partida automáticamente al cargar la escena
            EmpezarPartida();
        }
    }

    public void EmpezarPartida()
    {
        if (!IsServer) return;
        if (EstadoActual.Value == GameState.EnJuego) return;

        Debug.Log("[GameManager] Iniciando partida en la GameScene");

        // ✅ Limpiamos la memoria del ganador anterior para que el panel no aparezca
        EventosJuego.LimpiarGanador();

        for (int i = 0; i < contadoresPorColor.Count; i++)
            contadoresPorColor[i] = 0;

        TiempoInicioPartida.Value = NetworkManager.Singleton.ServerTime.Time;
        EstadoActual.Value = GameState.EnJuego;

        EventosJuego.PublicarEstado(GameState.EnJuego);
    }

    private void Update()
    {
        if (!IsServer) return;
        if (EstadoActual.Value != GameState.EnJuego) return;

        if (CalcularTiempoRestante() <= 0f)
            TerminarPartida();
    }

    public float CalcularTiempoRestante()
    {
        double ahora = NetworkManager.Singleton.ServerTime.Time;
        double transcurrido = ahora - TiempoInicioPartida.Value;
        return Mathf.Max(0f, duracionPartida - (float)transcurrido);
    }

    public void ActualizarContador(byte colorAnterior, byte colorNuevo)
    {
        if (!IsServer) return;
        if (colorAnterior == colorNuevo) return;

        if (colorAnterior > 0 && colorAnterior < contadoresPorColor.Count)
            contadoresPorColor[colorAnterior] = Mathf.Max(0, contadoresPorColor[colorAnterior] - 1);

        if (colorNuevo > 0 && colorNuevo < contadoresPorColor.Count)
            contadoresPorColor[colorNuevo] = contadoresPorColor[colorNuevo] + 1;
    }

    public int ObtenerBaldosas(byte colorId)
    {
        if (contadoresPorColor == null || colorId >= contadoresPorColor.Count) return 0;
        return contadoresPorColor[colorId];
    }

    private void TerminarPartida()
    {
        if (!IsServer) return;
        if (EstadoActual.Value == GameState.Finalizado) return;

        byte colorGanador = 0;
        int mayorCantidad = -1;
        bool hayEmpate = false;

        // ✅ Lógica de empate
        for (int i = 1; i < contadoresPorColor.Count; i++)
        {
            if (contadoresPorColor[i] > mayorCantidad)
            {
                mayorCantidad = contadoresPorColor[i];
                colorGanador = (byte)i;
                hayEmpate = false;
            }
            else if (contadoresPorColor[i] == mayorCantidad && mayorCantidad > 0)
            {
                hayEmpate = true;
            }
        }

        if (hayEmpate || mayorCantidad <= 0)
        {
            colorGanador = 0;
            if (mayorCantidad <= 0) mayorCantidad = 0;
        }

        Debug.Log($"[GameManager] GANADOR: color {colorGanador} con {mayorCantidad} tiles");

        EstadoActual.Value = GameState.Finalizado;

        EventosJuego.PublicarEstado(GameState.Finalizado);
        AnunciarGanadorRpc(colorGanador, mayorCantidad);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AnunciarGanadorRpc(byte colorGanador, int baldosas)
    {
        Debug.Log($"🏆 [GANADOR] Color {colorGanador} con {baldosas} tiles");
        EventosJuego.PublicarGanador(colorGanador, baldosas);
    }

    public void VolverAlLobby()
    {
        if (!IsServer) return;
        if (EstadoActual.Value != GameState.Finalizado) return;

        Debug.Log("[GameManager] Cargando LobbyScene...");

        NetworkManager.Singleton.SceneManager.LoadScene("LobbyScene", LoadSceneMode.Single);
    }
}