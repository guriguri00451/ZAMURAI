using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;
using Fusion.Addons.KCC;
using Cysharp.Threading.Tasks;
using Cursor = UnityEngine.Cursor;

namespace ZAMURAI.Player
{
	/// <summary>
	/// The minimalistic player implementation. Shows essential code to control KCC.
	/// </summary>
	[DefaultExecutionOrder(-5)]
	public class BasicPlayer_ZAMURAI : NetworkBehaviour
	{
		public KCC       KCC;
		public Transform CameraPivot;
		public Transform CameraHandle;

		[SerializeField] Animator inputerAnim;
		[SerializeField] Animator proxyAnim;
		//[SerializeField] VoiceDetector voiceDetector;
		[SerializeField] SpriteRenderer HorrorDeathEffect;
		[Header("Targeting Settings")]
		[SerializeField] private float castRadius = 2.0f; // 判定の太さ（大きくするほどガバくなる）
        [SerializeField] private float castRange = 10.0f; // 届く距離
		private PlayerRef myPlayerRef;
		private int	_lastInputFrame;
		private BasicInput_ZAMURAI.AccumulatedData _accumulatedBuffer;
		private BasicInput_ZAMURAI.ContinuousData _continuousBuffer;
		private Vector2Accumulator _lookRotationAccumulator = new Vector2Accumulator(0.02f, true);
		private float rightleft;
		private float frontback;
		private float updown;
		private bool isGrounded;
		private bool isDead;
		[Header("SE Settings")]
		[SerializeField] private AudioSource audioSource;
		[SerializeField] private AudioClip seTuntun;
		[SerializeField] private AudioClip seOtuntun;
		[SerializeField] private AudioClip seSamurai;
		[SerializeField] private AudioClip seTuntunSamurai;
		[SerializeField] private AudioClip seSyakin;
		[SerializeField] private AudioClip seBiron;
		[SerializeField] private AudioClip seMissed;
		[SerializeField] private AudioClip seDeath;
		[SerializeField] private AudioClip seClear;
		[Networked] public bool IsPointing { get; set; }

		public override void Spawned()
		{
			isDead = false;
			if (HasInputAuthority == true)
			{
				// Register input polling for local player.
				Runner.GetComponent<NetworkEvents>().OnInput.AddListener(OnPlayerInput);

				// Hide cursor.
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;

				//voiceDetector.OnTranscriptionResult += PointActionHandler;
				myPlayerRef = Object.InputAuthority;
				PlayersManager.Instance.AddPlayer(myPlayerRef);
			}
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			// Unregister input polling.
			runner.GetComponent<NetworkEvents>().OnInput.RemoveListener(OnPlayerInput);
		}

		public override void FixedUpdateNetwork()
		{
			if (GetInput(out BasicInput_ZAMURAI input) == true)
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
				if (Object.HasStateAuthority) 
				{
					IsPointing = input.Continuous.PointingData.pointing;
				}

				//Debug指差しコマンド
				if(input.Accumulated.PointAction.Type != PointActionType.none)
				{
					PointActionHandler(input.Accumulated.PointAction.Type);
				}

			}
		}
		

        public override void Render()
		{
			ProxyAnimUpdate();
			InputerAnimUpdate();
			TryAccumulateInput();
		}

		private void ProxyAnimUpdate()
		{
			Vector3 localVel = transform.InverseTransformDirection(KCC.Data.RealVelocity);
			rightleft = localVel.x;
			updown = localVel.y;
			frontback = localVel.z;

			isGrounded = KCC.Data.IsGrounded;

			proxyAnim.SetFloat("MoveX", rightleft);
			proxyAnim.SetFloat("MoveY", frontback);
			proxyAnim.SetBool("isGrounded", updown != 0);

			proxyAnim.SetBool("pointing", IsPointing);
		}

		private void InputerAnimUpdate()
		{
			inputerAnim.SetBool("pointing", IsPointing);
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

			BasicInput_ZAMURAI finalInput = new BasicInput_ZAMURAI();

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

				if (keyboard.zKey.wasPressedThisFrame == true)
				{
					_accumulatedBuffer.PointAction = new PointAction { Type = PointActionType.tuntun, PlayerId = Object.InputAuthority.PlayerId };
				}
				if (keyboard.xKey.wasPressedThisFrame == true)
				{
					_accumulatedBuffer.PointAction = new PointAction { Type = PointActionType.otuntun, PlayerId = Object.InputAuthority.PlayerId };
				}
				if (keyboard.cKey.wasPressedThisFrame == true)
				{
					_accumulatedBuffer.PointAction = new PointAction { Type = PointActionType.samurai, PlayerId = Object.InputAuthority.PlayerId };
				}
				if (keyboard.vKey.wasPressedThisFrame == true)
				{
					_accumulatedBuffer.PointAction = new PointAction { Type = PointActionType.tuntunsamurai, PlayerId = Object.InputAuthority.PlayerId };
				}
				if (keyboard.bKey.wasPressedThisFrame == true)
				{
					_accumulatedBuffer.PointAction = new PointAction { Type = PointActionType.biron, PlayerId = Object.InputAuthority.PlayerId };
				}
				if (keyboard.nKey.wasPressedThisFrame == true)
				{
					_accumulatedBuffer.PointAction = new PointAction { Type = PointActionType.syakin, PlayerId = Object.InputAuthority.PlayerId };
				}
			}

