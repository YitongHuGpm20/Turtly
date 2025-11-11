using UnityEngine;
using LLMUnity;

public class LLMTest : MonoBehaviour
{
    public LLMCharacter llmChar;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            TestChat();
    }

    private async void TestChat()
    {
        if (!llmChar)
        {
            Debug.LogWarning("[LLMTest] llmChar is not assigned!");
            return;
        }

        string message = "Hi! Say one short sentence.";
        Debug.Log("[LLMTest] Sending: " + message);

        //_ = llmChar.Chat(message, HandleReply);
        string reply = await llmChar.Chat(message);
        Debug.Log("[LLMTest] AI replied: " + reply);
    }

    private void HandleReply(string reply)
    {
        Debug.Log("[LLMTest] AI replied: " + reply);
    }
}
