using UnityEngine;

public class TurtlyGameManager : MonoBehaviour
{
    [Header("Config")]
    public TurtlyGameConfig gameConfig;

    [Header("References")]
    public TurtlyUIManager uiManager;

    [Header("Runtime (Read Only)")]
    [SerializeField] private TurtlyGameState gameState = new TurtlyGameState();
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        if (!gameConfig)
        {
            Debug.LogWarning("[TurtlyGameManager] No GameConfig assigned, creating a temporary one.");
            gameConfig = ScriptableObject.CreateInstance<TurtlyGameConfig>();
        }

        // Initialization
        gameState.Initialize(gameConfig.initialCoins);
        LogState("Game start");
        if (uiManager)
        {
            uiManager.UpdateCoins(gameState.Coins);
            uiManager.SetOpeningText("Opening: placeholder");
            uiManager.AppendDialogue("AI: Hi! Welcome to Turtly! Read the puzzle above and check the rule above!");
        }
    }

    #region Gameplay Buttons

    /// <summary> Ask Button: Ask a question </summary>
    public void HandleAsk(string questionText)
    {
        HandleQuestionInternal(questionText, isGuess: false);
    }

    /// <summary> Guess Button: Make a guess (same logic as Ask) </summary>
    public void HandleGuess(string guessText)
    {
        HandleQuestionInternal(guessText, isGuess: true);
    }

    /// <summary> Hint Button: Get a hint </summary>
    public void HandleHint()
    {
        if (gameState.IsBankrupt)
        {
            uiManager?.AppendDialogue("System: No enough coins to buy a hint");
            return;
        }

        if (gameState.IsHintLimitReached(gameConfig.maxHintsPerPuzzle))
        {
            uiManager?.AppendDialogue("System: Run out of hints for this puzzle");
            return;
        }

        if (gameState.Coins < gameConfig.hintCost)
        {
            uiManager?.AppendDialogue("System: No enough coins to buy a hint");
            return;
        }

        gameState.SpendCoins(gameConfig.hintCost);
        gameState.AddHintUse();
        uiManager?.UpdateCoins(gameState.Coins);

        // TODO: placeholder
        uiManager?.AppendDialogue("AI: you get a hint");

        CheckBankrupt();
    }

    /// <summary> Skip Button: Skip current puzzle and get a new one </summary>
    public void HandleSkip()
    {
        if (gameState.IsBankrupt)
        {
            uiManager?.AppendDialogue("System: No enough coin to Skip");
            return;
        }

        if (gameState.Coins < gameConfig.skipCost)
        {
            uiManager?.AppendDialogue("System: No enough coin to Skip");
            return;
        }

        gameState.SpendCoins(gameConfig.skipCost);
        gameState.NextPuzzle();
        uiManager?.UpdateCoins(gameState.Coins);

        uiManager?.AppendDialogue("System: You skipped the puzzle");
        // TODO：placeholder
        uiManager?.SetOpeningText($"Opening: This is No. {gameState.CurrentPuzzleIndex + 1}  puzzle");

        CheckBankrupt();
    }

    public void HandleRestart()
    {
        if (!gameConfig)
        {
            Debug.LogWarning("[TurtlyGameManager] No GameConfig on restart, creating a temporary one.");
            gameConfig = ScriptableObject.CreateInstance<TurtlyGameConfig>();
        }

        // Reset Game State
        gameState.Initialize(gameConfig.initialCoins);

        // Reset UI
        if (uiManager)
        {
            uiManager.ClearDialogue(); 
            uiManager.UpdateCoins(gameState.Coins);
            uiManager.SetOpeningText("Opening: This is No. 1 puzzle"); 
            uiManager.HideGameOver(); 
            uiManager.AppendDialogue("System: Game restarted!");
        }

        LogState("Game restart");
    }

    #endregion

    #region Internal Logics

    /// <summary>
    /// Ask / Guess Buttons
    /// </summary>
    private void HandleQuestionInternal(string text, bool isGuess)
    {
        if (gameState.IsBankrupt)
        {
            uiManager?.AppendDialogue("System: No enough coin to ask or guess");
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            uiManager?.AppendDialogue("System: You need to type something");
            return;
        }

        if (gameState.Coins < gameConfig.askCost)
        {
            uiManager?.AppendDialogue("System: No enough coin to ask or guess");
            return;
        }

        // Spend coins
        gameState.SpendCoins(gameConfig.askCost);
        uiManager?.UpdateCoins(gameState.Coins);

        // Display user input
        string speaker = isGuess ? "You (Guess)" : "You";
        uiManager?.AppendDialogue($"{speaker}: {text}");

        // TODO：这里之后接入 LLM，拿模型真正的回答
        uiManager?.AppendDialogue("AI: placeholder for response");

        CheckBankrupt();
    }

    /// <summary>
    /// When player solved the puzzle
    /// </summary>
    public void HandlePuzzleSolved()
    {
        gameState.MarkPuzzleSolved();

        // Calculate rewards
        int bonusFromCoins = Mathf.RoundToInt(gameState.Coins * gameConfig.rewardPerCoinLeftRatio);
        int reward = Mathf.Clamp(gameConfig.rewardBase + bonusFromCoins, gameConfig.minReward, gameConfig.maxReward);

        gameState.AddCoins(reward);
        uiManager?.UpdateCoins(gameState.Coins);

        uiManager?.AppendDialogue($"System: Congrats! You solved the puzzle and won {reward} coins");
        uiManager?.AppendDialogue($"System: Currently solved {gameState.PuzzlesSolved} puzzles");

        // Start a new puzzle
        gameState.NextPuzzle();
        uiManager?.SetOpeningText($"Opening: This is No. {gameState.CurrentPuzzleIndex + 1}  puzzle");

        LogState("Puzzle solved");
    }

    /// <summary> Check if bankrupt </summary>
    private void CheckBankrupt()
    {
        if (!gameState.IsBankrupt) return;

        uiManager?.AppendDialogue("System: You run out of coins, GAME OVER");
        uiManager?.ShowGameOver(gameState.PuzzlesSolved);
        LogState("Game over");
    }

    private void LogState(string tag)
    {
        if (gameConfig != null && !gameConfig.debugLog) return;

        Debug.Log($"[TurtlyGameManager] {tag}");
        gameState.DebugPrint();
    }

    #endregion
}
