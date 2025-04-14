using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using Firebase;
using Firebase.Storage;
using Firebase.Extensions;
using Unity.Netcode;

public class DLCStoreManager : NetworkBehaviour
{
    // --- UI Fields (assign these via the Inspector) ---
    [Header("Store UI")]
    public GameObject storePanel;
    public Transform itemContainer;
    public GameObject itemPrefab;
    public TextMeshProUGUI creditsText;
    public Button openStoreButton;
    public Button closeStoreButton;

    [Header("Avatar Display")]
    public Image whiteAvatarImage;
    public Image blackAvatarImage;

    // --- Firebase & DLC Variables ---
    private FirebaseStorage storage;
    private bool firebaseInitialized = false;
    private bool dlcDataLoaded = false;  // True when dlc_items.txt is successfully parsed
    private List<DLCItem> dlcItems = new List<DLCItem>();

    // --- Player Credits ---
    private float playerCredits = 1000.0f;  // Starting credits

    // --- Purchased Items Tracking ---
    private HashSet<string> purchasedItems = new HashSet<string>();

    // --- Host Equipped Item ---
    private string currentEquippedItemId = "";

    // --- Offline Equipped Data ---
    // If we equip offline, we store item ID here for broadcast after connect
    private string localOfflineEquippedItemId = "";
    private bool offlineUsedWhiteSide = false;

    // ---------- Data Classes ----------
    [Serializable]
    public class DLCItem
    {
        public string itemID;
        public string displayName;
        public string imageUrl;
        public float price;
    }

    [XmlRoot("DLCItems")]
    public class DLCItemArray
    {
        [XmlElement("DLCItem")]
        public DLCItem[] items;
    }

    // ----------------------------------------------------------------------
    private void Awake()
    {
        if (storePanel != null)
            storePanel.SetActive(false);

        if (openStoreButton != null)
            openStoreButton.onClick.AddListener(OpenStore);
        if (closeStoreButton != null)
            closeStoreButton.onClick.AddListener(CloseStore);

        UpdateCreditsText();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!firebaseInitialized)
        {
            Debug.Log("OnNetworkSpawn: Initializing Firebase...");
            InitializeFirebase();
        }

