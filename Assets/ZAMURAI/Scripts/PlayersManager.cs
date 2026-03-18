
using Fusion;
using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework.Internal;
using ZAMURAI.Player;
using Example.BasicMovement;
public class PlayersManager : NetworkBehaviour
{
    public static PlayersManager Instance { get; private set; }
    [SerializeField] private BasicGameplayManager gameplayManager;
    private List<PlayerRef> playerRefs = new List<PlayerRef>();

    // ▼ ちんちん侍用のネットワーク変数（ホストが管理して全員に共有する）
    [Networked] public PlayerRef currentTurnPlayer { get; set; }   // 今コマンドを言うべき人
    [Networked] public PlayerRef currentTargetPlayer { get; set; } // 指された人
    [Networked] public PointActionType currentCommand { get; set; } // 現在発動中のコマンド
    [Networked] public TickTimer reactionTimer { get; set; }       // 制限時間（時間切れで殺す）
    

    public override void Spawned()
    {
        Instance = this;
        // 起動時にとりあえず今の全員を入れる（安全策）
        foreach (var p in Runner.ActivePlayers) 
        {
            AddPlayer(p);
        }

        TestStart();
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

    private async void TestStart()
    {
        if (!Object.HasStateAuthority) return; 

        // 最初の人が集まるまで少し待機
        await UniTask.Delay(5000);

        if (playerRefs.Count > 0)
        {
            // 最初のターンプレイヤーをランダムに決定し、ゲームスタート！
            currentTurnPlayer = RandomPlayer();
            currentCommand = PointActionType.tuntun;
            reactionTimer = TickTimer.CreateFromSeconds(Runner, 10f); // 最初の人が喋るまでの制限時間
            
            Debug.Log($"ゲームスタート！最初の番は {currentTurnPlayer} です！");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_MissRitual(PlayerRef playerWhoActed)
    {
        // 判定はStateAuthority（部屋の主など）が一括で行うのが「安全」
        if (!Object.HasStateAuthority) return;
        {
            // ミスしたらゲームの進行を一旦止める
            currentTurnPlayer = PlayerRef.None;
            reactionTimer = TickTimer.None;

            // 失敗：間違えた奴に化物を送り込む！
            Debug.LogError($"儀式失敗！ {playerWhoActed} がトチった！");
            
            if (EnemyController.Instance != null)
            {
                // ここでEnemy側のRPCを叩く！
                EnemyController.Instance.RPC_HuntPlayer(playerWhoActed);
            }
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

            if (spokenWord == PointActionType.tuntun) // 「トゥントゥン」だった場合
            {
                // リアクション不要。ターゲットのターンへ即移行
                currentTurnPlayer = currentTargetPlayer;
                currentCommand = PointActionType.none;
                reactionTimer = TickTimer.CreateFromSeconds(Runner, 5f);
                
                // AddSuccessCount(); // 規定回数クリアを入れるならここでカウントアップ
            }
            else if (spokenWord == PointActionType.otuntun || spokenWord == PointActionType.samurai || spokenWord == PointActionType.tuntunsamurai)
            {
                // 「おtぅんトゥン」「侍」「トゥントゥン侍」は相手のリアクション待ちへ移行
                reactionTimer = TickTimer.CreateFromSeconds(Runner, 3f);
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
            // 指された本人以外が喋ったら処刑！
            if (sender != currentTargetPlayer)
            {
                RPC_MissRitual(sender);
                return;
            }

            bool isSuccess = false;

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
            // 「トゥントゥン侍」への正しい返し
            else if (currentCommand == PointActionType.tuntunsamurai && spokenWord == PointActionType.tuntunsamurai)
            {
                isSuccess = true;
                Debug.Log("おちんちん侍のポーズ成功！");
            }

            if (isSuccess)
            {
                // 成功！次はリアクションした人の番になる
                currentTurnPlayer = currentTargetPlayer;
                currentCommand = PointActionType.none;
                reactionTimer = TickTimer.CreateFromSeconds(Runner, 5f);
                
                // AddSuccessCount(); // 規定回数クリアを入れるならここでカウントアップ
            }
            else
            {
                // 返しの言葉を間違えたら処刑！
                RPC_MissRitual(sender);
            }
        }
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