using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Sigue al jugador local desde una posición elevada y detrás.
/// Solo se activa para el jugador dueño (el local).
/// </summary>
public class CameraFollow : NetworkBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -10f);
    [SerializeField] private float suavizado = 5f;

    private Camera cam;

    public override void OnNetworkSpawn()
    {
        // Solo el dueño del jugador controla SU cámara.
        // Los demás jugadores verán su propia cámara y este script no hará nada.
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        cam = Camera.main;
    }

    private void LateUpdate()
    {
        // LateUpdate para que la cámara se actualice DESPUÉS del movimiento
        // del jugador y no haya jitter.
        if (cam == null) return;

        // Posición deseada: por encima y detrás del jugador.
        Vector3 objetivo = transform.position + offset;

        // Interpolamos para suavizar el seguimiento.
        cam.transform.position = Vector3.Lerp(
            cam.transform.position,
            objetivo,
            Time.deltaTime * suavizado
        );

        // La cámara siempre mira al jugador.
        cam.transform.LookAt(transform.position + Vector3.up * 0.5f);
    }
}
