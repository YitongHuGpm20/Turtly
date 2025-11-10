using System.Xml;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurtlyUIManager : MonoBehaviour
{
    [Header("References")]
    public TurtlyGameManager gameManager;

    [Header("Header UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI coinsText;

    [Header("Dialogue UI")]
    public TextMeshProUGUI openingText;
    public Transform dialogueContainer;
    public GameObject dialoguePrefab;
    public ScrollRect dialogueScrollRect;

    [Header("Input UI")]
    public TMP_InputField questionInput;
    public Button askButton;
    public Button guessButton;
    public Button hintButton;
    public Button skipButton;

    [Header("Help UI")]
    public Button helpButton;
    public GameObject helpPanel;
    public Button helpCloseButton;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverText;

    private void Awake()
    {
        if (helpPanel)  helpPanel.SetActive(false);
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (titleText && string.IsNullOrEmpty(titleText.text))
            titleText.text = "Turtly";
    }

    private void Start()
    {
        // Bind button click functions
        if (askButton)  askButton.onClick.AddListener(OnAskButtonClicked);
        if (guessButton)  guessButton.onClick.AddListener(OnGuessButtonClicked);
        if (hintButton)  hintButton.onClick.AddListener(OnHintButtonClicked);
        if (skipButton)  skipButton.onClick.AddListener(OnSkipButtonClicked);
        if (helpButton)  helpButton.onClick.AddListener(OnHelpButtonClicked);
        if (helpCloseButton)  helpCloseButton.onClick.AddListener(HideHelpPanel);
    }

    #region Button Click Functions

    public void OnAskButtonClicked()
    {
        if (!gameManager) return;

        string text = questionInput != null ? questionInput.text : string.Empty;
        gameManager.HandleAsk(text);

        // Clear input box after used
        if (questionInput)  questionInput.text = string.Empty;
    }

    public void OnGuessButtonClicked()
    {
        if (!gameManager) return;

        string text = questionInput != null ? questionInput.text : string.Empty;
        gameManager.HandleGuess(text);

        if (questionInput)  questionInput.text = string.Empty;
    }

    public void OnHintButtonClicked()
    {
        if (!gameManager) return;
        gameManager.HandleHint();
    }

    public void OnSkipButtonClicked()
    {
        if (!gameManager) return;
        gameManager.HandleSkip();
    }

    public void OnHelpButtonClicked()
    {
        ShowHelpPanel();
    }

    #endregion

    #region Public methods for Game Manager
    
    public void UpdateCoins(int coins)
    {
        if (coinsText)  coinsText.text = $"Coins: {coins}";
    }
    
    public void SetOpeningText(string text)
    {
        if (openingText)  openingText.text = text;
    }

    /// <summary> Add a new dialogue line to Dialogue Box </summary>
    public void AppendDialogue(string line)
    {
        if (!dialogueContainer || !dialoguePrefab) return;

        // Create new line
        GameObject dialogueLine = Instantiate(dialoguePrefab, dialogueContainer);
        TextMeshProUGUI dialogueLineText = dialogueLine.GetComponentInChildren<TextMeshProUGUI>();
        if (dialogueLineText) dialogueLineText.text = line;
        
        // Scroll to bottom
        LayoutRebuilder.ForceRebuildLayoutImmediate(dialogueContainer.GetComponent<RectTransform>());
        if (dialogueScrollRect)
        {
            Canvas.ForceUpdateCanvases();
            dialogueScrollRect.verticalNormalizedPosition = 0f; // 0 = bottom
        }
    }
    
    public void ShowGameOver(int solvedCount)
    {
        if (gameOverPanel)  gameOverPanel.SetActive(true);
        if (gameOverText)  gameOverText.text = $"Game Over\nYou have solved {solvedCount} puzzles";
    }

    #endregion

    #region Help Panel

    private void ShowHelpPanel()
    {
        if (!helpPanel) return;
        helpPanel.SetActive(true);

        // 如果帮助面板里有 Text，可以在这里动态写规则；
        // 或者你直接在场景里写死。
    }

    private void HideHelpPanel()
    {
        if (!helpPanel) return;
        helpPanel.SetActive(false);
    }

    #endregion
}
