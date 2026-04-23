using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Controller : MonoBehaviour
{
    //GameObjects
    public GameObject board;
    public GameObject[] cops = new GameObject[2];
    public GameObject robber;
    public Text rounds;
    public Text finalMessage;
    public Button playAgainButton;

    //Otras variables
    Tile[] tiles = new Tile[Constants.NumTiles];
    private int roundCount = 0;
    private int state;
    private int clickedTile = -1;
    private int clickedCop = 0;

    void Start()
    {
        InitTiles();
        InitAdjacencyLists();
        state = Constants.Init;
    }

    void InitTiles()
    {
        for (int fil = 0; fil < Constants.TilesPerRow; fil++)
        {
            GameObject rowchild = board.transform.GetChild(fil).gameObject;
            for (int col = 0; col < Constants.TilesPerRow; col++)
            {
                GameObject tilechild = rowchild.transform.GetChild(col).gameObject;
                tiles[fil * Constants.TilesPerRow + col] = tilechild.GetComponent<Tile>();
            }
        }

        cops[0].GetComponent<CopMove>().currentTile = Constants.InitialCop0;
        cops[1].GetComponent<CopMove>().currentTile = Constants.InitialCop1;
        robber.GetComponent<RobberMove>().currentTile = Constants.InitialRobber;
    }

    public void InitAdjacencyLists()
    {
        // 1. Inicializar matriz a 0's
        int[,] matriu = new int[Constants.NumTiles, Constants.NumTiles];

        // 2. Rellenar matriz con adyacencias
        for (int i = 0; i < Constants.NumTiles; i++)
        {
            int fila = i / Constants.TilesPerRow;
            int columna = i % Constants.TilesPerRow;

            if (fila < 7) matriu[i, i + 8] = 1; // Arriba
            if (fila > 0) matriu[i, i - 8] = 1; // Abajo
            if (columna > 0) matriu[i, i - 1] = 1; // Izquierda
            if (columna < 7) matriu[i, i + 1] = 1; // Derecha
        }

        // 3. Rellenar la lista "adjacency" de cada Tile (Fuera del bucle anterior para evitar duplicados)
        for (int i = 0; i < Constants.NumTiles; i++)
        {
            tiles[i].adjacency.Clear(); // Limpiamos por seguridad
            for (int j = 0; j < Constants.NumTiles; j++)
            {
                if (matriu[i, j] == 1)
                {
                    tiles[i].adjacency.Add(j);
                }
            }
        }
    }

    public void ResetTiles()
    {
        foreach (Tile tile in tiles)
        {
            tile.Reset();
        }
    }

    public void ClickOnCop(int cop_id)
    {
        switch (state)
        {
            case Constants.Init:
            case Constants.CopSelected:
                clickedCop = cop_id;
                clickedTile = cops[cop_id].GetComponent<CopMove>().currentTile;
                ResetTiles();
                tiles[clickedTile].current = true;
                FindSelectableTiles(true);
                state = Constants.CopSelected;
                break;
        }
    }

    public void ClickOnTile(int t)
    {
        clickedTile = t;
        switch (state)
        {
            case Constants.CopSelected:
                if (tiles[clickedTile].selectable)
                {
                    cops[clickedCop].GetComponent<CopMove>().MoveToTile(tiles[clickedTile]);
                    cops[clickedCop].GetComponent<CopMove>().currentTile = tiles[clickedTile].numTile;
                    tiles[clickedTile].current = true;
                    state = Constants.TileSelected;
                }
                break;
            case Constants.TileSelected:
            case Constants.RobberTurn:
                state = Constants.Init;
                break;
        }
    }

    public void FinishTurn()
    {
        switch (state)
        {
            case Constants.TileSelected:
                ResetTiles();
                state = Constants.RobberTurn;
                RobberTurn();
                break;
            case Constants.RobberTurn:
                ResetTiles();
                IncreaseRoundCount();
                if (roundCount <= Constants.MaxRounds)
                    state = Constants.Init;
                else
                    EndGame(false);
                break;
        }
    }

    public void RobberTurn()
    {
        FindSelectableTiles(false);
        List<Tile> selectableTiles = new List<Tile>();
        foreach (Tile t in tiles) if (t.selectable) selectableTiles.Add(t);

        if (selectableTiles.Count == 0) return;

        Tile bestTile = selectableTiles[0];
        int maxDistance = -1;

        int posCop0 = cops[0].GetComponent<CopMove>().currentTile;
        int posCop1 = cops[1].GetComponent<CopMove>().currentTile;

        foreach (Tile candidate in selectableTiles)
        {
            int d0 = GetGraphDistance(candidate.numTile, posCop0);
            int d1 = GetGraphDistance(candidate.numTile, posCop1);
            int safeScore = Mathf.Min(d0, d1);

            if (safeScore > maxDistance)
            {
                maxDistance = safeScore;
                bestTile = candidate;
            }
        }

        robber.GetComponent<RobberMove>().MoveToTile(bestTile);
        robber.GetComponent<RobberMove>().currentTile = bestTile.numTile;
    }

    private int GetGraphDistance(int start, int end)
    {
        Queue<int> q = new Queue<int>();
        int[] dists = new int[Constants.NumTiles];
        for (int i = 0; i < Constants.NumTiles; i++) dists[i] = -1;

        q.Enqueue(start);
        dists[start] = 0;

        while (q.Count > 0)
        {
            int curr = q.Dequeue();
            if (curr == end) return dists[curr];
            foreach (int neighbor in tiles[curr].adjacency)
            {
                if (dists[neighbor] == -1)
                {
                    dists[neighbor] = dists[curr] + 1;
                    q.Enqueue(neighbor);
                }
            }
        }
        return 99;
    }

    public void EndGame(bool end)
    {
        if (end) finalMessage.text = "You Win!";
        else finalMessage.text = "You Lose!";
        playAgainButton.interactable = true;
        state = Constants.End;
    }

    public void PlayAgain()
    {
        cops[0].GetComponent<CopMove>().Restart(tiles[Constants.InitialCop0]);
        cops[1].GetComponent<CopMove>().Restart(tiles[Constants.InitialCop1]);
        robber.GetComponent<RobberMove>().Restart(tiles[Constants.InitialRobber]);
        ResetTiles();
        playAgainButton.interactable = false;
        finalMessage.text = "";
        roundCount = 0;
        rounds.text = "Rounds: 0";
        state = Constants.Restarting;
    }

    public void InitGame() { state = Constants.Init; }

    public void IncreaseRoundCount()
    {
        roundCount++;
        rounds.text = "Rounds: " + roundCount;
    }

    public void FindSelectableTiles(bool cop)
    {
        int indexcurrentTile = cop ? cops[clickedCop].GetComponent<CopMove>().currentTile
                                   : robber.GetComponent<RobberMove>().currentTile;

        ResetTiles();
        tiles[indexcurrentTile].current = true;

        int otherCopTile = -1;
        if (cop)
        {
            int otherCopId = (clickedCop == 0) ? 1 : 0;
            otherCopTile = cops[otherCopId].GetComponent<CopMove>().currentTile;
        }

        Queue<Tile> nodes = new Queue<Tile>();
        tiles[indexcurrentTile].visited = true;
        tiles[indexcurrentTile].distance = 0;
        nodes.Enqueue(tiles[indexcurrentTile]);

        while (nodes.Count > 0)
        {
            Tile t = nodes.Dequeue();
            if (t.distance < Constants.Distance)
            {
                foreach (int neighborIndex in t.adjacency)
                {
                    Tile neighbor = tiles[neighborIndex];
                    if (!neighbor.visited && neighborIndex != otherCopTile)
                    {
                        neighbor.visited = true;
                        neighbor.parent = t;
                        neighbor.distance = t.distance + 1;
                        neighbor.selectable = true;
                        nodes.Enqueue(neighbor);
                    }
                }
            }
        }
        tiles[indexcurrentTile].selectable = false;
    }
}