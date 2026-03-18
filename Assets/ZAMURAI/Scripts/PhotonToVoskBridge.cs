using UnityEngine;
using Photon.Voice.Unity;
using Photon.Voice;
using System.IO;

public class PhotonToVoskBridge : MonoBehaviour, IProcessor<float>
{
    [SerializeField] private VoiceDetector voskDetector;
    private AudioUtil.Resampler<float> resampler;
    private short[] tempShortBuf; // 配列の再利用（GC Alloc 対策）
    private int sourceSampleRate;
    private int channels;
    // スレッドエラー回避用のフラグ
    private bool isDebugRecordingRequested = false;
    // ★感度調整用パラメータ
    private float volumeMultiplier = 3.0f; // 音量を何倍にするか（2.0〜5.0くらいで調整）
    private int targetSampleRate = 16000;  // Voskが要求する周波数
    void PhotonVoiceCreated(PhotonVoiceCreatedParams p)
    {
        voskDetector.InitializeVosk();
        
        // 入力(マイク)と出力(Vosk)の周波数が違う場合のみリサンプラーを用意
        if (p.AudioDesc.SamplingRate != targetSampleRate)
        {
            sourceSampleRate = p.AudioDesc.SamplingRate;
        }
        // 3. 自分の「横流し機能（IProcessor）」を Photon に登録する
        if (p.Voice is LocalVoiceAudioFloat voiceFloat)
        {
            voiceFloat.AddPostProcessor(this);
            Debug.Log("🥷 連携完了：Photonの音声をVoskへ横流しします。");
        }
    }

    // Photon がマイクから音を拾うたびに呼ばれる関数
    public float[] Process(float[] buf)
    {
        // 1. nullチェックとフラグチェックを先に行う
        if (voskDetector == null) return buf;

        // 1. リサンプリング（Vosk用の16kHzに変換）
        if (resampler == null)
        {
             // 初回のみサイズ計算して初期化
             int dstSize = (buf.Length * targetSampleRate) / sourceSampleRate; // ※ここは実際のsourceSampleRateを使ってください
             resampler = new AudioUtil.Resampler<float>(dstSize, 1);
        }
        float[] resampledBuf = resampler.Process(buf);

         // 2. 音量調整（Voskの認識精度向上のため）

        // float [-1.0f ~ 1.0f] を short [-32768 ~ 32767] に変換
        if (tempShortBuf == null || tempShortBuf.Length != buf.Length)
        {
            tempShortBuf = new short[resampledBuf.Length];
        }

        // 3. 音量ブースト ＋ shortへの変換
        for (int i = 0; i < resampledBuf.Length; i++)
        {
            // マイクの音量を掛け算してブースト
            float boosted = resampledBuf[i] * volumeMultiplier;
            
            // -1.0 ~ 1.0 の範囲を超えないようにClampしてからshortに変換
            this.tempShortBuf[i] = (short)(Mathf.Clamp(boosted, -1f, 1f) * 32767f);
        }

        voskDetector.EnqueueExternalAudio(tempShortBuf);

        return buf;
    }

    public void Dispose() { }
}