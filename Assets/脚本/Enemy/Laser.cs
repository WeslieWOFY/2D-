using UnityEngine;
using System.Collections;

/// <summary>
/// 激光：触发器检测，持续伤害 + 击退，多激光共享冷却。
/// 支持垂直击退（横版激光，上下弹）和水平击退（竖版激光，左右弹）。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Laser : MonoBehaviour
{
    public enum KnockbackMode
    {
        Vertical,   // 横版激光：上下弹开（默认，不影响已有用法）
        Horizontal  // 竖版激光：左右弹开
    }

    [Header("伤害")]
    [SerializeField] private int damage = 10;
    [SerializeField] private float damageInterval = 0.5f;

    [Header("击退")]
    [SerializeField] private KnockbackMode knockbackMode = KnockbackMode.Vertical;
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

        if (Time.time - lastKnockbackTime >= knockbackCooldown)
        {
            lastKnockbackTime = Time.time;

            Rigidbody2D playerRb = other.attachedRigidbody;
            if (playerRb != null)
            {
                Vector2 dir = GetKnockbackDirection(other.transform.position);
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

    private Vector2 GetKnockbackDirection(Vector3 playerPos)
    {
        if (knockbackMode == KnockbackMode.Horizontal)
        {
            return playerPos.x >= transform.position.x ? Vector2.right : Vector2.left;
        }
        else
        {
            return playerPos.y >= transform.position.y ? Vector2.up : Vector2.down;
        }
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
            playerRb.velocity = new Vector2(dir.x * knockbackForce, dir.y * knockbackForce);
            yield return null;
        }
    }
}
