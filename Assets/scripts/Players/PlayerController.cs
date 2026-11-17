using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float velocidadMovimiento = 4f;

    public NetworkVariable<byte> ColorId = new NetworkVariable<byte>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public NetworkVariable<float> multiplicadorVelocidad = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> multiplicadorRalentizacion = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Rigidbody rb;
    private float inputH;
    private float inputV;
    private int ultimaCelda = -1;
    private Coroutine rutinaBuffActiva;
    private Coroutine rutinaRalentizacionActiva;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
       
        {
            rb.isKinematic = !IsOwner;

            if (IsOwner)
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }       
        ColorId.OnValueChanged += AlCambiarColor;

        if (IsOwner)
        {
            byte id = (byte)(OwnerClientId % (ulong)PlayerPalette.CantidadJugadores + 1);
            ColorId.Value = id;
        }

        AplicarColor(ColorId.Value);
        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged += AlCambiarEstado;
    }

    public override void OnNetworkDespawn()
    {
        ColorId.OnValueChanged -= AlCambiarColor;
        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged -= AlCambiarEstado;
        if (rutinaBuffActiva != null) StopCoroutine(rutinaBuffActiva);
        if (rutinaRalentizacionActiva != null) StopCoroutine(rutinaRalentizacionActiva);
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (!PartidaEnCurso())
        {
            inputH = 0f;
            inputV = 0f;
            return;
        }

        inputH = Input.GetAxis("Horizontal");
        inputV = Input.GetAxis("Vertical");

    }

    private void FixedUpdate()
    {
        if (!IsOwner || rb == null) return;

        if (PartidaEnCurso())
        {
            Vector3 direccion = new Vector3(inputH, 0f, inputV).normalized;
            float velocidadFinal = velocidadMovimiento *
                multiplicadorVelocidad.Value * multiplicadorRalentizacion.Value;

            Vector3 velocidad = direccion * velocidadFinal;
            velocidad.y = rb.linearVelocity.y;
            rb.linearVelocity = velocidad;

            DetectarCeldaYSolicitarPintura();
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    private bool PartidaEnCurso()
    {
        return GameManager.Instance != null &&
            GameManager.Instance.EstadoActual.Value == GameState.EnJuego;
    }

    private void DetectarCeldaYSolicitarPintura()
    {
        if (SueloController.Instance == null || !SueloController.Instance.BoundsListos)
            return;

        int celda = SueloController.Instance.ObtenerIndiceEnPosicion(transform.position);
        if (celda < 0 || celda == ultimaCelda) return;

        ultimaCelda = celda;

        if (IsServer)
            PintarEnServidor(celda);
        else
            SolicitarPinturaRpc(celda);
    }

    [Rpc(SendTo.Server)]
    private void SolicitarPinturaRpc(int indice)
    {
        PintarEnServidor(indice);
    }

    private void PintarEnServidor(int indice)
    {
        if (!IsServer || !PartidaEnCurso() || SueloController.Instance == null)
            return;
        if (ColorId.Value == 0) return;

        SueloController.Instance.EstablecerColorBaldosa(indice, ColorId.Value);
    }

    private void AlCambiarColor(byte anterior, byte nuevo)
    {
        AplicarColor(nuevo);
    }

    private void AplicarColor(byte id)
    {
        var render = GetComponent<Renderer>();
        if (render != null) render.material.color = PlayerPalette.Obtener(id);
    }

    private void AlCambiarEstado(GameState viejo, GameState nuevo)
    {
        ultimaCelda = -1;
        inputH = 0f;
        inputV = 0f;
    }

    public void ActivarBuffVelocidad(float multiplicador, float duracion)
    {
        if (!IsServer) return;
        if (rutinaBuffActiva != null) StopCoroutine(rutinaBuffActiva);
        rutinaBuffActiva = StartCoroutine(RutinaBuffVelocidad(multiplicador, duracion));
    }

    private IEnumerator RutinaBuffVelocidad(float multiplicador, float duracion)
    {
        multiplicadorVelocidad.Value = multiplicador;
        yield return new WaitForSeconds(duracion);
        multiplicadorVelocidad.Value = 1f;
        rutinaBuffActiva = null;
    }

    public void ActivarRalentizacion(float multiplicador, float duracion)
    {
        if (!IsServer) return;
        if (rutinaRalentizacionActiva != null) StopCoroutine(rutinaRalentizacionActiva);
        rutinaRalentizacionActiva = StartCoroutine(RutinaRalentizacion(multiplicador, duracion));
    }

    private IEnumerator RutinaRalentizacion(float multiplicador, float duracion)
    {
        multiplicadorRalentizacion.Value = multiplicador;
        yield return new WaitForSeconds(duracion);
        multiplicadorRalentizacion.Value = 1f;
        rutinaRalentizacionActiva = null;
    }
}
