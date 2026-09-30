using Unity.Netcode.Components;
using UnityEngine;

// Hereda de NetworkTransform para sobreescribir su autoridad.
// Fuerza que la autoridad sea siempre el propietario (el cliente).
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        // Al devolver false, el cliente (owner) es quien tiene la autoridad.
        return false;
    }
}
