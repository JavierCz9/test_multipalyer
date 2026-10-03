using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Controlador del jugador. Movimiento cinemático por física,
/// color propio y colocación en esquinas del suelo.
/// </summary>
public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float velocidadMovimiento = 8f;

    public NetworkVariable<byte> ColorId = new NetworkVariable<byte>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private Rigidbody rb;
    private float inputH;
    private float inputV;

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
            Debug.LogWarning("[Player] Falta Rigidbody en el prefab.");

        ColorId.OnValueChanged += AlCambiarColorId;

        if (IsOwner)
        {
            byte id = (byte)(OwnerClientId % (ulong)PlayerPalette.CantidadJugadores + 1);
            ColorId.Value = id;
        }

        AplicarColor(ColorId.Value);

        // Colocación diferida un frame para que el NetworkTransform
        // esté inicializado y acepte el Teleport.
        StartCoroutine(ColocarEnSalaEsperaDiferido());

        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged += AlCambiarEstado;
    }

    public override void OnNetworkDespawn()
    {
        ColorId.OnValueChanged -= AlCambiarColorId;

        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged -= AlCambiarEstado;
    }

    private void Update()
    {
        if (!IsOwner) { inputH = 0f; inputV = 0f; return; }

        if (GameManager.Instance == null ||
            GameManager.Instance.EstadoActual.Value != GameState.EnJuego)
        { inputH = 0f; inputV = 0f; return; }

        inputH = Input.GetAxis("Horizontal");
        inputV = Input.GetAxis("Vertical");
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return;
        if (GameManager.Instance == null ||
            GameManager.Instance.EstadoActual.Value != GameState.EnJuego)
            return;

        if (rb == null) return;

        rb.linearVelocity = Vector3.zero;

        // Dirección normalizada para que las diagonales no vayan más rápido.
        Vector3 direccion = new Vector3(inputH, 0f, inputV).normalized;

        // MovePosition con Rigidbody dinámico respeta colliders estáticos.
        // El NetworkTransform replica el resultado.
        rb.MovePosition(rb.position + direccion * velocidadMovimiento * Time.fixedDeltaTime);
    }

    private void AlCambiarColorId(byte viejo, byte nuevo) => AplicarColor(nuevo);

    private void AplicarColor(byte id)
    {
        var render = GetComponent<Renderer>();
        if (render != null)
            render.material.color = PlayerPalette.Obtener(id);
    }

    private void AlCambiarEstado(GameState viejo, GameState nuevo)
    {
        if (nuevo == GameState.EnJuego)
            ColocarEnSpawnDeJuego();
    }

    // -------------------------------------------------------------------
    // COLOCACIÓN
    // -------------------------------------------------------------------

    private IEnumerator ColocarEnSalaEsperaDiferido()
    {
        // Esperar a que SueloController exista Y tenga los bounds calculados.
        // Timeout de 5 segundos para no quedarnos colgados si algo falla.
        float timeout = 5f;
        float t = 0f;

        while (t < timeout)
        {
            if (SueloController.Instance != null &&
                SueloController.Instance.BoundsListos)
            {
                break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (t >= timeout)
            Debug.LogWarning("[Player] Timeout esperando bounds del SueloController. " +
                             "Usando fallback.");

        ColocarEnSalaEspera();
    }

    private void ColocarEnSalaEspera()
    {
        // Cada jugador aparece en su esquina apenas se conecta.
        Vector3 destino = ObtenerPosicionInicial();
        MoverConTeleport(destino);
    }

    private void ColocarEnSpawnDeJuego()
    {
        // Al empezar la partida, se reposicionan por si algo los movió.
        Vector3 destino = ObtenerPosicionInicial();
        MoverConTeleport(destino);
    }

    /// <summary>
    /// Calcula la posición inicial del jugador según su ClientId.
    /// Usa SueloController si está disponible, o un fallback simple.
    /// </summary>
    private Vector3 ObtenerPosicionInicial()
    {
        if (SueloController.Instance != null)
        {
            Vector3 pos = SueloController.Instance.ObtenerPosicionSpawn((int)OwnerClientId);
            Debug.Log($"[Player] ClientId={OwnerClientId} → " +
                      $"boundsListos={SueloController.Instance.BoundsListos} → pos={pos}");
            return pos;
        }

        Debug.Log($"[Player] ClientId={OwnerClientId} → FALLBACK (SueloController.Instance es null)");
        float x = (OwnerClientId - 1.5f) * 1.5f;
        return new Vector3(x, 1.05f, -3f);
    }

    private void MoverConTeleport(Vector3 destino)
    {
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 1) Movemos el Rigidbody. Con Rigidbody dinámico, esta es la
        //    fuente de verdad de la posición.
        if (rb != null)
            rb.position = destino;

        // 2) Movemos también el transform, para que NetworkTransform
        //    vea la nueva posición inmediatamente.
        transform.position = destino;
        transform.rotation = Quaternion.identity;

        // 3) Avisamos al NetworkTransform del salto para que no interpole.
        if (IsOwner)
        {
            var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null)
            {
                try
                {
                    nt.Teleport(destino, Quaternion.identity, transform.localScale);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Player] Teleport falló: {e.Message}");
                }
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