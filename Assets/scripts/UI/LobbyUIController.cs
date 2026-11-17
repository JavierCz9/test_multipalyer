using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
    [SerializeField] private GameObject panelSeleccionPersonaje;

    [Header("Datos")]
    [SerializeField] private TMP_InputField inputNombreJugador;
    [SerializeField] private TMP_InputField inputJoinCode;
    [SerializeField] private TMP_InputField inputNombreInvitado;

    [Header("Botones")]
    [SerializeField] private GameObject botonCrearPartida;

    [Header("Selección de personajes")]
    [Tooltip("Orden: Esqueleto, Pirata, Tiburón, Pulpito")]
    [SerializeField] private Button[] botonesPersonajes = new Button[4];
    [SerializeField] private Button botonConfirmarPersonaje;

    [Header("Elementos de la Sala")]
    [SerializeField] private TextMeshProUGUI textoJoinCode;
    [SerializeField] private Transform contenedorJugadores;
    [SerializeField] private GameObject prefabItemJugador;
    [SerializeField] private TextMeshProUGUI textoBotonReady;
    [SerializeField] private GameObject botonIniciarObj;

    private bool entrandoASala;
    private int seleccionLocal = -1;
    private static readonly string[] NombresPersonajes =
        { "Esqueleto", "Pirata", "Tiburón", "Pulpito" };

    private void Start()
    {
        EventosJuego.OnSalaActualizada += RefrescarSala;
        EventosJuego.OnPartidaIniciada += OcultarLobby;
        var cm = ConnectionManager.Instance;
        if (cm != null)
        {
            cm.SeleccionActualizada += RefrescarSala;
            cm.ConfirmacionRespondida += AlConfirmarPersonaje;
        }
        if (cm != null && cm.SessionActual != null)
        {
            if (cm.EstoyListo) MostrarSalaEspera();
            else MostrarSeleccionPersonaje();
            RefrescarSala();
        }
        else MostrarMenuInicial();
        ActualizarBotonesPersonajes();
    }

    private void MostrarMenuInicial()
    {
        menuLobby.SetActive(true);
        salaPanel.SetActive(false);
        panelElegirPartida.SetActive(true);
        panelCrearPartida.SetActive(false);
        panelUnirsePartida.SetActive(false);
        if (panelSeleccionPersonaje != null) panelSeleccionPersonaje.SetActive(false);
    }

    public void MostrarPanelCrear()
    {
        panelElegirPartida.SetActive(false);
        panelUnirsePartida.SetActive(false);
        panelCrearPartida.SetActive(true);
        if (textoJoinCode != null) textoJoinCode.text = "Generando código...";
        ConnectionManager.Instance.CrearPartidaRelay("Jugador");
    }

    public void MostrarPanelUnirse()
    {
        panelElegirPartida.SetActive(false);
        panelCrearPartida.SetActive(false);
        panelUnirsePartida.SetActive(true);
    }

    public void VolverAElegirPartida()
    {
        if (ConnectionManager.Instance != null && ConnectionManager.Instance.SesionLista)
        {
            Debug.LogWarning("Ya hay una sesión activa. Usá Salir de la sala para abandonarla.");
            return;
        }
        panelCrearPartida.SetActive(false);
        panelUnirsePartida.SetActive(false);
        panelElegirPartida.SetActive(true);
    }

    private void MostrarSeleccionPersonaje()
    {
        menuLobby.SetActive(true);
        salaPanel.SetActive(false);
        panelElegirPartida.SetActive(false);
        panelCrearPartida.SetActive(false);
        panelUnirsePartida.SetActive(false);
        if (panelSeleccionPersonaje != null) panelSeleccionPersonaje.SetActive(true);
        ActualizarBotonesPersonajes();
    }

    private void MostrarSalaEspera()
    {
        menuLobby.SetActive(true);
        salaPanel.SetActive(true);
        panelElegirPartida.SetActive(false);
        panelCrearPartida.SetActive(false);
        panelUnirsePartida.SetActive(false);
        if (panelSeleccionPersonaje != null) panelSeleccionPersonaje.SetActive(false);
        RefrescarSala();
    }

    public async void OnClickCrearPartida()
    {
        if (entrandoASala) return;
        string nombre = inputNombreJugador.text.Trim();
        if (string.IsNullOrEmpty(nombre)) { Debug.LogWarning("Ingresá tu nombre."); return; }
        var cm = ConnectionManager.Instance;
        if (cm == null || !cm.SesionLista)
        {
            Debug.LogWarning("Esperá a que se genere el código.");
            return;
        }
        entrandoASala = true;
        bool guardado = await cm.ActualizarNombre(nombre);
        entrandoASala = false;
        if (guardado) { MostrarSeleccionPersonaje(); RefrescarSala(); }
    }

    public void OnClickUnirse()
    {
        string codigo = inputJoinCode.text.Trim().ToUpperInvariant();
        string nombre = inputNombreInvitado.text.Trim();
        if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(nombre))
        {
            Debug.LogWarning("Ingresá nombre y código.");
            return;
        }
        ConnectionManager.Instance.UnirseAPartidaRelay(codigo, nombre);
    }

    // Orden de los IDs igual al prefab PlayerCharacterSelector:
    // 0 Esqueleto, 1 Pirata, 2 Tiburón, 3 Pulpito.
    public void ElegirEsqueleto() => SeleccionarPersonaje(0);
    public void ElegirPirata() => SeleccionarPersonaje(1);
    public void ElegirTiburon() => SeleccionarPersonaje(2);
    public void ElegirPulpito() => SeleccionarPersonaje(3);

    public void SeleccionarPersonaje(int id)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null || cm.EstoyListo || cm.EsperandoConfirmacion || cm.PersonajeOcupado(id)) return;
        seleccionLocal = id;
        ActualizarBotonesPersonajes();
    }

    public void OnClickConfirmarPersonaje()
    {
        var cm = ConnectionManager.Instance;
        if (cm == null || seleccionLocal < 0 || cm.PersonajeOcupado(seleccionLocal)) return;
        cm.ConfirmarPersonaje(seleccionLocal);
        ActualizarBotonesPersonajes();
    }

    private void AlConfirmarPersonaje(bool aceptado)
    {
        if (aceptado)
        {
            RefrescarSala();
            ActualizarBotonesPersonajes();
        }
        else
        {
            seleccionLocal = -1;
            ActualizarBotonesPersonajes();
        }
    }

    private void ActualizarBotonesPersonajes()
    {
        var cm = ConnectionManager.Instance;
        if (botonesPersonajes != null)
        {
            for (int i = 0; i < botonesPersonajes.Length; i++)
            {
                Button boton = botonesPersonajes[i];
                if (boton == null) continue;
                bool ocupado = cm != null && cm.PersonajeOcupado(i);
                boton.interactable = !ocupado && (cm == null || !cm.EsperandoConfirmacion);
                // El Color Tint afecta a la imagen objetivo del Button.
                ColorBlock colores = boton.colors;
                colores.normalColor = i == seleccionLocal ? new Color(0.65f, 0.65f, 0.65f) : Color.white;
                colores.selectedColor = colores.normalColor;
                colores.disabledColor = new Color(0.13f, 0.13f, 0.13f, 1f);
                boton.colors = colores;
            }
        }
        if (botonConfirmarPersonaje != null)
            botonConfirmarPersonaje.interactable = cm != null &&
                seleccionLocal >= 0 && !cm.EsperandoConfirmacion &&
                !cm.PersonajeOcupado(seleccionLocal);
    }

    // Conservado por compatibilidad con un botón antiguo, ya no es necesario usarlo.
    public void OnClickReady() { }

    public void OnClickIniciarPartida() => ConnectionManager.Instance.OnClickIniciarPartida();

    public async void OnClickSalirDeLaSala()
    {
        if (ConnectionManager.Instance != null)
            await ConnectionManager.Instance.SalirYApagarRed();
        SceneManager.LoadScene("MainMenuScene");
    }

    private void RefrescarSala()
    {
        var cm = ConnectionManager.Instance;
        if (cm == null || cm.SessionActual == null) return;
        var session = cm.SessionActual;
        if (!cm.EsHost && panelUnirsePartida.activeSelf) MostrarSeleccionPersonaje();
        if (textoJoinCode != null && cm.EsHost)
        {
            textoJoinCode.text = "CÓDIGO: " + session.Code;
            textoJoinCode.gameObject.SetActive(true);
        }
        if (contenedorJugadores != null)
            foreach (Transform hijo in contenedorJugadores) Destroy(hijo.gameObject);
        if (contenedorJugadores != null && prefabItemJugador != null)
        {
            foreach (var player in session.Players)
            {
                GameObject fila = Instantiate(prefabItemJugador, contenedorJugadores);
                TextMeshProUGUI textoFila = fila.GetComponentInChildren<TextMeshProUGUI>();
                if (textoFila == null) continue;
                string nombre = "Jugador";
                if (player.Properties.TryGetValue("PlayerName", out var nameProp))
                    nombre = nameProp.Value;
                if (player.Id == session.Host) nombre += " (Host)";
                bool listo = cm.PersonajeConfirmado(player.Id, out int id);
                string personaje = listo && id >= 0 && id < NombresPersonajes.Length
                    ? " - " + NombresPersonajes[id] : "";
                textoFila.text = $"{nombre}{personaje} {(listo ? "[Confirmado]" : "[Esperando]")}";
                textoFila.color = listo ? Color.green : Color.white;
            }
        }
        if (botonIniciarObj != null)
        {
            botonIniciarObj.SetActive(cm.EsHost);
            Button btn = botonIniciarObj.GetComponent<Button>();
            if (btn != null) btn.interactable = cm.TodosListos();
        }
        ActualizarBotonesPersonajes();
    }

    private void OcultarLobby()
    {
        menuLobby.SetActive(false);
        salaPanel.SetActive(false);
        panelElegirPartida.SetActive(false);
        panelCrearPartida.SetActive(false);
        panelUnirsePartida.SetActive(false);
        if (panelSeleccionPersonaje != null) panelSeleccionPersonaje.SetActive(false);
    }

    private void OnDestroy()
    {
        EventosJuego.OnSalaActualizada -= RefrescarSala;
        EventosJuego.OnPartidaIniciada -= OcultarLobby;
        var cm = ConnectionManager.Instance;
        if (cm != null)
        {
            cm.SeleccionActualizada -= RefrescarSala;
            cm.ConfirmacionRespondida -= AlConfirmarPersonaje;
        }
    }
}
