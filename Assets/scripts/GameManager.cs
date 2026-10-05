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
    // TIMER
    // ============================================================
    // En lugar de sincronizar el tiempo restante cada frame
    // (que genera ~60 paquetes por segundo), sincronizamos
    // SOLO el instante en que empezó la partida.
    //
    // Cada cliente calcula su tiempo localmente usando ServerTime,
    // que es un reloj compartido y sincronizado entre todas las máquinas.

    [Header("Timer")]
    [SerializeField] private float duracionPartida = 120f;

    // Propiedad pública para que el TimerUI lea la duración.
    public float DuracionPartida => duracionPartida;

    // Instante en que empezó la partida, según el reloj del servidor.
    // Se escribe UNA SOLA VEZ al pulsar "Iniciar Partida".
    // Tipo double porque ServerTime.Time es double y necesitamos precisión.
    public NetworkVariable<double> TiempoInicioPartida =
        new NetworkVariable<double>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

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
        // Solo el servidor inicializa la lista de contadores.
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

        // Resetear contadores.
        for (int i = 0; i < contadoresPorColor.Count; i++)
            contadoresPorColor[i] = 0;

        // ✅ CLAVE: guardamos el instante de inicio según el reloj del servidor.
        // Este valor se sincroniza a todos los clientes UNA SOLA VEZ.
        // A partir de acá, cada cliente calcula su tiempo restante localmente.
        TiempoInicioPartida.Value = NetworkManager.Singleton.ServerTime.Time;

        EstadoActual.Value = GameState.EnJuego;
    }

    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        // Mostrar el Canvas del lobby SOLO en SalaEspera.
        if (canvasLobby != null)
        {
            bool deberiaEstarActivo = (EstadoActual.Value == GameState.SalaEspera);

            if (canvasLobby.activeSelf != deberiaEstarActivo)
                canvasLobby.SetActive(deberiaEstarActivo);
        }

        // El timer solo lo controla el servidor.
        if (!IsServer) return;
        if (EstadoActual.Value != GameState.EnJuego) return;

        // ✅ Calculamos el tiempo restante a partir del instante de inicio.
        // No restamos deltaTime cada frame (eso acumulaba error).
        // Tampoco escribimos en una NetworkVariable cada frame.
        float restante = CalcularTiempoRestante();

        if (restante <= 0f)
            TerminarPartida();
    }

    /// <summary>
    /// Devuelve cuántos segundos quedan de partida, calculado localmente
    /// a partir del instante de inicio compartido por el servidor.
    /// Sirve tanto para el servidor como para los clientes.
    /// </summary>
    public float CalcularTiempoRestante()
    {
        // Reloj compartido: es el mismo valor en todas las máquinas
        // (dentro de unos pocos milisegundos).
        double ahora = NetworkManager.Singleton.ServerTime.Time;

        // Tiempo transcurrido desde el inicio.
        double transcurrido = ahora - TiempoInicioPartida.Value;

        // Tiempo restante = duración - transcurrido. Nunca negativo.
        float restante = duracionPartida - (float)transcurrido;

        return Mathf.Max(0f, restante);
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

        // Buscar el ganador (el color con más tiles).
        byte colorGanador = 0;
        int mayorCantidad = -1;

        for (int i = 1; i < contadoresPorColor.Count; i++)
        {
            if (contadoresPorColor[i] > mayorCantidad)
            {
                mayorCantidad = contadoresPorColor[i];
                colorGanador = (byte)i;
            }
        }

        // Si nadie pintó nada, es empate.
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
        Debug.Log($" [GANADOR] Color {colorGanador} con {baldosas} tiles");
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

        // Resetear contadores.
        for (int i = 0; i < contadoresPorColor.Count; i++)
            contadoresPorColor[i] = 0;

        // Resetear tiles a su color original.
        if (SueloController.Instance != null)
            SueloController.Instance.ResetearTodasLasTiles();

        // El TiempoInicioPartida no hace falta resetearlo: al empezar la
        // próxima partida se sobrescribe con el nuevo instante.

        EstadoActual.Value = GameState.SalaEspera;
    }
}