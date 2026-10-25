using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ResultadoUI : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject panelResultado;
    public TextMeshProUGUI textoGanador;
    public TextMeshProUGUI textoCantidad;
    public Image iconoGanador;

    [Header("Botones")]
    public Button botonVolverAlLobby; // Solo lo usará el Host
    public Button botonVolverAlMenu;  // Lo usan ambos

    private void Start()
    {
        if (panelResultado != null) panelResultado.SetActive(false);
        LimpiarPanel();

        EventosJuego.OnGanadorAnunciado += AlAnunciarGanador;
        EventosJuego.OnEstadoCambiado += AlCambiarEstado;

        if (botonVolverAlLobby != null)
            botonVolverAlLobby.onClick.AddListener(OnClickVolverAlLobby);

        if (botonVolverAlMenu != null)
            botonVolverAlMenu.onClick.AddListener(OnClickVolverAlMenu);

        if (GameManager.Instance != null)
        {
            AlCambiarEstado(GameManager.Instance.EstadoActual.Value);

            if (GameManager.Instance.EstadoActual.Value == GameState.Finalizado && EventosJuego.HayGanadorSnapshot)
                AlAnunciarGanador(EventosJuego.ColorGanadorSnapshot, EventosJuego.BaldosasGanadorSnapshot);
        }
    }

    private void OnDestroy()
    {
        EventosJuego.OnGanadorAnunciado -= AlAnunciarGanador;
        EventosJuego.OnEstadoCambiado -= AlCambiarEstado;
    }

    private void AlAnunciarGanador(byte colorGanador, int baldosas)
    {
        if (panelResultado != null)
            panelResultado.SetActive(true);

        if (botonVolverAlLobby != null)
        {
            bool esHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            botonVolverAlLobby.gameObject.SetActive(esHost);
        }

        // ✅ Lógica visual para mostrar si es empate o victoria
        if (textoGanador != null)
        {
            if (colorGanador == 0)
            {
                textoGanador.text = baldosas > 0 ? "¡Empate!" : "¡Empate! Nadie pintó nada.";
                textoGanador.color = Color.white;
            }
            else
            {
                textoGanador.text = $"¡Ganó el jugador {colorGanador}!";
                textoGanador.color = Color.white;
            }
        }

        // ✅ Texto de la cantidad (si es empate, indica que son por jugador)
        if (textoCantidad != null)
        {
            if (colorGanador == 0 && baldosas > 0)
                textoCantidad.text = $"{baldosas} tiles pintadas cada uno";
            else
                textoCantidad.text = $"{baldosas} tiles pintadas";
        }

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

    private void AlCambiarEstado(GameState nuevo)
    {
        if (nuevo != GameState.Finalizado && panelResultado != null)
        {
            panelResultado.SetActive(false);
            LimpiarPanel();
        }
    }

    private void LimpiarPanel()
    {
        if (textoGanador != null) { textoGanador.text = ""; textoGanador.color = Color.white; }
        if (textoCantidad != null) textoCantidad.text = "";
        if (iconoGanador != null) iconoGanador.enabled = false;
    }

    private void OnClickVolverAlLobby()
    {
        if (!NetworkManager.Singleton.IsServer) return;
        if (GameManager.Instance != null) GameManager.Instance.VolverAlLobby();
    }

    private async void OnClickVolverAlMenu()
    {
        Debug.Log("[RESULTADO] Saliendo de la partida y volviendo al Menú Principal...");

        if (ConnectionManager.Instance != null)
        {
            await ConnectionManager.Instance.SalirYApagarRed();
        }
        else if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        await System.Threading.Tasks.Task.Delay(100);
        SceneManager.LoadScene("MainMenuScene");
    }
}