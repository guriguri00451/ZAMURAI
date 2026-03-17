using System;
using Fusion;
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
            StartSharedClients(nds);
        } else {
            Debug.LogError("FusionBootstrap not found in the scene.");
        }
    }
    private async void StartSharedClients(FusionBootstrap nds) {
        await UniTask.WaitUntil(() => nds.CurrentStage == FusionBootstrap.Stage.Disconnected); // Wait for a moment to ensure everything is initialized
        nds.DefaultRoomName = roomName; // Set the default room name to the specified scene name
        // 現在の Peer Mode 設定を確認して挙動を変える
        var config = NetworkProjectConfig.Global;
        if (config.PeerMode == NetworkProjectConfig.PeerModes.Multiple) 
        {
            // Multiple Peer モードなら 4 人一斉起動
            Debug.Log($"[ZAMURAI] Multiple Peer Mode: Starting {clientCount} clients.");
            nds.StartMultipleSharedClients(clientCount);
        } 
        else 
        {
            // Single Peer モード（ビルド用）なら 1 人だけ起動
            Debug.Log("[ZAMURAI] Single Peer Mode: Starting Single Shared Client.");
            nds.StartSharedClient(); 
        }
    }
}