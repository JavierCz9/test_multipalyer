using System;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using TMPro;

public class ConnectionManager : MonoBehaviour
{
    private const int MaxJugadoresPermitidos = 2;

    private NetworkManager networkManager;

    // =========================================================
    // UI
    // =========================================================

    private GameObject HostButton;
    private GameObject ClientButton;
    private GameObject JoinCodeInput;
    private GameObject JoinCodeText;
    private GameObject IniciarPartida;

    private TMP_Text HostCodeText;

    // =========================================================
    // SESSION
    // =========================================================

    private ISession session;

    private bool buscandoPartida = true;
    private bool buscandoAhora = false;

    private float tiempoBusqueda = 0f;

    // =========================================================
    // AWAKE
    // =========================================================

    private async void Awake()
    {
        networkManager = GetComponent<NetworkManager>();

        // -----------------------------------------------------
        // BUSCAR AUTOMÁTICAMENTE LOS OBJETOS DE LA UI
        // -----------------------------------------------------

        HostButton = GameObject.Find("HostButton");
        ClientButton = GameObject.Find("ClientButton");
        JoinCodeInput = GameObject.Find("JoinCodeInput");
        JoinCodeText = GameObject.Find("JoinCodeText");
        IniciarPartida = GameObject.Find("IniciarPartida");

        if (JoinCodeText != null)
        {
            HostCodeText =
                JoinCodeText.GetComponentInChildren<TMP_Text>(true);
        }

        // -----------------------------------------------------
        // UI INICIAL
        // -----------------------------------------------------

        MostrarUIInicial();

        // -----------------------------------------------------
        // UNITY SERVICES
        // -----------------------------------------------------

        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance
                    .SignInAnonymouslyAsync();
            }

            Debug.Log("[SESSION] Unity Services inicializados.");
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[SESSION] Error inicializando Unity Services: " + e
            );
        }

        // -----------------------------------------------------
        // NETWORK
        // -----------------------------------------------------

        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback =
            VerificarConexion;
    }

    // =========================================================
    // UI INICIAL
    // =========================================================

    private void MostrarUIInicial()
    {
        // Al arrancar:
        // CREAR + UNIRSE visibles
        // INICIAR + CÓDIGO ocultos

        if (HostButton != null)
            HostButton.SetActive(true);

        if (ClientButton != null)
            ClientButton.SetActive(true);

        if (JoinCodeInput != null)
            JoinCodeInput.SetActive(true);

        if (JoinCodeText != null)
            JoinCodeText.SetActive(false);

        if (IniciarPartida != null)
            IniciarPartida.SetActive(false);
    }

    // =========================================================
    // CONNECTION APPROVAL
    // =========================================================

    private void VerificarConexion(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        if (GameManager.Instance != null &&
            GameManager.Instance.EstadoActual.Value == GameState.EnJuego)
        {
            response.Approved = false;
            response.Reason = "La partida ya comenzó";
            return;
        }

        if (networkManager.ConnectedClientsIds.Count >=
            MaxJugadoresPermitidos)
        {
            response.Approved = false;
            response.Reason = "Servidor lleno";
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;
    }

    // =========================================================
    // CREAR PARTIDA
    // =========================================================

    public async void CrearPartidaRelay()
    {
        if (session != null)
            return;

        try
        {
            Debug.Log("[SESSION] Creando partida...");

            var options = new SessionOptions
            {
                MaxPlayers = MaxJugadoresPermitidos,
                Name = "StealIsland"
            }.WithRelayNetwork();

            session =
                await MultiplayerService.Instance
                    .CreateSessionAsync(options);

            Debug.Log(
                "[SESSION] Partida creada. Código: "
                + session.Code
            );

            // -------------------------------------------------
            // MOSTRAR CÓDIGO
            // -------------------------------------------------

            if (HostCodeText != null)
            {
                HostCodeText.text =
                    "Código: " + session.Code;
            }

            // -------------------------------------------------
            // CAMBIAR UI DEL HOST
            // -------------------------------------------------

            MostrarUIHost();
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[SESSION] Error creando partida: " + e
            );
        }
    }

    // =========================================================
    // BUSCAR PARTIDA
    // =========================================================

    private async void BuscarPartida()
    {
        if (!buscandoPartida)
            return;

        if (session != null)
            return;

        if (buscandoAhora)
            return;

        buscandoAhora = true;

        try
        {
            var resultados =
                await MultiplayerService.Instance
                    .QuerySessionsAsync(
                        new QuerySessionsOptions()
                    );

            foreach (var partida in resultados.Sessions)
            {
                // Solo nos interesa nuestra partida
                if (partida.Name == "StealIsland")
                {
                    // Encontramos una partida disponible
                    MostrarUICliente();

                    break;
                }
            }
        }
        catch
        {
            // No llenar la consola con errores
        }

        buscandoAhora = false;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        // ATAJO H
        if (Input.GetKeyDown(KeyCode.H))
        {
            CrearPartidaRelay();
        }

        // Buscar partida cada 2 segundos
        if (buscandoPartida && session == null)
        {
            tiempoBusqueda += Time.deltaTime;

            if (tiempoBusqueda >= 2f)
            {
                tiempoBusqueda = 0f;

                BuscarPartida();
            }
        }
    }

    // =========================================================
    // UNIRSE A PARTIDA
    // =========================================================

    public async void UnirseAPartidaRelay(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            Debug.LogWarning(
                "[SESSION] Introducí un código."
            );

            return;
        }

        if (session != null)
            return;

        try
        {
            Debug.Log(
                "[SESSION] Uniéndose con código: "
                + codigo
            );

            session =
                await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(codigo);

            Debug.Log(
                "[SESSION] Unido correctamente."
            );

            MostrarUICliente();
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[SESSION] Error uniéndose: " + e
            );
        }
    }

    // =========================================================
    // UI HOST
    // =========================================================

    private void MostrarUIHost()
    {
        // HOST:
        //
        // ❌ CREAR PARTIDA
        // ❌ UNIRSE A PARTIDA
        // ❌ INPUT CÓDIGO
        // ✅ CÓDIGO
        // ✅ INICIAR PARTIDA

        if (HostButton != null)
            HostButton.SetActive(false);

        if (ClientButton != null)
            ClientButton.SetActive(false);

        if (JoinCodeInput != null)
            JoinCodeInput.SetActive(false);

        if (JoinCodeText != null)
            JoinCodeText.SetActive(true);

        if (IniciarPartida != null)
            IniciarPartida.SetActive(true);
    }

    // =========================================================
    // UI CLIENTE
    // =========================================================

    private void MostrarUICliente()
    {
        // CLIENTE:
        //
        // ❌ CREAR PARTIDA
        // ❌ INICIAR PARTIDA
        // ❌ CÓDIGO DEL HOST
        // ✅ UNIRSE A PARTIDA
        // ✅ INPUT CÓDIGO

        if (HostButton != null)
            HostButton.SetActive(false);

        if (IniciarPartida != null)
            IniciarPartida.SetActive(false);

        if (JoinCodeText != null)
            JoinCodeText.SetActive(false);

        if (ClientButton != null)
            ClientButton.SetActive(true);

        if (JoinCodeInput != null)
            JoinCodeInput.SetActive(true);
    }

    // =========================================================
    // BOTÓN UNIRSE
    // =========================================================

    public void UnirseDesdeUI(TMP_InputField input)
    {
        if (input == null)
            return;

        UnirseAPartidaRelay(input.text);
    }
}