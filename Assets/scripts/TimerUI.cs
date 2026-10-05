using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Muestra en pantalla el tiempo restante de la partida.
/// El cálculo se hace LOCALMENTE en cada cliente, usando el instante
/// de inicio sincronizado por el servidor y el ServerTime compartido.
/// No recibe actualizaciones por red mientras corre la partida.
/// </summary>
public class TimerUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private TMP_Text textoTiempo;

    private void Update()
    {
        // Seguridad: si no hay GameManager, no hacemos nada.
        if (GameManager.Instance == null) return;
        if (NetworkManager.Singleton == null) return;

        var gm = GameManager.Instance;

        // Solo mostramos el tiempo durante la partida.
        // En SalaEspera y Finalizado, el timer queda oculto o en 00:00.
        if (gm.EstadoActual.Value != GameState.EnJuego)
        {
            textoTiempo.text = "00:00";
            return;
        }

        // ✅ Calculamos el tiempo restante localmente.
        // No viene por red: sale de restar el instante actual
        // menos el instante de inicio de la partida.
        float restante = gm.CalcularTiempoRestante();

        int minutos = Mathf.FloorToInt(restante / 60f);
        int segundos = Mathf.FloorToInt(restante % 60f);

        textoTiempo.text = $"{minutos:00}:{segundos:00}";
    }
}