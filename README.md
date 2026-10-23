Proyecto de Desarrollo en Unity para la materia de Desarrollo de Videojuegos Multijugador-2026-UPSO
STEAL ISLAND

El proyecto consiste en un juego multiplayer competitivo de arena y control de territorio, donde los jugadores compiten por conquistar la mayor cantidad de baldosas dentro de un tiempo determinado. Cada jugador tiene un color diferente y, al desplazarse por la arena, puede conquistar las baldosas que pisa.

El proyecto combina una mecánica sencilla de movimiento y conquista con elementos de riesgo como bombas e ítems, utilizando Unity Netcode for GameObjects y Unity Relay para permitir que varios jugadores compartan la misma partida en tiempo real, desarrollado  en la versión 6000.6.2f1.

La implementación se enfocara no solamente en conseguir que el juego funcione, sino también en utilizar correctamente las principales herramientas de networking solicitadas: NetworkObject, NetworkTransform, NetworkVariable, NetworkList y RPC.

Etapa 1-Prototipo Jugable-1er PARCIAL

Al finalizar la partida:

- Se cuentan las baldosas de cada jugador.
- Se comparan las puntuaciones.
- Se muestra quién ganó.

¿Cómo jugar?

Jugador 1 — Host

Selecciona CREAR PARTIDA.
Se genera un código de conexión.
Comparte el código con el otro jugador.
Queda esperando al segundo jugador.
Cuando aparezcan ambos jugadores en pantalla, INICIAR PARTIDA

Jugador 2 — Cliente

Ingresa el código recibido.
Selecciona UNIRSE A LA PARTIDA.
Se conecta a la partida del Host.

El movimiento del personaje se realiza con las teclas generales de WASD y al terminar la partida el jugador se puede retirar presionando la tecla esc.

Mecánicas principales

Pintar baldosas: El jugador se desplaza por la grilla y va pintando las baldosas que pisa.
La información sobre las baldosas pintadas se sincroniza entre los jugadores para que todos puedan ver el mismo estado de la arena.

Ítem cajita: El item implementado es una caja que al tocarla roba 5 colores del contrincante, la cual aparece cada 10 segundos de haber sido usada. (único por ahora)

Tiempo: La partida tiene una duración limitada para evitar que las partidas sean demasiado largas.
Al terminar el tiempo, el juego pasa al estado de finalización y se determina el ganador. Actualmente se encuentra en 30 seg ya que quisimos hacer corta la demostración de las partidas.



Multiplayer
El juego utiliza Unity Netcode for GameObjects para implementar la comunicación entre los jugadores. Los jugadores pueden conectarse desde diferentes máquinas mediante Internet utilizando Unity Relay.
La arquitectura permite que todos los jugadores compartan el mismo estado de la partida.

Conexión 
La conexión multiplayer se realiza mediante Unity Relay, permitiendo que los jugadores se conecten desde diferentes máquinas a través de Internet.


Una vez iniciada la partida, Unity Netcode sincroniza el movimiento, las baldosas, los ítems y las diferentes acciones de los jugadores.
Esto permite probar el juego desde diferentes máquinas conectadas por Internet sin tener que configurar manualmente las conexiones directas entre jugadores.

Implementación de Networking
Para cumplir con los requisitos del trabajo práctico se implementaron diferentes elementos de Unity Netcode.

NetworkObject: Se utiliza para convertir determinados objetos en objetos administrados por la red.
El GameManager, por ejemplo, funciona como un objeto de red principal para controlar el estado de la partida.
Los jugadores también utilizan `NetworkObject` para poder existir y ser identificados dentro de la partida multiplayer.

NetworkTransform: Se utiliza para sincronizar la posición y movimiento de los jugadores.
De esta manera, cuando un jugador se mueve en una máquina, los demás jugadores pueden ver su movimiento.

NetworkVariable: lo utilizamos en el estado del juego puede pasar por diferentes estados:

SalaEspera
EnJuego
Finalizado

También se utiliza información sincronizada para mantener el estado de las baldosas.

RPC Lo utilizamos en el sistema de suelo. El escenario está formado por una grilla de baldosas. Las baldosas se generan a partir de un prefab y forman la arena donde se desarrolla la partida.

Una decisión importante de implementación fue no convertir cada baldosa en un NetworkObject.

En lugar de eso:

- El suelo se genera localmente en cada máquina.
- El GameManager controla la información general.
- El estado de las baldosas se sincroniza mediante netcode.
- Cada cliente actualiza visualmente las baldosas según la información recibida.
Esto evita tener cientos deNetworkObject funcionando simultáneamente y mejora el rendimiento de la partida multiplayer.


El GameManager es uno de los componentes principales del proyecto.
Se encarga de controlar aspectos generales como:

- Estado de la partida.
- Inicio y finalización.
- Cantidad de jugadores.
- Tamaño de la grilla.
- Información relacionada con las baldosas.
- Sincronización de determinados datos.
- Control del flujo de la partida.


SueloController
El SueloController se ocupa de la lógica relacionada con el suelo y las baldosas.
Su función principal es trabajar con la grilla generada y actualizar visualmente las baldosas según la información recibida.
El suelo se genera a partir de un prefab de baldosa, permitiendo crear una arena de diferentes tamaños dependiendo de la cantidad de jugadores.


ItemSpawner
El ItemSpawne controla la aparición de los ítems dentro de la arena.
Entre sus responsabilidades se encuentran:

- Generar ítems.
- Controlar el tiempo entre apariciones.
- Controlar cuánto tiempo permanece un ítem.
- Evitar que los ítems aparezcan fuera de los límites del escenario.

El sistema se ejecuta teniendo en cuenta el funcionamiento de la partida multiplayer.


Interfaz
El juego cuenta con una interfaz básica para mostrar información importante durante la partida.
Entre ella:

- Estado de conexión.
- Estado de la partida.
- Tiempo restante.
- Puntuación/baldosas conquistadas.
- Información del ganador al finalizar.

También se implementó un sistema de lobby/sala de espera antes de comenzar la partida.

A medida que avance el desarrollo, se irán perfeccionando y ampliando los sistemas de conexión y las mecánicas del juego. También se incorporarán nuevos ítems, obstáculos y elementos interactivos, con el objetivo de lograr progresivamente una experiencia más completa, estable y acorde a los contenidos y objetivos de la materia.
