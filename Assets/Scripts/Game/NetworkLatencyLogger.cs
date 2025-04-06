using UnityEngine;
using Unity.Netcode;

public class NetworkLatencyLogger : NetworkBehaviour
{
    private float lastPingTime;
    private float pingInterval = 5f;
    private float currentPing = 0f;

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
        Debug.Log($"Current Ping: {currentPing:F1} ms");
    }
}
