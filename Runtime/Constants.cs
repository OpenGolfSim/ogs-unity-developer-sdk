using UnityEngine;

public static class OGSUnitConversions
{
    public static readonly float YardsInMeter = 1.09361f;
    public static readonly float FeetInMeter = 3.28084f;
    public static readonly float milesPerHourToMeters = 0.44704f;
    public static readonly float ballRadius = 0.0427f;
    public static readonly float ballOffset = 0.0214f;
}

[System.Serializable]
public class Club
{
    public string name;
    public string id;
    public int distance;
}

[System.Serializable]
public enum ControlCommand
{
    AimLeft,
    AimRight,
    DistanceIncrease,
    DistanceDecrease,
    ClubUp,
    ClubDown,
    PlayerUp,
    PlayerDown,
    Drop,
    ReHit,
    Mulligan,
    Scorecard,
    ToggleMap
}

[System.Serializable]
public enum ControlDataState
{
    Pressed,
    Down,
    Up
}

[System.Serializable]
public class ControlData
{
    public ControlCommand command; // left, right, up, down club-up, club-down
    public ControlDataState state; // press = 0, down = 1, up = 2 (down+up)
}

[System.Serializable]
public class LaunchMonitorData
{
    public bool connected;
    public bool ready;
    public float batteryLevel;
    public string firmware;
    public string statusText;
}

[System.Serializable]
public class SetupData
{
    // The players the user selected for this round
    public PlayerData[] players;
    // The game mode selected in OpenGolfSim (0=range, 1=range-game, 2=course)
    public int gameMode;
    // The user is using the mouse as their launch monitor
    public bool mouseMode;
    // The amount to offset the camera from center
    public float cameraOffset;
    // The users measurement preference ('imperial' or 'metric')
    public string units;
    // User has putting enabled
    public bool puttingEnabled;
    // The users elevation setting (in feet)
    public float elevation;
}


[System.Serializable]
public class BallData
{
    public float carry = 0;
    public float height = 0;
    public float roll = 0;
    public float total = 0;
    public float lateral = 0;
}

[System.Serializable]
public class PlayerData
{
    public string name;
    public string id;
    public Club[] clubs;
}

[System.Serializable]
public class PlayerUpdateEvent
{
    public string type;
    public string playerId;
    public Club club;
}

[System.Serializable]
public class ShotData
{
    public float ballSpeed; // mph
    public float verticalLaunchAngle; // degrees
    public float horizontalLaunchAngle; // degrees
    public float spinAxis; // degrees, positive = hook/left, negative = slice/right
    public float spinSpeed; // ball RPM
}

[System.Serializable]
public class ShotResultData
{
    public string type;
    public BallData data;
    public ShotData shot;
    public Club club;
    public Vector3 startPosition;
    public Vector3 landPosition;
    public Vector3 endPosition;
    public Vector3[] ballTrail;
    public float[] heightSamples;
    public float[] distanceSamples;
    public float[] lateralSamples;
    public string playerId;
    public string sessionId;
}

[System.Serializable]
public class SystemStatusEvent
{
    public string type;
    public string status;
}

[System.Serializable]
public class ReceivedJsonData
{
    public string type;
    public ShotData shot;
    public ControlData control;
    public SetupData setupData;
    public LaunchMonitorData status;
}