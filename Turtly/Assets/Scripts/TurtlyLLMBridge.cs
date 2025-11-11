using System;
using UnityEngine;
using LLMUnity;
using Turtly;

public class TurtlyLLMBridge : MonoBehaviour
{
    [Header("LLMUnity")]
    public LLMCharacter llmChar;

    [Header("Referee base instruction (kept short)")]
    [TextArea(3, 6)]
    public string shortInstruction =
        "You are a referee for a 'Turtle Soup' puzzle. Use the FACTS to judge the player's statements. " +
        "If the LLM output is unclear, a rule-based engine will decide.";

    [Header("Optional: extra instruction appended to the query")]
    [TextArea(8, 20)]
    public string extraInstruction =
        "GENERAL RULES:\n" +
        "- Treat each line in FACTS as absolutely true.\n" +
        "- Base judgment ONLY on FACTS. Do not invent facts.\n" +
        "- Do NOT reveal, quote, or describe the hidden solution.\n" +
        "- Do not repeat the puzzle text or the player's question.\n\n" +
        "OUTPUT RULE:\n" +
        "- For QUESTIONS: output exactly one of {yes, no, irrelevant}.\n" +
        "- For GUESSES: output exactly one of {correct, close, wrong}.\n" +
        "- Place the label between <ANSWER> and </ANSWER> with nothing else outside those tags.\n" +
        "You may optionally include internal reasoning inside <think>...</think>, but the ONLY visible output must be <ANSWER>...</ANSWER>.";

    private void Awake()
    {
        if (!llmChar)
            Debug.LogWarning("TurtlyLLMBridge: llmChar not set in Inspector.");
    }

    /// <summary>
    /// UI 调用的一次问答接口
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

        string labelFromLLM = null;
        string rawReply = null;

        // 1) 尝试先用 LLM 判定
        try
        {
            string role = isGuess ? "GUESS" : "QUESTION";

            // factsBlock
            string factsBlock = "";
            if (puzzle.facts != null && puzzle.facts.Length > 0)
            {
                factsBlock = "FACTS (use ONLY these facts; do not invent anything):\n";
                foreach (var fact in puzzle.facts)
                {
                    if (!string.IsNullOrWhiteSpace(fact))
                        factsBlock += "- " + fact.Trim() + "\n";
                }
            }
            else
            {
                factsBlock =
                    "FACTS (approx from answer):\n- " +
                    (string.IsNullOrWhiteSpace(puzzle.answer) ? "(no answer provided)" : puzzle.answer.Trim()) + "\n";
            }

            string query =
                shortInstruction + "\n\n" +
                extraInstruction + "\n\n" +
                "PUZZLE TEXT:\n" + puzzle.opening + "\n\n" +
                factsBlock + "\n" +
                "PLAYER_" + role + ":\n" + playerText + "\n\n" +
                "FINAL OUTPUT (required):\n" +
                "Think in <think>...</think> if needed.\n" +
                "Then output EXACTLY one label between <ANSWER> and </ANSWER>.\n" +
                "Allowed labels:\n" +
                (isGuess ? "- correct, close, wrong\n" : "- yes, no, irrelevant\n") +
                "Example: <ANSWER>yes</ANSWER>\n" +
                "DO NOT output any other visible text outside the <ANSWER> tags.";

            int oldNumPredict = llmChar.numPredict;
            bool oldStream = llmChar.stream;

            if (llmChar.numPredict <= 0) llmChar.numPredict = 128;
            llmChar.stream = false; // 简化处理：非流式

            rawReply = await llmChar.Chat(query, callback: null, completionCallback: null, addToHistory: false);

            llmChar.numPredict = oldNumPredict;
            llmChar.stream = oldStream;

            if (rawReply == null) rawReply = string.Empty;

            Debug.Log("RAW LLM REPLY:\n" + rawReply);

            // 先试标签解析
            labelFromLLM = ExtractBetweenMarker(rawReply, "<ANSWER>", "</ANSWER>");
            if (string.IsNullOrWhiteSpace(labelFromLLM))
            {
                // 再试老的 heuristic
                labelFromLLM = ExtractJudgement(rawReply, isGuess);
            }

            // 如果 raw 基本等于 prompt 或完全没关键词，当作 LLM 失效
            if (IsBasicallyPromptEcho(rawReply, llmChar.prompt))
            {
                Debug.LogWarning("LLM reply looks like prompt echo, will use rule-based judge.");
                labelFromLLM = null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("LLM Chat error: " + e);
            labelFromLLM = null;
        }

        string finalLabel;

        if (!string.IsNullOrWhiteSpace(labelFromLLM))
        {
            // LLM 成功返回了可解析标签
            finalLabel = NormalizeLabel(labelFromLLM, isGuess);
        }
        else
        {
            // 2) LLM 失败 / 只回 prompt：启用规则判定
            finalLabel = RuleBasedJudge(puzzle, playerText, isGuess);
        }

        if (string.IsNullOrWhiteSpace(finalLabel))
            finalLabel = isGuess ? "wrong" : "irrelevant";

        onReply(finalLabel);
    }

