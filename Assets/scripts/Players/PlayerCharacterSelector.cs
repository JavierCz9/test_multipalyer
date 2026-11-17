
using Unity.Netcode;
using UnityEngine;

public class PlayerCharacterSelector : NetworkBehaviour
{
    [Header("Personajes en orden: Esqueleto, Pirata, Tiburon, Pulpito")]
    [SerializeField] private GameObject[] personajes;

    [Header("Control visual")]
    [SerializeField] private PlayerVisual playerVisual;

    public NetworkVariable<int> personajeId = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        personajeId.OnValueChanged += AlCambiarPersonaje;

        // Usar la selección confirmada en el lobby.
        if (IsServer)
        {
            ConnectionManager cm = ConnectionManager.Instance;

            if (cm != null &&
                cm.TryObtenerPersonajeJugador(OwnerClientId, out int elegido))
            {
                personajeId.Value = elegido;
            }
            else
            {
                Debug.LogError(
                    $"[PERSONAJE] No hay seleccion confirmada para cliente {OwnerClientId}. " +
                    "Revisar que ConnectionManager persista al cargar GameScene."
                );
            }
        }

        AlCambiarPersonaje(-1, personajeId.Value);
    }

    public override void OnNetworkDespawn()
    {
        personajeId.OnValueChanged -= AlCambiarPersonaje;
    }

    private void AlCambiarPersonaje(int anterior, int nuevo)
    {
        if (personajes == null) return;

        for (int i = 0; i < personajes.Length; i++)
        {
            if (personajes[i] != null)
                personajes[i].SetActive(i == nuevo);
        }

        if (nuevo >= 0 &&
            nuevo < personajes.Length &&
            personajes[nuevo] != null &&
            playerVisual != null)
        {
            // Busca el Animator también dentro de los hijos.
            Animator anim = personajes[nuevo]
                .GetComponentInChildren<Animator>(true);

            playerVisual.ConfigurarAnimator(anim);

            if (anim == null)
            {
                Debug.LogWarning(
                    $"[PERSONAJE] No se encontró Animator en {personajes[nuevo].name}"
                );
            }
        }
    }
}
