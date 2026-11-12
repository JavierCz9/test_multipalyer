
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

    [Header("Paneles de Selección")]
    [SerializeField] private GameObject panelElegirPartida;
    [SerializeField] private GameObject panelCrearPartida;
    [SerializeField] private GameObject panelUnirsePartida;

    [Header("Elementos del Menú Principal")]
    [SerializeField] private TMP_InputField inputNombreJugador;
    [SerializeField] private TMP_InputField inputJoinCode;

    [Header("Nombre del Invitado")]
    [SerializeField] private TMP_InputField inputNombreInvitado;

    [Header("Botones del Menú")]
    [SerializeField] private GameObject botonCrearPartida;

    [Header("Elementos de la Sala (Lobby)")]
    [SerializeField] private TextMeshProUGUI textoJoinCode;
    [SerializeField] private Transform contenedorJugadores;
    [SerializeField] private GameObject prefabItemJugador;
    [SerializeField] private TextMeshProUGUI textoBotonReady;
    [SerializeField] private GameObject botonIniciarObj;

    private void Start()
    {
        EventosJuego.OnSalaActualizada += RefrescarSala;
        EventosJuego.OnPartidaIniciada += OcultarLobby;

        var cm = ConnectionManager.Instance;

        if (cm != null && cm.SessionActual != null)
        {
            if (inputNombreJugador != null)
                inputNombreJugador.text = cm.MiNombre;

            MostrarSala();
            RefrescarSala();
        }
        else
        {
            MostrarMenuInicial();
        }
    }

    private void MostrarMenuInicial()
    {
        menuLobby.SetActive(true);
        salaPanel.SetActive(false);

        panelElegirPartida.SetActive(true);
        panelCrearPartida.SetActive(false);
        panelUnirsePartida.SetActive(false);
    }

    public void MostrarPanelCrear()
    {
        panelElegirPartida.SetActive(false);
        panelUnirsePartida.SetActive(false);
        panelCrearPartida.SetActive(true);
    }

    public void MostrarPanelUnirse()
    {
        panelElegirPartida.SetActive(false);
        panelCrearPartida.SetActive(false);
        panelUnirsePartida.SetActive(true);
    }

    public void VolverAElegirPartida()
    {
        panelCrearPartida.SetActive(false);
        panelUnirsePartida.SetActive(false);
        panelElegirPartida.SetActive(true);
    }

    private void MostrarSala()
    {
        menuLobby.SetActive(false);
        panelElegirPartida.SetActive(false);
        panelCrearPartida.SetActive(false);
        panelUnirsePartida.SetActive(false);

        salaPanel.SetActive(true);
    }

    public void OnClickCrearPartida()
    {
        string nombre = inputNombreJugador.text.Trim();

        if (string.IsNullOrEmpty(nombre))
        {
            Debug.LogWarning("Ingresá tu nombre para crear la partida.");
            return;
        }

        ConnectionManager.Instance.CrearPartidaRelay(nombre);
    }

    public void OnClickUnirse()
    {
        string codigo = inputJoinCode.text.Trim().ToUpper();
        string nombre = inputNombreInvitado.text.Trim();

        if (string.IsNullOrEmpty(codigo) ||
            string.IsNullOrEmpty(nombre))
        {
            Debug.LogWarning("Ingresá tu nombre y el código de la sala.");
            return;
        }

        ConnectionManager.Instance.UnirseAPartidaRelay(codigo, nombre);
    }

    public void OnClickReady()
    {
        ConnectionManager.Instance.ToggleReady();
    }

    public void OnClickIniciarPartida()
    {
        ConnectionManager.Instance.OnClickIniciarPartida();
    }

    public async void OnClickSalirDeLaSala()
    {
        if (ConnectionManager.Instance != null)
            await ConnectionManager.Instance.SalirYApagarRed();

        SceneManager.LoadScene("MainMenuScene");
    }

    private void RefrescarSala()
    {
        var cm = ConnectionManager.Instance;

        if (cm == null || cm.SessionActual == null)
            return;

        MostrarSala();

        var session = cm.SessionActual;

        // Código de la sala
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

        // Botón Ready
        if (textoBotonReady != null)
        {
            textoBotonReady.text = cm.EstoyListo
                ? "Cancelar Listo"
                : "Marcar Listo";
        }

        // Limpiar lista de jugadores
        if (contenedorJugadores != null)
        {
            foreach (Transform hijo in contenedorJugadores)
                Destroy(hijo.gameObject);
        }

        // Crear lista actualizada
        if (contenedorJugadores != null && prefabItemJugador != null)
        {
            foreach (var player in session.Players)
            {
                GameObject fila = Instantiate(
                    prefabItemJugador,
                    contenedorJugadores
                );

                TextMeshProUGUI textoFila =
                    fila.GetComponentInChildren<TextMeshProUGUI>();

                if (textoFila != null)
                {
                    string nombreMostrar = "Jugador";

                    if (player.Properties.TryGetValue(
                        "PlayerName", out var nameProp))
                    {
                        nombreMostrar = nameProp.Value;
                    }

                    if (player.Id == session.Host)
                        nombreMostrar += " (Host)";

                    bool listo =
                        player.Properties.TryGetValue(
                            "IsReady", out var prop)
                        && prop.Value == "true";

                    textoFila.text =
                        $"{nombreMostrar} {(listo ? "[Listo]" : "[Esperando]")}";

                    textoFila.color =
                        listo ? Color.green : Color.white;
                }
            }
        }

        // Botón iniciar partida: solo Host
        if (botonIniciarObj != null)
        {
            if (cm.EsHost)
            {
                botonIniciarObj.SetActive(true);

                Button btn =
                    botonIniciarObj.GetComponent<Button>();

                if (btn != null)
                    btn.interactable = cm.TodosListos();
            }
            else
            {
                botonIniciarObj.SetActive(false);
            }
        }
    }

    private void OcultarLobby()
    {
        menuLobby.SetActive(false);
        salaPanel.SetActive(false);
        panelElegirPartida.SetActive(false);
        panelCrearPartida.SetActive(false);
        panelUnirsePartida.SetActive(false);
    }

    private void OnDestroy()
    {
        EventosJuego.OnSalaActualizada -= RefrescarSala;
        EventosJuego.OnPartidaIniciada -= OcultarLobby;
    }
}
