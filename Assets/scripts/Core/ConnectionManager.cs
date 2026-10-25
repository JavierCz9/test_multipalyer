using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    private const int MaxJugadoresPermitidos = 4;
    private NetworkManager networkManager;

    [Header("UI del Lobby")]
    [SerializeField] private GameObject menuLobby;
    [SerializeField] private GameObject salaPanel;
    [SerializeField] private TMP_InputField inputJoinCode;
    [SerializeField] private TMP_Text textoJoinCode;
    [SerializeField] private TMP_Text textoBotonReady;
    [SerializeField] private GameObject botonIniciarObj;

    private ISession session;
    public bool EstoyListo { get; private set; } = false;
    public string MiNombre { get; private set; } = "Jugador";
    public bool EsHost => session != null && session.IsHost;
    public ISession SessionActual => session;

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        networkManager = GetComponent<NetworkManager>();
        MostrarMenu();

        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch (Exception e) { Debug.LogError("[SESSION] Error: " + e); }

        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = VerificarConexion;

        networkManager.OnClientDisconnectCallback += AlPerderConexionConHost;
    }

    private void OnDestroy()
    {
        if (networkManager != null)
            networkManager.OnClientDisconnectCallback -= AlPerderConexionConHost;
    }

    private void AlPerderConexionConHost(ulong clientId)
    {
        // ✅ FIX CS0176: Usamos la clase NetworkManager en lugar de la instancia networkManager
        if (!networkManager.IsServer && clientId == NetworkManager.ServerClientId)
        {
            Debug.LogWarning("[SESSION] El Host se desconectó. Volviendo al Menú Principal...");
            SceneManager.LoadScene("MainMenuScene");
        }
    }

    public async void CrearPartidaRelay(string nombreJugador)
    {
        if (session != null) return;
        MiNombre = string.IsNullOrWhiteSpace(nombreJugador) ? "Jugador" : nombreJugador.Trim();

        try
        {
            var options = new SessionOptions
            {
                MaxPlayers = MaxJugadoresPermitidos,
                Name = "StealIsland",
                IsPrivate = false
            }.WithRelayNetwork();

            session = await MultiplayerService.Instance.CreateSessionAsync(options);
            session.Changed += AlCambiarSala;

            await GuardarDatosJugador(MiNombre, false);
            EstoyListo = false;

            MostrarSala();
        }
        catch (Exception e) { Debug.LogError("[SESSION] Error creando: " + e); }
    }

    public async void UnirseAPartidaRelay(string codigo, string nombreJugador)
    {
        if (string.IsNullOrWhiteSpace(codigo) || session != null) return;
        MiNombre = string.IsNullOrWhiteSpace(nombreJugador) ? "Jugador" : nombreJugador.Trim();

        try
        {
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(codigo);
            session.Changed += AlCambiarSala;

            await GuardarDatosJugador(MiNombre, false);
            EstoyListo = false;

            MostrarSala();
        }
        catch (Exception e) { Debug.LogError("[SESSION] Error uniéndose: " + e); }
    }

    public async Task SalirYApagarRed()
    {
        if (session != null)
        {
            try
            {
                await session.LeaveAsync();
                session = null;
                Debug.Log("[SESSION] Sesión abandonada correctamente.");
            }
            catch (Exception e)
            {
                Debug.LogError("[SESSION] Error al salir de la sesión: " + e.Message);
            }
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
            Debug.Log("[NETWORK] NetworkManager apagado y destruido.");
        }
    }

    private async Task GuardarDatosJugador(string nombre, bool ready)
    {
        try
        {
            var properties = new Dictionary<string, PlayerProperty>
            {
                { "PlayerName", new PlayerProperty(nombre, VisibilityPropertyOptions.Member) },
                { "IsReady", new PlayerProperty(ready.ToString().ToLower(), VisibilityPropertyOptions.Member) }
            };
            session.CurrentPlayer.SetProperties(properties);
            await session.SaveCurrentPlayerDataAsync();
        }
        catch (Exception e) { Debug.LogError("[LOBBY] Error guardando datos: " + e.Message); }
    }

    public async void ToggleReady()
    {
        if (session == null) return;
        EstoyListo = !EstoyListo;

        var properties = new Dictionary<string, PlayerProperty>
        {
            { "IsReady", new PlayerProperty(EstoyListo.ToString().ToLower(), VisibilityPropertyOptions.Member) }
        };
        session.CurrentPlayer.SetProperties(properties);
        await session.SaveCurrentPlayerDataAsync();

        if (textoBotonReady != null)
            textoBotonReady.text = EstoyListo ? "Cancelar Listo" : "Marcar Listo";

        EventosJuego.PublicarSalaActualizada();
    }

    public bool TodosListos()
    {
        if (session == null || session.Players == null || session.Players.Count == 0) return false;
        foreach (var player in session.Players)
        {
            if (!player.Properties.TryGetValue("IsReady", out var prop) || prop.Value != "true") return false;
        }
        return true;
    }

    public void OnClickIniciarPartida()
    {
        if (!EsHost || !TodosListos()) return;
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void AlCambiarSala() => EventosJuego.PublicarSalaActualizada();

    private void VerificarConexion(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (networkManager.ConnectedClientsIds.Count >= MaxJugadoresPermitidos)
        {
            response.Approved = false;
            response.Reason = "Servidor lleno";
            return;
        }
        response.Approved = true;
        response.CreatePlayerObject = true;
    }

    private void MostrarMenu()
    {
        if (menuLobby != null) menuLobby.SetActive(true);
        if (salaPanel != null) salaPanel.SetActive(false);
    }

    private void MostrarSala()
    {
        if (menuLobby != null) menuLobby.SetActive(false);
        if (salaPanel != null) salaPanel.SetActive(true);
        EventosJuego.PublicarSalaActualizada();
    }
}