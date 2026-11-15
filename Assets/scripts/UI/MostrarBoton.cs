
using System.Collections;
using UnityEngine;

public class MostrarBoton : MonoBehaviour
{
    [SerializeField] private GameObject botonContinuar;
    [SerializeField] private GameObject portadaPanel;
    [SerializeField] private GameObject menuPrincipal;

    [Header("Animación")]
    [SerializeField] private float tiempoEspera = 3f;
    [SerializeField] private float duracionFade = 1.5f;

    private CanvasGroup canvasGroup;

    void Start()
    {
        canvasGroup = botonContinuar.GetComponent<CanvasGroup>();

        botonContinuar.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        menuPrincipal.SetActive(false);

        StartCoroutine(Mostrar());
    }

    IEnumerator Mostrar()
    {
        yield return new WaitForSeconds(tiempoEspera);

        float tiempo = 0f;

        while (tiempo < duracionFade)
        {
            tiempo += Time.deltaTime;

            canvasGroup.alpha =
                Mathf.Clamp01(tiempo / duracionFade);

            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    public void Continuar()
    {
        portadaPanel.SetActive(false);
        menuPrincipal.SetActive(true);
    }
}
