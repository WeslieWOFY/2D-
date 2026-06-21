using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Normal,     // 正常游戏
        Paused,     // 游戏暂停
        GameOver    // 游戏结束
    }
     public static GameManager Instance { get; private set; }
    
    [Header("玩家属性")]
    public int playerMaxHealth = 100;//玩家最高生命值
    public int playerMaxMana =100;
    public int playerDefense = 5;//玩家防御
    public float stealing = 0f;//击杀回血

    public float stealbule = 0f;//击杀回MP
    public int Resurrection = 1;//可以复活次数

    public int attackPower = 5;//玩家攻击力
    
    public int attackPowerup = 10;//玩家每升一级提升的攻击力

    public int lever = 1;//玩家等级

    public static float volume=1f;
    
    [Header("当前宠物属性")]
    public string petAttackType = "Fire"; // Fire, Ice, Thunder
    
    private int playerCurrentHealth;
    private int playerCurrentMana;

    public System.Action<int, int> OnPlayerTakeDamage;
    public System.Action<int, int> OnPlayerManaChanged;
    public System.Action<int, int> Init;
    private void Awake()
    {
        Debug.Log("数据单例注册时机");
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            playerCurrentHealth = playerMaxHealth;
            playerCurrentMana = playerMaxMana;
            playerCurrentMana = playerMaxMana;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        Init?.Invoke(playerCurrentHealth,playerMaxHealth);
    }
    public int PlayerCurrentHealth
    {
        get => playerCurrentHealth;
        set => playerCurrentHealth = Mathf.Clamp(value, 0, playerMaxHealth);
    }
    
    public void PlayerTakeDamage(int damage)
    {
        int finalDamage = Mathf.Max(1, damage - playerDefense);
        playerCurrentHealth -= finalDamage;
        OnPlayerTakeDamage?.Invoke(Math.Max(playerCurrentHealth,0), playerMaxHealth);
        if (playerCurrentHealth <= 0)
        {
            playerCurrentHealth=0;
            OnPlayerDeath();
        }
    }
        // ========== 新增：恢复生命值方法 ==========
    
    /// <summary>
    /// 恢复指定血量
    /// </summary>
    /// <param name="amount">恢复量</param>
    public void HealPlayer(int amount)
    {
        int oldHealth = playerCurrentHealth;
        playerCurrentHealth = Mathf.Min(playerCurrentHealth + amount, playerMaxHealth);
        //int actualHeal = playerCurrentHealth - oldHealth;
        OnPlayerTakeDamage?.Invoke(Math.Max(playerCurrentHealth,0), playerMaxHealth);
    }

    public void FullyHealPlayer()
    {
        playerCurrentHealth = playerMaxHealth;
        //Debug.Log($"人物完全恢复，当前血量 {playerCurrentHealth}/{playerMaxHealth}");
    }

    public void HealPlayerPercentage(float percentage)
    {
        int healAmount = Mathf.RoundToInt(playerMaxHealth * Mathf.Clamp01(percentage));
        HealPlayer(healAmount);
    }
    private void OnPlayerDeath()
    {
        //Debug.Log("游戏结束");
        // 游戏结束逻辑
    }
    public bool HasEnoughMana(int amount) => playerCurrentMana >= amount;

    public bool TryUseMana(int amount)
    {
        if (playerCurrentMana < amount) return false;
        playerCurrentMana -= amount;
        OnPlayerManaChanged?.Invoke(playerCurrentMana, playerMaxMana);
        return true;
    }

    public void RestoreMana(int amount)
    {
        playerCurrentMana = Mathf.Min(playerCurrentMana + amount, playerMaxMana);
        OnPlayerManaChanged?.Invoke(playerCurrentMana, playerMaxMana);
    }

    public void FullyRestoreMana()
    {
        playerCurrentMana = playerMaxMana;
        OnPlayerManaChanged?.Invoke(playerCurrentMana, playerMaxMana);
    }

    public int PlayerCurrentMana
    {
        get => playerCurrentMana;
        set => playerCurrentMana = Mathf.Clamp(value, 0, playerMaxMana);
    }
}