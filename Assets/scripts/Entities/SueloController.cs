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
    private Dictionary<Vector2Int, Tile> tilesPorCelda = new Dictionary<Vector2Int, Tile>();

    // ✅ Solo conservamos origenGrid para calcular las coordenadas de las celdas
    private Vector3 origenGrid;
    private bool boundsCalculados = false;

    public bool BoundsListos => boundsCalculados;

    private void Awake()
    {
        Instance = this;
        idsColores = new NetworkList<byte>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        var todas = FindObjectsByType<Tile>(FindObjectsInactive.Exclude);

        if (todas.Length == 0)
        {
            Debug.LogWarning("[Suelo] No hay ninguna tile en la escena.");
            return;
        }

        AsignarIndicesPorPosicion(todas);

        int maxIndice = -1;
        foreach (var t in todas)
            if (t.Indice > maxIndice) maxIndice = t.Indice;

        tilesPorIndice = new Tile[maxIndice + 1];
        foreach (var t in todas)
            if (t.Indice >= 0 && t.Indice < tilesPorIndice.Length)
                tilesPorIndice[t.Indice] = t;

        CalcularOrigenGrid(todas);
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
        base.OnNetworkDespawn();
        idsColores.OnListChanged -= AlCambiarColor;
    }

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

    // ✅ Simplificado: Solo busca el punto más bajo en X y Z para tener una referencia
    private void CalcularOrigenGrid(Tile[] tiles)
    {
        if (tiles.Length == 0) return;

        origenGrid = tiles[0].transform.position;

        foreach (var t in tiles)
        {
            Vector3 p = t.transform.position;
            if (p.x < origenGrid.x) origenGrid.x = p.x;
            if (p.z < origenGrid.z) origenGrid.z = p.z;
        }

        boundsCalculados = true;
    }

    private void ConstruirDiccionarioCeldas(Tile[] tiles)
    {
        tilesPorCelda.Clear();

        foreach (var t in tiles)
        {
            Vector2Int celda = PosicionACelda(t.transform.position);
            if (!tilesPorCelda.ContainsKey(celda))
                tilesPorCelda[celda] = t;
        }
    }

    private Vector2Int PosicionACelda(Vector3 pos)
    {
        float relX = pos.x - origenGrid.x;
        float relZ = pos.z - origenGrid.z;

        int x = Mathf.RoundToInt(relX / TamBaldosa);
        int z = Mathf.RoundToInt(relZ / TamBaldosa);

        return new Vector2Int(x, z);
    }

    public int ObtenerIndiceEnPosicion(Vector3 pos)
    {
        if (!boundsCalculados) return -1;

        Vector2Int celda = PosicionACelda(pos);

        if (tilesPorCelda.TryGetValue(celda, out Tile tile))
            return tile.Indice;

        return -1;
    }

   

    private void AlCambiarColor(NetworkListEvent<byte> evento)
    {
        if (evento.Type != NetworkListEvent<byte>.EventType.Value) return;
        if (tilesPorIndice == null) return;
        if (evento.Index < 0 || evento.Index >= tilesPorIndice.Length) return;

        Tile tile = tilesPorIndice[evento.Index];
        if (tile == null) return;

        if (evento.Value == 0)
            tile.RestaurarColorOriginal();
        else
            tile.EstablecerColor(PlayerPalette.Obtener(evento.Value));
    }

    public void EstablecerColorBaldosa(int indice, byte idColor)
    {
        if (!IsServer) return;
        if (indice < 0 || indice >= idsColores.Count) return;

        byte colorAnterior = idsColores[indice];
        if (colorAnterior == idColor) return;

        idsColores[indice] = idColor;

        if (GameManager.Instance != null)
            GameManager.Instance.ActualizarContador(colorAnterior, idColor);
    }

    public List<int> ObtenerIndicesDeColor(byte colorId)
    {
        List<int> indices = new List<int>();
        if (!IsServer || idsColores == null) return indices;

        for (int i = 0; i < idsColores.Count; i++)
        {
            if (idsColores[i] == colorId)
                indices.Add(i);
        }
        return indices;
    }

    public Vector3 ObtenerPosicionRandomParaItem()
    {
        if (tilesPorCelda.Count == 0) return Vector3.zero;

        var celdas = new List<Vector2Int>(tilesPorCelda.Keys);
        Vector2Int celda = celdas[Random.Range(0, celdas.Count)];
        Tile tile = tilesPorCelda[celda];

        return tile.transform.position + Vector3.up * 0.5f;
    }

    public void ResetearTodasLasTiles()
    {
        if (!IsServer || idsColores == null) return;

        for (int i = 0; i < idsColores.Count; i++)
            idsColores[i] = 0;
    }
}