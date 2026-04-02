using System;
using System.Text;
using System.IO;
using System.Threading;
using UnityEngine;

public class IpcServer : MonoBehaviour
{
    private Thread inputThread;
    private volatile bool running = true;
    public bool isResultSent = false;
    public Action<SetupData> setupCallback;
    public Action<ShotData> shotCallback;
    public Action<ControlData> controlCallback;
    public Action<LaunchMonitorData> statusCallback;

    void Start()
    {
        inputThread = new Thread(InputLoop);
        inputThread.Start();
        Debug.Log("stdio thread started...");
    }

    void InputLoop()
    {
        var stdin = Console.OpenStandardInput();
        var reader = new StreamReader(stdin);

        while (running)
        {
            string line = reader.ReadLine();
            if (line == null)
            {
                // End of stream
                break;
            }
            Debug.Log("[IPC] Received: " + line);
            ReceivedJsonData jsonData = JsonUtility.FromJson<ReceivedJsonData>(line);

            if (jsonData != null)
            {
                if (jsonData.type == "shot")
                {
                    Debug.Log($"[IPC] shot: (ballSpeed={jsonData.shot.ballSpeed})");
                    if (shotCallback != null)
                    {
                        shotCallback(jsonData.shot);
                    }
                }
                else if (jsonData.type == "control")
                {
                    Debug.Log($"[IPC] control: (command={jsonData.control.command},state={jsonData.control.state})");
                    if (controlCallback != null)
                    {
                        controlCallback(jsonData.control);
                    }
                }
                else if (jsonData.type == "status")
                {
                    Debug.Log($"[IPC] status: (connected={jsonData.status.connected},ready={jsonData.status.ready})");
                    if (statusCallback != null)
                    {
                        statusCallback(jsonData.status);
                    }
                }
                else if (jsonData.type == "setup")
                {
                    Debug.Log($"[IPC] setup: (gameMode={jsonData.setupData.gameMode}, players={jsonData.setupData.players.Length})");
                    if (setupCallback != null)
                    {
                        setupCallback(jsonData.setupData);
                    }
                }
            }
        }
    }

    public void SendResponse(string response)
    {
        var stderr = Console.OpenStandardError();
        var writer = new StreamWriter(stderr) { AutoFlush = true };
        writer.WriteLine(response);
    }

    public void SendStatusEvent(string status)
    {
        SystemStatusEvent statusEvent = new SystemStatusEvent();
        statusEvent.type = "status";
        statusEvent.status = status;
        string raw = JsonUtility.ToJson(statusEvent);
        SendResponse(raw);
    }

    public void SendPlayerUpdateEvent(string playerId, Club club)
    {
        PlayerUpdateEvent statusEvent = new PlayerUpdateEvent();
        statusEvent.type = "player";
        statusEvent.club = club;
        statusEvent.playerId = playerId;

        string raw = JsonUtility.ToJson(statusEvent);
        SendResponse(raw);
    }

    public void SendShotResult(BallData data, ShotData shot, string playerId, Club club)
    {

        ShotResultData resultData = new ShotResultData();
        resultData.type = "result";
        resultData.data = data;
        resultData.shot = shot;
        resultData.playerId = playerId;
        resultData.club = club;

        isResultSent = true;
        string message = JsonUtility.ToJson(resultData);
        SendResponse(message);
    }

    public void OnApplicationQuit()
    {
        running = false;
        if (inputThread != null && inputThread.IsAlive)
        {
            inputThread.Join(500); // Wait briefly for clean exit
        }
    }
}