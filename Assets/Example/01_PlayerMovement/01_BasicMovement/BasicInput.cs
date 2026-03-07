using UnityEngine;
using Fusion;

namespace Example.BasicMovement
{
	/// <summary>
	/// Input structure polled by Fusion. This is sent over network and processed by server, keep it optimized and remove unused data.
	/// </summary>
	public struct BasicInput : INetworkInput
	{
		//Updateごとに中身がDefaultに戻るInput群
		public struct AccumulatedData : INetworkStruct
		{
			public Vector2 MoveDirection;
			public Vector2 LookRotationDelta;
			public NetworkBool Jump;
		}
		//中身がDefaultに戻らずキープされるInput群
		public struct ContinuousData : INetworkStruct
		{
			public NetworkBool Point;
		}

		// 上で作った型を変数として宣言する
		public ContinuousData Continuous;
		public AccumulatedData Accumulated;
	}
}
