using UnityEngine;
using Turtly;

public class TurtlyGameManager : MonoBehaviour
{
    [Header("Config")]
    public TurtlyGameConfig gameConfig;

    [Header("References")]
    public TurtlyUIManager uiManager;
    public TurtlyPuzzleDatabase puzzleDatabase;
    public TurtlyLLMBridge llmBridge;

    [Header("Runtime (Read Only)")]
    [SerializeField] private TurtlyGameState gameState = new TurtlyGameState();

    private TurtlyPuzzle curPuzzle;
    
    private void Start()
    {
        if (!gameConfig)
        {
            Debug.LogWarning("[TurtlyGameManager] No GameConfig assigned, creating a temporary one.");
            gameConfig = ScriptableObject.CreateInstance<TurtlyGameConfig>();
        }

        // 初始化金币等
        gameState.Initialize(gameConfig.initialCoins);
        uiManager?.UpdateCoins(gameState.Coins);

        // 从题库加载当前题目
        LoadCurrentPuzzle();

        if (uiManager) uiManager.gameManager = this;

        LogState("Game Start");
    }

    private void LoadCurrentPuzzle()
    {
        if (puzzleDatabase == null || puzzleDatabase.Count == 0)
        {
            curPuzzle = null;
            uiManager?.SetOpeningText("No puzzles in database. Please add some to TurtlyPuzzleDatabase.");
            return;
        }

        int index = gameState.CurrentPuzzleIndex;
        curPuzzle = puzzleDatabase.GetPuzzleByIndex(index);

        // ⚠️ 这里不能写 if (!curPuzzle)，要用 == null
        if (curPuzzle == null)
        {
            uiManager?.SetOpeningText($"Invalid puzzle index: {index}");
            return;
        }

        string titlePrefix = $"Puzzle {index + 1}";
        //string titlePrefix = string.IsNullOrEmpty(curPuzzle.title)
            //? $"Puzzle {index + 1}\n"
            //: $"Puzzle {index + 1}: {curPuzzle.title}\n";

        uiManager?.SetOpeningText(titlePrefix + curPuzzle.opening);
    }
    
    #region Gameplay Buttons

    public void HandleAsk(string questionText)
    {
        HandleQuestionInternal(questionText, isGuess: false);
    }

    public void HandleGuess(string guessText)
    {
        HandleQuestionInternal(guessText, isGuess: true);
    }

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

        // TODO: 现在还没接题库的 hint，这里先占位
        uiManager?.AppendDialogue("AI: you get a hint");

        CheckBankrupt();
    }

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
        uiManager?.UpdateCoins(gameState.Coins);

        uiManager?.AppendDialogue("System: You skipped this puzzle.");

        gameState.NextPuzzle();
        gameState.ResetHints();

        uiManager?.ClearDialogue();
        LoadCurrentPuzzle();

        LogState("Skip puzzle");
        CheckBankrupt();
    }

    public void HandleRestart()
    {
        if (!gameConfig)
        {
            Debug.LogWarning("[TurtlyGameManager] No GameConfig on restart, creating a temporary one.");
            gameConfig = ScriptableObject.CreateInstance<TurtlyGameConfig>();
        }

        gameState.Initialize(gameConfig.initialCoins);

        if (uiManager)
        {
            uiManager.ClearDialogue();
            uiManager.UpdateCoins(gameState.Coins);
            LoadCurrentPuzzle();             // 用题库重新设置开场
            uiManager.HideGameOver();
            uiManager.AppendDialogue("System: Game restarted!");
        }

        LogState("Game restart");
    }

    #endregion

    #region Internal Logics

    private async void HandleQuestionInternal(string text, bool isGuess)
    {
        if (gameState.IsBankrupt)
        {
            uiManager?.AppendDialogue("System: You are bankrupt, cannot ask or guess.");
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            uiManager?.AppendDialogue("System: Please type something.");
            return;
        }

        if (gameState.Coins < gameConfig.askCost)
        {
            uiManager?.AppendDialogue("System: Not enough coins to ask or guess.");
            return;
        }

        if (curPuzzle == null)
        {
            uiManager?.AppendDialogue("System: No puzzle loaded.");
            return;
        }

        gameState.SpendCoins(gameConfig.askCost);
        uiManager?.UpdateCoins(gameState.Coins);

        string speaker = isGuess ? "You (Guess)" : "You";
        uiManager?.AppendDialogue($"{speaker}: {text}");

        uiManager?.AppendDialogue("AI: Thinking...");
        uiManager?.SetInputInteractable(false);

        if (llmBridge != null)
        {
            string aiReply = null;
            bool done = false;

            llmBridge.AskOnce(curPuzzle, text, isGuess, reply =>
            {
                aiReply = reply;
                done = true;
            });

            while (!done)
            {
                await System.Threading.Tasks.Task.Yield();
            }

            uiManager?.AppendDialogue("AI: " + aiReply);
        }
        else
        {
            uiManager?.AppendDialogue("AI: (LLM not connected, using placeholder answer.)");
        }

        uiManager?.SetInputInteractable(true);

        CheckBankrupt();
    }

    public void HandlePuzzleSolved()
    {
        gameState.MarkPuzzleSolved();

        int bonusFromCoins = Mathf.RoundToInt(gameState.Coins * gameConfig.rewardPerCoinLeftRatio);
        int reward = Mathf.Clamp(gameConfig.rewardBase + bonusFromCoins, gameConfig.minReward, gameConfig.maxReward);

        gameState.AddCoins(reward);
        uiManager?.UpdateCoins(gameState.Coins);

        uiManager?.AppendDialogue($"System: Congrats! You solved the puzzle and won {reward} coins");
        uiManager?.AppendDialogue($"System: Currently solved {gameState.PuzzlesSolved} puzzles");

        gameState.NextPuzzle();
        uiManager?.SetOpeningText($"Opening: This is No. {gameState.CurrentPuzzleIndex + 1}  puzzle");

        LogState("Puzzle solved");
    }

    private void CheckBankrupt()
    {
        if (gameState.Coins <= 0)
        {
            uiManager?.AppendDialogue("System: You are bankrupt. Game over.");
            uiManager?.ShowGameOver(gameState.PuzzlesSolved);
            LogState("Game over");
        }
    }

    private void LogState(string tag)
    {
        if (gameConfig && !gameConfig.debugLog) return;

        Debug.Log($"[TurtlyGameManager] {tag}");
        gameState.DebugPrint();
    }

    #endregion
}
