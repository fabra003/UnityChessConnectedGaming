using UnityEngine;
using Unity.Netcode;

public class ChessNetcodeInitializer : MonoBehaviour
{
    // Call this method (for example, via a UI button) to start the game as host.
    public void StartHost()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartHost();
            Debug.Log("Host started.");
        }
        else
        {
            Debug.LogError("NetworkManager.Singleton is null.");
        }
    }

    // Call this method (for example, via a UI button) to join as a client.
    public void StartClient()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartClient();
            Debug.Log("Client started.");
        }
        else
        {
            Debug.LogError("NetworkManager.Singleton is null.");
        }
    }
}
