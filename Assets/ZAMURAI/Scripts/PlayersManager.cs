
using Fusion;
using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework.Internal;
using ZAMURAI.Player;
using Example.BasicMovement;
using ZAMURAI;
using System.Threading.Tasks;
public class PlayersManager : NetworkBehaviour
{
    public static PlayersManager Instance { get; private set; }
    [SerializeField] private BasicGameplayManager gameplayManager;
    private List<PlayerRef> playerRefs = new List<PlayerRef>();

    // とうんトゥン侍用のネットワーク変数（ホストが管理して全員に共有する）
    [Networked] public PlayerRef currentTurnPlayer { get; set; }   // 今コマンドを言うべき人
    [Networked] public PlayerRef currentTargetPlayer { get; set; } // 指された人
    [Networked] public PointActionType currentCommand { get; set; } // 現在発動中のコマンド
    [Networked] public TickTimer reactionTimer { get; set; }       // 制限時間（時間切れで殺す）
    [Header("Game Difficulty")]
    [SerializeField] private float firstTurnTime = 15f;    // 最初の人が喋るまで（長め）
    [SerializeField] private float nextCommandTime = 8f;   // 次のコマンドを言うまで
    [SerializeField] private float reactionTime = 6f;      // リアクション（シャキーン等）の猶予
    [SerializeField] private int goalTurnCount = 20;
    private int turnCount;
    private HashSet<PlayerRef> readyPlayers = new HashSet<PlayerRef>();
    

    public override void Spawned()
    {
        Instance = this;
        TuntunStart();
    }

    public void AddPlayer(PlayerRef playerRef)
    {
        playerRefs.Add(playerRef);
    }

    public void RemovePlayer(PlayerRef playerRef)
    {
        playerRefs.Remove(playerRef);
    }

