namespace Example
{
	using UnityEngine;
	using Fusion;

	/// <summary>
	/// Script for setting photon cap visual based on network authority state.
	/// </summary>
	public sealed class VisualChanger : NetworkBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private GameObject _inputAuthority;
        private GameObject[] _inputAuthorityChildren;
		[SerializeField]
		private GameObject _proxy;
        private GameObject[] _proxyChildren;

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
            GetChildren();
			RefreshVisual();
		}

		public override void Render()
		{
			RefreshVisual();
		}

		// PRIVATE METHODS
        private void GetChildren()
        {
            _inputAuthorityChildren = new GameObject[_inputAuthority.transform.childCount];
            for (int i = 0; i < _inputAuthority.transform.childCount; i++)
            {
                _inputAuthorityChildren[i] = _inputAuthority.transform.GetChild(i).gameObject;
            }

            _proxyChildren = new GameObject[_proxy.transform.childCount];
            for (int i = 0; i < _proxy.transform.childCount; i++)
            {
                _proxyChildren[i] = _proxy.transform.GetChild(i).gameObject;
            }
        }

		private void RefreshVisual()
		{
            SetActiveChildren(_inputAuthorityChildren, false);
            SetActiveChildren(_proxyChildren, false);

			if (HasInputAuthority == true)
			{
                SetActiveChildren(_inputAuthorityChildren, true);
			}
			else
            {
                SetActiveChildren(_proxyChildren, true);
            }
		}

        private void SetActiveChildren(GameObject[] children, bool isActive)
        {
            foreach (var child in children)
            {
                child.SetActive(isActive);
            }
        }
	}
}
