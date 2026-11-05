using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class SpawnerJugadores : NetworkBehaviour
{
    [SerializeField] private Transform[] puntosSpawn;
    [SerializeField] private GameObject jugadorPrefab;

    private int indiceSpawnActual = 0;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            // ✅ FIX: Spawneamos a todos los jugadores que ya están conectados 
            // (porque vienen de la LobbyScene)
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                SpawnearJugador(clientId);
            }

            // Escuchamos por si alguien se une a mitad de partida (opcional)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClienteConectado;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (NetworkManager.Singleton != null && IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClienteConectado;
        }
    }

    private void OnClienteConectado(ulong clientId)
    {
        if (!IsServer) return;
        SpawnearJugador(clientId);
    }

    private void SpawnearJugador(ulong clientId)
    {
        if (puntosSpawn == null || puntosSpawn.Length == 0)
        {
            Debug.LogError("SpawnerJugadores: falta asignar los puntos de spawn.");
            return;
        }

        // Evitar doble spawn si ya existe
        if (NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null) return;

        int indice = indiceSpawnActual % puntosSpawn.Length;
        indiceSpawnActual++;

        Vector3 pos = puntosSpawn[indice].position;
        Quaternion rot = puntosSpawn[indice].rotation;

        GameObject jugador = Instantiate(jugadorPrefab, pos, rot);
        var networkObj = jugador.GetComponent<NetworkObject>();

        networkObj.SpawnAsPlayerObject(clientId);

        Debug.Log($"[SPAWN] Jugador {clientId} spawneado en punto {indice}");
    }
}