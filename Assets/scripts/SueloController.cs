using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Coordina la sincronización de colores de las baldosas y calcula
/// las posiciones de spawn para cada jugador.
/// Las baldosas ya están en la escena (colocadas con el tilemap).
/// Este script las encuentra, les asigna un índice, y sincroniza sus colores.
/// Es el único NetworkObject relacionado con el suelo.
/// </summary>
public class SueloController : NetworkBehaviour
{
    public static SueloController Instance;

    // Lista sincronizada de IDs de color. Índice = Tile.Indice.
    private NetworkList<byte> idsColores;
    public bool BoundsListos => boundsCalculados;

    // Array local de tiles indexadas. Para lookup rápido al pintar.
    private Tile[] tilesPorIndice;

    // Bounding box calculado desde las tiles reales de la escena.
    private Vector3 minBounds;
    private Vector3 maxBounds;
    private bool boundsCalculados = false;

    private void Awake()
    {
        Instance = this;
        idsColores = new NetworkList<byte>();
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[Suelo] OnNetworkSpawn INICIO. Instance={Instance != null}");
        // 1) Buscar todas las tiles en la escena.
        var todas = FindObjectsByType<Tile>();

        if (todas.Length == 0)
        {
            Debug.LogWarning("[Suelo] No hay ninguna tile en la escena.");
            return;
        }

        // 2) Asignar índices determinísticos por posición (X, luego Z).
        AsignarIndicesPorPosicion(todas);

        // 3) Construir el array indexado.
        int maxIndice = -1;
        foreach (var t in todas)
            if (t.Indice > maxIndice) maxIndice = t.Indice;

        tilesPorIndice = new Tile[maxIndice + 1];
        foreach (var t in todas)
            if (t.Indice >= 0 && t.Indice < tilesPorIndice.Length)
                tilesPorIndice[t.Indice] = t;

        // 4) Calcular el bounding box del suelo (esquinas mín/máx).
        CalcularBounds(todas);

        // 5) El servidor inicializa la NetworkList con ceros.
        if (IsServer)
        {
            idsColores.Clear();
            for (int i = 0; i < tilesPorIndice.Length; i++)
                idsColores.Add(0);
        }

        // 6) Suscribir el listener de cambios.
        idsColores.OnListChanged += AlCambiarColor;

        Debug.Log($"[Suelo] {todas.Length} tiles encontradas, " +
                  $"bounds X[{minBounds.x:F1}, {maxBounds.x:F1}] " +
                  $"Z[{minBounds.z:F1}, {maxBounds.z:F1}]");
        Debug.Log($"[Suelo] OnNetworkSpawn terminado. " +
          $"boundsListos={boundsCalculados}, " +
          $"minBounds={minBounds}, maxBounds={maxBounds}");
    }

    public override void OnNetworkDespawn()
    {
        idsColores.OnListChanged -= AlCambiarColor;
    }

    /// <summary>
    /// Ordena las tiles por posición y les asigna índices consecutivos.
    /// </summary>
    private void AsignarIndicesPorPosicion(Tile[] tiles)
    {
        System.Array.Sort(tiles, (a, b) =>
        {
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

    /// <summary>
    /// Calcula las esquinas del área cubierta por las tiles.
    /// Se usa para posicionar a los jugadores en las esquinas del suelo.
    /// </summary>
    private void CalcularBounds(Tile[] tiles)
    {
        if (tiles.Length == 0) return;

        minBounds = tiles[0].transform.position;
        maxBounds = tiles[0].transform.position;

        foreach (var t in tiles)
        {
            Vector3 p = t.transform.position;
            if (p.x < minBounds.x) minBounds.x = p.x;
            if (p.z < minBounds.z) minBounds.z = p.z;
            if (p.x > maxBounds.x) maxBounds.x = p.x;
            if (p.z > maxBounds.z) maxBounds.z = p.z;
        }

        boundsCalculados = true;
    }

    /// <summary>
    /// Devuelve la posición de spawn para un jugador según su índice.
    /// Los primeros 4 van a las 4 esquinas del grid. Los siguientes 4
    /// van a los puntos medios de los bordes. Con más de 8 se repiten.
    /// La Y se fija a 1.05 para que la cápsula quede apoyada sobre el piso.
    /// </summary>
    public Vector3 ObtenerPosicionSpawn(int indice)
    {
        if (!boundsCalculados)
        {
            // Fallback por si las tiles no se encontraron.
            float xFallback = (indice - 1.5f) * 1.5f;
            return new Vector3(xFallback, 1.05f, -3f);
        }

        // Margen desde el borde para no aparecer dentro de las paredes.
        // En unidades de mundo.
        float margen = 1.5f;

        float minX = minBounds.x + margen;
        float maxX = maxBounds.x - margen;
        float minZ = minBounds.z + margen;
        float maxZ = maxBounds.z - margen;

        float y = 1.05f;

        // 4 esquinas para los primeros 4 jugadores.
        Vector3[] esquinas = new Vector3[]
        {
            new Vector3(minX, y, minZ),  // 0: suroeste
            new Vector3(maxX, y, maxZ),  // 1: noreste
            new Vector3(minX, y, maxZ),  // 2: noroeste
            new Vector3(maxX, y, minZ),  // 3: sureste
        };

        if (indice < 4)
            return esquinas[indice];

        // Puntos medios de los bordes para jugadores 4 a 7.
        Vector3[] medios = new Vector3[]
        {
            new Vector3((minX + maxX) * 0.5f, y, minZ),  // 4: sur
            new Vector3((minX + maxX) * 0.5f, y, maxZ),  // 5: norte
            new Vector3(minX, y, (minZ + maxZ) * 0.5f),  // 6: oeste
            new Vector3(maxX, y, (minZ + maxZ) * 0.5f),  // 7: este
        };

        return medios[(indice - 4) % 4];
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
    /// El servidor llama a esto cuando un jugador pisa una baldosa.
    /// </summary>
    public void EstablecerColorBaldosa(int indice, byte idColor)
    {
        if (!IsServer) return;
        if (indice < 0 || indice >= idsColores.Count) return;
        idsColores[indice] = idColor;
    }
}