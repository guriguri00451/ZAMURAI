using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

public class ScreenFadeManager : MonoBehaviour
{
    public static ScreenFadeManager Instance { get; private set; }

    [SerializeField] private Image fadeImage; // ここに真っ黒なUI画像をセットする

    private void Awake()
    {
        Instance = this;
        
        if (fadeImage != null)
        {
            // 最初は黒にする
            fadeImage.color = new Color(0,0,0,1);
            fadeImage.raycastTarget = false; // マウスクリックの邪魔にならないように
        }
    }

    // 画面を真っ黒にする（死んだ時）
    public async UniTask FadeOut(float duration = 0.5f)
    {
        if (fadeImage == null) return;
        
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, time / duration);
            await UniTask.Yield();
        }
        fadeImage.color = new Color(0, 0, 0, 1); // 完全に真っ黒
    }

    // 画面を明るくする（リスポーンした時）
    public async UniTask FadeIn(float duration = 0.5f)
    {
        if (fadeImage == null) return;

        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            fadeImage.color = new Color(0, 0, 0, 1f - (time / duration));
            await UniTask.Yield();
        }
        fadeImage.color = new Color(0, 0, 0, 0); // 完全に透明
    }
}