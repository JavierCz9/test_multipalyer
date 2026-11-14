using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [Header("Configuración de Movimiento")]
    [SerializeField] private float velocidadMovimiento = 8f;

    // ID de color del jugador
    public NetworkVariable<byte> ColorId = new NetworkVariable<byte>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    // =========================================================
    // MULTIPLICADORES DE ESTADO (Power-ups)
    // =========================================================

    // Multiplicador para el buff de velocidad (ej: 2 = el doble)
    public NetworkVariable<float> multiplicadorVelocidad = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Multiplicador para la ralentización (ej: 0.2 = 20% de la velocidad)
    public NetworkVariable<float> multiplicadorRalentizacion = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Rigidbody rb;
    private float inputH;
    private float inputV;
    private int ultimaCelda = -1;

    // Corrutinas para controlar el tiempo de los efectos
    private Coroutine rutinaBuffActiva;
    private Coroutine rutinaRalentizacionActiva;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        ColorId.OnValueChanged += (v, n) => AplicarColor(n);

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
        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged -= AlCambiarEstado;

        // Limpiamos las corrutinas si el jugador se desconecta
        if (rutinaBuffActiva != null) StopCoroutine(rutinaBuffActiva);
        if (rutinaRalentizacionActiva != null) StopCoroutine(rutinaRalentizacionActiva);
    }

    private void Update()
    {
        // 1. El servidor pinta las baldosas de todos los jugadores
        if (NetworkManager.Singleton.IsServer)
        {
            if (GameManager.Instance != null && GameManager.Instance.EstadoActual.Value == GameState.EnJuego)
                DetectarCeldaYpintar();
        }

        // 2. Solo el dueño del jugador lee el teclado
        if (!IsOwner) { inputH = 0f; inputV = 0f; return; }

        if (GameManager.Instance == null || GameManager.Instance.EstadoActual.Value != GameState.EnJuego)
        { inputH = 0f; inputV = 0f; return; }

        inputH = Input.GetAxis("Horizontal");
        inputV = Input.GetAxis("Vertical");
    }

    private void FixedUpdate()
    {
        if (!IsOwner || rb == null) return;

        if (GameManager.Instance != null && GameManager.Instance.EstadoActual.Value == GameState.EnJuego)
        {
            if (rb.isKinematic) rb.isKinematic = false;

            Vector3 direccion = new Vector3(inputH, 0f, inputV).normalized;

            // ✅ APLICAMOS AMBOS MULTIPLICADORES:
            // Si multiplicadorVelocidad es 2 y multiplicadorRalentizacion es 0.5, la velocidad final es la normal.
            float velocidadFinal = velocidadMovimiento * multiplicadorVelocidad.Value * multiplicadorRalentizacion.Value;

            rb.linearVelocity = direccion * velocidadFinal;
        }
        else
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
    }

    private void DetectarCeldaYpintar()
    {
        if (SueloController.Instance == null) return;
        int celdaActual = SueloController.Instance.ObtenerIndiceEnPosicion(transform.position);

        if (celdaActual == -1 || celdaActual == ultimaCelda) return;

        ultimaCelda = celdaActual;
        SueloController.Instance.EstablecerColorBaldosa(celdaActual, ColorId.Value);
    }

    private void AplicarColor(byte id)
    {
        var render = GetComponent<Renderer>();
        if (render != null) render.material.color = PlayerPalette.Obtener(id);
    }

    private void AlCambiarEstado(GameState viejo, GameState nuevo)
    {
        if (nuevo == GameState.EnJuego)
        {
            ultimaCelda = -1;
            inputH = 0f;
            inputV = 0f;
        }
    }

    // =========================================================
    // LÓGICA DE POWER-UPS
    // =========================================================

    // --- BUFF DE VELOCIDAD ---
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

    // --- RALENTIZACIÓN ---
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