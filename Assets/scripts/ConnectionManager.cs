using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Gestiona cómo arranca la partida (Host o Client) y controla el acceso
/// rechazando conexiones nuevas cuando la partida ya comenzó.
/// También permite al Host empezar la partida con la tecla P.
/// NO tiene lógica de juego: esa vive en GameManager.
/// </summary>
public class ConnectionManager : MonoBehaviour
{
    // Capacidad máxima de jugadores. Puedes subirlo cuando añadas
    // más colores a la paleta (PlayerPalette).
    private const int MaxJugadoresPermitidos = 8;

    private NetworkManager networkManager;

    private void Awake()
    {
        // Referencia al NetworkManager del mismo GameObject.
        // Por eso este script DEBE estar en el mismo objeto que NetworkManager.
        networkManager = GetComponent<NetworkManager>();

        // Activamos la aprobación de conexión para poder aceptar/rechazar.
        // Sin esto, NGO aceptaría a cualquiera sin control.
        networkManager.NetworkConfig.ConnectionApproval = true;

        // Registramos el callback que se ejecuta en el servidor
        // cada vez que un cliente intenta conectarse.
        networkManager.ConnectionApprovalCallback = VerificarConexion;
    }

    /// <summary>
    /// Se ejecuta SOLO en el servidor cuando un cliente intenta conectarse.
    /// Decide si aceptar o rechazar la conexión.
    /// </summary>
    private void VerificarConexion(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        // Si la partida ya arrancó, rechazamos a quien llega tarde.
        // GameManager.Instance puede ser null en los primeros ms del
        // arranque del servidor, por eso el chequeo defensivo.
        if (GameManager.Instance != null &&
            GameManager.Instance.EstadoActual.Value == GameState.EnJuego)
        {
            response.Approved = false;
            response.Reason = "La partida ya comenzó";
            return;
        }

        // Rechazamos si ya estamos al máximo permitido.
        // ConnectedClientsIds incluye al Host, que cuenta como cliente.
        if (networkManager.ConnectedClientsIds.Count >= MaxJugadoresPermitidos)
        {
            response.Approved = false;
            response.Reason = $"Servidor lleno (máx {MaxJugadoresPermitidos})";
            return;
        }

        // Aprobamos la conexión y pedimos que NGO genere el Player Prefab
        // automáticamente para este cliente. Sin esto, el jugador
        // entraría a la escena sin personaje.
        response.Approved = true;
        response.CreatePlayerObject = true;
    }

    // ---------------------- MÉTODOS PÚBLICOS ----------------------
    // Se pueden enganchar a botones de UI cuando la añadamos.

    public void IniciarHost() => networkManager.StartHost();
    public void IniciarCliente() => networkManager.StartClient();
    public void IniciarServidor() => networkManager.StartServer();

    // ---------------------- ATAJOS DE TECLADO ----------------------
    // Solo para pruebas rápidas en el editor.
    //
    //   H = arrancar Host (servidor + cliente local)
    //   C = arrancar Client (solo cliente)
    //   P = empezar la partida (solo Host; los clientes la ignoran)
    //
    // Cuando tengamos UI, estos atajos se pueden quitar o dejar
    // como modo desarrollador.

    private void Update()
    {
        // H → arrancar Host.
        if (Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log("[ConnectionManager] Iniciando Host...");
            IniciarHost();
        }

        // C → arrancar Client.
        if (Input.GetKeyDown(KeyCode.C))
        {
            Debug.Log("[ConnectionManager] Iniciando Client...");
            IniciarCliente();
        }

        // P → empezar partida. Solo el Host (servidor) puede dispararlo.
        // El chequeo de IsServer es doble seguridad: además de que
        // EmpezarPartida ya filtra por IsServer internamente, aquí
        // evitamos llamadas inútiles desde clientes.
        if (Input.GetKeyDown(KeyCode.P) && networkManager.IsServer)
        {
            if (GameManager.Instance != null)
            {
                Debug.Log("[ConnectionManager] Empezando partida...");
                GameManager.Instance.EmpezarPartida();
            }
            else
            {
                Debug.LogWarning("[ConnectionManager] GameManager.Instance es null. " +
                                 "¿Falta el objeto GameManager en la escena?");
            }
        }
    }
}