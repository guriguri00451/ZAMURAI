using UnityEngine;
using ZAMURAI;

namespace Fusion.Addons.KCC
{
    public class TitleManager : MonoBehaviour
    {
        public void OnStartButtonClicked()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(Scenes.Main.ToString());
        }
    }
}