    /// <summary>
    /// 判断 LLM 返回是否只是 prompt 的 echo
    /// </summary>
    private bool IsBasicallyPromptEcho(string reply, string prompt)
    {
        if (string.IsNullOrWhiteSpace(reply)) return true;
        if (string.IsNullOrWhiteSpace(prompt)) return false;

        string r = reply.Trim();
        string p = prompt.Trim();

        if (r.Length <= 0) return true;

        // 如果几乎完全一样（长度差少、前缀相同），视为 echo
        if (r.StartsWith(p.Substring(0, Math.Min(p.Length, 40)), StringComparison.OrdinalIgnoreCase))
        {
            int diff = Math.Abs(r.Length - p.Length);
            if (diff < 40) return true;
        }

        return false;
    }

    private string NormalizeLabel(string label, bool isGuess)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        label = label.Trim().ToLowerInvariant();

        if (!isGuess)
        {
            if (label.Contains("yes")) return "yes";
            if (label.Contains("no")) return "no";
            if (label.Contains("irrelevant")) return "irrelevant";
        }
        else
        {
            if (label.Contains("correct")) return "correct";
            if (label.Contains("close")) return "close";
            if (label.Contains("wrong")) return "wrong";
        }

        return null;
    }

    private string ExtractBetweenMarker(string text, string startMarker, string endMarker)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        string lower = text.ToLowerInvariant();
        string s = startMarker.ToLowerInvariant();
        string e = endMarker.ToLowerInvariant();

        int idxStart = lower.IndexOf(s, StringComparison.Ordinal);
        int idxEnd = lower.IndexOf(e, StringComparison.Ordinal);

        if (idxStart >= 0 && idxEnd > idxStart)
        {
            int actualStart = idxStart + s.Length;
            string extracted = text.Substring(actualStart, idxEnd - actualStart).Trim();
            return extracted.ToLowerInvariant();
        }

        return null;
    }

    /// <summary>
    /// 老的 heuristic：删 <think>，取最后一行关键字
    /// </summary>
    private string ExtractJudgement(string reply, bool isGuess)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return string.Empty;

        string r = reply;

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

        r = r.Replace("<im_start>", "", StringComparison.OrdinalIgnoreCase)
             .Replace("</im_start>", "", StringComparison.OrdinalIgnoreCase)
             .Replace("<im_end>", "", StringComparison.OrdinalIgnoreCase)
             .Replace("</im_end>", "", StringComparison.OrdinalIgnoreCase);

        string[] lines = r.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        string lastLine = lines.Length > 0 ? lines[lines.Length - 1].Trim() : r.Trim();

        if (string.IsNullOrWhiteSpace(lastLine))
            lastLine = r.Trim();

        string firstWord = GetFirstWord(lastLine).ToLowerInvariant();

        string[] questionWords = { "yes", "no", "irrelevant" };
        string[] guessWords = { "correct", "close", "wrong" };
        string[] allowed = isGuess ? guessWords : questionWords;

        foreach (var w in allowed)
        {
            if (firstWord == w) return w;
        }

        string lowerLast = lastLine.ToLowerInvariant();
        foreach (var w in allowed)
        {
            if (lowerLast.Contains(w)) return w;
        }

        string lowerAll = r.ToLowerInvariant();
        foreach (var w in allowed)
        {
            if (lowerAll.Contains(w)) return w;
        }

        return string.Empty;
    }

    private string GetFirstWord(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var parts = text.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    /// <summary>
    /// 规则判定：当 LLM 失效时，用 facts + 简单匹配回答
    /// （先粗暴支持“第五个人抬棺材”这题，再给出通用 fallback）
    /// </summary>
    private string RuleBasedJudge(TurtlyPuzzle puzzle, string playerText, bool isGuess)
    {
        string q = playerText.ToLowerInvariant();

        if (isGuess)
        {
            // 简单：如果猜测里包含 coffin / corpse / pallbearer 等关键词，就认为 close，否则 wrong
            bool mentionsCoffin = q.Contains("coffin");
            bool mentionsCorpse = q.Contains("corpse") || q.Contains("dead");
            bool mentionsPallbearer = q.Contains("pallbearer") || q.Contains("carry");

            if (mentionsCoffin && mentionsCorpse && mentionsPallbearer)
                return "correct";
            if (mentionsCoffin || mentionsCorpse || mentionsPallbearer)
                return "close";
            return "wrong";
        }

        // 以下是针对第一题的直觉规则（包含这些 fact 的题大概率就是抬棺材这类）
        bool hasCorpseFact = HasFactContaining(puzzle, "corpse");
        bool hasCoffinFact = HasFactContaining(puzzle, "coffin");

        if (hasCorpseFact && hasCoffinFact)
        {
            // 1) 是否活着
            if (q.Contains("alive") || q.Contains("still alive") || q.Contains("living"))
                return "no";
            if (q.Contains("dead") || q.Contains("a corpse"))
                return "yes";

            // 2) 是否在棺材里面
            if (q.Contains("inside") && (q.Contains("coffin") || q.Contains("box")))
                return "yes";
            if (q.Contains("outside") && q.Contains("coffin"))
                return "no";

            // 3) 是否被雨淋湿
            if (q.Contains("wet") || q.Contains("drenched") || q.Contains("get wet") || q.Contains("got wet"))
                return "no"; // 第五人没淋湿
            if (q.Contains("dry") || q.Contains("stay dry") || q.Contains("completely dry"))
                return "yes";

            // 4) 是否走路 / 自己在走
            if (q.Contains("walking") || q.Contains("walk by himself") || q.Contains("walk by himself"))
                return "no"; // 尸体没有自己走

            // 5) 有没有伞
            if (q.Contains("umbrella"))
                return "irrelevant";

            // 6) 是否五个人都活着
            if (q.Contains("all five") && (q.Contains("alive") || q.Contains("living")))
                return "no";
        }

        // 通用 fallback：如果问题明显在问 facts 里有的关键词，则 yes，否则 irrelevant
        if (FactsContainKeywords(puzzle, q))
            return "yes";

        return "irrelevant";
    }

    private bool HasFactContaining(TurtlyPuzzle puzzle, string keyword)
    {
        if (puzzle.facts == null) return false;
        foreach (var f in puzzle.facts)
        {
            if (!string.IsNullOrWhiteSpace(f) &&
                f.ToLowerInvariant().Contains(keyword.ToLowerInvariant()))
                return true;
        }
        return false;
    }

    private bool FactsContainKeywords(TurtlyPuzzle puzzle, string questionLower)
    {
        if (puzzle.facts == null) return false;
        foreach (var f in puzzle.facts)
        {
            if (string.IsNullOrWhiteSpace(f)) continue;
            string fl = f.ToLowerInvariant();
            // 如果问题里的关键词都出现在某条 fact 中，简单认为 yes
            string[] words = questionLower.Split(new[] { ' ', '?', ',', '.', '!' }, StringSplitOptions.RemoveEmptyEntries);
            int match = 0;
            foreach (var w in words)
            {
                if (w.Length <= 3) continue; // 过滤太短的词
                if (fl.Contains(w)) match++;
            }
            if (match >= 2) return true;
        }
        return false;
    }

    public void Cancel()
    {
        if (llmChar != null) llmChar.CancelRequests();
    }
}
