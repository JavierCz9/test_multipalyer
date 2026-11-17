using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
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
    private const string Solicitud = "SI_SeleccionSolicitar";
    private const string Respuesta = "SI_SeleccionResponder";
    private const string Estado = "SI_SeleccionEstado";
    private NetworkManager networkManager;

    [Header("UI del Lobby")]
    [SerializeField] private GameObject menuLobby;
    [SerializeField] private GameObject salaPanel;
    [SerializeField] private TMP_InputField inputJoinCode;
    [SerializeField] private TMP_Text textoJoinCode;
    [SerializeField] private TMP_Text textoBotonReady;
    [SerializeField] private GameObject botonIniciarObj;

    private ISession session;
    private bool creandoSesion;
    private bool uniendoseSesion;
    private bool serviciosListos;
    private bool cerrandoSesion;
    private bool mensajesRegistrados;
    private readonly Dictionary<ulong, int> reservas = new Dictionary<ulong, int>();
    private readonly Dictionary<ulong, int> reservasRecibidas = new Dictionary<ulong, int>();
    private readonly Dictionary<ulong, string> idsSesion = new Dictionary<ulong, string>();
    private readonly Dictionary<string, int> personajesPorSesion = new Dictionary<string, int>();
    private bool esperandoConfirmacion;

    public event Action SeleccionActualizada;
    public event Action<bool> ConfirmacionRespondida;
    public bool EstoyListo { get; private set; }
    public int MiPersonaje { get; private set; } = -1;
    public string MiNombre { get; private set; } = "Jugador";
    public bool EsHost => session != null && session.IsHost;
    public ISession SessionActual => session;
    public bool SesionLista => session != null;
    public bool EsperandoConfirmacion => esperandoConfirmacion;

    private async void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        networkManager = GetComponent<NetworkManager>();
        if (menuLobby != null) menuLobby.SetActive(true);
        if (salaPanel != null) salaPanel.SetActive(false);

        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            serviciosListos = true;
        }
        catch (Exception e) { Debug.LogError("[SESSION] Inicialización: " + e); }

        if (networkManager == null) return;
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = VerificarConexion;
        networkManager.OnClientDisconnectCallback += AlPerderConexionConHost;
        networkManager.OnClientConnectedCallback += AlConectarCliente;
    }

    private void Update()
    {
        // Las sesiones arrancan la red de forma asíncrona: registrar mensajes cuando esté disponible.
        if (!mensajesRegistrados && networkManager != null && networkManager.IsListening &&
            networkManager.CustomMessagingManager != null)
            RegistrarMensajes();
    }

    private void RegistrarMensajes()
    {
        var mensajes = networkManager.CustomMessagingManager;
        mensajes.RegisterNamedMessageHandler(Respuesta, RecibirRespuesta);
        mensajes.RegisterNamedMessageHandler(Estado, RecibirEstado);
        if (networkManager.IsServer)
            mensajes.RegisterNamedMessageHandler(Solicitud, RecibirSolicitud);
        mensajesRegistrados = true;
        if (networkManager.IsServer) PublicarReservas();
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback -= AlPerderConexionConHost;
            networkManager.OnClientConnectedCallback -= AlConectarCliente;
            if (mensajesRegistrados && networkManager.CustomMessagingManager != null)
            {
                var mensajes = networkManager.CustomMessagingManager;
                mensajes.UnregisterNamedMessageHandler(Respuesta);
                mensajes.UnregisterNamedMessageHandler(Estado);
                if (networkManager.IsServer) mensajes.UnregisterNamedMessageHandler(Solicitud);
            }
        }
        if (session != null) session.Changed -= AlCambiarSala;
        if (Instance == this) Instance = null;
    }

    private void AlPerderConexionConHost(ulong clientId)
    {
        if (networkManager != null && networkManager.IsServer)
        {
            bool eliminado = reservas.Remove(clientId);
            idsSesion.Remove(clientId);
            if (eliminado) PublicarReservas();
        }
        else if (clientId == NetworkManager.ServerClientId &&
                 session != null && !uniendoseSesion && !cerrandoSesion)
            SceneManager.LoadScene("MainMenuScene");
    }

    private void AlConectarCliente(ulong clientId)
    {
        if (networkManager != null && networkManager.IsServer && mensajesRegistrados)
            EnviarReservas(clientId);
    }

    public bool PersonajeOcupado(int personajeId)
    {
        foreach (int valor in reservasRecibidas.Values)
            if (valor == personajeId) return true;
        return false;
    }

    public bool PersonajeEsMio(int personajeId) => MiPersonaje == personajeId;

    // Identifica al jugador por su ID de Sessions, no por el ID de Netcode.
    public bool PersonajeConfirmado(string sessionPlayerId, out int personajeId)
    {
        return personajesPorSesion.TryGetValue(sessionPlayerId, out personajeId);
    }

    // Consulta exclusiva del servidor: personaje reservado por el cliente de Netcode.
    public bool TryObtenerPersonajeJugador(ulong clientId, out int personajeId)
    {
        personajeId = -1;
        return networkManager != null && networkManager.IsServer &&
               reservas.TryGetValue(clientId, out personajeId);
    }

    public bool JugadorConfirmado(ulong clientId)
    {
        return reservasRecibidas.ContainsKey(clientId);
    }

    public void ConfirmarPersonaje(int personajeId)
    {
        if (personajeId < 0 || personajeId >= 4 || session == null ||
            networkManager == null || !networkManager.IsListening ||
            !mensajesRegistrados || esperandoConfirmacion || EstoyListo ||
            PersonajeOcupado(personajeId)) return;

        esperandoConfirmacion = true;
        if (networkManager.IsServer)
        {
            ResolverSolicitud(networkManager.LocalClientId, personajeId, session.CurrentPlayer.Id);
        }
        else
        {
            using (var writer = new FastBufferWriter(256, Allocator.Temp))
            {
                writer.WriteValueSafe(personajeId);
                var idSesion = new FixedString128Bytes(session.CurrentPlayer.Id);
                writer.WriteValueSafe(idSesion);
                networkManager.CustomMessagingManager.SendNamedMessage(
                    Solicitud, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }
    }

    private void RecibirSolicitud(ulong remitente, FastBufferReader reader)
    {
        if (!networkManager.IsServer) return;
        reader.ReadValueSafe(out int personajeId);
        reader.ReadValueSafe(out FixedString128Bytes idSesion);
        ResolverSolicitud(remitente, personajeId, idSesion.ToString());
    }

    private void ResolverSolicitud(ulong clientId, int personajeId, string idSesion)
    {
        bool disponible = personajeId >= 0 && personajeId < 4 &&
                          !string.IsNullOrEmpty(idSesion) && idSesion.Length <= 128;
        if (disponible)
        {
            foreach (var par in reservas)
                if (par.Key != clientId && par.Value == personajeId)
                    disponible = false;
        }
        // Una elección ya confirmada no se reemplaza desde esta pantalla.
        if (reservas.ContainsKey(clientId)) disponible = false;
        if (disponible)
        {
            reservas[clientId] = personajeId;
            idsSesion[clientId] = idSesion;
            PublicarReservas();
        }
        if (clientId == networkManager.LocalClientId)
            ProcesarRespuesta(disponible, personajeId);
        else
        {
            using (var writer = new FastBufferWriter(sizeof(int) + sizeof(byte), Allocator.Temp))
            {
                writer.WriteValueSafe(disponible);
                writer.WriteValueSafe(personajeId);
                networkManager.CustomMessagingManager.SendNamedMessage(
                    Respuesta, clientId, writer, NetworkDelivery.ReliableSequenced);
            }
        }
    }

    private void RecibirRespuesta(ulong remitente, FastBufferReader reader)
    {
        if (remitente != NetworkManager.ServerClientId) return;
        reader.ReadValueSafe(out bool aceptado);
        reader.ReadValueSafe(out int personajeId);
        ProcesarRespuesta(aceptado, personajeId);
    }

    private async void ProcesarRespuesta(bool aceptado, int personajeId)
    {
        esperandoConfirmacion = false;
        if (aceptado)
        {
            MiPersonaje = personajeId;
            EstoyListo = true;
            try
            {
                var properties = new Dictionary<string, PlayerProperty>
                {
                    { "PersonajeId", new PlayerProperty(personajeId.ToString(), VisibilityPropertyOptions.Member) },
                    { "IsReady", new PlayerProperty("true", VisibilityPropertyOptions.Member) }
                };
                session.CurrentPlayer.SetProperties(properties);
                await session.SaveCurrentPlayerDataAsync();
            }
            catch (Exception e) { Debug.LogError("[LOBBY] Error guardando personaje: " + e); }
            EventosJuego.PublicarSalaActualizada();
        }
        ConfirmacionRespondida?.Invoke(aceptado);
        SeleccionActualizada?.Invoke();
    }

    private void PublicarReservas()
    {
        reservasRecibidas.Clear();
        personajesPorSesion.Clear();
        foreach (var par in reservas)
        {
            reservasRecibidas[par.Key] = par.Value;
            if (idsSesion.TryGetValue(par.Key, out string idSesion))
                personajesPorSesion[idSesion] = par.Value;
        }
        SeleccionActualizada?.Invoke();
        if (networkManager == null || !networkManager.IsServer || !mensajesRegistrados) return;
        foreach (ulong id in networkManager.ConnectedClientsIds)
            if (id != networkManager.LocalClientId) EnviarReservas(id);
    }

    private void EnviarReservas(ulong clientId)
    {
        if (networkManager == null || !networkManager.IsServer || !mensajesRegistrados) return;
        using (var writer = new FastBufferWriter(1024, Allocator.Temp))
        {
            writer.WriteValueSafe(reservas.Count);
            foreach (var par in reservas)
            {
                writer.WriteValueSafe(par.Key);
                writer.WriteValueSafe(par.Value);
                var idSesion = new FixedString128Bytes(idsSesion[par.Key]);
                writer.WriteValueSafe(idSesion);
            }
            networkManager.CustomMessagingManager.SendNamedMessage(
                Estado, clientId, writer, NetworkDelivery.ReliableSequenced);
        }
    }

    private void RecibirEstado(ulong remitente, FastBufferReader reader)
    {
        if (remitente != NetworkManager.ServerClientId) return;
        reader.ReadValueSafe(out int cantidad);
        if (cantidad < 0 || cantidad > MaxJugadoresPermitidos) return;
        reservasRecibidas.Clear();
        personajesPorSesion.Clear();
        for (int i = 0; i < cantidad; i++)
        {
            reader.ReadValueSafe(out ulong clientId);
            reader.ReadValueSafe(out int personajeId);
            reader.ReadValueSafe(out FixedString128Bytes idSesion);
            reservasRecibidas[clientId] = personajeId;
            personajesPorSesion[idSesion.ToString()] = personajeId;
        }
        SeleccionActualizada?.Invoke();
    }

    public async void CrearPartidaRelay(string nombreJugador)
    {
        if (session != null || creandoSesion || uniendoseSesion || cerrandoSesion) return;
        creandoSesion = true;
        try
        {
            if (!await AsegurarServiciosListos()) return;
            var options = new SessionOptions
            {
                MaxPlayers = MaxJugadoresPermitidos,
                Name = "StealIsland",
                IsPrivate = false
            }.WithRelayNetwork();
            session = await MultiplayerService.Instance.CreateSessionAsync(options);
            session.Changed += AlCambiarSala;
            MiNombre = string.IsNullOrWhiteSpace(nombreJugador) ? "Jugador" : nombreJugador.Trim();
            EstoyListo = false;
            MiPersonaje = -1;
            await GuardarDatosJugador(MiNombre, false);
            if (textoJoinCode != null)
            {
                textoJoinCode.text = "CÓDIGO: " + session.Code;
                textoJoinCode.gameObject.SetActive(true);
            }
            EventosJuego.PublicarSalaActualizada();
        }
        catch (Exception e) { Debug.LogError("[SESSION] Error creando: " + e); }
        finally { creandoSesion = false; }
    }

    public async Task<bool> ActualizarNombre(string nombre)
    {
        if (session == null || string.IsNullOrWhiteSpace(nombre)) return false;
        MiNombre = nombre.Trim();
        await GuardarDatosJugador(MiNombre, EstoyListo);
        EventosJuego.PublicarSalaActualizada();
        return true;
    }

    private async Task<bool> AsegurarServiciosListos()
    {
        if (serviciosListos && AuthenticationService.Instance.IsSignedIn) return true;
        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            serviciosListos = true;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[SESSION] No se pudo iniciar Unity Services: " + e);
            return false;
        }
    }

    public async void UnirseAPartidaRelay(string codigo, string nombreJugador)
    {
        codigo = (codigo ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(codigo) || session != null ||
            uniendoseSesion || creandoSesion || cerrandoSesion) return;

        // Bloquea el segundo clic ANTES del primer await.
        uniendoseSesion = true;
        MiNombre = string.IsNullOrWhiteSpace(nombreJugador) ? "Jugador" : nombreJugador.Trim();
        try
        {
            if (!await AsegurarServiciosListos()) return;
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(codigo);
            session.Changed += AlCambiarSala;
            EstoyListo = false;
            MiPersonaje = -1;
            await GuardarDatosJugador(MiNombre, false);
            EventosJuego.PublicarSalaActualizada();
            Debug.Log("[SESSION] Cliente unido a la sala correctamente.");
        }
        catch (Exception e)
        {
            Debug.LogError("[SESSION] Error uniéndose: " + e);
        }
        finally
        {
            uniendoseSesion = false;
        }
    }

    public async Task SalirYApagarRed()
    {
        if (cerrandoSesion) return;
        cerrandoSesion = true;
        if (session != null)
        {
            try
            {
                session.Changed -= AlCambiarSala;
                await session.LeaveAsync();
                session = null;
            }
            catch (Exception e) { Debug.LogError("[SESSION] Error al salir: " + e.Message); }
        }
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }
        cerrandoSesion = false;
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

    // Se conserva para compatibilidad, pero el botón Listo deja de utilizarse.
    public void ToggleReady() { }

    public bool TodosListos()
    {
        if (networkManager == null || !networkManager.IsServer || session == null ||
            session.Players == null) return false;

        int conectados = networkManager.ConnectedClientsIds.Count;
        if (conectados < 2 || conectados > MaxJugadoresPermitidos ||
            session.Players.Count != conectados || reservas.Count != conectados)
            return false;

        foreach (ulong clientId in networkManager.ConnectedClientsIds)
            if (!reservas.ContainsKey(clientId)) return false;

        return true;
    }

    public void OnClickIniciarPartida()
    {
        if (!EsHost || !TodosListos()) return;
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }

    private void AlCambiarSala() { EventosJuego.PublicarSalaActualizada(); }

    private void VerificarConexion(NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        if (networkManager.ConnectedClientsIds.Count >= MaxJugadoresPermitidos)
        {
            response.Approved = false;
            response.Reason = "Servidor lleno";
            return;
        }
        response.Approved = true;
        response.CreatePlayerObject = false;
    }
}