			// Pointing action
			if (mouse.leftButton.isPressed == true)
			{
				_continuousBuffer.PointingData.pointing = true;
				_continuousBuffer.PointingData.PointingPlayerId = Object.InputAuthority.PlayerId;

				//voiceDetector.SwitchRecording(true);
			}
			else
			{
				_continuousBuffer.PointingData.pointing = false;
				//voiceDetector.SwitchRecording(false);
			}

		}
        private void PointActionHandler(PointActionType command)
		{
			Debug.Log($"Transcribed command: {command}");
			if(_continuousBuffer.PointingData.pointing == false) return;

			if (isDead) return;

            Debug.Log($"音声認識コマンド: {command}");
			RPC_PointActionSound(command);
            PlayersManager.Instance.RPC_ProcessVoiceInput(Object.InputAuthority, command, GetFrontPlayerRef());
		}

		[Rpc(RpcSources.All, RpcTargets.All)] // 全員の画面で音を鳴らす
		private void RPC_PointActionSound(PointActionType type)
		{
			AudioClip clipToPlay = null;

			switch (type)
			{
				case PointActionType.tuntun:         clipToPlay = seTuntun; break;
				case PointActionType.otuntun:        clipToPlay = seOtuntun; break;
				case PointActionType.samurai:        clipToPlay = seSamurai; break;
				case PointActionType.tuntunsamurai:  clipToPlay = seTuntunSamurai; break;
				case PointActionType.syakin:         clipToPlay = seSyakin; break;
				case PointActionType.biron:          clipToPlay = seBiron; break;
				default: return; // none の時などは何もしない
			}

			if (clipToPlay != null && audioSource != null)
			{
				audioSource.PlayOneShot(clipToPlay);
			}
		}

		// 目の前のプレイヤーの「PlayerRef」を返すように変更
        private PlayerRef GetFrontPlayerRef()
        {
			RaycastHit hit;
            if (Physics.SphereCast(CameraHandle.position, castRadius, CameraHandle.forward, out hit, castRange))
            {
                BasicPlayer_ZAMURAI frontPlayer = hit.collider.GetComponentInParent<BasicPlayer_ZAMURAI>();
                if (frontPlayer != null)
                {
                    Debug.Log($"指した相手: {frontPlayer.name}");
                    return frontPlayer.Object.InputAuthority;
                }
            }
			if (Physics.Raycast(CameraHandle.position, CameraHandle.forward, out hit, 10f))
			{
				BasicPlayer_ZAMURAI frontPlayer = hit.collider.GetComponentInParent<BasicPlayer_ZAMURAI>();

				if (frontPlayer != null)
				{
					Debug.Log($"指した相手: {frontPlayer.name}");
					return frontPlayer.Object.InputAuthority;
				}
			}
			Debug.Log("見つからず");
            return PlayerRef.None; // 誰もいなかったらNoneを返す
        }
		[Rpc(RpcSources.All, RpcTargets.InputAuthority)]
        public void RPC_PlayMissEffect()
        {
			audioSource.PlayOneShot(seMissed);
        }
		
        [Rpc(RpcSources.All, RpcTargets.InputAuthority)]
        public void RPC_PlayDeathEffect()
        {
            // クライアント側で非同期の演出をスタートさせる
            // 警告が出ないように .Forget() をつけるのが UniTask の推奨です
            DeathEffect().Forget();
			audioSource.PlayOneShot(seDeath);
        }

        // Death() は消すか残すか自由ですが、演出本体はこちらを使います
        private async UniTask DeathEffect()
        {
            // 安全策：オブジェクトが既に消えていたら何もしない
            if (HorrorDeathEffect == null) return; 

            for(int i = 0; i < 14; i++)
            {
                // 待機中にDespawnされてオブジェクトが消えたらループを抜ける
                if (this == null || HorrorDeathEffect == null) break;

                HorrorDeathEffect.enabled = !HorrorDeathEffect.enabled;
                Color color = Random.ColorHSV();
				color.a = 1;
				HorrorDeathEffect.color = color;
                
                await UniTask.Delay(200);
            }

            if (HorrorDeathEffect != null)
            {
                HorrorDeathEffect.enabled = false;
            }

			if(CameraHandle == null) return;
			Vector3 tempPos = CameraHandle.position; // 一度コピーを変数に入れる
			tempPos.y = -30f;                        // コピーのYを書き換える
			CameraHandle.position = tempPos;         // 本体のpositionに丸ごと上書きする
        }

		[Rpc(RpcSources.All, RpcTargets.InputAuthority)]
        public async void RPC_GameClear()
		{
			audioSource.PlayOneShot(seClear);
		}

	}
}
