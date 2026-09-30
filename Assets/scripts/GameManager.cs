using Unity.Netcode;
using UnityEngine;

public enum GameState
{
    SalaEspera = 0,
    EnJuego = 1
}

/// <summary>
/// Gestor central de la partida.
/// El tamaño del grid se define desde el Inspector y es FIJO durante
/// toda la partida. No depende del número de jugadores conectados.
/// </summary>
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Configuración del grid")]
    // Tamaño del grid, configurable desde el Inspector.
    // El grid tendrá AnchoGrid × AltoGrid baldosas.
    [Tooltip("Cantidad de baldosas en el eje X (horizontal).")]
    public int AnchoGrid = 20;

    [Tooltip("Cantidad de baldosas en el eje Z (vertical/profundidad).")]
    public int AltoGrid = 15;

    [Tooltip("Distancia entre centros de baldosas. 1.0 = pegadas, " +
             "1.05 = con separación visible.")]
    public float TamBaldosa = 1.05f;

    [Header("Referencias")]
    public GameObject TilePrefab;

    // -------------------- ESTADO DE RED --------------------

    public NetworkVariable<GameState> EstadoActual = new NetworkVariable<GameState>(
        GameState.SalaEspera,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<int> anchoGridRed = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> altoGridRed = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkList<byte> idsColores;

    // -------------------- ESTADO LOCAL --------------------

    private Tile[] baldosas;
    private bool gridGenerado = false;

    private void Awake()
    {
        Instance = this;
        idsColores = new NetworkList<byte>();
    }

    public override void OnNetworkSpawn()
    {
        EstadoActual.OnValueChanged += AlCambiarEstado;
        anchoGridRed.OnValueChanged += AlCambiarTamano;
        altoGridRed.OnValueChanged += AlCambiarTamano;
        idsColores.OnListChanged += AlCambiarColor;
    }

    /// <summary>
    /// Llamado por el Host al pulsar P. Publica el tamaño del grid
    /// (tomado del Inspector) y arranca la partida.
    /// </summary>
    public void EmpezarPartida()
    {
        if (!IsServer) return;
        if (EstadoActual.Value == GameState.EnJuego) return;

        // Usamos los valores configurados en el Inspector.
        int ancho = AnchoGrid;
        int alto = AltoGrid;
        int total = ancho * alto;

        Debug.Log($"[GameManager] Empezando partida → grid {ancho}×{alto} = {total} baldosas");

        // Publicamos el tamaño por red. Esto dispara la generación
        // de baldosas en todos los clientes.
        anchoGridRed.Value = ancho;
        altoGridRed.Value = alto;

        // Inicializamos la NetworkList de colores con el tamaño definitivo.
        idsColores.Clear();
        for (int i = 0; i < total; i++)
            idsColores.Add(0);

        // Cambiamos el estado a EnJuego.
        EstadoActual.Value = GameState.EnJuego;
    }

    private void AlCambiarEstado(GameState viejo, GameState nuevo)
    {
        if (nuevo == GameState.EnJuego && !gridGenerado)
        {
            if (anchoGridRed.Value > 0 && altoGridRed.Value > 0)
                GenerarBaldosas(anchoGridRed.Value, altoGridRed.Value);
        }
    }

    private void AlCambiarTamano(int viejo, int nuevo)
    {
        if (anchoGridRed.Value > 0 && altoGridRed.Value > 0 &&
            !gridGenerado &&
            EstadoActual.Value == GameState.EnJuego)
        {
            GenerarBaldosas(anchoGridRed.Value, altoGridRed.Value);
        }
    }

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

                // Centramos el grid en el origen.
                // Con ancho=20, X va de -9.5×TamBaldosa a +9.5×TamBaldosa.
                // Con alto=15, Z va de -7×TamBaldosa a +7×TamBaldosa.
                Vector3 pos = new Vector3(
                    (x - (ancho - 1) * 0.5f) * TamBaldosa,
                    0f,
                    (z - (alto - 1) * 0.5f) * TamBaldosa
                );

                GameObject go = Instantiate(TilePrefab, pos, Quaternion.identity, transform);
                Tile t = go.GetComponent<Tile>();
                t.Indice = indice;

                byte idColor = (indice < idsColores.Count) ? idsColores[indice] : (byte)0;
                t.EstablecerColor(PlayerPalette.Obtener(idColor));

                baldosas[indice] = t;
            }
        }

        Debug.Log($"[GameManager] Grid generado: {ancho}×{alto} = {ancho * alto} baldosas");
    }

    private void AlCambiarColor(NetworkListEvent<byte> evento)
    {
        if (evento.Type != NetworkListEvent<byte>.EventType.Value) return;
        if (baldosas == null || evento.Index >= baldosas.Length) return;

        Color32 color = PlayerPalette.Obtener(evento.Value);
        baldosas[evento.Index].EstablecerColor(color);
    }

    public void EstablecerColorBaldosa(int indice, byte idColor)
    {
        if (!IsServer) return;
        if (indice < 0 || indice >= idsColores.Count) return;

        idsColores[indice] = idColor;
    }

    public override void OnNetworkDespawn()
    {
        EstadoActual.OnValueChanged -= AlCambiarEstado;
        anchoGridRed.OnValueChanged -= AlCambiarTamano;
        altoGridRed.OnValueChanged -= AlCambiarTamano;
        idsColores.OnListChanged -= AlCambiarColor;
    }
}