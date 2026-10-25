using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Multiplayer;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public class LobbyUIController : MonoBehaviour
{
    [Header("Paneles Principales")]
    [SerializeField] private GameObject menuLobby;
    [SerializeField] private GameObject salaPanel;

    [Header("Elementos del Menú Principal")]
    [SerializeField] private TMP_InputField inputNombreJugador;
    [SerializeField] private TMP_InputField inputJoinCode;

    [Header("Botones del Menú")]
    [SerializeField] private GameObject botonCrearPartida;

    [Header("Elementos de la Sala (Lobby)")]
    [SerializeField] private TextMeshProUGUI textoJoinCode;
    [SerializeField] private Transform contenedorJugadores;
    [SerializeField] private GameObject prefabItemJugador;
    [SerializeField] private TextMeshProUGUI textoBotonReady;
    [SerializeField] private GameObject botonIniciarObj;

    private bool buscandoPartida = false;

    private void Start()
    {
        EventosJuego.OnSalaActualizada += RefrescarSala;
        EventosJuego.OnPartidaIniciada += OcultarLobby;

        if (ConnectionManager.Instance != null && ConnectionManager.Instance.SessionActual != null)
        {
            // Ya estamos en una sala (ej: volvimos del juego)
            menuLobby.SetActive(false);
            salaPanel.SetActive(true);

            if (inputNombreJugador != null)
                inputNombreJugador.text = ConnectionManager.Instance.MiNombre;

            RefrescarSala();
        }
        else
        {
            menuLobby.SetActive(true);
            salaPanel.SetActive(false);

            // Empezamos a buscar partidas constantemente
            buscandoPartida = true;
            _ = RevisarPartidasLoop();
        }
    }

    private async Task RevisarPartidasLoop()
    {
        while (buscandoPartida)
        {
            if (ConnectionManager.Instance.SessionActual != null) break; // Si ya entramos a una, paramos.

            try
            {
                var resultados = await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions());
                bool hayPartida = false;

                foreach (var partida in resultados.Sessions)
                {
                    if (partida.Name == "StealIsland" && partida.AvailableSlots > 0)
                    {
                        hayPartida = true;
                        break;
                    }
                }

                // Si hay partida ocultamos el botón, si no, lo mostramos
                if (botonCrearPartida != null)
                    botonCrearPartida.SetActive(!hayPartida);
            }
            catch
            {
                // Si falla, mostramos el botón por defecto
                if (botonCrearPartida != null) botonCrearPartida.SetActive(true);
            }

            await Task.Delay(2000); // Espera 2 segundos antes de volver a consultar
        }
    }

    private void OnDestroy()
    {
        buscandoPartida = false;
        EventosJuego.OnSalaActualizada -= RefrescarSala;
        EventosJuego.OnPartidaIniciada -= OcultarLobby;
    }

    // =========================================================
    // BOTONES DEL MENÚ PRINCIPAL
    // =========================================================

    public void OnClickCrearPartida()
    {
        ConnectionManager.Instance.CrearPartidaRelay(inputNombreJugador.text);
    }

    public void OnClickUnirse()
    {
        string codigo = inputJoinCode.text.Trim().ToUpper();
        if (!string.IsNullOrEmpty(codigo))
            ConnectionManager.Instance.UnirseAPartidaRelay(codigo, inputNombreJugador.text);
    }

    // =========================================================
    // BOTONES DE LA SALA
    // =========================================================

    public void OnClickReady() => ConnectionManager.Instance.ToggleReady();
    public void OnClickIniciarPartida() => ConnectionManager.Instance.OnClickIniciarPartida();

    // ✅ NUEVO: Botón para salir de la sala y volver al menú
    public async void OnClickSalirDeLaSala()
    {
        if (ConnectionManager.Instance != null)
            await ConnectionManager.Instance.SalirYApagarRed();

        SceneManager.LoadScene("MainMenuScene");
    }

    // =========================================================
    // ACTUALIZACIÓN VISUAL
    // =========================================================

    private void RefrescarSala()
    {
        var cm = ConnectionManager.Instance;
        if (cm == null || cm.SessionActual == null) return;

        var session = cm.SessionActual;

        if (textoJoinCode != null)
        {
            if (cm.EsHost)
            {
                textoJoinCode.text = "Código: " + session.Code;
                textoJoinCode.gameObject.SetActive(true);
            }
            else
            {
                textoJoinCode.gameObject.SetActive(false);
            }
        }

        if (textoBotonReady != null)
            textoBotonReady.text = cm.EstoyListo ? "Cancelar Listo" : "Marcar Listo";

        foreach (Transform hijo in contenedorJugadores)
            Destroy(hijo.gameObject);

        foreach (var player in session.Players)
        {
            GameObject fila = Instantiate(prefabItemJugador, contenedorJugadores);
            TextMeshProUGUI textoFila = fila.GetComponentInChildren<TextMeshProUGUI>();

            if (textoFila != null)
            {
                string nombreMostrar = "Jugador";
                if (player.Properties.TryGetValue("PlayerName", out var nameProp))
                    nombreMostrar = nameProp.Value;

                if (player.Id == session.Host) nombreMostrar += " (Host)";

                bool listo = player.Properties.TryGetValue("IsReady", out var prop) && prop.Value == "true";

                textoFila.text = $"{nombreMostrar} {(listo ? "[Listo]" : "[Esperando]")}";
                textoFila.color = listo ? Color.green : Color.white;
            }
        }

        if (cm.EsHost && botonIniciarObj != null)
        {
            botonIniciarObj.SetActive(true);
            Button btn = botonIniciarObj.GetComponent<Button>();
            if (btn != null) btn.interactable = cm.TodosListos();
        }
        else if (botonIniciarObj != null)
        {
            botonIniciarObj.SetActive(false);
        }
    }

    private void OcultarLobby()
    {
        menuLobby.SetActive(false);
        salaPanel.SetActive(false);
    }
}