using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    /*
    o Gestionar la conexión de los jugadores: implementar métodos para la
    conexión y desconexión de los jugadores.

    o La asignación de equipos: asignar los jugadores al conectarse a los
    personajes de los equipos humano o zombi.

    o La sincronización de los estados del juego: garantizar que las posiciones y
    estados de los jugadores se sincronicen entre todos los clientes.

    o Sincronización de eventos del juego: recolección de monedas, conversión
    de humano a zombi y condiciones de fin de juego.
    */

    // Start is called before the first frame update
    public static GameManager Instance;

    void Start()
    {
        if (Instance == null)
        {
            Instance = new GameManager();
        }
        else
        {
            Destroy(this);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