        if (IsServer)
        {
            // We are the host
            // If we had an offline item but never assigned currentEquippedItemId, do that now
            if (string.IsNullOrEmpty(currentEquippedItemId) && !string.IsNullOrEmpty(localOfflineEquippedItemId))
            {
                currentEquippedItemId = localOfflineEquippedItemId;
                Debug.Log("Host: Setting currentEquippedItemId from offline data: " + currentEquippedItemId);
            }

            // If we have an equipped item, re-broadcast so new clients see it
            if (!string.IsNullOrEmpty(currentEquippedItemId))
            {
                Debug.Log("Host OnNetworkSpawn: Re-sending equipped item " + currentEquippedItemId + " to clients");
                UpdatePlayerDLCClientRpc(currentEquippedItemId, 0);
            }
        }
        else
        {
            // We are the client
            // If we had an offline-equipped item, we need to:
            // 1) Show it on black for ourselves,
            // 2) Broadcast to host so the host sees it on black as well
            if (!string.IsNullOrEmpty(localOfflineEquippedItemId))
            {
                Debug.Log("Client OnNetworkSpawn: Broadcasting offline item " + localOfflineEquippedItemId + " to host, and switching from white to black locally.");

                // Locally, switch from white to black
                whiteAvatarImage.sprite = null; 
                DLCItem item = dlcItems.Find(x => x.itemID == localOfflineEquippedItemId);
                if (item != null)
                {
                    StartCoroutine(DownloadAndSetImage(item.imageUrl, blackAvatarImage));
                }
                else
                {
                    Debug.LogWarning("Offline item " + localOfflineEquippedItemId + " not found yet in dlcItems. Will show once loaded...");
                }

                // Now broadcast so the host sees it
                UpdatePlayerDLCServerRpc(localOfflineEquippedItemId);
            }

            // Also request the host's item after DLC items are loaded
            if (dlcItems.Count > 0 && dlcDataLoaded)
            {
                Debug.Log("OnNetworkSpawn (client): DLC loaded => Requesting host's item now.");
                RequestCurrentEquippedItemServerRpc();
            }
            else
            {
                StartCoroutine(WaitForDLCItemsAndRequestEquipped());
            }
        }
    }

    private IEnumerator WaitForDLCItemsAndRequestEquipped()
    {
        while (!dlcDataLoaded)
            yield return null;

        Debug.Log("WaitForDLCItemsAndRequestEquipped: DLC loaded => requesting host's avatar.");
        RequestCurrentEquippedItemServerRpc();
    }

    // ------------------ UI Buttons and Store Logic -----------------------
    public void OpenStore()
    {
        Debug.Log("OpenStore called, enabling store panel.");
        if (storePanel != null)
            storePanel.SetActive(true);

        if (!firebaseInitialized)
        {
            Debug.Log("Firebase not initialized. Initializing now...");
            InitializeFirebase();
        }
        else
        {
            PopulateStoreUI();
        }
    }

    public void CloseStore()
    {
        Debug.Log("CloseStore called, disabling store panel.");
        if (storePanel != null)
            storePanel.SetActive(false);
    }

    private void UpdateCreditsText()
    {
        if (creditsText != null)
            creditsText.text = "Credits: " + playerCredits.ToString("F2");
    }

    // ------------------ Firebase Initialization and DLC Loading -------------------------
    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                storage = FirebaseStorage.GetInstance("gs://unitychess-12876.firebasestorage.app");
                firebaseInitialized = true;
                Debug.Log("Firebase Storage connected. Loading DLC items...");
                LoadDLCItemsFromFirebase();
            }
            else
            {
                Debug.LogError("Could not resolve Firebase deps: " + dependencyStatus);
            }
        });
    }

    private void LoadDLCItemsFromFirebase()
    {
        string dlcConfigPath = "dlc_items.txt";
        Debug.Log("Requesting URL for " + dlcConfigPath);
        storage.GetReference(dlcConfigPath)
            .GetDownloadUrlAsync()
            .ContinueWithOnMainThread(task =>
        {
            if (!task.IsFaulted && !task.IsCanceled)
            {
                string url = task.Result.ToString();
                Debug.Log("Got DLC config URL: " + url);
                StartCoroutine(DownloadDLCConfig(url));
            }
            else
            {
                Debug.LogError("Failed to get DLC config download URL.");
            }
        });
    }

    private IEnumerator DownloadDLCConfig(string url)
    {
        Debug.Log("Downloading DLC config from: " + url);
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError("Error downloading DLC config: " + request.error);
            }
            else
            {
                string xmlText = request.downloadHandler.text;
                Debug.Log("DLC config downloaded:\n" + xmlText);
                ParseDLCItems(xmlText);
            }
        }
    }

    private void ParseDLCItems(string xmlText)
    {
        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(DLCItemArray));
            using (StringReader reader = new StringReader(xmlText))
            {
                DLCItemArray itemArray = (DLCItemArray)serializer.Deserialize(reader);
                if (itemArray != null && itemArray.items != null)
                {
                    dlcItems = new List<DLCItem>(itemArray.items);
                    Debug.Log("Parsed " + dlcItems.Count + " items, marking dlcDataLoaded=true.");
                    dlcDataLoaded = true;
                }
                else
                {
                    Debug.LogWarning("No DLC items found in the XML config.");
                    dlcDataLoaded = true;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to parse XML DLC items: " + ex);
        }

        PopulateStoreUI();

        // If we're client, request host's item
        if (!IsServer)
        {
            Debug.Log("Client: dlc data loaded => requesting host's item soon...");
            StartCoroutine(RequestEquippedAfterDelay());
        }
        else
        {
            if (!string.IsNullOrEmpty(currentEquippedItemId))
            {
                Debug.Log("Host: Re-broadcasting item " + currentEquippedItemId + " after DLC load.");
                UpdatePlayerDLCClientRpc(currentEquippedItemId, 0);
            }
        }
    }

    private IEnumerator RequestEquippedAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        Debug.Log("RequestEquippedAfterDelay => calling RequestCurrentEquippedItemServerRpc()");
        RequestCurrentEquippedItemServerRpc();
    }

    private void PopulateStoreUI()
    {
        Debug.Log("PopulateStoreUI with " + dlcItems.Count + " items (dlcDataLoaded=" + dlcDataLoaded + ")");
        foreach (Transform child in itemContainer)
            Destroy(child.gameObject);

        foreach (var item in dlcItems)
            SetupStoreItem(item);
    }

    private void SetupStoreItem(DLCItem item)
    {
        Debug.Log("Instantiating store item for " + item.itemID);
        GameObject newItem = Instantiate(itemPrefab, itemContainer);
        if (!newItem)
        {
            Debug.LogError("Failed to instantiate store item for " + item.itemID);
            return;
        }

        TextMeshProUGUI nameTMP  = newItem.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI priceTMP = newItem.transform.Find("Price")?.GetComponent<TextMeshProUGUI>();
        Image iconImage          = newItem.transform.Find("Icon")?.GetComponent<Image>();
        Button purchaseButton    = newItem.transform.Find("PurchaseButton")?.GetComponent<Button>();
        Button equipButton       = newItem.transform.Find("EquipButton")?.GetComponent<Button>();

        if (nameTMP)  nameTMP.text = item.displayName;
        if (priceTMP) priceTMP.text = "$" + item.price.ToString("F2");
        if (iconImage)
            StartCoroutine(DownloadAndSetImage(item.imageUrl, iconImage));

        bool isPurchased = purchasedItems.Contains(item.itemID);
        if (purchaseButton)
            purchaseButton.gameObject.SetActive(!isPurchased);
        if (equipButton)
            equipButton.gameObject.SetActive(isPurchased);

        if (purchaseButton)
        {
            purchaseButton.onClick.RemoveAllListeners();
            purchaseButton.onClick.AddListener(() => PurchaseItem(item));
        }
        if (equipButton)
        {
            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(() => EquipItem(item));
        }
        Debug.Log("Created store item for " + item.itemID);
    }

    private IEnumerator DownloadAndSetImage(string url, Image targetImage)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();
