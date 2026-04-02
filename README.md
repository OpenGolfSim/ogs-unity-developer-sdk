This is the experimental OpenGolfSim SDK for Unity, to allow building your own full games that run within the OpenGolfSim desktop platform. It contains open source versions of our core golf physics and IPC communication with the parent OGS process over stdio.



## Usage


We also have a more complete [example project](https://github.com/OpenGolfSim/example-unity-game) that shows how to integrate with the SDK, but the basic usage is fairly simple:

```cs

public class MyCoolGame : MonoBehaviour
{
  // Create and connect a golf ball sized sphere with a rigid body...
  public GameObject golfBall;
  private SetupData setupData;
  private Queue<ShotData> shotQueue = new Queue<ShotData>();
  private IpcServer ipc;
  private BallPhysics ballPhysics;

  // ...
  void Start()
  {
    ballPhysics = golfBall.GetComponent<BallPhysics>();
    if (ballPhysics == null) {
      ballPhysics = golfBall.AddComponent<BallPhysics>();
    }
    // Setup IPC with OpenGolfSim
    ipc = gameObject.AddComponent<IpcServer>();
    ipc.setupCallback += SetupGame;
    ipc.shotCallback += EnqueueShot;
    ipc.controlCallback += EnqueueControl;

  }

  void SetupGame(SetupData parsedSetupData)
  {
    setupData = parsedSetupData;
    // setup players, clubs, etc.
  }

  public void EnqueueShot(ShotData shot)
  {
    Debug.Log($"Received shot: {shot}");
    lock (shotQueue)
    {
      shotQueue.Enqueue(shot);
    }
  }

  void Update()
  {
   
    // receive shots from shot queue
    lock (shotQueue)
    {
        while (shotQueue.Count > 0)
        {
            ShotData shot = shotQueue.Dequeue();
            // Now it's safe to use Unity APIs!
            Debug.Log($"Received a shot in the queue ballSpeed={shot.ballSpeed}");
            bool isPutt = false;

            ballPhysics.LaunchShot(shot, isPutt);

        }
    }
  }


}
```