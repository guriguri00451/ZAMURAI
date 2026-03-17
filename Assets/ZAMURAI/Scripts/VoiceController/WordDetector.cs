using UnityEngine;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Vosk;
using System.IO;

public class WordDetector : MonoBehaviour
{
    [Header("Settings")]
    public string ModelPath = "vosk-model"; // StreamingAssets内のフォルダ名
    public VoiceProcessor VoiceProcessor;
    
    [Header("Events")]
    public System.Action<string> OnFinalResult;   // クリックを離した時の確定結果
    public System.Action<string> OnPartialResult; // 喋っている最中の推測結果

    private Model _model;
    private VoskRecognizer _recognizer;
    private bool _running;
    private readonly ConcurrentQueue<short[]> _audioQueue = new ConcurrentQueue<short[]>();
    private readonly ConcurrentQueue<string> _resultQueue = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> _partialResultQueue = new ConcurrentQueue<string>();

    async void Start()
    {
        // 1. モデルのロード（Zip解凍済み前提のシンプル版）
        string path = Path.Combine(Application.streamingAssetsPath, ModelPath);
        _model = new Model(path);
        _recognizer = new VoskRecognizer(_model, 16000.0f);
        
        VoiceProcessor.OnFrameCaptured += (samples) => _audioQueue.Enqueue(samples);
        
        Debug.Log("🥷 Vosk Ready!");
    }

    // 外部（Updateなど）から叩く開始命令
    public void StartListening()
    {
        if (_running) return;
        _running = true;
        VoiceProcessor.StartRecording();
        Task.Run(ThreadedWork);
    }

    // 外部から叩く停止命令
    public void StopListening()
    {
        _running = false;
        VoiceProcessor.StopRecording();
    }

    private void Update()
    {
        // メインスレッドで結果を処理
        if (_partialResultQueue.TryDequeue(out string partial)) OnPartialResult?.Invoke(partial);
        if (_resultQueue.TryDequeue(out string final)) OnFinalResult?.Invoke(final);
    }

    private async Task ThreadedWork()
    {
        while (_running)
        {
            if (_audioQueue.TryDequeue(out short[] samples))
            {
                if (_recognizer.AcceptWaveform(samples, samples.Length))
                {
                    _resultQueue.Enqueue(_recognizer.Result());
                }
                else
                {
                    // ここが重要！喋っている途中のデータを送る
                    _partialResultQueue.Enqueue(_recognizer.PartialResult());
                }
            }
            await Task.Delay(10);
        }
        // 停止した瞬間の最終結果を取得
        _resultQueue.Enqueue(_recognizer.FinalResult());
    }
}