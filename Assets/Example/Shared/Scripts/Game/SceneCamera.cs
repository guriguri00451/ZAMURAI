namespace Example
{
	using System;
	using UnityEngine;
	using UnityEngine.EventSystems;

	/// <summary>
	/// Component for handling cameras with different platform configurations.
	/// </summary>
	public sealed class SceneCamera : MonoBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private PlatformConfiguration _defaultConfiguration;

		private PlatformConfiguration _activeConfiguration;

		// PUBLIC METHODS

		public Camera GetActiveCamera()
		{
			return _activeConfiguration.Camera;
		}

		public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
		{
			_activeConfiguration.Camera.transform.SetPositionAndRotation(position, rotation);
		}

		public void SetFieldOfView(float fieldOfView)
		{
			_activeConfiguration.Camera.fieldOfView = fieldOfView;
		}

		// MonoBehaviour INTERFACE

		private void Awake()
		{
			// VRの分岐を削除し、常にデフォルト設定を使用
            _activeConfiguration = _defaultConfiguration;
			
			if (Application.isBatchMode == true)
				return;

			_defaultConfiguration.SetActive(false);

			_activeConfiguration.SetActive(true);

			EventSystem.current = _activeConfiguration.EventSystem;
		}

		// DATA STRUCTURES

		[Serializable]
		private sealed class PlatformConfiguration
		{
			public Camera      Camera;
			public EventSystem EventSystem;

			public void SetActive(bool isActive)
			{
				Camera.gameObject.SetActive(isActive);
				EventSystem.gameObject.SetActive(isActive);
			}
		}
	}
}
