using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;
using Fusion.Addons.KCC;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using UnityEngine.UIElements.Experimental;

namespace Example.BasicMovement
{
	/// <summary>
	/// The minimalistic player implementation. Shows essential code to control KCC.
	/// </summary>
	[DefaultExecutionOrder(-5)]
	public sealed class BasicPlayer : NetworkBehaviour
	{
		public KCC       KCC;
		public Transform CameraPivot;
		public Transform CameraHandle;

		[SerializeField] Animator anim;
		private int	_lastInputFrame;
		private BasicInput.AccumulatedData _accumulatedBuffer;
		private BasicInput.ContinuousData _continuousBuffer;
		private Vector2Accumulator _lookRotationAccumulator = new Vector2Accumulator(0.02f, true);
		[Networked] public NetworkBool IsPointing { get; set; }

		public override void Spawned()
		{
			if (HasInputAuthority == true)
			{
				// Register input polling for local player.
				Runner.GetComponent<NetworkEvents>().OnInput.AddListener(OnPlayerInput);

				// Hide cursor.
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			// Unregister input polling.
			runner.GetComponent<NetworkEvents>().OnInput.RemoveListener(OnPlayerInput);
		}

		public override void FixedUpdateNetwork()
		{
			if (GetInput(out BasicInput input) == true)
			{
				// Processing input every tick.
				// This code path is executed on InputAuthority and StateAuthority.

				// Apply look rotation delta. This propagates to Transform component immediately.
				KCC.AddLookRotation(input.Accumulated.LookRotationDelta);

				// Set world space input direction. This value is processed later when KCC executes its FixedUpdateNetwork().
				// By default the value is processed by EnvironmentProcessor - which defines base character speed, handles acceleration/friction, gravity and many other features.
				Vector3 inputDirection = KCC.Data.TransformRotation * new Vector3(input.Accumulated.MoveDirection.x, 0.0f, input.Accumulated.MoveDirection.y);
				KCC.SetInputDirection(inputDirection);

				if (input.Accumulated.Jump == true && KCC.Data.IsGrounded == true)
				{
					// Set world space jump vector. This value is processed later when KCC executes its FixedUpdateNetwork().
					KCC.Jump(Vector3.up * 6.0f);
				}

				//Pointing更新
				IsPointing = input.Continuous.Point;
				Debug.Log(input.Continuous.Point);
			}
		}

		public override void Render()
		{
			TryAccumulateInput();
			anim.SetBool("pointing",IsPointing);
		}

		private void LateUpdate()
		{
			// Only input authority needs to update camera.
			if (HasInputAuthority == false)
				return;

			// Update camera pivot and transfer properties from camera handle to Main Camera.
			// LateUpdate() is called after all Render() calls => data in KCC is correctly interpolated.

			Vector2 pitchRotation = KCC.Data.GetLookRotation(true, false);
			CameraPivot.localRotation = Quaternion.Euler(pitchRotation);

			Camera.main.transform.SetPositionAndRotation(CameraHandle.position, CameraHandle.rotation);
		}

		private void OnPlayerInput(NetworkRunner runner, NetworkInput networkInput)
		{
			TryAccumulateInput();

			BasicInput finalInput = new BasicInput();

			// Mouse movement (delta values) is aligned to engine update.
			// To get perfectly smooth interpolated look, we need to align the mouse input with Fusion ticks.
			_accumulatedBuffer.LookRotationDelta = _lookRotationAccumulator.ConsumeTickAligned(runner);

			finalInput.Accumulated = _accumulatedBuffer;
			finalInput.Continuous = _continuousBuffer;

			// Accumulated input is consumed.
			networkInput.Set(finalInput);

			// Reset accumulated input to default.
			_accumulatedBuffer = default;
		}

		private void TryAccumulateInput()
		{
			// Accumulate input only once per frame.
			int currentFrame = Time.frameCount;
			if (currentFrame == _lastInputFrame)
				return;

			_lastInputFrame = currentFrame;

			// Only InputAuthority needs to process device input.
			if (HasInputAuthority == false)
				return;

			// Input is tracked only if the cursor is locked.
			if (Cursor.lockState != CursorLockMode.Locked)
				return;

			// Here we accumulate mouse and keyboard changes into accumulated input.
			// This is important in case of multiple render frames between input polls (which happen with fast rendering speed).

			Mouse mouse = Mouse.current;
			if (mouse != null)
			{
				Vector2 mouseDelta = mouse.delta.ReadValue();
				_lookRotationAccumulator.Accumulate(new Vector2(-mouseDelta.y, mouseDelta.x) * 0.25f);
			}

			Keyboard keyboard = Keyboard.current;
			if (keyboard != null)
			{
				Vector2 moveDirection = default;

				if (keyboard.wKey.isPressed == true) { moveDirection += Vector2.up;    }
				if (keyboard.sKey.isPressed == true) { moveDirection += Vector2.down;  }
				if (keyboard.aKey.isPressed == true) { moveDirection += Vector2.left;  }
				if (keyboard.dKey.isPressed == true) { moveDirection += Vector2.right; }

				_accumulatedBuffer.MoveDirection = moveDirection.normalized;

				if (keyboard.spaceKey.wasPressedThisFrame == true)
				{
					_accumulatedBuffer.Jump = true;
				}
			}

			_continuousBuffer.Point = mouse.leftButton.isPressed;
		}
	}
}
