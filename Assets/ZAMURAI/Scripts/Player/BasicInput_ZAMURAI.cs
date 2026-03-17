using UnityEngine;
using Fusion;

namespace ZAMURAI.Player
{
	/// <summary>
	/// Input structure polled by Fusion. This is sent over network and processed by server, keep it optimized and remove unused data.
	/// </summary>
	public struct BasicInput_ZAMURAI : INetworkInput
	{
		//Updateごとに中身がDefaultに戻るInput群
		public struct AccumulatedData : INetworkStruct
		{
			public Vector2 MoveDirection;
			public Vector2 LookRotationDelta;
			public NetworkBool Jump;
			public PointAction PointAction;
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
