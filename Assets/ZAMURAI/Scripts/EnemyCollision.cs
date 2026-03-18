using UnityEngine;
using Fusion;
using ZAMURAI.Player;
using Cysharp.Threading.Tasks; 
public class EnemyCollision : NetworkBehaviour
{
    [SerializeField] private float killDistance = 1.5f; // 当たり判定の広さ（コライダーの代わり）
    private bool isKilled;
    public override void FixedUpdateNetwork()
    {
        // ホスト（StateAuthority）のPCだけで判定する
        if (!Object.HasStateAuthority || isKilled) return;

        // シーン内の全プレイヤーを取得して距離を測る
        foreach (var playerRef in Runner.ActivePlayers)
        {
            var playerObj = Runner.GetPlayerObject(playerRef);
            if (playerObj != null)
            {
                // プレイヤーと敵の距離を計算
                float distance = Vector3.Distance(transform.position, playerObj.transform.position);

                // 距離が killDistance 以下（＝触れた！）なら
                if (distance <= killDistance)
                {
                    Debug.Log($"距離判定で接触！ {playerRef} をキルします。");
                    
                    // キル処理を実行（必要なら isDead などのフラグ管理を追加してください）
                    PlayersManager.Instance.RPC_PlayerDied(playerRef);
                    EnemyController.Instance.Kill();
                    isKilled = true;
                    KillReset();
                    
                    // 1フレームで複数回呼ばれないように抜ける
                    break;
                }
            }
        }
    }

    private async void KillReset()
    {
        await UniTask.Delay(10000);
        isKilled = false;
    }
}