using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra el panel de resultado al terminar la partida.
/// Se suscribe al evento OnGanadorAnunciado del GameManager
/// (que se dispara desde el RPC AnunciarGanadorRpc).
/// Se oculta y limpia al volver a SalaEspera.
/// </summary>
public class ResultadoUI : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject panelResultado;
    public TextMeshProUGUI textoGanador;
    public TextMeshProUGUI textoCantidad;
    public Image iconoGanador;
    public Button botonVolverAlLobby;

    private void Start()
    {
        // Ocultar al arrancar.
        if (panelResultado != null)
            panelResultado.SetActive(false);

        // Limpiar los textos por si quedaron datos de una sesión anterior.
        LimpiarPanel();

        // Suscribirse al evento del GameManager.
        GameManager.OnGanadorAnunciado += AlAnunciarGanador;

        // Conectar el botón de volver al lobby.
        if (botonVolverAlLobby != null)
            botonVolverAlLobby.onClick.AddListener(OnClickVolverAlLobby);

        // Suscribirse al estado para ocultar el panel si vuelve a SalaEspera.
        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged += AlCambiarEstado;
    }

    private void OnDestroy()
    {
        GameManager.OnGanadorAnunciado -= AlAnunciarGanador;

        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged -= AlCambiarEstado;
    }

    // ============================================================
    // MOSTRAR EL GANADOR
    // ============================================================

    private void AlAnunciarGanador(byte colorGanador, int baldosas)
    {
        if (panelResultado != null)
            panelResultado.SetActive(true);

        // Texto del ganador.
        if (textoGanador != null)
        {
            if (colorGanador == 0 || baldosas == 0)
            {
                textoGanador.text = "¡Empate! Nadie pintó nada.";
                textoGanador.color = Color.white;
            }
            else
            {
                textoGanador.text = $"¡Ganó el jugador {colorGanador}!";
                textoGanador.color = Color.white;
            }
        }

        // Cantidad de tiles.
        if (textoCantidad != null)
            textoCantidad.text = $"{baldosas} tiles pintadas";

        // Ícono del ganador con el color correspondiente.
        if (iconoGanador != null)
        {
            if (colorGanador == 0)
            {
                iconoGanador.enabled = false;
            }
            else
            {
                iconoGanador.enabled = true;
                iconoGanador.color = PlayerPalette.Obtener(colorGanador);
            }
        }
    }

    // ============================================================
    // OCULTAR Y LIMPIAR AL VOLVER AL LOBBY
    // ============================================================

    private void AlCambiarEstado(GameState viejo, GameState nuevo)
    {
        // Si salimos del estado Finalizado, ocultar el panel y limpiarlo.
        if (nuevo != GameState.Finalizado && panelResultado != null)
        {
            panelResultado.SetActive(false);
            LimpiarPanel();
        }
    }

    /// <summary>
    /// Limpia los textos y el ícono del panel de resultado.
    /// Se llama al volver al lobby para que la próxima partida empiece limpia.
    /// </summary>
    private void LimpiarPanel()
    {
        if (textoGanador != null)
        {
            textoGanador.text = "";
            textoGanador.color = Color.white;
        }

        if (textoCantidad != null)
            textoCantidad.text = "";

        if (iconoGanador != null)
            iconoGanador.enabled = false;
    }

    // ============================================================
    // BOTÓN VOLVER AL LOBBY
    // ============================================================

    private void OnClickVolverAlLobby()
    {
        Debug.Log(">>> [ResultadoUI] Clic en Volver al Lobby");

        // Solo el Host puede volver al lobby.
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log(">>> [ResultadoUI] No soy Host, no puedo volver.");
            return;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.VolverAlLobby();
    }
}