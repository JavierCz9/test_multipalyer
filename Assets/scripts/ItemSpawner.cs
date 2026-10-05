using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Spawnea cajas periódicamente en posiciones aleatorias del mapa.
/// Solo spawnea durante la partida (EnJuego).
/// Limpia todas las cajas activas al volver al lobby.
/// </summary>
public class ItemSpawner : NetworkBehaviour
{
    [Header("Prefab del item")]
    [SerializeField] private GameObject itemPrefab;

    [Header("Configuración")]
    [Tooltip("Tiempo entre apariciones de cajas.")]
    [SerializeField] private float tiempoEntreItems = 10f;

    [Tooltip("Cuánto dura una caja antes de desaparecer sola.")]
    [SerializeField] private float duracionItem = 5f;

    // Corrutina principal del spawner.
    private Coroutine rutinaSpawner;

    // Lista de items activos, para poder limpiarlos al volver al lobby.
    private readonly List<NetworkObject> itemsActivos = new List<NetworkObject>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Solo el servidor spawnea.
        if (!IsServer) return;

        // Nos suscribimos al cambio de estado para saber cuándo empezar
        // a spawnear y cuándo limpiar.
        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged += AlCambiarEstado;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (rutinaSpawner != null)
            StopCoroutine(rutinaSpawner);

        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged -= AlCambiarEstado;
    }

    private void AlCambiarEstado(GameState viejo, GameState nuevo)
    {
        if (!IsServer) return;

        if (nuevo == GameState.EnJuego)
        {
            // Empezar a spawnear.
            if (rutinaSpawner == null)
                rutinaSpawner = StartCoroutine(GenerarItems());
        }
        else if (nuevo == GameState.SalaEspera || nuevo == GameState.Finalizado)
        {
            // Parar el spawner y limpiar todos los items.
            if (rutinaSpawner != null)
            {
                StopCoroutine(rutinaSpawner);
                rutinaSpawner = null;
            }

            LimpiarItems();
        }
    }

    private IEnumerator GenerarItems()
    {
        while (true)
        {
            yield return new WaitForSeconds(tiempoEntreItems);

            // Por seguridad, solo spawnear si seguimos en juego.
            if (GameManager.Instance == null) continue;
            if (GameManager.Instance.EstadoActual.Value != GameState.EnJuego)
                continue;

            CrearItem();
        }
    }

    private void CrearItem()
    {
        if (itemPrefab == null)
        {
            Debug.LogError("[ItemSpawner] No hay ItemPrefab asignado.");
            return;
        }

        if (SueloController.Instance == null)
        {
            Debug.LogError("[ItemSpawner] No existe SueloController.");
            return;
        }

        Vector3 posicion = SueloController.Instance.ObtenerPosicionRandomParaItem();

        GameObject nuevoItem = Instantiate(itemPrefab, posicion, Quaternion.identity);

        NetworkObject networkObject = nuevoItem.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError("[ItemSpawner] El prefab no tiene NetworkObject.");
            Destroy(nuevoItem);
            return;
        }

        networkObject.Spawn();

        // Guardarlo en la lista para poder limpiarlo después.
        itemsActivos.Add(networkObject);

        StartCoroutine(DestruirItemDespuesDeTiempo(networkObject));
    }

    private IEnumerator DestruirItemDespuesDeTiempo(NetworkObject item)
    {
        yield return new WaitForSeconds(duracionItem);

        // Verificamos que siga existiendo y no lo hayan agarrado.
        if (item != null && item.IsSpawned)
        {
            itemsActivos.Remove(item);
            item.Despawn(true);
        }
    }

    /// <summary>
    /// Despawnea todos los items activos. Se llama al volver al lobby.
    /// </summary>
    private void LimpiarItems()
    {
        Debug.Log($"[ItemSpawner] Limpiando {itemsActivos.Count} items.");

        // Recorremos al revés porque vamos a modificar la lista.
        for (int i = itemsActivos.Count - 1; i >= 0; i--)
        {
            var item = itemsActivos[i];

            if (item != null && item.IsSpawned)
                item.Despawn(true);
        }

        itemsActivos.Clear();
    }
}