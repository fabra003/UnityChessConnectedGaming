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

    // Call this method to leave the current session.
    public void LeaveSession()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("Left session.");
        }
        else
        {
            Debug.LogError("NetworkManager.Singleton is null.");
        }
    }

    // Call this method to rejoin the game as a client.
    // It first shuts down any existing connection, then starts the client.
    public void RejoinSession()
    {
        if (NetworkManager.Singleton != null)
        {
            // Ensure any existing session is shut down.
            NetworkManager.Singleton.Shutdown();
            // Optionally, you might wait a brief moment here for a clean shutdown.
            NetworkManager.Singleton.StartClient();
            Debug.Log("Rejoined session as client.");
        }
        else
        {
            Debug.LogError("NetworkManager.Singleton is null.");
        }
    }
}
