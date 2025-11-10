using UnityEngine;

[CreateAssetMenu(fileName = "TurtlyGameConfig", menuName = "Scriptable Objects/TurtlyGameConfig")]
public class TurtlyGameConfig : ScriptableObject
{
    [Header("Initial Settings")] 
    [Tooltip("Coins amount at game start")]
    public int initialCoins = 100;
    
    [Header("Action Costs")]
    [Tooltip("Cost amount when ask or guess")]
    public int askCost = 1;
    [Tooltip("Cost amount when get a hint")]
    public int hintCost = 10;
    [Tooltip("Cost amount when request a new puzzle")]
    public int skipCost = 20;
    
    [Header("Hint / Reward Settings")]
    [Tooltip("Max hint amount per puzzle")]
    public int maxHintsPerPuzzle = 3;
    [Tooltip("Base reward of solving a puzzle")]
    public int rewardBase = 30;
    [Tooltip("Extra reward according to remaining coins（0.5 means += remaining coins * 0.5）")]
    [Range(0f, 1f)] public float rewardPerCoinLeftRatio = 0.5f;
    [Tooltip("Min reward per puzzle")]
    public int minReward = 10;
    [Tooltip("Max reward per puzzle")]
    public int maxReward = 80;
    
    [Header("Debug Options")]
    [Tooltip("If print Coin Amount Change in console")]
    public bool coinChangeLog = true;
}
