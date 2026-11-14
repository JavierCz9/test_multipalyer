using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ItemSpawner : NetworkBehaviour
{
    [Header("Prefabs de Ítems")]
    [Tooltip("Arrastrá acá los prefabs de la Caja y de la Velocidad")]
    [SerializeField] private GameObject[] prefabsItems;

    [Header("Configuración")]
    [SerializeField] private float tiempoEntreItems = 10f;
    [SerializeField] private int cantidadItemsPorTanda = 2; // Cuántos ítems a la vez
    [SerializeField] private float distanciaMinimaEntreItems = 3f; // Para que no aparezcan pegados
    [SerializeField] private float duracionItem = 8f;

    private Coroutine rutinaSpawner;
    private readonly List<NetworkObject> itemsActivos = new List<NetworkObject>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;

        if (GameManager.Instance != null)
            GameManager.Instance.EstadoActual.OnValueChanged += AlCambiarEstado;

        if (GameManager.Instance != null && GameManager.Instance.EstadoActual.Value == GameState.EnJuego)
            rutinaSpawner = StartCoroutine(GenerarItems());
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
            if (rutinaSpawner == null) rutinaSpawner = StartCoroutine(GenerarItems());
        }
        else
        {
            if (rutinaSpawner != null) { StopCoroutine(rutinaSpawner); rutinaSpawner = null; }
            LimpiarItems();
        }
    }

    private IEnumerator GenerarItems()
    {
        while (true)
        {
            yield return new WaitForSeconds(tiempoEntreItems);
            if (GameManager.Instance != null && GameManager.Instance.EstadoActual.Value == GameState.EnJuego)
                SpawnearTanda();
        }
    }

    private void SpawnearTanda()
    {
        if (prefabsItems.Length == 0 || SueloController.Instance == null) return;

        List<Vector3> posicionesElegidas = new List<Vector3>();

        for (int i = 0; i < cantidadItemsPorTanda; i++)
        {
            Vector3 posicion = Vector3.zero;
            bool valida = false;
            int intentos = 0;

            // Buscamos una posición que no esté pegada a otro ítem de esta tanda
            while (!valida && intentos < 15)
            {
                posicion = SueloController.Instance.ObtenerPosicionRandomParaItem();
                valida = true;
                foreach (var pos in posicionesElegidas)
                {
                    if (Vector3.Distance(posicion, pos) < distanciaMinimaEntreItems) { valida = false; break; }
                }
                intentos++;
            }

            if (valida)
            {
                posicionesElegidas.Add(posicion);

                // Elegimos un prefab al azar
                GameObject prefab = prefabsItems[Random.Range(0, prefabsItems.Length)];
                GameObject item = Instantiate(prefab, posicion, Quaternion.identity);

                NetworkObject netObj = item.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn();
                    itemsActivos.Add(netObj);
                    StartCoroutine(DestruirItem(netObj));
                }
            }
        }
    }

    private IEnumerator DestruirItem(NetworkObject item)
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
            if (itemsActivos[i] != null && itemsActivos[i].IsSpawned) itemsActivos[i].Despawn(true);
        }
        itemsActivos.Clear();
    }
}