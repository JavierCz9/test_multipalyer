using System.Collections;
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
        ColorId.OnValueChanged += (v, n) => AplicarColor(n);

        if (IsOwner)
        {
            byte id = (byte)(OwnerClientId % (ulong)PlayerPalette.CantidadJugadores + 1);
            ColorId.Value = id;
        }
        AplicarColor(ColorId.Value);

        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged += AlCambiarEstado;

        // ✅ FIX CRÍTICO: Revisamos el estado al nacer.
        // Si la GameScene cargó y ya estamos "EnJuego", el jugador se activa de inmediato.
        if (GameManager.Instance != null && GameManager.Instance.EstadoActual.Value == GameState.EnJuego)
        {
            ResetearEstadoInterno();
            ColocarEnSpawnDeJuego();
            ActivarFisica();
        }
        else
        {
            StartCoroutine(ColocarEnSalaEsperaDiferido());
            DesactivarFisica();
        }
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
        if (!IsOwner) return;
        if (GameManager.Instance == null || GameManager.Instance.EstadoActual.Value != GameState.EnJuego) return;
        if (rb == null) return;

        Vector3 direccion = new Vector3(inputH, 0f, inputV);
        if (direccion.sqrMagnitude > 0.01f) direccion = direccion.normalized;
        else direccion = Vector3.zero;

        rb.MovePosition(rb.position + direccion * velocidadMovimiento * Time.fixedDeltaTime);
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
            ResetearEstadoInterno();
            ColocarEnSpawnDeJuego();
            ActivarFisica();
        }
        else if (nuevo == GameState.SalaEspera)
        {
            ResetearEstadoInterno();
            ColocarEnSalaEspera();
            DesactivarFisica();
        }
    }

    private void ResetearEstadoInterno()
    {
        ultimaCelda = -1;
        inputH = 0f;
        inputV = 0f;
    }

    private void ActivarFisica()
    {
        if (rb == null) return;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private void DesactivarFisica()
    {
        if (rb == null) return;
        rb.isKinematic = true;
    }

    private IEnumerator ColocarEnSalaEsperaDiferido()
    {
        yield return null;
        ColocarEnSalaEspera();
    }

    private void ColocarEnSalaEspera()
    {
        MoverConTeleport(ObtenerPosicionInicial());
    }

    private void ColocarEnSpawnDeJuego()
    {
        MoverConTeleport(ObtenerPosicionInicial());
    }

    private Vector3 ObtenerPosicionInicial()
    {
        if (SueloController.Instance != null)
            return SueloController.Instance.ObtenerPosicionSpawn((int)OwnerClientId);

        float x = (OwnerClientId - 1.5f) * 1.5f;
        return new Vector3(x, 1.10f, -3f);
    }

    private void MoverConTeleport(Vector3 destino)
    {
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (rb != null)
        {
            rb.position = destino;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        transform.position = destino;
        transform.rotation = Quaternion.identity;

        if (IsOwner)
        {
            var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null)
            {
                try { nt.Teleport(destino, Quaternion.identity, transform.localScale); }
                catch (System.Exception) { }
            }
        }
        StartCoroutine(ReactivarCollider(col));
    }

    private IEnumerator ReactivarCollider(Collider col)
    {
        yield return null;
        if (col != null) col.enabled = true;
    }
}