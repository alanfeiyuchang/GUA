using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    readonly bool[] joined = new bool[4];

    public int JoinedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < joined.Length; i++)
                if (joined[i]) count++;
            return count;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool IsJoined(int playerIndex)
    {
        return playerIndex >= 0 && playerIndex < joined.Length && joined[playerIndex];
    }

    public void Join(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < joined.Length)
            joined[playerIndex] = true;
    }

    public static void EnsureExists()
    {
        if (Instance != null) return;
        var go = new GameObject("GameManager");
        go.AddComponent<GameManager>();
    }
}
