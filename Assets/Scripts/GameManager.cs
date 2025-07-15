using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [SerializeField]
    GameObject red, green;

    bool isPlayer, hasGameFinished, playerIsRed;

    [SerializeField]
    Text turnMessage;

    const string RED_MESSAGE = "Red's Turn";
    const string GREEN_MESSAGE = "Greens's Turn";

    Color RED_COLOR = new Color(231, 29, 54, 255) / 255f;
    Color GREEN_COLOR = new Color(0, 222, 1, 255) / 255f;

    Board myBoard;

    // Configurações P2P
    TcpListener server;
    Thread listenThread;
    int listenPort = 5050; // Porta para escutar conexões
    string otherIp = "10.57.10.46"; // IP do outro peer (troque para o IP real)

    private void Awake()
    {
        playerIsRed = PlayerPreferences.Instance.IsPlayerRed;
        isPlayer = playerIsRed;
        hasGameFinished = false;
        turnMessage.text = RED_MESSAGE;
        turnMessage.color = RED_COLOR;
        myBoard = new Board();

        StartServer();
    }

    void StartServer()
    {
        listenThread = new Thread(() =>
        {
            try
            {
                server = new TcpListener(IPAddress.Any, listenPort);
                server.Start();
                Debug.Log("[P2P] Servidor iniciado na porta " + listenPort);

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    NetworkStream stream = client.GetStream();

                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Debug.Log("[P2P] Mensagem recebida: " + msg);

                    if (int.TryParse(msg, out int coluna))
                    {
                        // Recebe jogada do outro peer e aplica no main thread
                        UnityMainThreadDispatcher.Instance().Enqueue(() => JogadaRecebida(coluna));
                    }

                    stream.Close();
                    client.Close();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[P2P] Erro no servidor: " + e.Message);
            }
        });
        listenThread.IsBackground = true;
        listenThread.Start();
    }

    void SendMove(int coluna)
    {
        try
        {
            TcpClient client = new TcpClient();
            client.Connect(otherIp, listenPort);

            NetworkStream stream = client.GetStream();
            byte[] message = Encoding.UTF8.GetBytes(coluna.ToString());
            stream.Write(message, 0, message.Length);

            stream.Close();
            client.Close();

            Debug.Log("[P2P] Jogada enviada: " + coluna);
        }
        catch (Exception e)
        {
            Debug.LogError("[P2P] Erro ao enviar jogada: " + e.Message);
        }
    }

    private void Update()
    {
        if (!isPlayer || hasGameFinished)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            //If GameFinsished then return
            if (hasGameFinished) return;

            //Raycast2D
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mousePos2D = new Vector2(mousePos.x, mousePos.y);
            RaycastHit2D hit = Physics2D.Raycast(mousePos2D, Vector2.zero);
            if (!hit.collider) return;

            if (hit.collider.CompareTag("Press"))
            {
                if (hit.collider.gameObject.GetComponent<Column>().targetlocation.y > 1.5f) return;

                int coluna = hit.collider.gameObject.GetComponent<Column>().col - 1;

                // Spawn local e atualizar tabuleiro
                Vector3 spawnPos = hit.collider.gameObject.GetComponent<Column>().spawnLocation;
                Vector3 targetPos = hit.collider.gameObject.GetComponent<Column>().targetlocation;
                bool adversarioEhVermelho = !isPlayer;  // Se é sua vez, adversário fez a jogada
                GameObject circle = Instantiate(adversarioEhVermelho ? red : green);
                circle.transform.position = spawnPos;
                circle.GetComponent<Mover>().targetPostion = targetPos;

                hit.collider.gameObject.GetComponent<Column>().targetlocation = new Vector3(targetPos.x, targetPos.y + 0.7f, targetPos.z);

                myBoard.UpdateBoard(coluna, isPlayer);
                if (myBoard.Result(isPlayer))
                {
                    turnMessage.text = (isPlayer ? "Red" : "Green") + " Wins!";
                    hasGameFinished = true;
                    return;
                }

                // Envia jogada para outro peer
                SendMove(coluna);

                // Atualiza turno local
                isPlayer = false;
                turnMessage.text = GREEN_MESSAGE;
                turnMessage.color = GREEN_COLOR;
            }
        }
    }

    void JogadaRecebida(int coluna)
    {
        if (hasGameFinished) return;

        // Busca Column para spawnar peça recebida
        Column colObj = null;
        Column[] columns = FindObjectsOfType<Column>();
        foreach (var c in columns)
        {
            if (c.col - 1 == coluna)
            {
                colObj = c;
                break;
            }
        }
        if (colObj == null) return;

        Vector3 spawnPos = colObj.spawnLocation;
        Vector3 targetPos = colObj.targetlocation;

        GameObject circle = Instantiate(playerIsRed ? green : red);
        circle.transform.position = spawnPos;
        circle.GetComponent<Mover>().targetPostion = targetPos;

        colObj.targetlocation = new Vector3(targetPos.x, targetPos.y + 0.7f, targetPos.z);

        myBoard.UpdateBoard(coluna, !isPlayer);
        if (myBoard.Result(!isPlayer))
        {
            turnMessage.text = (!isPlayer ? "Red" : "Green") + " Wins!";
            hasGameFinished = true;
            return;
        }

        // Volta o turno para você
        isPlayer = true;
        if (playerIsRed)
        {
            turnMessage.text = RED_MESSAGE;
            turnMessage.color = RED_COLOR;
        }
        else
        {
            turnMessage.text = GREEN_MESSAGE;
            turnMessage.color = GREEN_COLOR;
        }

    }

    private void OnApplicationQuit()
    {
        try
        {
            server?.Stop();
            listenThread?.Abort();
        }
        catch { }
    }
}
