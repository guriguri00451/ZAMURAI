using UnityEngine;
using Fusion;
using Cysharp.Threading.Tasks;

namespace Example.BasicMovement
{
	/// <summary>
	/// Main entry point for gameplay logic and spawning players.
	/// There exists only ONE instance spawned in the scene.
	/// </summary>
	public sealed class BasicGameplayManager : NetworkBehaviour
	{
		public NetworkObject PlayerPrefab;

		public override void Spawned()
		{
			if (Runner.GameMode == GameMode.Shared)
			{
				// In Shared mode every player spawn the player object on their own.
				SpawnPlayer(Runner.LocalPlayer);
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (Runner.IsServer == true)
			{
				// With Client-Server topology only the Server spawn player objects.
				// PlayerManager is a special helper class which iterates over list of active players (NetworkRunner.ActivePlayers) and call spawn/despawn callbacks on demand.
				PlayerManager<BasicPlayer>.UpdatePlayerConnections(Runner, SpawnPlayer, DespawnPlayer);
			}
		}

		private void SpawnPlayer(PlayerRef playerRef)
		{
			// Get all spawnpoints in the scene.
			SpawnPoint[] spawnPoints = Runner.SimulationUnityScene.GetComponents<SpawnPoint>(false);

			// Select random spawnpoint.
			Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)].transform;

			// Spawn the player object with correct input authority.
			NetworkObject player = Runner.Spawn(PlayerPrefab, spawnPoint.position, spawnPoint.rotation, playerRef);

			// Set the spawned instance as player object so we can easily get it from other locations using Runner.GetPlayerObject(playerRef).
			// This is optional, but it is a good practice as there is usually 1 main object spawned for each player.
			Runner.SetPlayerObject(playerRef, player);

			// Every player should be always interested to his player object to prevent accidentally getting out of Area of Interest.
			// This is valid only if the Interest Management is enabled in Network Project Config.
			Runner.SetPlayerAlwaysInterested(playerRef, player, true);
			PlayersManager.Instance.AddPlayer(playerRef);
		}

		private void DespawnPlayer(PlayerRef playerRef, BasicPlayer player)
		{
			// We simply despawn the player object. No other cleanup is needed here.
			Runner.Despawn(player.Object);
			PlayersManager.Instance.RemovePlayer(playerRef);
		}

		private void DespawnPlayer(PlayerRef playerRef, NetworkObject playerObj)
		{
			Runner.Despawn(playerObj);
			PlayersManager.Instance.RemovePlayer(playerRef);
		}

		public async void RespawnPlayer(PlayerRef playerRef)
		{
			// 1. ネットワーク上からプレイヤーオブジェクトを完全に消去 (Despawn)
			DespawnPlayer(playerRef, Runner.GetPlayerObject(playerRef));
			
			// 2. リスポーンまでの待機時間（例：3秒）
			await UniTask.Delay(3000);

			SpawnPlayer(playerRef);
		}
	}
}
