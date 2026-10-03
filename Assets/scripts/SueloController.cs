using Unity.Netcode;
using UnityEngine;

public class SueloController : NetworkBehaviour
{
    public static SueloController Instance;

    private NetworkList<byte> idsColores;
    private NetworkList<ulong> dueñosBaldosas;

    public bool BoundsListos => boundsCalculados;

    private Tile[] tilesPorIndice;

    private Vector3 minBounds;
    private Vector3 maxBounds;
    private bool boundsCalculados = false;

    private void Awake()
    {
        Instance = this;

        idsColores = new NetworkList<byte>();
        dueñosBaldosas = new NetworkList<ulong>();
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

        if (IsServer)
        {
            idsColores.Clear();
            dueñosBaldosas.Clear();

            for (int i = 0; i < tilesPorIndice.Length; i++)
            {
                idsColores.Add(0);
                dueñosBaldosas.Add(ulong.MaxValue);
            }
        }

        idsColores.OnListChanged += AlCambiarColor;

        Debug.Log($"[Suelo] {todas.Length} tiles encontradas.");
    }

    public override void OnNetworkDespawn()
    {
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

            if (cmp != 0)
                return cmp;

            return az.CompareTo(bz);
        });

        for (int i = 0; i < tiles.Length; i++)
            tiles[i].Indice = i;
    }

    private void CalcularBounds(Tile[] tiles)
    {
        if (tiles.Length == 0)
            return;

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

    public Vector3 ObtenerPosicionSpawn(int indice)
    {
        if (!boundsCalculados)
        {
            float xFallback = (indice - 1.5f) * 1.5f;
            return new Vector3(xFallback, 1.05f, -3f);
        }

        float margen = 1.5f;

        float minX = minBounds.x + margen;
        float maxX = maxBounds.x - margen;

        float minZ = minBounds.z + margen;
        float maxZ = maxBounds.z - margen;

        float y = 1.05f;

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
            new Vector3((minX + maxX) * 0.5f, y, minZ),
            new Vector3((minX + maxX) * 0.5f, y, maxZ),
            new Vector3(minX, y, (minZ + maxZ) * 0.5f),
            new Vector3(maxX, y, (minZ + maxZ) * 0.5f)
        };

        return medios[(indice - 4) % 4];
    }

    private void AlCambiarColor(NetworkListEvent<byte> evento)
    {
        if (evento.Type != NetworkListEvent<byte>.EventType.Value)
            return;

        if (tilesPorIndice == null)
            return;

        if (evento.Index < 0 || evento.Index >= tilesPorIndice.Length)
            return;

        Tile tile = tilesPorIndice[evento.Index];

        if (tile != null)
            tile.EstablecerColor(PlayerPalette.Obtener(evento.Value));
    }

    public void EstablecerColorBaldosa(
        int indice,
        byte idColor,
        ulong clientId)
    {
        if (!IsServer)
            return;

        if (indice < 0 || indice >= idsColores.Count)
            return;

        ulong dueñoAnterior = dueñosBaldosas[indice];

        // Si ya pertenece a este jugador, no hacemos nada.
        if (dueñoAnterior == clientId)
            return;

        // Si pertenecía a otro jugador,
        // le quitamos una baldosa.
        if (dueñoAnterior != ulong.MaxValue)
        {
            GameManager.Instance.RestarBaldosa(dueñoAnterior);
        }

        // El nuevo jugador gana una baldosa.
        GameManager.Instance.SumarBaldosa(clientId);

        // Guardamos nuevo dueño y color.
        dueñosBaldosas[indice] = clientId;
        idsColores[indice] = idColor;
    }
}