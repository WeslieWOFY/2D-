using UnityEngine;
using System.Collections;

/// <summary>
/// 普通激光：触发器检测，持续伤害 + 击退，多激光共享冷却
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Laser : MonoBehaviour
{
    [Header("伤害")]
    [SerializeField] private int damage = 10;
    [SerializeField] private float damageInterval = 0.5f;

    [Header("击退")]
    [SerializeField] private float knockbackForce = 15f;
    [SerializeField] private float knockbackCooldown = 0.3f;
    [SerializeField] private float knockbackDuration = 0.12f;

    // 所有激光共享冷却
    private static float lastKnockbackTime = float.MinValue;
    private static float lastDamageTime = float.MinValue;

    public void SetDamage(int value)
    {
        damage = value;
    }

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.gameObject.CompareTag("Player")) return;

        // 击退：共享冷却，多激光同时击中只弹一次
        if (Time.time - lastKnockbackTime >= knockbackCooldown)
        {
            lastKnockbackTime = Time.time;

            Rigidbody2D playerRb = other.attachedRigidbody;
            if (playerRb != null)
            {
                Vector2 dir = other.transform.position.y >= transform.position.y ? Vector2.up : Vector2.down;
                StartCoroutine(KnockbackRoutine(playerRb, dir));
            }
        }

        ApplyDamageIfReady();
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.gameObject.CompareTag("Player")) return;
        ApplyDamageIfReady();
    }

    private void ApplyDamageIfReady()
    {
        if (Time.time - lastDamageTime >= damageInterval)
        {
            lastDamageTime = Time.time;
            GameManager.Instance.PlayerTakeDamage(damage);
        }
    }

    private IEnumerator KnockbackRoutine(Rigidbody2D playerRb, Vector2 dir)
    {
        float endTime = Time.time + knockbackDuration;
        while (Time.time < endTime)
        {
            if (playerRb == null) yield break;
            playerRb.velocity = new Vector2(playerRb.velocity.x, dir.y * knockbackForce);
            yield return null;
        }
    }
}
