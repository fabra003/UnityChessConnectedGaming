using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Firestore;                // Cloud Firestore
using Firebase.Extensions;               // For ContinueWithOnMainThread
using UnityEngine;

public class FirebaseAnalyticsManager : MonoBehaviour
{
    public static FirebaseAnalyticsManager Instance;
    private FirebaseFirestore firestore;  // Firestore instance for event logging and game state
    private bool isFirestoreReady = false;

    private void Awake()
    {
        // Use a singleton pattern so this manager persists across scenes.
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeFirebase();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Checks Firebase dependencies and initializes Firestore.
    /// </summary>
    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;
                // Initialize Firestore instead of Realtime Database.
                firestore = FirebaseFirestore.DefaultInstance;
                isFirestoreReady = true;
                Debug.Log("Firebase initialized for Firestore (analytics and game state).");
            }
            else
            {
                Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);
            }
        });
    }

    /// <summary>
    /// Helper method to log an event to Firestore into the "analytics_events" collection.
    /// </summary>
    /// <param name="eventType">The type of the event (e.g., "match_start").</param>
    /// <param name="eventData">Additional event data as key/value pairs.</param>
    private void LogEventToFirestore(string eventType, Dictionary<string, object> eventData)
    {
        // Ensure Firestore is ready before proceeding.
        if (!isFirestoreReady || firestore == null)
        {
            Debug.LogWarning($"Firestore is not ready yet. Skipping log for event: {eventType}");
            return;
        }

        // Add common fields for every event.
        eventData["event_type"] = eventType;
        eventData["timestamp"] = DateTime.UtcNow.ToString("o");

        firestore.Collection("analytics_events").AddAsync(eventData)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    Debug.Log($"{eventType} event logged to Firestore.");
                }
                else
                {
                    Debug.LogError($"Failed to log {eventType} event to Firestore: {task.Exception}");
                }
            });
    }

    /// <summary>
    /// Logs a match start event.
    /// </summary>
    public void LogMatchStart()
    {
        Dictionary<string, object> data = new Dictionary<string, object>();
        LogEventToFirestore("match_start", data);
        Debug.Log("Logged match start event.");
    }

    /// <summary>
    /// Logs a match end event along with result data.
    /// </summary>
    /// <param name="result">A string indicating the outcome (e.g., "checkmate", "stalemate", "resignation").</param>
    /// <param name="winner">The side that won (or empty for draw).</param>
    public void LogMatchEnd(string result, string winner)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "result", result },
            { "winner", winner }
        };
        LogEventToFirestore("match_end", data);
        Debug.Log($"Logged match end event: result = {result}, winner = {winner}.");
    }

    /// <summary>
    /// Logs a DLC purchase event.
    /// </summary>
    /// <param name="itemID">The unique identifier of the purchased DLC.</param>
    /// <param name="price">The price of the DLC.</param>
    public void LogDLCPurchase(string itemID, float price)
    {
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "itemID", itemID },
            { "price", price }
        };
        LogEventToFirestore("dlc_purchase", data);
        Debug.Log($"Logged DLC purchase event: itemID = {itemID}, price = {price}.");
    }

    /// <summary>
    /// Saves the current game state to Firestore.
    /// </summary>
    /// <param name="serializedGameState">A string that represents the current game state (e.g., FEN or PGN).</param>
    /// <param name="onSaved">Callback that receives the generated game ID or null if failed.</param>
    public void SaveGameState(string serializedGameState, Action<string> onSaved)
    {
        if (!isFirestoreReady || firestore == null)
        {
            Debug.LogWarning("Firestore is not ready, cannot save game state.");
            onSaved?.Invoke(null);
            return;
        }

        // Generate a unique game ID (using UTC ticks, for example)
        string gameId = DateTime.UtcNow.Ticks.ToString();

        // Prepare the data to be stored.
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "state", serializedGameState },
            { "timestamp", DateTime.UtcNow.ToString("o") }
        };

        firestore.Collection("saved_games").Document(gameId)
            .SetAsync(data)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    Debug.Log("Game state saved successfully with ID: " + gameId);
                    onSaved?.Invoke(gameId);
                }
                else
                {
                    Debug.LogError("Failed to save game state: " + task.Exception);
                    onSaved?.Invoke(null);
                }
            });

        // Optionally log the save event as well.
        Dictionary<string, object> eventData = new Dictionary<string, object>
        {
            { "gameId", gameId }
        };
        LogEventToFirestore("save_game_state", eventData);
    }

    /// <summary>
    /// Retrieves a game state from Firestore using the provided game ID.
    /// </summary>
    /// <param name="gameId">The identifier of the saved game state.</param>
    /// <param name="onComplete">Callback that receives the serialized game state or null if not found.</param>
    public void RetrieveGameState(string gameId, Action<string> onComplete)
    {
        if (!isFirestoreReady || firestore == null)
        {
            Debug.LogWarning("Firestore is not ready, cannot retrieve game state.");
            onComplete(null);
            return;
        }

        firestore.Collection("saved_games").Document(gameId)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("Error retrieving game state: " + task.Exception);
                    onComplete(null);
                }
                else if (task.IsCompleted)
                {
                    DocumentSnapshot snapshot = task.Result;
                    if (snapshot.Exists)
                    {
                        // Assume the game state is stored in the "state" field.
                        string gameState = snapshot.GetValue<string>("state");
                        Debug.Log("Retrieved game state for gameId " + gameId + ": " + gameState);
                        onComplete(gameState);
                    }
                    else
                    {
                        Debug.LogWarning("No game state found for gameId: " + gameId);
                        onComplete(null);
                    }
                }
            });
    }
}
