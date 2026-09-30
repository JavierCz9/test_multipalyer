using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Fases de la partida.
/// </summary>
public enum GameState
{
    SalaEspera = 0, // los jugadores esperan, no pueden moverse ni hay piso
    EnJuego = 1     // el piso está generado y los jugadores pueden moverse
}

/// <summary>
/// Gestor central de la partida.
/// - Controla el estado (SalaEspera / EnJuego).
/// - Calcula el tamaño del grid al empezar.
/// - Genera las baldosas localmente en cada peer.
/// - Sincroniza el color de las baldosas vía NetworkList.
/// Es el ÚNICO NetworkObject relacionado con el suelo.
/// </summary>
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Configuración del grid")]
    // Baldosas por jugador. Con 4 jugadores → 200 baldosas.
    public int BaldosasPorJugador = 50;

    // Tamaño mínimo del lado del grid. Con 1-2 jugadores no queremos
    // un grid diminuto de 7x7; forzamos 10x10 como mínimo.
    public int MinLado = 10;

    // Separación física entre baldosas.
    public float TamBaldosa = 1f;

    [Header("Referencias")]
    public GameObject TilePrefab;

    // -------------------- ESTADO DE RED --------------------

    // Fase actual. La escribe el servidor, todos la leen.
    public NetworkVariable<GameState> EstadoActual = new NetworkVariable<GameState>(
        GameState.SalaEspera,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Tamaño del grid. Se fijan UNA VEZ al empezar, y no cambian más.
    private NetworkVariable<int> anchoGrid = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> altoGrid = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Lista de IDs de color por baldosa. 0 = sin pisar, 1..8 = jugador.
    // NetworkList solo acepta tipos no administrados; byte nos sirve.
    private NetworkList<byte> idsColores;

    // -------------------- ESTADO LOCAL --------------------

    // Referencias a las baldosas instanciadas en este peer.
    private Tile[] baldosas;

    // Bandera local para evitar generar dos veces por callbacks duplicados.
    private bool gridGenerado = false;

    private void Awake()
    {
        Instance = this;

        // NetworkList se inicializa en Awake, no en la declaración.
        idsColores = new NetworkList<byte>();
    }

    public override void OnNetworkSpawn()
    {
        // Escuchamos cambios de estado.
        EstadoActual.OnValueChanged += AlCambiarEstado;

        // Escuchamos cambios de tamaño. Cualquiera de los dos puede llegar
        // primero, así que verificamos los dos en cada callback.
        anchoGrid.OnValueChanged += AlCambiarTamano;
        altoGrid.OnValueChanged += AlCambiarTamano;

        // Escuchamos cambios de color.
        idsColores.OnListChanged += AlCambiarColor;
    }

    // -------------------------------------------------------------------
    // EMPEZAR PARTIDA (solo Host)
    // -------------------------------------------------------------------

    /// <summary>
    /// Llamado por el Host para empezar la partida.
    /// Cuenta los jugadores presentes AHORA, calcula el tamaño del grid
    /// y lo publica. A partir de aquí el tamaño no cambia.
    /// </summary>
    public void EmpezarPartida()
    {
        // Seguridad: solo el servidor puede invocar esto.
        if (!IsServer) return;
        if (EstadoActual.Value == GameState.EnJuego) return;

        // Cantidad real de jugadores conectados en este instante.
        // El Host también cuenta como cliente.
        int jugadores = NetworkManager.Singleton.ConnectedClientsIds.Count;

        // Total de baldosas deseadas.
        int totalBaldosas = jugadores * BaldosasPorJugador;

        // Lado del grid cuadrado. Ej: 200 baldosas → √200 ≈ 14.1 → 15.
        int lado = Mathf.CeilToInt(Mathf.Sqrt(totalBaldosas));

        // Aplicamos el mínimo para que con 1-2 jugadores no quede diminuto.
        lado = Mathf.Max(lado, MinLado);

        // Publicamos ancho y alto. Esto dispara AlCambiarTamano
        // en todos los peers (incluido el servidor).
        anchoGrid.Value = lado;
        altoGrid.Value = lado;

        // Inicializamos la lista de colores con el tamaño definitivo.
        // Todos empiezan en 0 (blanco).
        idsColores.Clear();
        for (int i = 0; i < lado * lado; i++)
            idsColores.Add(0);

        // Cambiamos el estado. Esto hace que todos los clientes
        // generen el piso y habiliten el movimiento.
        EstadoActual.Value = GameState.EnJuego;
    }

    // -------------------------------------------------------------------
    // REACCIÓN A CAMBIOS DE RED
    // -------------------------------------------------------------------

    private void AlCambiarEstado(GameState viejo, GameState nuevo)
    {
        // Cuando pasamos a EnJuego, generamos el grid si aún no está.
        if (nuevo == GameState.EnJuego && !gridGenerado)
        {
            // Aseguramos que ya tenemos el tamaño.
            if (anchoGrid.Value > 0 && altoGrid.Value > 0)
                GenerarBaldosas(anchoGrid.Value, altoGrid.Value);
        }
    }

    private void AlCambiarTamano(int viejo, int nuevo)
    {
        // Este callback se dispara dos veces (una por cada NetworkVariable).
        // El guard gridGenerado evita que se genere dos veces.
        if (anchoGrid.Value > 0 && altoGrid.Value > 0 &&
            !gridGenerado &&
            EstadoActual.Value == GameState.EnJuego)
        {
            GenerarBaldosas(anchoGrid.Value, altoGrid.Value);
        }
    }

    // -------------------------------------------------------------------
    // GENERACIÓN LOCAL DE BALDOSAS (sin red)
    // -------------------------------------------------------------------

    private void GenerarBaldosas(int ancho, int alto)
    {
        if (gridGenerado) return;
        gridGenerado = true;

        baldosas = new Tile[ancho * alto];

        for (int x = 0; x < ancho; x++)
        {
            for (int z = 0; z < alto; z++)
            {
                int indice = x + z * ancho;

                // Centramos el grid en el origen. Sin esto, con 30x30
                // el jugador aparecería en una esquina y no en el centro.
                Vector3 pos = new Vector3(
                    (x - (ancho - 1) * 0.5f) * TamBaldosa,
                    0f,
                    (z - (alto - 1) * 0.5f) * TamBaldosa
                );

                GameObject go = Instantiate(TilePrefab, pos, Quaternion.identity, transform);
                Tile t = go.GetComponent<Tile>();
                t.Indice = indice;

                // Color inicial según la NetworkList (normalmente blanco).
                byte idColor = (indice < idsColores.Count) ? idsColores[indice] : (byte)0;
                t.EstablecerColor(PlayerPalette.Obtener(idColor));

                baldosas[indice] = t;
            }
        }

        Debug.Log($"[GameManager] Grid generado: {ancho}×{alto} = {ancho * alto} baldosas");
    }

    // -------------------------------------------------------------------
    // SINCRONIZACIÓN DE COLORES
    // -------------------------------------------------------------------

    private void AlCambiarColor(NetworkListEvent<byte> evento)
    {
        // Ignoramos cualquier evento que no sea un cambio de valor.
        // (Add/Remove/Clear los usamos solo en EmpezarPartida.)
        if (evento.Type != NetworkListEvent<byte>.EventType.Value) return;

        // Guard defensivo: durante transiciones la lista podría ser
        // más grande que las baldosas locales.
        if (baldosas == null || evento.Index >= baldosas.Length) return;

        // Traducimos el ID a color y pintamos la baldosa local.
        Color32 color = PlayerPalette.Obtener(evento.Value);
        baldosas[evento.Index].EstablecerColor(color);
    }

    /// <summary>
    /// El servidor llama a esto cuando un jugador pisa una baldosa.
    /// Escribir en la NetworkList propaga el cambio a todos los clientes.
    /// </summary>
    public void EstablecerColorBaldosa(int indice, byte idColor)
    {
        if (!IsServer) return;
        if (indice < 0 || indice >= idsColores.Count) return;

        idsColores[indice] = idColor;
    }

    // -------------------------------------------------------------------
    // LIMPIEZA
    // -------------------------------------------------------------------

    public override void OnNetworkDespawn()
    {
        EstadoActual.OnValueChanged -= AlCambiarEstado;
        anchoGrid.OnValueChanged -= AlCambiarTamano;
        altoGrid.OnValueChanged -= AlCambiarTamano;
        idsColores.OnListChanged -= AlCambiarColor;
    }
}