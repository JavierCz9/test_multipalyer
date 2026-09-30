using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Controlador del jugador. Maneja el movimiento y sincroniza el ID
/// de color de cada jugador.
/// Se coloca en el Player Prefab, junto con NetworkObject y NetworkTransform.
/// </summary>
public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float velocidadMovimiento = 8f;

    // ID de color del jugador. Solo el Owner escribe, todos leen.
    public NetworkVariable<byte> ColorId = new NetworkVariable<byte>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[Player] OnNetworkSpawn. OwnerClientId={OwnerClientId}, IsOwner={IsOwner}");

        // Suscribimos el listener ANTES de asignar el valor, para no perder
        // el primer cambio que hará el owner.
        ColorId.OnValueChanged += AlCambiarColorId;

        // Solo el dueño decide su propio ID.
        if (IsOwner)
        {
            // OwnerClientId empieza en 0. Sumamos 1 porque el 0 está
            // reservado para "baldosa sin pisar". El módulo es una red
            // de seguridad si algún día hay más jugadores que colores.
            byte id = (byte)(OwnerClientId % (ulong)PlayerPalette.CantidadJugadores + 1);
            ColorId.Value = id;
        }

        // Aplicamos el color actual de una vez. Si somos el owner,
        // ya tiene el ID correcto. Si no, será 0 y se corregirá
        // automáticamente cuando llegue la sincronización.
        AplicarColor(ColorId.Value);

        // Posición inicial en la sala de espera.
        ColocarEnSalaEspera();

        // Suscripción al cambio de estado de la partida.
        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged += AlCambiarEstado;
    }

    public override void OnNetworkDespawn()
    {
        // Limpiamos todos los listeners para evitar fugas de memoria.
        ColorId.OnValueChanged -= AlCambiarColorId;

        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged -= AlCambiarEstado;
    }

    private void Update()
    {
        // LOG TEMPORAL de diagnóstico. Bórralo cuando todo funcione.
        if (Input.GetKeyDown(KeyCode.W))
        {
            Debug.Log($"[Input] W pulsada. IsOwner={IsOwner}, " +
                      $"estado={GameManager.Instance?.EstadoActual.Value}");
        }

        // Solo el dueño mueve su propio personaje.
        if (!IsOwner) return;

        // Solo se mueve si la partida empezó.
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.EstadoActual.Value != GameState.EnJuego) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 mov = new Vector3(h, 0, v) * velocidadMovimiento * Time.deltaTime;
        transform.Translate(mov, Space.World);
    }

    /// <summary>
    /// Se ejecuta cuando el ColorId cambia por sincronización de red.
    /// Así todos los clientes ven el color correcto, no solo el owner.
    /// </summary>
    private void AlCambiarColorId(byte viejo, byte nuevo)
    {
        AplicarColor(nuevo);
    }

    /// <summary>
    /// Aplica el color del ID al Renderer del jugador.
    /// </summary>
    private void AplicarColor(byte id)
    {
        var render = GetComponent<Renderer>();
        if (render != null)
            render.material.color = PlayerPalette.Obtener(id);
    }

    /// <summary>
    /// Se ejecuta cuando el estado del juego cambia.
    /// Cuando pasamos a EnJuego, teletransportamos al jugador dentro del grid.
    /// </summary>
    private void AlCambiarEstado(GameState viejo, GameState nuevo)
    {
        if (nuevo == GameState.EnJuego)
            ColocarEnSpawnDeJuego();
    }

    /// <summary>
    /// Coloca al jugador en la fila de espera antes de empezar la partida.
    /// </summary>
    private void ColocarEnSalaEspera()
    {
        float separacion = 2f;
        float x = (OwnerClientId - 3.5f) * separacion;
        float y = 1f;   // base de la cápsula apoyada en el suelo
        float z = -5f;  // dentro del campo de visión de la cámara

        transform.position = new Vector3(x, y, z);
        transform.rotation = Quaternion.identity;
    }

    /// <summary>
    /// Coloca al jugador dentro del grid cuando empieza la partida.
    /// </summary>
    private void ColocarEnSpawnDeJuego()
    {
        float separacion = 1.5f;
        float x = (OwnerClientId - 1.5f) * separacion;
        float y = 1f;
        float z = -3f;

        transform.position = new Vector3(x, y, z);
        transform.rotation = Quaternion.identity;
    }
}