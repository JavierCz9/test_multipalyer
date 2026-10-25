using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Muestra en el HUD cuántas tiles lleva pintadas cada jugador.
/// Se suscribe a la NetworkList de contadores del GameManager
/// para actualizarse solo cuando hay cambios reales.
/// Se coloca en un GameObject dentro del Canvas del HUD.
/// </summary>
public class MarcadorUI : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("Un TextMeshPro por cada jugador posible (índice 0 = jugador 1). " +
             "Podés dejar huecos vacíos si no usás los 8.")]
    public TextMeshProUGUI[] textosContadores = new TextMeshProUGUI[8];

    [Tooltip("Panel contenedor del marcador. Se oculta en el lobby.")]
    public GameObject panelMarcador;

    private void Start()
    {
        // Ocultar hasta que arranque la partida.
        if (panelMarcador != null)
            panelMarcador.SetActive(false);

        // Suscribirse cuando el GameManager esté listo.
        if (GameManager.Instance != null)
        {
            Suscribirse();
        }
        else
        {
            StartCoroutine(EsperarGameManager());
        }
    }

    private System.Collections.IEnumerator EsperarGameManager()
    {
        float timeout = 5f;
        float t = 0f;

        while (GameManager.Instance == null && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (GameManager.Instance != null)
            Suscribirse();
        else
            Debug.LogWarning("[Marcador] No se encontró GameManager.");
    }

    private void Suscribirse()
    {
        var gm = GameManager.Instance;

        // Cuando la NetworkList cambie, actualizamos el marcador.
        gm.ContadoresPorColor.OnListChanged += AlCambiarContadores;

        // También escuchamos el estado para mostrar/ocultar el panel.
        gm.EstadoActual.OnValueChanged += AlCambiarEstado;

        // Aplicar el estado actual.
        AlCambiarEstado(GameState.SalaEspera, gm.EstadoActual.Value);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.ContadoresPorColor.OnListChanged -= AlCambiarContadores;
        GameManager.Instance.EstadoActual.OnValueChanged -= AlCambiarEstado;
    }

    private void AlCambiarEstado(GameState viejo, GameState nuevo)
    {
        bool enJuego = (nuevo == GameState.EnJuego);

        if (panelMarcador != null)
            panelMarcador.SetActive(enJuego);

        // Al empezar la partida, refrescar todos los textos por si acaso.
        if (enJuego)
            RefrescarTodos();
    }

    private void AlCambiarContadores(NetworkListEvent<int> evento)
    {
        // Actualizamos solo el texto correspondiente a ese índice.
        // El índice de la lista ES el ColorId del jugador (1..8).
        if (evento.Index < 0 || evento.Index >= textosContadores.Length)
            return;

        ActualizarTexto(evento.Index, evento.Value);
    }

    /// <summary>
    /// Relee todos los contadores y actualiza los textos.
    /// </summary>
    private void RefrescarTodos()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        for (int colorId = 1; colorId < textosContadores.Length; colorId++)
        {
            int cantidad = gm.ObtenerBaldosas((byte)colorId);
            ActualizarTexto(colorId, cantidad);
        }
    }

    /// <summary>
    /// Actualiza el texto del jugador con un ColorId dado.
    /// Los textos se ordenan por índice en el array: textosContadores[0]
    /// corresponde al ColorId 1 (Rojo), textosContadores[1] al ColorId 2 (Azul), etc.
    /// </summary>
    private void ActualizarTexto(int colorId, int cantidad)
    {
        // El array está indexado desde 0, pero el ColorId empieza en 1.
        int indiceArray = colorId - 1;

        if (indiceArray < 0 || indiceArray >= textosContadores.Length)
            return;

        TextMeshProUGUI texto = textosContadores[indiceArray];
        if (texto == null) return;

        // Mostrar el número y colorear el texto con el color del jugador.
        texto.text = cantidad.ToString();

        // Opcional: colorear el texto.
        Color32 color = PlayerPalette.Obtener((byte)colorId);
        texto.color = color;
    }
}
