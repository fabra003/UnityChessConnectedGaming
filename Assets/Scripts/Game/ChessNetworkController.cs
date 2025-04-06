using UnityEngine;
using Unity.Netcode;
using UnityChess; // For Movement, Square, etc.

public class ChessNetworkController : NetworkBehaviour
{
    // Simple mapping: host (clientId == NetworkManager.Singleton.LocalClientId on the server) is White; others are Black.
    private Side GetAssignedSide(ulong clientId)
    {
        return (clientId == NetworkManager.Singleton.LocalClientId) ? Side.White : Side.Black;
    }

    // Called on clients to request a move.
    public void RequestMove(string startSquareStr, string endSquareStr)
    {
        // Only allow local players to send move requests if they are connected.
        if (NetworkManager.Singleton.IsClient)
        {
            RequestMoveServerRpc(startSquareStr, endSquareStr);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestMoveServerRpc(string startSquareStr, string endSquareStr, ServerRpcParams rpcParams = default)
    {
        // Determine the side of the requesting client.
        Side requesterSide = GetAssignedSide(rpcParams.Receive.SenderClientId);

        // Enforce turn-based play: only process if it is the requester's turn.
        if (requesterSide != GameManager.Instance.SideToMove)
        {
            Debug.LogWarning($"Move rejected. It is not {requesterSide}'s turn.");

            // Immediately sync the current board state so the client reverts the piece
            string serializedGame = GameManager.Instance.SerializeGame();
            SyncGameStateClientRpc(serializedGame);

            return;
        }

        // Create a Movement from the provided square strings.
        Square startSquare = new Square(startSquareStr);
        Square endSquare = new Square(endSquareStr);
        Movement move = new Movement(startSquare, endSquare);

        // Execute the move on the server via our new public method.
        bool success = GameManager.Instance.NetworkExecuteMove(move);
        if (success)
        {
            // If move execution succeeds, serialize the updated game state.
            string serializedGame = GameManager.Instance.SerializeGame();
            Debug.Log("Move executed successfully on server. Broadcasting new game state.");
            // Broadcast the new game state to all clients.
            SyncGameStateClientRpc(serializedGame);
        }
        else
        {
            Debug.LogWarning("Move execution failed on server.");
            // Also revert if the move fails for any other reason:
            string serializedGame = GameManager.Instance.SerializeGame();
            SyncGameStateClientRpc(serializedGame);
        }
    }

    [ClientRpc]
    private void SyncGameStateClientRpc(string serializedGame)
    {
        // Instead of GameManager.Instance.LoadGame(serializedGame);
        GameManager.Instance.LoadGame(serializedGame);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestResignServerRpc()
    {
        // Only the server can call Resign
        GameManager.Instance.Resign();
    }
}
