using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class SpawnerJugadores : NetworkBehaviour
{
    [Header("Configuración")]
    [SerializeField] private GameObject jugadorPrefab;
    [SerializeField] private float alturaSpawn = 1.1f;

    private int indiceSpawnActual = 0;
    private List<Vector3> posicionesSpawn = new List<Vector3>();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            LeerPosicionesDeLaEscena();

            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                SpawnearJugador(clientId);
            }

            NetworkManager.Singleton.OnClientConnectedCallback += OnClienteConectado;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (NetworkManager.Singleton != null && IsServer)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClienteConectado;
    }

    private void OnClienteConectado(ulong clientId)
    {
        if (!IsServer) return;
        SpawnearJugador(clientId);
    }

    private void LeerPosicionesDeLaEscena()
    {
        posicionesSpawn.Clear();
        SpawnPoint[] spawns = FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude);

        foreach (var spawn in spawns)
        {
            Vector3 pos = spawn.transform.position;
            pos.y = alturaSpawn;
            posicionesSpawn.Add(pos);
        }

        Debug.Log($"[Spawner] Se encontraron {posicionesSpawn.Count} puntos de spawn.");
    }

    private void SpawnearJugador(ulong clientId)
    {
        if (posicionesSpawn.Count == 0)
        {
            Debug.LogError("[Spawner] No hay SpawnPoints en la escena.");
            return;
        }

        // ✅ FIX: Si hay un jugador vivo de la partida anterior, lo destruimos
        NetworkObject jugadorViejo = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
        if (jugadorViejo != null)
        {
            jugadorViejo.Despawn(true);
        }

        // Ahora creamos al jugador nuevo en el SpawnPoint
        int indice = indiceSpawnActual % posicionesSpawn.Count;
        indiceSpawnActual++;

        Vector3 pos = posicionesSpawn[indice];

        GameObject jugador = Instantiate(jugadorPrefab, pos, Quaternion.identity);
        var networkObj = jugador.GetComponent<NetworkObject>();

        networkObj.SpawnAsPlayerObject(clientId);

        Debug.Log($"[SPAWN] Jugador {clientId} spawneado en {pos}");
    }
}