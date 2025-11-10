using System;
using UnityEngine;

[Serializable]
public class TurtlyGameState
{
    [Header("Core State")]
    [SerializeField] private int coins;
    [SerializeField] private int currentPuzzleIndex;
    [SerializeField] private int puzzlesSolved;
    [SerializeField] private int hintsUsed;

    /// <summary> Current Coin Amount </summary>
    public int Coins => coins;

    /// <summary> Current Puzzle Index (Start from 0) </summary>
    public int CurrentPuzzleIndex => currentPuzzleIndex;

    /// <summary> Amount of Solved Puzzles </summary>
    public int PuzzlesSolved => puzzlesSolved;

    /// <summary> Amount of Used Hints in Current Puzzle </summary>
    public int HintsUsed => hintsUsed;

    /// <summary> If Already Bankrupt </summary>
    public bool IsBankrupt => coins <= 0;

    /// <summary> If Reached Max Hint Amount of Current Puzzle </summary>
    public bool IsHintLimitReached(int maxHintsPerPuzzle) => hintsUsed >= maxHintsPerPuzzle;

    /// <summary> Initialize State </summary>
    public void Initialize(int initialCoins)
    {
        coins = Mathf.Max(initialCoins, 0);
        currentPuzzleIndex = 0;
        puzzlesSolved = 0;
        hintsUsed = 0;
    }

    /// <summary> Spend Coins While Prevent Negative Amount </summary>
    public void SpendCoins(int amount)
    {
        coins -= amount;
        if (coins < 0) coins = 0;
    }

    /// <summary> Add Coins from Reward </summary>
    public void AddCoins(int amount)
    {
        coins += Mathf.Max(0, amount);
    }

    /// <summary> Reset for New Puzzle </summary>
    public void NextPuzzle()
    {
        currentPuzzleIndex++;
        hintsUsed = 0;
    }

    /// <summary> Mark Current Puzzle as Solved </summary>
    public void MarkPuzzleSolved()
    {
        puzzlesSolved++;
    }

    /// <summary> Add One Hint </summary>
    public void AddHintUse()
    {
        hintsUsed++;
    }

    /// <summary> Print Current State </summary>
    public void DebugPrint()
    {
        Debug.Log($"[GameState] Coins={coins}, Puzzle={currentPuzzleIndex}, Solved={puzzlesSolved}, Hints={hintsUsed}");
    }
}
