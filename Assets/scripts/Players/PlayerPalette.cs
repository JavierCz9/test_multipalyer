using UnityEngine;

/// <summary>
/// Paleta de colores: mapea ID de jugador → color.
/// Debe ser idéntica en el servidor y en todos los clientes.
/// El ID 0 está reservado para "baldosa sin pisar" (blanco).
/// </summary>
public static class PlayerPalette
{
    // El índice ES el ID del jugador.
    // 0 = blanco (sin pisar), 1..8 = los ocho jugadores posibles.
    public static readonly Color32[] Colores = new Color32[]
    {
        new Color32(255, 255, 255, 255), // 0 = blanco
        new Color32(255, 60,  60,  255), // 1 = rojo
        new Color32(60,  120, 255, 255), // 2 = azul
        new Color32(60,  255, 80,  255), // 3 = verde
        new Color32(255, 200, 0,   255), // 4 = amarillo
        new Color32(200, 80,  255, 255), // 5 = violeta
        new Color32(255, 140, 40,  255), // 6 = naranja
        new Color32(0,   230, 230, 255), // 7 = cian
        new Color32(255, 100, 180, 255), // 8 = rosa
    };

    /// <summary>
    /// Devuelve el color asociado a un ID. Si está fuera de rango,
    /// devuelve blanco (índice 0) como red de seguridad.
    /// </summary>
    public static Color32 Obtener(byte id)
    {
        return id < Colores.Length ? Colores[id] : Colores[0];
    }

    /// <summary>
    /// Cuántos jugadores soporta la paleta (sin contar el ID 0).
    /// </summary>
    public static int CantidadJugadores => Colores.Length - 1;
}