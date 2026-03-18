using Fusion;
using UnityEngine;
using UnityEngine.AI;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

public class EnemyController : NetworkBehaviour
{
    // どこからでも「EnemyController.Instance」で呼べるようにする
    public static EnemyController Instance { get; private set; }

    public NavMeshAgent Agent;
    
    [Networked]
    public PlayerRef TargetPlayer { get; set; }

    [Networked] public int EnemyState { get; set; } // 0:待機, 1:追跡
    private int warpedDelayMs = 3000; // ワープしてから振り向くまでの遅延時間
    private int chaseDurationMs = 10000; // 追跡する時間
    [Networked] public Vector3 homePosition { get; set; } // 待機状態のときにいる場所
    [SerializeField] private NetworkTransform networkTransform; // これも必要
    [SerializeField] private float killDistance;

    public override void Spawned()
    {
        Instance = this;        
        Agent.enabled = false;

        if(Object.HasStateAuthority)
        {
            homePosition = transform.position; // 最初の位置を待機状態の場所として保存
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 持ち主（StateAuthority）だけが物理的な移動を計算する
        if (Object.HasStateAuthority && EnemyState == 1)
        {
            var targetObj = Runner.GetPlayerObject(TargetPlayer);
            if (targetObj != null)
            {
                Agent.SetDestination(targetObj.transform.position);
            }
        }
        if (Object.HasStateAuthority && EnemyState == 2)
        {
            Agent.enabled = false;
            networkTransform.Teleport(homePosition); // 待機位置に戻る
            EnemyState = 0; // 待機状態に戻る
        }
        
    }

    public void Kill()
    {
        EnemyState = 2;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public async void RPC_HuntPlayer(PlayerRef target)
    {
        if (Object.HasStateAuthority == false) return; // 安全のため、StateAuthority以外はこの関数を実行しない
        TargetPlayer = target;
        EnemyState = 1; 
        await WarpBehindTarget(target);
    }

    private async UniTask WarpBehindTarget(PlayerRef target)
    {
        Agent.enabled = true;

        var targetTransform = Runner.GetPlayerObject(target).transform;
        // 背後6mの位置にワープ
        Vector3 warpPos = targetTransform.position - (targetTransform.forward * 6f);
        warpPos.y = 0; // 地面に接地
        Agent.Warp(warpPos);
        
        await UniTask.Delay(warpedDelayMs); // 少し待ってから振り向く
        transform.LookAt(targetTransform);
        EnemyState = 1; // ワープ後に追跡開始
        await UniTask.Delay(chaseDurationMs); // 追跡時間後に待機状態に戻る
        EnemyState = 2;
    }

}