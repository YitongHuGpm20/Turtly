using System;
using UnityEngine;
using LLMUnity;
using Turtly;

public class TurtlyLLMBridge : MonoBehaviour
{
    [Header("LLMUnity")]
    public LLMCharacter llmChar;

    [Header("System Prompt")]
    [TextArea(3, 10)]
    public string systemPrompt =
        "You are the game master of a 'Turtle Soup' lateral thinking puzzle. " +
        "Answer briefly in English. Do NOT reveal the full hidden answer unless the player clearly guesses it. " +
        "Use at most two sentences.";
    
    public async void AskOnce(
        TurtlyPuzzle puzzle,
        string playerQuestion,
        bool isGuess,
        Action<string> onReply)
    {
        if (!llmChar)
        {
            onReply?.Invoke("(LLM not configured)");
            return;
        }

        if (puzzle == null)
        {
            onReply?.Invoke("(No puzzle loaded)");
            return;
        }
        
        string role = isGuess ? "GUESS" : "QUESTION";

        string prompt =
            systemPrompt + "\n\n" +
            $"Puzzle opening:\n{puzzle.opening}\n\n" +
            $"Hidden solution (for you only, do not reveal unless confirming a correct guess):\n{puzzle.answer}\n\n" +
            $"Player {role}:\n{playerQuestion}\n\n" +
            "If this is a GUESS, first say whether it is correct, close or wrong, " +
            "then optionally give a tiny hint. " +
            "If this is a QUESTION, answer only what is asked, " +
            "do NOT spoil the full solution.";
        
        string reply = await llmChar.Chat(prompt);

        onReply?.Invoke(reply);
    }
    
    public void Cancel()
    {
        if (llmChar)
            llmChar.CancelRequests();
    }
}