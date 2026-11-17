
using UnityEngine;

public class PlayerVisual : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform modelo; // VisualRoot

    [Header("Animacion")]
    [SerializeField] private float velocidadCaminata = 3.5f;
    [SerializeField] private float tiempoParaDetener = 0.18f;
    [SerializeField] private float velocidadMinima = 0.04f;

    [Header("Rotacion")]
    [SerializeField] private float correccionRotacion = 0f;
    [SerializeField] private float velocidadGiro = 15f;

    private Vector3 posicionAnterior;
    private Vector3 direccionAnterior;
    private float tiempoSinMovimiento;
    private bool caminando;

    private void OnEnable()
    {
        posicionAnterior = transform.position;
        direccionAnterior = Vector3.forward;
        tiempoSinMovimiento = 0f;
        caminando = false;
    }

    private void LateUpdate()
    {
        float delta = Time.deltaTime;
        if (delta <= 0f) return;

        Vector3 movimiento = transform.position - posicionAnterior;
        movimiento.y = 0f;
        posicionAnterior = transform.position;

        float velocidad = movimiento.magnitude / delta;
        bool hayMovimiento = velocidad > velocidadMinima;

        if (hayMovimiento)
        {
            tiempoSinMovimiento = 0f;
            caminando = true;
            direccionAnterior = movimiento.normalized;
        }
        else
        {
            tiempoSinMovimiento += delta;

            if (tiempoSinMovimiento >= tiempoParaDetener)
                caminando = false;
        }

        if (animator != null)
            animator.speed = caminando ? velocidadCaminata : 0f;

        if (caminando && modelo != null && hayMovimiento)
        {
            Quaternion direccion = Quaternion.LookRotation(
                direccionAnterior, Vector3.up);

            Quaternion destino =
                Quaternion.Inverse(transform.rotation) *
                direccion *
                Quaternion.Euler(0f, correccionRotacion, 0f);

            modelo.localRotation = Quaternion.Slerp(
                modelo.localRotation,
                destino,
                1f - Mathf.Exp(-velocidadGiro * delta));
        }
    }

    public void ConfigurarAnimator(Animator nuevoAnimator)
    {
        animator = nuevoAnimator;
    }
}
