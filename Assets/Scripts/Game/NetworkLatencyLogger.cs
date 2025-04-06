using UnityEngine;
using Unity.Netcode;

public class NetworkLatencyLogger : NetworkBehaviour
{
    private float lastPingTime;
    private float pingInterval = 5f;
    private float currentPing = 0f;

    // Public static property to hold the latest ping value (in milliseconds)
    public static float LatestPing { get; private set; }

    private void Start()
    {
        // Only run the ping logic on clients.
        if (IsClient)
        {
            InvokeRepeating(nameof(SendPing), pingInterval, pingInterval);
        }
    }

    private void SendPing()
    {
        lastPingTime = Time.time;
        PingServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void PingServerRpc(ServerRpcParams rpcParams = default)
    {
        // Reply immediately to the client.
        PongClientRpc(Time.time);
    }

    [ClientRpc]
    private void PongClientRpc(float serverTime)
    {
        currentPing = (Time.time - lastPingTime) * 1000f; // Convert to milliseconds.
        LatestPing = currentPing;
        Debug.Log($"Current Ping: {currentPing:F1} ms");
    }
}
