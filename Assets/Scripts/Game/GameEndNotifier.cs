using UnityEngine;
using Unity.Netcode;

public class GameEndNotifier : NetworkBehaviour
{
    public static GameEndNotifier Instance;

    private void Awake()
    {
        Instance = this;
        Debug.Log("GameEndNotifier instance created on " + gameObject.name);
    }

    // This method should only be called on the server.
    public void NotifyGameEnd(string resultMessage)
    {
        if (IsServer)
        {
            NotifyGameEndClientRpc(resultMessage);
        }
    }

    [ClientRpc]
    private void NotifyGameEndClientRpc(string resultMessage)
    {
        Debug.Log("Game Ended: " + resultMessage);
        // Inform the UI so players see the outcome.
        UIManager.Instance.ShowGameEndMessage(resultMessage);
    }
}
