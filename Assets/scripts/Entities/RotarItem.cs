using UnityEngine;

public class RotarItem : MonoBehaviour
{
    [Tooltip("Velocidad de giro en grados por segundo.")]
    [SerializeField] private float velocidadGiro = 90f;

    private void Update()
    {
        // Rota el objeto en su eje Y (vertical)
        transform.Rotate(Vector3.up, velocidadGiro * Time.deltaTime, Space.World);
    }
}