using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityChess; // for Side and GameManager

public class TurnAndPlayerInfoDisplay : MonoBehaviour
{
    // Reference to the UI Text element that will display whose turn it is.
    [SerializeField] private Text turnInfoText;
    // Reference to the UI Text element that will display the local player's assigned side.
    [SerializeField] private Text playerSideText;

    private void Update()
    {
        UpdateTurnInfo();
        UpdatePlayerSideInfo();
    }

    private void UpdateTurnInfo()
    {
        // If GameManager is available, update the turn info.
        if (GameManager.Instance != null)
        {
            // Show the current turn from the game state.
            turnInfoText.text = $"Turn: {GameManager.Instance.SideToMove}";
        }
    }

    private void UpdatePlayerSideInfo()
    {
        // Determine the local player's assigned side.
        // Here we assume that if you are running as the server (host), you are White;
        // otherwise, you are assigned Black.
        string assignedSide = "Offline";
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                assignedSide = "White";
            }
            else if (NetworkManager.Singleton.IsClient)
            {
                assignedSide = "Black";
            }
        }
        playerSideText.text = $"You are: {assignedSide}";
    }
}
