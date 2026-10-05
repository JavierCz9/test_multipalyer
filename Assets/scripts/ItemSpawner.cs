using System.Collections;
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

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        rutinaSpawner = StartCoroutine(GenerarItems());
    }

    public override void OnNetworkDespawn()
    {
        if (rutinaSpawner != null)
            StopCoroutine(rutinaSpawner);
    }

    private IEnumerator GenerarItems()
    {
        while (true)
        {
            yield return new WaitForSeconds(tiempoEntreItems);

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

        Vector3 posicion = ObtenerPosicionRandom();

        GameObject nuevoItem = Instantiate(itemPrefab, posicion, Quaternion.identity);

        NetworkObject networkObject = nuevoItem.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError("[ItemSpawner] El prefab no tiene NetworkObject.");
            Destroy(nuevoItem);
            return;
        }

        networkObject.Spawn();

        StartCoroutine(DestruirItemDespuesDeTiempo(networkObject));
    }

    private Vector3 ObtenerPosicionRandom()
    {
    return SueloController.Instance.ObtenerPosicionRandomParaItem();

    }

    private IEnumerator DestruirItemDespuesDeTiempo(NetworkObject item)
    {
        yield return new WaitForSeconds(duracionItem);

        if (item != null && item.IsSpawned)
            item.Despawn(true);
    }
}