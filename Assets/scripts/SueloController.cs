using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SueloController : NetworkBehaviour
{
    public static SueloController Instance;

    [Header("Configuración del grid")]
    [Tooltip("Tamaño de cada celda en unidades del mundo.")]
    public float TamBaldosa = 1f;

    private NetworkList<byte> idsColores;
    private Tile[] tilesPorIndice;

    // Diccionario: coordenada de celda → tile.
    private Dictionary<Vector2Int, Tile> tilesPorCelda
        = new Dictionary<Vector2Int, Tile>();

    private Vector3 minBounds;
    private Vector3 maxBounds;
    private bool boundsCalculados = false;
    private Vector3 origenGrid;

    public bool BoundsListos => boundsCalculados;

    private void Awake()
    {
        Instance = this;
        idsColores = new NetworkList<byte>();
    }

    public override void OnNetworkSpawn()
    {
        var todas = FindObjectsByType<Tile>();

        if (todas.Length == 0)
        {
            Debug.LogWarning("[Suelo] No hay ninguna tile en la escena.");
            return;
        }

        AsignarIndicesPorPosicion(todas);

        int maxIndice = -1;

        foreach (var t in todas)
        {
            if (t.Indice > maxIndice)
                maxIndice = t.Indice;
        }

        tilesPorIndice = new Tile[maxIndice + 1];

        foreach (var t in todas)
        {
            if (t.Indice >= 0 && t.Indice < tilesPorIndice.Length)
                tilesPorIndice[t.Indice] = t;
        }

        CalcularBounds(todas);
        ConstruirDiccionarioCeldas(todas);

        if (IsServer)
        {
            idsColores.Clear();

            for (int i = 0; i < tilesPorIndice.Length; i++)
                idsColores.Add(0);
        }

        idsColores.OnListChanged += AlCambiarColor;

        Debug.Log($"[Suelo] {todas.Length} tiles encontradas.");
    }

    public override void OnNetworkDespawn()
    {
        idsColores.OnListChanged -= AlCambiarColor;
    }

    // ============================================================
    // ÍNDICES
    // ============================================================

    private void AsignarIndicesPorPosicion(Tile[] tiles)
    {
        System.Array.Sort(tiles, (a, b) =>
        {
            float ax = Mathf.Round(a.transform.position.x * 100f);
            float az = Mathf.Round(a.transform.position.z * 100f);

            float bx = Mathf.Round(b.transform.position.x * 100f);
            float bz = Mathf.Round(b.transform.position.z * 100f);

            int cmp = ax.CompareTo(bx);

            if (cmp != 0)
                return cmp;

            return az.CompareTo(bz);
        });

        for (int i = 0; i < tiles.Length; i++)
            tiles[i].Indice = i;
    }

    // ============================================================
    // BOUNDS
    // ============================================================

    private void CalcularBounds(Tile[] tiles)
    {
        if (tiles.Length == 0)
            return;

        minBounds = tiles[0].transform.position;
        maxBounds = tiles[0].transform.position;

        foreach (var t in tiles)
        {
            Vector3 p = t.transform.position;

            if (p.x < minBounds.x)
                minBounds.x = p.x;

            if (p.z < minBounds.z)
                minBounds.z = p.z;

            if (p.x > maxBounds.x)
                maxBounds.x = p.x;

            if (p.z > maxBounds.z)
                maxBounds.z = p.z;
        }

        origenGrid = minBounds;
        boundsCalculados = true;
    }

    // ============================================================
    // DICCIONARIO DE CELDAS
    // ============================================================

    private void ConstruirDiccionarioCeldas(Tile[] tiles)
    {
        tilesPorCelda.Clear();

        foreach (var t in tiles)
        {
            Vector2Int celda =
                PosicionACelda(t.transform.position);

            if (!tilesPorCelda.ContainsKey(celda))
                tilesPorCelda[celda] = t;
        }

        Debug.Log(
            $"[Suelo] Diccionario con {tilesPorCelda.Count} celdas."
        );
    }

    private Vector2Int PosicionACelda(Vector3 pos)
    {
        float relX = pos.x - origenGrid.x;
        float relZ = pos.z - origenGrid.z;

        int x = Mathf.RoundToInt(relX / TamBaldosa);
        int z = Mathf.RoundToInt(relZ / TamBaldosa);

        return new Vector2Int(x, z);
    }

    // ============================================================
    // DETECTAR BALDOSA
    // ============================================================

    public int ObtenerIndiceEnPosicion(Vector3 pos)
    {
        if (!boundsCalculados)
            return -1;

        Vector2Int celda = PosicionACelda(pos);

        if (tilesPorCelda.TryGetValue(celda, out Tile tile))
            return tile.Indice;

        return -1;
    }

    // ============================================================
    // SPAWN DE JUGADORES
    // ============================================================

    public Vector3 ObtenerPosicionSpawn(int indice)
    {
        if (!boundsCalculados)
        {
            float xFallback =
                (indice - 1.5f) * 1.5f;

            return new Vector3(
                xFallback,
                1.05f,
                -3f
            );
        }

        float margen = 1f;

        float minX = minBounds.x + margen;
        float maxX = maxBounds.x - margen;

        float minZ = minBounds.z + margen;
        float maxZ = maxBounds.z - margen;

        float y = 1f;

        Vector3[] esquinas =
        {
            new Vector3(minX, y, minZ),
            new Vector3(maxX, y, maxZ),
            new Vector3(minX, y, maxZ),
            new Vector3(maxX, y, minZ)
        };

        if (indice < 4)
            return esquinas[indice];

        Vector3[] medios =
        {
            new Vector3(
                (minX + maxX) * 0.5f,
                y,
                minZ
            ),

            new Vector3(
                (minX + maxX) * 0.5f,
                y,
                maxZ
            ),

            new Vector3(
                minX,
                y,
                (minZ + maxZ) * 0.5f
            ),

            new Vector3(
                maxX,
                y,
                (minZ + maxZ) * 0.5f
            )
        };

        return medios[(indice - 4) % 4];
    }

    // ============================================================
    // CAMBIO DE COLOR DE BALDOSAS
    // ============================================================

    private void AlCambiarColor(
        NetworkListEvent<byte> evento)
    {
        if (evento.Type !=
            NetworkListEvent<byte>.EventType.Value)
            return;

        if (tilesPorIndice == null)
            return;

        if (evento.Index < 0 ||
            evento.Index >= tilesPorIndice.Length)
            return;

        Tile tile = tilesPorIndice[evento.Index];

        if (tile == null)
            return;

        // 0 = sin dueño
        // 1..8 = jugador

        if (evento.Value == 0)
        {
            tile.RestaurarColorOriginal();
        }
        else
        {
            tile.EstablecerColor(
                PlayerPalette.Obtener(evento.Value)
            );
        }
    }

    // ============================================================
    // PINTAR BALDOSA
    // ============================================================

    public void EstablecerColorBaldosa(
        int indice,
        byte idColor)
    {
        if (!IsServer)
            return;

        if (indice < 0 ||
            indice >= idsColores.Count)
            return;

        byte colorAnterior =
            idsColores[indice];

        if (colorAnterior == idColor)
            return;

        idsColores[indice] = idColor;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ActualizarContador(
                colorAnterior,
                idColor
            );
        }
    }

    // ============================================================
    // ROBAR BALDOSAS CON LA CAJA
    // ============================================================

    public void RobarBaldosas(
        byte colorLadron,
        byte colorEnemigo)
    {
        if (!IsServer)
            return;

        List<int> baldosasEnemigas =
            new List<int>();

        // Buscar todas las baldosas del enemigo.
        for (int i = 0;
             i < idsColores.Count;
             i++)
        {
            if (idsColores[i] == colorEnemigo)
                baldosasEnemigas.Add(i);
        }

        // Robar como máximo 5.
        int cantidadARobar =
            Mathf.Min(
                5,
                baldosasEnemigas.Count
            );

        // Mezclar las baldosas.
        for (int i = 0;
             i < baldosasEnemigas.Count;
             i++)
        {
            int randomIndex =
                Random.Range(
                    i,
                    baldosasEnemigas.Count
                );

            int temporal =
                baldosasEnemigas[i];

            baldosasEnemigas[i] =
                baldosasEnemigas[randomIndex];

            baldosasEnemigas[randomIndex] =
                temporal;
        }

        // Pasarlas al jugador que agarró la caja.
        for (int i = 0;
             i < cantidadARobar;
             i++)
        {
            int indice =
                baldosasEnemigas[i];

            idsColores[indice] =
                colorLadron;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ActualizarContador(
                    colorEnemigo,
                    colorLadron
                );
            }
        }

        Debug.Log(
            $"[Item] Se robaron {cantidadARobar} baldosas."
        );
    }

    // ============================================================
    // POSICIÓN RANDOM PARA LA CAJA
    // ============================================================

    public Vector3 ObtenerPosicionRandomParaItem()
    {
        if (tilesPorIndice == null ||
            tilesPorIndice.Length == 0)
            return Vector3.zero;

        List<Tile> tilesValidas =
            new List<Tile>();

        // Encontrar el tamaño del grid.
        int maxX = 0;
        int maxZ = 0;

        foreach (Tile tile in tilesPorIndice)
        {
            if (tile == null)
                continue;

            Vector2Int celda =
                PosicionACelda(
                    tile.transform.position
                );

            if (celda.x > maxX)
                maxX = celda.x;

            if (celda.y > maxZ)
                maxZ = celda.y;
        }

        // La caja puede aparecer desde
        // la segunda baldosa desde el borde.
        int margen = 1;

        foreach (Tile tile in tilesPorIndice)
        {
            if (tile == null)
                continue;

            Vector2Int celda =
                PosicionACelda(
                    tile.transform.position
                );

            if (celda.x < margen)
                continue;

            if (celda.x > maxX - margen)
                continue;

            if (celda.y < margen)
                continue;

            if (celda.y > maxZ - margen)
                continue;

            tilesValidas.Add(tile);
        }

        if (tilesValidas.Count == 0)
            return Vector3.zero;

        Tile tileElegida =
            tilesValidas[
                Random.Range(
                    0,
                    tilesValidas.Count
                )
            ];

        // Aparece encima de la baldosa.
        return tileElegida.transform.position
               + Vector3.up * 0.5f;
    }

    // ============================================================
    // RESET DE TODAS LAS BALDOSAS
    // ============================================================

    public void ResetearTodasLasTiles()
    {
        if (!IsServer)
            return;

        if (idsColores == null)
            return;

        for (int i = 0;
             i < idsColores.Count;
             i++)
        {
            idsColores[i] = 0;
        }

        Debug.Log(
            "[Suelo] Todas las tiles reseteadas a su color original."
        );
    }
}