using UnityEngine;

/// <summary>
/// 受击转移：自身只是一个额外的受击盒，不扣血、不变红、不处理伤害。
/// 碰到玩家子弹时，直接把这次命中转发给目标物体的 HandlePlayerBulletHit 方法，
/// 由目标（RotatingAmmoLauncherHitPoint）统一执行受伤 + 联动变红。
/// </summary>
public class HitTransfer : MonoBehaviour
{
    [Header("受击目标")]
    [SerializeField] private RotatingAmmoLauncherHitPoint target;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("PlayerBullet")) return;
        if (target == null || !target.gameObject.activeInHierarchy) return;

        target.HandlePlayerBulletHit(other);
    }
}
