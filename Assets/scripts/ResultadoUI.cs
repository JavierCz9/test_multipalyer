using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra el panel de resultado al terminar la partida.
/// Se suscribe al evento OnGanadorAnunciado del GameManager
/// (que se dispara desde el RPC AnunciarGanadorRpc).
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
        Debug.Log("[ResultadoUI] Start ejecutado");

        if (panelResultado != null)
            panelResultado.SetActive(false);
        else
            Debug.LogWarning("[ResultadoUI] panelResultado es NULL");

        GameManager.OnGanadorAnunciado += AlAnunciarGanador;
        Debug.Log("[ResultadoUI] Suscrito al evento OnGanadorAnunciado");
        if (panelResultado != null)
            panelResultado.SetActive(false);

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

    private void AlAnunciarGanador(byte colorGanador, int baldosas)
    {
        Debug.Log($"[ResultadoUI] Evento recibido. " +
             $"panelResultado={(panelResultado != null ? panelResultado.name : "NULL")}");

        if (panelResultado != null)
        {
            panelResultado.SetActive(true);
            Debug.Log($"[ResultadoUI] Panel activado. " +
                      $"activeSelf={panelResultado.activeSelf}, " +
                      $"activeInHierarchy={panelResultado.activeInHierarchy}");
        }
        else
        {
            Debug.LogError("[ResultadoUI] panelResultado es NULL. " +
                           "Falta asignarlo en el Inspector.");
        }
        Debug.Log($"[ResultadoUI] Evento recibido. Ganador={colorGanador}, tiles={baldosas}");
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
                textoGanador.color = PlayerPalette.Obtener(colorGanador);
            }
        }

        // Cantidad de tiles.
        if (textoCantidad != null)
            textoCantidad.text = $"{baldosas} tiles pintadas";

        // Ícono del ganador.
        if (iconoGanador != null)
        {
            if (colorGanador == 0)
                iconoGanador.enabled = false;
            else
            {
                iconoGanador.enabled = true;
                iconoGanador.color = PlayerPalette.Obtener(colorGanador);
            }
        }
    }

    private void AlCambiarEstado(GameState viejo, GameState nuevo)
    {
        // Si salimos del estado Finalizado, ocultar el panel.
        if (nuevo != GameState.Finalizado && panelResultado != null)
            panelResultado.SetActive(false);
    }

    private void OnClickVolverAlLobby()
    {
        // Solo el Host puede volver al lobby.
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[Resultado] Solo el Host puede volver al lobby.");
            return;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.VolverAlLobby();
    }
}