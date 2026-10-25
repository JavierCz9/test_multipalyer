using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ItemSpawner : NetworkBehaviour
{
    [Header("Prefab del item")]
    [SerializeField] private GameObject itemPrefab;

    [Header("Configuración")]
    [SerializeField] private float tiempoEntreItems = 10f;
    [SerializeField] private float duracionItem = 5f;

    private Coroutine rutinaSpawner;
    private readonly List<NetworkObject> itemsActivos = new List<NetworkObject>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;

        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged += AlCambiarEstado;

        // ✅ FIX CRÍTICO: Si la escena ya cargó en estado EnJuego, arrancamos de inmediato.
        if (GameManager.Instance != null && GameManager.Instance.EstadoActual.Value == GameState.EnJuego)
        {
            if (rutinaSpawner == null)
                rutinaSpawner = StartCoroutine(GenerarItems());
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (rutinaSpawner != null) StopCoroutine(rutinaSpawner);

        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged -= AlCambiarEstado;
    }

    private void AlCambiarEstado(GameState viejo, GameState nuevo)
    {
        if (!IsServer) return;

        if (nuevo == GameState.EnJuego)
        {
            if (rutinaSpawner == null)
                rutinaSpawner = StartCoroutine(GenerarItems());
        }
        else if (nuevo == GameState.SalaEspera || nuevo == GameState.Finalizado)
        {
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

            if (GameManager.Instance == null || GameManager.Instance.EstadoActual.Value != GameState.EnJuego)
                continue;

            CrearItem();
        }
    }

    private void CrearItem()
    {
        if (itemPrefab == null || SueloController.Instance == null) return;

        Vector3 posicion = SueloController.Instance.ObtenerPosicionRandomParaItem();
        GameObject nuevoItem = Instantiate(itemPrefab, posicion, Quaternion.identity);
        NetworkObject networkObject = nuevoItem.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Destroy(nuevoItem);
            return;
        }

        networkObject.Spawn();
        itemsActivos.Add(networkObject);
        StartCoroutine(DestruirItemDespuesDeTiempo(networkObject));
    }

    private IEnumerator DestruirItemDespuesDeTiempo(NetworkObject item)
    {
        yield return new WaitForSeconds(duracionItem);

        if (item != null && item.IsSpawned)
        {
            itemsActivos.Remove(item);
            item.Despawn(true);
        }
    }

    private void LimpiarItems()
    {
        for (int i = itemsActivos.Count - 1; i >= 0; i--)
        {
            var item = itemsActivos[i];
            if (item != null && item.IsSpawned) item.Despawn(true);
        }
        itemsActivos.Clear();
    }
}