using UnityEngine;
using Unity.Netcode;

public class PersistentNetworkManager : MonoBehaviour
{
    private void Awake()
    {
        // 
        if (FindObjectsByType<NetworkManager>(FindObjectsInactive.Exclude).Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        // Hacemos que este objeto sobreviva al cambio de escena
        DontDestroyOnLoad(gameObject);
    }
}