using System.Collections;
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

    public NetworkVariable<byte> ColorId = new NetworkVariable<byte>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[Player] OnNetworkSpawn. OwnerClientId={OwnerClientId}, IsOwner={IsOwner}");

        ColorId.OnValueChanged += AlCambiarColorId;

        if (IsOwner)
        {
            byte id = (byte)(OwnerClientId % (ulong)PlayerPalette.CantidadJugadores + 1);
            ColorId.Value = id;
            Debug.Log($"[Player] Asignado ColorId={id}");
        }

        AplicarColor(ColorId.Value);

        // ❗ El Teleport no se puede llamar dentro de OnNetworkSpawn porque
        //    el NetworkTransform aún no terminó de inicializarse y da
        //    "Teleporting on non-authoritative side is not allowed!".
        //    Lo diferimos un frame con una corrutina.
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
        if (Input.GetKeyDown(KeyCode.W))
        {
            Debug.Log($"[Input] W pulsada. IsOwner={IsOwner}, " +
                      $"estado={GameManager.Instance?.EstadoActual.Value}");
        }

        if (!IsOwner) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.EstadoActual.Value != GameState.EnJuego) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 mov = new Vector3(h, 0, v) * velocidadMovimiento * Time.deltaTime;
        transform.Translate(mov, Space.World);
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
    // COLOCACIÓN CON TELETRANSPORTE
    // -------------------------------------------------------------------

    /// <summary>
    /// Corrutina que espera un frame antes de colocar al jugador en la
    /// sala de espera. Sin esta espera, el NetworkTransform puede no
    /// estar listo y rechazar la llamada a Teleport.
    /// </summary>
    private IEnumerator ColocarEnSalaEsperaDiferido()
    {
        // Esperamos a que termine el frame de spawn.
        yield return null;

        ColocarEnSalaEspera();
    }

    private void ColocarEnSalaEspera()
    {
        float separacion = 2f;
        float x = (OwnerClientId - 3.5f) * separacion;
        Vector3 destino = new Vector3(x, 1f, -5f);
        MoverConTeleport(destino);
    }

    private void ColocarEnSpawnDeJuego()
    {
        float separacion = 1.5f;
        float x = (OwnerClientId - 1.5f) * separacion;
        Vector3 destino = new Vector3(x, 1f, -3f);
        MoverConTeleport(destino);
    }

    /// <summary>
    /// Mueve al jugador usando NetworkTransform.Teleport si está disponible
    /// y SOMOS el lado autoritativo. Si no, cae a transform.position.
    /// Además desactiva el Collider durante el salto para no disparar
    /// triggers de tiles intermedias.
    /// </summary>
    private void MoverConTeleport(Vector3 destino)
    {
        // Desactivamos el collider durante el salto.
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        bool movido = false;

        // Solo el owner puede llamar Teleport sobre su propio NetworkTransform.
        if (IsOwner)
        {
            var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null)
            {
                try
                {
                    nt.Teleport(destino, Quaternion.identity, transform.localScale);
                    movido = true;
                }
                catch (System.Exception e)
                {
                    // Si NGO aún no permite el Teleport, caemos al fallback.
                    Debug.LogWarning($"[Player] Teleport falló, usando fallback: {e.Message}");
                }
            }
        }

        // Fallback: mover directo con transform.position.
        if (!movido)
        {
            transform.position = destino;
            transform.rotation = Quaternion.identity;
        }

        // Reactivamos el collider en el siguiente frame.
        StartCoroutine(ReactivarCollider(col));
    }

    private IEnumerator ReactivarCollider(Collider col)
    {
        yield return null;
        if (col != null) col.enabled = true;
    }
}