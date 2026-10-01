using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Coordina la sincronización de los colores de las baldosas.
/// Las baldosas ya están en la escena (colocadas con el tilemap).
/// Este script las encuentra, les asigna un índice, y sincroniza los colores.
/// Es el único NetworkObject relacionado con el suelo.
/// </summary>
public class SueloController : NetworkBehaviour
{
    public static SueloController Instance;

    // Lista sincronizada de IDs de color. Índice = Tile.Indice.
    private NetworkList<byte> idsColores;

    // Array local de tiles indexadas. Para lookup rápido al pintar.
    private Tile[] tilesPorIndice;

    private void Awake()
    {
        Instance = this;
        idsColores = new NetworkList<byte>();
    }

    public override void OnNetworkSpawn()
    {
        // 1) Buscar todas las tiles en la escena.
        //    El tilemap ya las dejó instanciadas como GameObjects.
        var todas = FindObjectsByType<Tile>();

        if (todas.Length == 0)
        {
            Debug.LogWarning("[Suelo] No hay ninguna tile en la escena. " +
                             "¿Olvidaste pintar con el tilemap?");
            return;
        }

        // 2) Asignar índices determinísticos por posición (X, luego Z).
        //    Todos los clientes ejecutan esto y obtienen el mismo resultado.
        AsignarIndicesPorPosicion(todas);

        // 3) Construir el array indexado para lookup rápido.
        int maxIndice = -1;
        foreach (var t in todas)
            if (t.Indice > maxIndice) maxIndice = t.Indice;

        tilesPorIndice = new Tile[maxIndice + 1];
        foreach (var t in todas)
            if (t.Indice >= 0 && t.Indice < tilesPorIndice.Length)
                tilesPorIndice[t.Indice] = t;

        // 4) El servidor inicializa la NetworkList con ceros.
        if (IsServer)
        {
            idsColores.Clear();
            for (int i = 0; i < tilesPorIndice.Length; i++)
                idsColores.Add(0);
        }

        // 5) Suscribir el listener de cambios.
        idsColores.OnListChanged += AlCambiarColor;

        Debug.Log($"[Suelo] {todas.Length} tiles encontradas, " +
                  $"{tilesPorIndice.Length} slots en la NetworkList.");
    }

    public override void OnNetworkDespawn()
    {
        idsColores.OnListChanged -= AlCambiarColor;
    }

    /// <summary>
    /// Ordena las tiles por posición y les asigna índices consecutivos.
    /// El orden debe ser determinístico para que servidor y clientes
    /// coincidan en qué índice corresponde a qué tile.
    /// </summary>
    private void AsignarIndicesPorPosicion(Tile[] tiles)
    {
        System.Array.Sort(tiles, (a, b) =>
        {
            // Redondeamos a 2 decimales para evitar problemas de precisión.
            float ax = Mathf.Round(a.transform.position.x * 100f);
            float az = Mathf.Round(a.transform.position.z * 100f);
            float bx = Mathf.Round(b.transform.position.x * 100f);
            float bz = Mathf.Round(b.transform.position.z * 100f);

            int cmp = ax.CompareTo(bx);
            if (cmp != 0) return cmp;
            return az.CompareTo(bz);
        });

        for (int i = 0; i < tiles.Length; i++)
            tiles[i].Indice = i;
    }

    private void AlCambiarColor(NetworkListEvent<byte> evento)
    {
        if (evento.Type != NetworkListEvent<byte>.EventType.Value) return;
        if (tilesPorIndice == null) return;
        if (evento.Index < 0 || evento.Index >= tilesPorIndice.Length) return;

        var tile = tilesPorIndice[evento.Index];
        if (tile != null)
            tile.EstablecerColor(PlayerPalette.Obtener(evento.Value));
    }

    /// <summary>
    /// El servidor llama a esto cuando un jugador pisa una tile.
    /// Escribir en la NetworkList sincroniza el cambio a todos los clientes.
    /// </summary>
    public void EstablecerColorBaldosa(int indice, byte idColor)
    {
        if (!IsServer) return;
        if (indice < 0 || indice >= idsColores.Count) return;
        idsColores[indice] = idColor;
    }
}