#if UNITY_2020_1_OR_NEWER
            if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.ProtocolError)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Debug.LogError("Error loading image from " + url + ": " + req.error);
            }
            else
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(req);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                                              new Vector2(0.5f, 0.5f));
                if (targetImage)
                {
                    targetImage.sprite = sprite;
                    Debug.Log("Image loaded from " + url);
                }
            }
        }
    }

    // ----------------- Purchase & Equip Logic ------------------
    private void PurchaseItem(DLCItem item)
    {
        if (playerCredits < item.price)
        {
            Debug.Log("Not enough credits to purchase " + item.displayName);
            return;
        }

        playerCredits -= item.price;
        purchasedItems.Add(item.itemID);
        Debug.Log("Purchased item: " + item.itemID);

        if (FirebaseAnalyticsManager.Instance != null)
        FirebaseAnalyticsManager.Instance.LogDLCPurchase(item.itemID, item.price);
        
        UpdateCreditsText();
        PopulateStoreUI();
        EquipItem(item);
    }

    private void EquipItem(DLCItem item)
    {
        Debug.Log("EquipItem: Equipping " + item.displayName);

        bool isConnected = NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsServer;
        if (!isConnected)
        {
            // Offline => White by default
            Debug.Log("Offline equip => White side");
            offlineUsedWhiteSide = true;
            localOfflineEquippedItemId = item.itemID;
            StartCoroutine(DownloadAndSetImage(item.imageUrl, whiteAvatarImage));
        }
        else
        {
            // Online
            if (IsServer)
            {
                // Host => White
                currentEquippedItemId = item.itemID;
                localOfflineEquippedItemId = item.itemID;
                StartCoroutine(DownloadAndSetImage(item.imageUrl, whiteAvatarImage));
            }
            else
            {
                // Client => Black
                localOfflineEquippedItemId = item.itemID;
                StartCoroutine(DownloadAndSetImage(item.imageUrl, blackAvatarImage));
            }
            // Broadcast so the other side sees it
            UpdatePlayerDLCServerRpc(item.itemID);
        }
    }

    // -------------------- Netcode Syncing ----------------------
    [ServerRpc(RequireOwnership = false)]
    private void UpdatePlayerDLCServerRpc(string itemID, ServerRpcParams rpcParams = default)
    {
        Debug.Log("ServerRpc: Broadcasting equipped item " + itemID + " from sender " + rpcParams.Receive.SenderClientId);
        UpdatePlayerDLCClientRpc(itemID, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    private void UpdatePlayerDLCClientRpc(string itemID, ulong senderClientId)
    {
        // If this update is from me, ignore
        if (senderClientId == NetworkManager.Singleton.LocalClientId)
            return;

        Debug.Log("ClientRpc: Received equipped item " + itemID + " from " + senderClientId);
        DLCItem item = dlcItems.Find(x => x.itemID == itemID);
        if (item == null)
        {
            Debug.LogWarning("Could not find DLC item with ID " + itemID);
            return;
        }

        // If sender is 0 => host => White, else => Black
        if (senderClientId == 0)
        {
            Debug.Log("Applying host's avatar to White side on this instance.");
            StartCoroutine(DownloadAndSetImage(item.imageUrl, whiteAvatarImage));
        }
        else
        {
            Debug.Log("Applying client's avatar to Black side on this instance.");
            StartCoroutine(DownloadAndSetImage(item.imageUrl, blackAvatarImage));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestCurrentEquippedItemServerRpc(ServerRpcParams rpcParams = default)
    {
        Debug.Log("ServerRpc: Client " + rpcParams.Receive.SenderClientId + " requested host's equipped item.");
        if (!string.IsNullOrEmpty(currentEquippedItemId))
        {
            Debug.Log("Host re-sending currentEquippedItemId (" + currentEquippedItemId + ") to client " + rpcParams.Receive.SenderClientId);
            UpdatePlayerDLCClientRpc(currentEquippedItemId, 0);
        }
        else
        {
            Debug.Log("Host has no equipped item to send.");
        }
    }
}
