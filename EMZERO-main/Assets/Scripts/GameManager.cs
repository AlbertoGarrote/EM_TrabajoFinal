using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    /*
    o Gestionar la conexi�n de los jugadores: implementar m�todos para la
    conexi�n y desconexi�n de los jugadores.

    o La asignaci�n de equipos: asignar los jugadores al conectarse a los
    personajes de los equipos humano o zombi.

    o La sincronizaci�n de los estados del juego: garantizar que las posiciones y
    estados de los jugadores se sincronicen entre todos los clientes.

    o Sincronizaci�n de eventos del juego: recolecci�n de monedas, conversi�n
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
