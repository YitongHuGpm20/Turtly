using System;
using UnityEngine;
using LLMUnity;
using Turtly;

public class TurtlyLLMBridge : MonoBehaviour
{
    [Header("LLMUnity")]
    public LLMCharacter llmChar;

    [Header("Turtle Soup System Prompt")]
    [TextArea(5, 15)]
    public string systemPrompt =
        // 这是给 LLMCharacter 的真正 system prompt（通过 SetPrompt 塞进去）
        "You are the referee for a 'Turtle Soup' lateral thinking puzzle.\n" +
        "You know the hidden true story (solution), but you must NEVER reveal, quote, or describe it.\n" +
        "Your only job is to judge the player's questions and guesses.\n\n" +
        "RULES:\n" +
        "- You will receive the puzzle text and the hidden solution in the user message.\n" +
        "- Use the hidden solution only for internal reasoning.\n" +
        "- Do NOT repeat or summarize the puzzle text.\n" +
        "- Do NOT repeat or rephrase the player's question.\n" +
        "- Do NOT explain the rules or your internal reasoning to the player.\n" +
        "- Keep answers very short.\n\n" +
        "For a QUESTION about the situation:\n" +
        "- Decide if the statement is true, false, or impossible to tell.\n" +
        "- Then output exactly one final word: 'yes', 'no', or 'irrelevant'.\n\n" +
        "For a GUESS about the solution:\n" +
        "- Decide if the guess is exactly correct, close but not exact, or wrong.\n" +
        "- Then output exactly one final word: 'correct', 'close', or 'wrong'.\n\n" +
        "Format:\n" +
        "1) You may think step by step inside <think>...</think> tags.\n" +
        "2) On a new line after </think>, output exactly ONE WORD only (yes/no/irrelevant/correct/close/wrong).\n" +
        "3) Output nothing else after that one word. No extra text, no explanation.\n";

    private bool _promptInitialized = false;

    private void Awake()
    {
        // 把上面的 systemPrompt 真正塞进 LLMCharacter 里
        InitLLMPrompt();
    }

    private void OnEnable()
    {
        InitLLMPrompt();
    }

    private void InitLLMPrompt()
    {
        if (_promptInitialized) return;
        if (!llmChar) return;

        // 用官方提供的 SetPrompt，清空旧 chat，把 system message 换成我们的规则
        llmChar.SetPrompt(systemPrompt, clearChat: true);
        _promptInitialized = true;
    }

    /// <summary>
    /// UI 调用这个：问一次 / 猜一次
    /// </summary>
    public async void AskOnce(
        TurtlyPuzzle puzzle,
        string playerText,
        bool isGuess,
        Action<string> onReply)
    {
        if (onReply == null) return;

        if (!llmChar)
        {
            onReply("(LLM not configured)");
            return;
        }

        if (puzzle == null)
        {
            onReply("(No puzzle loaded)");
            return;
        }

        if (string.IsNullOrWhiteSpace(playerText))
        {
            onReply("(Please type something)");
            return;
        }

        InitLLMPrompt(); // 确保每次调用前 system prompt 已经正确设置

        string role = isGuess ? "GUESS" : "QUESTION";

        // 这里的 query 会作为“user message”加进 chat 里
        // 真正的 system prompt 已经在 LLMCharacter.chat[0] 里了（上面的 SetPrompt）
        string query =
            "PUZZLE_TEXT:\n" +
            puzzle.opening + "\n\n" +
            "HIDDEN_SOLUTION (for your reasoning only, NEVER reveal this):\n" +
            puzzle.answer + "\n\n" +
            "PLAYER_" + role + ":\n" +
            playerText + "\n\n" +
            "Remember: think in <think>...</think>, then output exactly ONE final word on a new line.";

        string rawReply;
        try
        {
            // 关键点：addToHistory = false 让每次问答独立，不积累一大堆历史
            rawReply = await llmChar.Chat(query, callback: null, completionCallback: null, addToHistory: false);
        }
        catch (Exception e)
        {
            Debug.LogError("LLM Chat error: " + e);
            onReply("(Error talking to AI)");
            return;
        }

        if (rawReply == null)
            rawReply = string.Empty;

        // Debug：需要的话打开
        // Debug.Log("RAW LLM REPLY:\n" + rawReply);

        string judged = ExtractJudgement(rawReply, isGuess);

        if (string.IsNullOrWhiteSpace(judged))
            judged = isGuess ? "wrong" : "irrelevant"; // 最后兜底

        onReply(judged);
    }

    /// <summary>
    /// 从 LLM 的整段输出里，抽出 yes/no/irrelevant/correct/close/wrong 这个词
    /// 忽略它复述的所有信息，只要判定结果。
    /// </summary>
    private string ExtractJudgement(string reply, bool isGuess)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return string.Empty;

        string r = reply;

        // 1）删掉 <think>...</think> （模型内部推理）
        int thinkStart = r.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        if (thinkStart >= 0)
        {
            int thinkEnd = r.IndexOf("</think>", thinkStart + 7, StringComparison.OrdinalIgnoreCase);
            if (thinkEnd > thinkStart)
            {
                int removeLen = thinkEnd + "</think>".Length - thinkStart;
                r = r.Remove(thinkStart, removeLen);
            }
        }

        // 2）去掉流式标记
        r = r.Replace("<im_start>", "", StringComparison.OrdinalIgnoreCase)
             .Replace("</im_start>", "", StringComparison.OrdinalIgnoreCase)
             .Replace("<im_end>", "", StringComparison.OrdinalIgnoreCase)
             .Replace("</im_end>", "", StringComparison.OrdinalIgnoreCase);

        // 3）取最后一行（通常规范的实现会在最后一行给那个单词）
        string[] lines = r.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        string lastLine = lines.Length > 0 ? lines[lines.Length - 1].Trim() : r.Trim();

        if (string.IsNullOrWhiteSpace(lastLine))
            lastLine = r.Trim();

        string firstWord = GetFirstWord(lastLine).ToLowerInvariant();

        string[] questionWords = { "yes", "no", "irrelevant" };
        string[] guessWords    = { "correct", "close", "wrong" };
        string[] allowed       = isGuess ? guessWords : questionWords;

        // 4）先看首词是不是合法
        foreach (var w in allowed)
        {
            if (firstWord == w)
                return w;
        }

        // 5）不行就在最后一行里搜
        string lower = lastLine.ToLowerInvariant();
        foreach (var w in allowed)
        {
            if (lower.Contains(w))
                return w;
        }

        // 6）再不行就在整段里搜
        lower = r.ToLowerInvariant();
        foreach (var w in allowed)
        {
            if (lower.Contains(w))
                return w;
        }

        // 7）实在找不到，就给个默认
        return isGuess ? "wrong" : "irrelevant";
    }

    private string GetFirstWord(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var parts = text.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    /// <summary>
    /// 可选：在切场景 / 停止游戏时取消请求
    /// </summary>
    public void Cancel()
    {
        if (llmChar)
            llmChar.CancelRequests();
    }
}
