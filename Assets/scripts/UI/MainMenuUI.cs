using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    /// <summary>
    /// Carga la escena del Lobby. 
    /// El NetworkManager se inicializará cuando entremos a esa escena.
    /// </summary>
    public void OnClickJugar()
    {
        Debug.Log("[MENU] Cargando LobbyScene...");
        SceneManager.LoadScene("LobbyScene");
    }

    /// <summary>
    /// Cierra el juego. 
    /// (En el Editor de Unity, este botón no hace nada, solo funciona en la build final).
    /// </summary>
    public void OnClickSalir()
    {
        Debug.Log("[MENU] Saliendo del juego...");
        Application.Quit();
    }
}