    // 毎フレーム、ホストが「時間切れ」を監視する
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        // 制限時間が切れたら...
        if (reactionTimer.Expired(Runner))
        {
            reactionTimer = TickTimer.None; // タイマー停止

            if (currentCommand == PointActionType.none) 
            {
                // コマンド入力待ちで時間切れ ＝ ターンプレイヤーのミス！
                RPC_MissRitual(currentTurnPlayer);
            } 
            else 
            {
                // リアクション待ちで時間切れ ＝ 指された人のミス！
                RPC_MissRitual(currentTargetPlayer);
            }
        }
    }

    private async void TuntunStart()
    {
        if (!Object.HasStateAuthority) return; 

        // 最初の人が集まるまで少し待機
        await UniTask.Delay(5000);

        if (playerRefs.Count > 0)
        {
            turnCount = 0;
            // 最初のターンプレイヤーをランダムに決定し、ゲームスタート！
            currentTurnPlayer = RandomPlayer();
            currentCommand = PointActionType.none;
            reactionTimer = TickTimer.CreateFromSeconds(Runner, 100f); // 最初の人が喋るまでの制限時間
            
            Debug.Log($"ゲームスタート！最初の番は {currentTurnPlayer} です！");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public async void RPC_MissRitual(PlayerRef playerWhoActed)
    {
        // 判定はStateAuthority（部屋の主など）が一括で行うのが「安全」
        if (!Object.HasStateAuthority) return;
        {
            // ミスしたらゲームの進行を一旦止める
            currentTurnPlayer = PlayerRef.None;
            reactionTimer = TickTimer.None;

            foreach(var p in playerRefs)
            {
                GetPlayerScript(p).RPC_PlayMissEffect();
            }

            // 失敗：間違えた奴に化物を送り込む！
            Debug.LogError($"儀式失敗！ {playerWhoActed} がトチった！");
            
            if (EnemyController.Instance != null)
            {
                // ここでEnemy側のRPCを叩く！

                Debug.LogError($"本番だったらお前は死んでいる");
                EnemyController.Instance.RPC_HuntPlayer(playerWhoActed);
            }
            // 5秒くらい「あーあ…」という絶望タイムを作る
            await UniTask.Delay(5000);

            // --- 3. 再スタート処理 ---
            TuntunStart();
        }
    }

    // ▼ プレイヤーは「自分のID、喋った言葉、指した相手（いなければNone）」を投げるだけ！
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ProcessVoiceInput(PlayerRef sender, PointActionType spokenWord, PlayerRef target)
    {
        if (!Object.HasStateAuthority) return;

        // --------------------------------------------------------
        // 状態①：誰かが「コマンド」を言うのを待っている状態
        // --------------------------------------------------------
        if (currentCommand == PointActionType.none)
        {
            // 自分の番じゃないのに喋った奴は処刑！
            // （※厳しすぎる場合は return; だけで無視する設定にしてもOK）
            if (sender != currentTurnPlayer)
            {
                RPC_MissRitual(sender);
                return;
            }

            // 誰も指ささずに喋った場合は処刑！
            if (target == PlayerRef.None)
            {
                RPC_MissRitual(sender);
                return;
            }

            // 有効なコマンドが発動した！
            Debug.Log($"{sender} が {target} に向かって {spokenWord} を発動！");
            currentTargetPlayer = target;
            currentCommand = spokenWord;
            readyPlayers.Clear(); // 判定用リストをリセット

            if (spokenWord == PointActionType.tuntun) // 「トゥントゥン」だった場合
            {
                TurnSuccess();
                // AddSuccessCount(); // 規定回数クリアを入れるならここでカウントアップ
            }
            else if (spokenWord == PointActionType.otuntun || spokenWord == PointActionType.samurai || spokenWord == PointActionType.tuntunsamurai)
            {
                // 「おtぅんトゥン」「侍」「トゥントゥン侍」は相手のリアクション待ちへ移行
                reactionTimer = TickTimer.CreateFromSeconds(Runner, reactionTime);
            }
            else
            {
                // 自分の番に「シャキーン」など関係ない言葉を言ったら処刑！
                RPC_MissRitual(sender);
            }
        }
        // --------------------------------------------------------
        // 状態②：指された人が「リアクション」するのを待っている状態
        // --------------------------------------------------------
        else
        {

            bool isSuccess = false;

            // 「トゥントゥン侍」への正しい返し
            if (currentCommand == PointActionType.tuntunsamurai)
            {
                // 指した本人（currentTurnPlayer）以外が「トゥントゥン侍」と言ったらカウント
                if (spokenWord == PointActionType.tuntunsamurai)
                {
                    readyPlayers.Add(sender);
                    Debug.Log($"{sender} がポーズ！ 現在の人数: {readyPlayers.Count}");

                    // 自分以外の全員が成功したかチェック
                    Debug.Log(playerRefs.Count);
                    if (readyPlayers.Count >= playerRefs.Count)
                    {
                        Debug.Log("全員成功！トゥントゥン侍！");
                        isSuccess = true;
                    }
                    else
                    {
                        return;
                    }
                }
                else if (spokenWord != PointActionType.none && spokenWord != PointActionType.tuntunsamurai)
                {
                    // 全員タイムなのに違う言葉を叫んだら処刑
                    RPC_MissRitual(sender);
                    return;
                }
            }
            // 指された本人以外が喋ったら処刑！
            else if (sender != currentTargetPlayer)
            {
                Debug.Log("sender:" + sender);
                Debug.Log("target:" + currentTargetPlayer);
                RPC_MissRitual(sender);
                return;
            }

            // 「侍」への正しい返し
            if (currentCommand == PointActionType.samurai && spokenWord == PointActionType.syakin)
            {
                isSuccess = true;
                Debug.Log("シャキーン成功！");
            }
            // 「侍」への正しい返し
            if (currentCommand == PointActionType.otuntun && spokenWord == PointActionType.biron)
            {
                isSuccess = true;
                Debug.Log("びろーん成功！");
            }
            

            if (isSuccess)
            {
                TurnSuccess();
            }
            else
            {
                // 返しの言葉を間違えたら処刑！
                RPC_MissRitual(sender);
            }
        }
    }

    private void TurnSuccess()
    {
        turnCount++;
        if(turnCount >= goalTurnCount)
        {
            GameClear();
            return;
        }
        // リアクション不要。ターゲットのターンへ即移行
        currentTurnPlayer = currentTargetPlayer;
        currentCommand = PointActionType.none;
        reactionTimer = TickTimer.CreateFromSeconds(Runner, nextCommandTime);
    }
    public async void GameClear()
    {
        Debug.Log("Clear!!");
        foreach(var p in playerRefs)
        {
            GetPlayerScript(p).RPC_GameClear();
        }
        await UniTask.Delay(7000);
        LeaveAndTitle().Forget();
    }

    // 接続を切ってタイトルシーンへ遷移する
    [Rpc(RpcSources.All, RpcTargets.All)]
    public async UniTask LeaveAndTitle()
    {
        if (Runner != null)
        {
            // 1. Fusionのネットワーク接続をシャットダウン
            await Runner.Shutdown();
        }

        // 2. カーソルを表示状態に戻す（これ忘れるとタイトルで何も押せないｗ）
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. Unityのシーンマネージャーでタイトルシーンを読み込む
        // ※"Title" の部分は、自分のプロジェクトのタイトルシーン名に変えてください
        UnityEngine.SceneManagement.SceneManager.LoadScene(Scenes.Title.ToString());
    }
    public PlayerRef RandomPlayer()
    {
        if (playerRefs.Count == 0) return default;
        int index = Random.Range(0, playerRefs.Count);
        return playerRefs[index];
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public async void RPC_PlayerDied(PlayerRef playerRef)
    {
        if (!Object.HasStateAuthority) return;
        Debug.Log($"Player {playerRef} died!");
        var playerScript = GetPlayerScript(playerRef);
        if (playerScript != null)
        {
            // 死んだ本人の画面でのみ、ホラーエフェクトのRPCを発動！
            playerScript.RPC_PlayDeathEffect();
        }

        // ホスト側はエフェクトが終わる時間（約6秒）だけ待機
        await UniTask.Delay(3000);

        // 待機後にリスポーン処理を実行
        gameplayManager.RespawnPlayer(playerRef);
    }

    public BasicPlayer_ZAMURAI GetPlayerScript(PlayerRef playerRef)
    {
        var playerObj = Runner.GetPlayerObject(playerRef);
        if (playerObj != null)
        {
            return playerObj.GetComponent<BasicPlayer_ZAMURAI>();
        }
        return null;
    }
}