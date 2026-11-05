using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float velocidadMovimiento = 8f;

    public NetworkVariable<byte> ColorId = new NetworkVariable<byte>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner
    );

    private Rigidbody rb;
    private float inputH;
    private float inputV;
    private int ultimaCelda = -1;

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
    }

    private void Update()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            if (GameManager.Instance != null && GameManager.Instance.EstadoActual.Value == GameState.EnJuego)
                DetectarCeldaYpintar();
        }

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
            rb.linearVelocity = direccion * velocidadMovimiento;
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
}