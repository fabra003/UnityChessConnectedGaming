using UnityEngine;
using UnityEngine.UI;

public class PerformanceDebugPanel : MonoBehaviour
{
    // Reference to a UI Text element to display the ping.
    public Text pingText;

    private void Update()
    {
        if (pingText != null)
        {
            pingText.text = $"Ping: {NetworkLatencyLogger.LatestPing:F1} ms";
        }
    }
}
