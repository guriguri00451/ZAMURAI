using System;
using Fusion;
using UnityEditor.SearchService;
using UnityEngine;
using ZAMURAI;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

public class RoomLauncher : MonoBehaviour
{
    private FusionBootstrap nds;
    [SerializeField] private string roomName = "ZAMURAI_Main";
    [SerializeField] private int clientCount = 4;
    private void Start() {
        nds = FindObjectOfType<FusionBootstrap>();
        roomName = ZAMURAIAppManager.Instance.gameObject.name; // Set the room name to the name of the GameObject that holds ZAMURAIAppManager
        if (nds != null) {
            StartMultipleSharedClients(nds);
        } else {
            Debug.LogError("FusionBootstrap not found in the scene.");
        }
    }
    private async void StartMultipleSharedClients(FusionBootstrap nds) {
        await UniTask.WaitUntil(() => nds.CurrentStage == FusionBootstrap.Stage.Disconnected); // Wait for a moment to ensure everything is initialized
        nds.DefaultRoomName = roomName; // Set the default room name to the specified scene name
        nds.StartMultipleSharedClients(clientCount);
    }
}