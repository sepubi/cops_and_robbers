using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{
    // =========================================================
    // REFERENCIAS A LOS GAMEOBJECTS DE LA ESCENA
    // =========================================================
    public GameObject board;             // El tablero (contiene las 64 casillas como hijos)
    public GameObject[] cops = new GameObject[2]; // Las dos fichas de policía
    public GameObject robber;            // La ficha del ladrón
    public Text rounds;                  // Texto de la UI que muestra el número de rondas
    public Text finalMessage;            // Texto de la UI que muestra "You Win!" / "You Lose!"
    public Button playAgainButton;       // Botón para reiniciar la partida

    // =========================================================
    // VARIABLES INTERNAS DEL JUEGO
    // =========================================================
    Tile[] tiles = new Tile[Constants.NumTiles]; // Array con las 64 casillas del tablero
    private int roundCount = 0;   // Contador de rondas jugadas
    private int state;            // Estado actual del juego (ver diagrama de estados)
    private int clickedTile = -1; // Índice de la última casilla pulsada
    private int clickedCop  = 0;  // Índice del último policía pulsado (0 ó 1)


    // =========================================================
    // START — Se ejecuta una sola vez al arrancar el juego
    // =========================================================
    void Start()
    {
        InitTiles();           // Rellenamos el array "tiles" con los objetos Tile de la escena
        InitAdjacencyLists();  // Construimos el grafo (listas de adyacencia de cada casilla)
        state = Constants.Init; // El juego empieza en el estado inicial
    }


    // =========================================================
    // INITTILES — Rellena el array "tiles" recorriendo la jerarquía
    //             del tablero en la escena de Unity.
    //             También fija las posiciones iniciales de las fichas.
    // =========================================================
    void InitTiles()
    {
        // El tablero tiene 8 filas; cada fila tiene 8 casillas hijas
        for (int fil = 0; fil < Constants.TilesPerRow; fil++)
        {
            GameObject rowchild = board.transform.GetChild(fil).gameObject;
            for (int col = 0; col < Constants.TilesPerRow; col++)
            {
                GameObject tilechild = rowchild.transform.GetChild(col).gameObject;
                // Guardamos cada Tile en la posición fila*8 + columna
                tiles[fil * Constants.TilesPerRow + col] = tilechild.GetComponent<Tile>();
            }
        }

        // Asignamos las casillas iniciales a cada ficha
        cops[0].GetComponent<CopMove>().currentTile  = Constants.InitialCop0;   // Policía 0 → casilla 7
        cops[1].GetComponent<CopMove>().currentTile  = Constants.InitialCop1;   // Policía 1 → casilla 0
        robber.GetComponent<RobberMove>().currentTile = Constants.InitialRobber; // Ladrón   → casilla 60
    }


    // =========================================================
    // INITADJACENCYLISTS — Construye el grafo del tablero.
    //
    //   Cada casilla es un vértice. Dos vértices están conectados
    //   si se puede pasar de uno al otro en un solo movimiento
    //   ortogonal (arriba, abajo, izquierda, derecha).
    //
    //   Usamos una matriz de adyacencia 64×64 como paso intermedio,
    //   y luego volcamos cada fila a la lista "adjacency" del Tile.
    // =========================================================
    public void InitAdjacencyLists()
    {
        // --- Paso 1: Crear la matriz de adyacencia 64×64 inicializada a 0 ---
        int[,] matriu = new int[Constants.NumTiles, Constants.NumTiles];

        // --- Paso 2: Marcar con 1 las conexiones válidas ---
        for (int i = 0; i < Constants.NumTiles; i++)
        {
            int fila    = i / Constants.TilesPerRow; // Fila de la casilla i (0–7)
            int columna = i % Constants.TilesPerRow; // Columna de la casilla i (0–7)

            // Movimiento ARRIBA: la casilla de encima es i+8 (si no estamos en la fila más alta)
            if (fila < 7) matriu[i, i + 8] = 1;

            // Movimiento ABAJO: la casilla de abajo es i-8 (si no estamos en la fila más baja)
            if (fila > 0) matriu[i, i - 8] = 1;

            // Movimiento IZQUIERDA: la casilla de la izquierda es i-1 (si no estamos en la columna 0)
            if (columna > 0) matriu[i, i - 1] = 1;

            // Movimiento DERECHA: la casilla de la derecha es i+1 (si no estamos en la columna 7)
            if (columna < 7) matriu[i, i + 1] = 1;
        }

        // --- Paso 3: Volcar la matriz a la lista "adjacency" de cada Tile ---
        // (Hacemos esto en un bucle separado para no mezclar escritura y lectura)
        for (int i = 0; i < Constants.NumTiles; i++)
        {
            tiles[i].adjacency.Clear(); // Limpiamos por si se llama más de una vez
            for (int j = 0; j < Constants.NumTiles; j++)
            {
                if (matriu[i, j] == 1)
                    tiles[i].adjacency.Add(j); // Añadimos el índice del vecino
            }
        }
    }


    // =========================================================
    // RESETTILES — Restaura el estado visual y las variables BFS
    //              de todas las casillas (visited, parent, distance,
    //              selectable, current).
    // =========================================================
    public void ResetTiles()
    {
        foreach (Tile tile in tiles)
            tile.Reset();
    }


    // =========================================================
    // CLICKONCOP — Transición del diagrama de estados:
    //              Init / CopSelected → CopSelected
    //
    //   El jugador ha hecho clic en un policía. Guardamos cuál es,
    //   calculamos sus casillas alcanzables y las resaltamos en rojo.
    // =========================================================
    public void ClickOnCop(int cop_id)
    {
        switch (state)
        {
            case Constants.Init:
            case Constants.CopSelected: // Permite cambiar de policía si aún no se ha movido
                clickedCop  = cop_id;
                clickedTile = cops[cop_id].GetComponent<CopMove>().currentTile;

                ResetTiles();
                tiles[clickedTile].current = true; // Resaltamos la casilla actual del policía

                FindSelectableTiles(true); // Calculamos a dónde puede moverse (BFS)
                state = Constants.CopSelected;
                break;
        }
    }


    // =========================================================
    // CLICKONTILE — Transición del diagrama de estados:
    //              CopSelected → TileSelected  (si la casilla es alcanzable)
    //              TileSelected / RobberTurn   → Init  (clic fuera de turno)
    //
    //   El jugador ha hecho clic en una casilla. Si es alcanzable,
    //   ordenamos al policía que se mueva a ella.
    // =========================================================
    public void ClickOnTile(int t)
    {
        clickedTile = t;
        switch (state)
        {
            case Constants.CopSelected:
                if (tiles[clickedTile].selectable) // Solo actuamos si la casilla está resaltada
                {
                    // Ordenamos al policía que se mueva (animación)
                    cops[clickedCop].GetComponent<CopMove>().MoveToTile(tiles[clickedTile]);
                    // Actualizamos su casilla lógica de inmediato
                    cops[clickedCop].GetComponent<CopMove>().currentTile = tiles[clickedTile].numTile;
                    tiles[clickedTile].current = true;
                    state = Constants.TileSelected;
                }
                break;

            // Si se hace clic durante la animación, volvemos al estado inicial
            case Constants.TileSelected:
            case Constants.RobberTurn:
                state = Constants.Init;
                break;
        }
    }


    // =========================================================
    // FINISHTURN — Se llama automáticamente cuando una ficha
    //              (policía o ladrón) termina su animación de movimiento.
    //
    //   TileSelected → RobberTurn : el policía ha llegado, toca al ladrón
    //   RobberTurn   → Init       : el ladrón ha llegado, nueva ronda
    //                → End        : si se agotaron los turnos, el ladrón gana
    // =========================================================
    public void FinishTurn()
    {
        switch (state)
        {
            case Constants.TileSelected:
                ResetTiles();
                state = Constants.RobberTurn;
                RobberTurn(); // Disparamos el turno del ladrón
                break;

            case Constants.RobberTurn:
                ResetTiles();
                IncreaseRoundCount(); // Sumamos una ronda completa (policía + ladrón)
                if (roundCount <= Constants.MaxRounds)
                    state = Constants.Init; // Seguimos jugando
                else
                    EndGame(false); // Se agotaron los turnos → gana el ladrón
                break;
        }
    }


    // =========================================================
    // ROBBERTURN — Lógica de movimiento del ladrón (IA).
    //
    //   VERSIÓN INTELIGENTE (parte opcional):
    //   En lugar de moverse a una casilla aleatoria, el ladrón
    //   evalúa todas sus casillas alcanzables y elige la que
    //   maximiza su distancia mínima a cualquiera de los dos policías.
    //
    //   Estrategia:
    //     Para cada casilla candidata:
    //       - Calcula distancia real en el grafo hasta policía 0 (d0)
    //       - Calcula distancia real en el grafo hasta policía 1 (d1)
    //       - safeScore = min(d0, d1)  → el "peligro" del vecino más cercano
    //     Elige la candidata con mayor safeScore.
    // =========================================================
    public void RobberTurn()
    {
        // Calculamos las casillas alcanzables desde la posición actual del ladrón
        FindSelectableTiles(false);

        // Recogemos todas las casillas marcadas como seleccionables
        List<Tile> selectableTiles = new List<Tile>();
        foreach (Tile t in tiles)
            if (t.selectable) selectableTiles.Add(t);

        // Si no hay casillas disponibles (caso extremo), no hacemos nada
        if (selectableTiles.Count == 0) return;

        // Posición actual de los dos policías (para calcular distancias)
        int posCop0 = cops[0].GetComponent<CopMove>().currentTile;
        int posCop1 = cops[1].GetComponent<CopMove>().currentTile;

        // Buscamos la casilla con mayor distancia mínima a los policías
        Tile bestTile    = selectableTiles[0]; // Candidata ganadora (se irá actualizando)
        int  maxDistance = -1;                 // El mejor safeScore encontrado hasta ahora

        foreach (Tile candidate in selectableTiles)
        {
            int d0 = GetGraphDistance(candidate.numTile, posCop0); // Distancia al policía 0
            int d1 = GetGraphDistance(candidate.numTile, posCop1); // Distancia al policía 1
            int safeScore = Mathf.Min(d0, d1); // Nos importa el más cercano (el más peligroso)

            if (safeScore > maxDistance) // ¿Esta casilla es más segura que la mejor hasta ahora?
            {
                maxDistance = safeScore;
                bestTile    = candidate;
            }
        }

        // Movemos el ladrón a la casilla más segura
        robber.GetComponent<RobberMove>().MoveToTile(bestTile);
        robber.GetComponent<RobberMove>().currentTile = bestTile.numTile;
    }


    // =========================================================
    // GETGRAPHDISTANCE — BFS sin límite de distancia.
    //
    //   Calcula el número mínimo de movimientos ortogonales
    //   necesarios para ir de la casilla "start" a la casilla "end"
    //   dentro del grafo del tablero.
    //
    //   Se usa en RobberTurn para que el ladrón tome decisiones
    //   basadas en distancias reales (no en distancia Manhattan).
    // =========================================================
    private int GetGraphDistance(int start, int end)
    {
        Queue<int> q = new Queue<int>();
        int[] dists  = new int[Constants.NumTiles];

        // Inicializamos todas las distancias a -1 (no visitado)
        for (int i = 0; i < Constants.NumTiles; i++) dists[i] = -1;

        // Arrancamos el BFS desde "start"
        q.Enqueue(start);
        dists[start] = 0;

        while (q.Count > 0)
        {
            int curr = q.Dequeue();

            // Si hemos llegado al destino, devolvemos la distancia
            if (curr == end) return dists[curr];

            // Exploramos los vecinos no visitados
            foreach (int neighbor in tiles[curr].adjacency)
            {
                if (dists[neighbor] == -1)
                {
                    dists[neighbor] = dists[curr] + 1;
                    q.Enqueue(neighbor);
                }
            }
        }

        return 99; // No debería ocurrir en un grafo conexo como este tablero
    }


    // =========================================================
    // ENDGAME — Muestra el mensaje final y activa el botón de reinicio.
    //           Se llama tanto si el ladrón es capturado (end=true)
    //           como si se agotan los turnos (end=false).
    // =========================================================
    public void EndGame(bool end)
    {
        if (end) finalMessage.text = "You Win!";  // Los policías atraparon al ladrón
        else     finalMessage.text = "You Lose!"; // Se acabaron los turnos
        playAgainButton.interactable = true;
        state = Constants.End;
    }


    // =========================================================
    // PLAYAGAIN — Reinicia la partida: devuelve las fichas a sus
    //             posiciones iniciales y resetea todos los contadores.
    // =========================================================
    public void PlayAgain()
    {
        cops[0].GetComponent<CopMove>().Restart(tiles[Constants.InitialCop0]);
        cops[1].GetComponent<CopMove>().Restart(tiles[Constants.InitialCop1]);
        robber.GetComponent<RobberMove>().Restart(tiles[Constants.InitialRobber]);
        ResetTiles();
        playAgainButton.interactable = false;
        finalMessage.text = "";
        roundCount    = 0;
        rounds.text   = "Rounds: 0";
        state = Constants.Restarting; // Estado especial mientras las fichas se recolocan
    }


    // =========================================================
    // INITGAME — Pone el estado a Init una vez que las fichas han
    //            terminado de animarse al reiniciar.
    //            Lo llama RobberMove.Move() cuando restarting == true.
    // =========================================================
    public void InitGame() { state = Constants.Init; }


    // =========================================================
    // INCREASEROUNDCOUNT — Suma una ronda y actualiza la UI.
    // =========================================================
    public void IncreaseRoundCount()
    {
        roundCount++;
        rounds.text = "Rounds: " + roundCount;
    }


    // =========================================================
    // FINDSELECTABLETILES — BFS limitado a distancia ≤ 2.
    //
    //   Calcula las casillas a las que puede moverse una ficha
    //   en este turno (hasta 2 movimientos encadenados).
    //
    //   Parámetro "cop":
    //     true  → calculamos para el policía seleccionado
    //     false → calculamos para el ladrón
    //
    //   Restricciones especiales para los policías:
    //     · La ficha no puede quedarse en su casilla actual
    //       (→ al final: tiles[origen].selectable = false)
    //     · Un policía no puede pasar por la casilla del otro policía
    //       (→ bloqueamos esa casilla durante la expansión del BFS)
    // =========================================================
    public void FindSelectableTiles(bool cop)
    {
        // Determinamos la casilla de origen según si es un policía o el ladrón
        int indexcurrentTile = cop
            ? cops[clickedCop].GetComponent<CopMove>().currentTile
            : robber.GetComponent<RobberMove>().currentTile;

        ResetTiles(); // Limpiamos el estado de todas las casillas antes del BFS
        tiles[indexcurrentTile].current = true; // Marcamos la casilla de origen

        // Si es un policía, guardamos la casilla del otro para bloquearla
        int otherCopTile = -1;
        if (cop)
        {
            int otherCopId = (clickedCop == 0) ? 1 : 0;
            otherCopTile = cops[otherCopId].GetComponent<CopMove>().currentTile;
        }

        // --- BFS desde la casilla de origen ---
        Queue<Tile> nodes = new Queue<Tile>();

        // Inicializamos el nodo de origen
        tiles[indexcurrentTile].visited  = true;
        tiles[indexcurrentTile].distance = 0;
        nodes.Enqueue(tiles[indexcurrentTile]);

        while (nodes.Count > 0)
        {
            Tile t = nodes.Dequeue();

            // Solo expandimos si aún podemos dar más pasos (distancia < 2)
            if (t.distance < Constants.Distance)
            {
                foreach (int neighborIndex in t.adjacency)
                {
                    Tile neighbor = tiles[neighborIndex];

                    // Saltamos vecinos ya visitados o la casilla bloqueada del otro policía
                    if (!neighbor.visited && neighborIndex != otherCopTile)
                    {
                        neighbor.visited    = true;
                        neighbor.parent     = t;               // Guardamos el padre para reconstruir el camino
                        neighbor.distance   = t.distance + 1;
                        neighbor.selectable = true;            // Esta casilla es alcanzable → se pinta en rojo
                        nodes.Enqueue(neighbor);
                    }
                }
            }
        }

        // La ficha no puede quedarse en su casilla actual → la desmarcamos
        tiles[indexcurrentTile].selectable = false;
    }
}