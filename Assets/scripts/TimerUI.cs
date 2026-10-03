using UnityEngine;
using TMPro;

public class TimerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text textoTiempo;

    private void Update()
    {
        if (GameManager.Instance == null)
            return;

        float tiempo = GameManager.Instance.TiempoRestante.Value;

        int minutos = Mathf.FloorToInt(tiempo / 60f);
        int segundos = Mathf.FloorToInt(tiempo % 60f);

        textoTiempo.text = $"{minutos:00}:{segundos:00}";
    }
}