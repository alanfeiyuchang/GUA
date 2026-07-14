using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LevelController : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("If true, unjoined players can press a button to join and spawn here (lobby). If false, everyone already joined just spawns immediately.")]
    public bool isHomeLobby = false;
    [Tooltip("Minimum joined players required before the end zone can trigger a level start (only enforced in the lobby).")]
    public int minPlayersToStart = 2;
    public string nextSceneName = "Level1";
    public FrogController frogPrefab;
    public Transform[] spawnPoints = new Transform[4];
    public EndZoneTrigger endZone;

    [Header("Per-player visuals (index 0-3)")]
    public Sprite[] closeSprites = new Sprite[4];
    public Sprite[] openSprites = new Sprite[4];

    [Header("UI (lobby only)")]
    public Text joinStatusText;
    public GameObject readyBanner;

    readonly FrogController[] activeFrogs = new FrogController[4];
    bool transitioning;

    void Start()
    {
        GameManager.EnsureExists();

        if (isHomeLobby)
        {
            RefreshJoinUI();
        }
        else
        {
            for (int i = 0; i < 4; i++)
                if (GameManager.Instance.IsJoined(i))
                    SpawnFrog(i);
        }
    }

    void Update()
    {
        if (transitioning) return;

        // Defends against GameManager.Instance being wiped by a domain reload
        // (e.g. scripts recompiling while still in Play mode).
        if (GameManager.Instance == null) GameManager.EnsureExists();

        if (isHomeLobby)
            HandleJoining();

        CheckEndZone();
    }

    void HandleJoining()
    {
        var pads = Gamepad.all;

        for (int i = 0; i < 4; i++)
        {
            if (GameManager.Instance.IsJoined(i)) continue;

            bool pressed = false;

            // Slot 0 = keyboard/mouse only. Slots 1-3 map to Gamepad.all[0..2] —
            // matches FrogController's input mapping so keyboard and a single
            // connected controller never join into (or control) the same slot.
            if (i == 0)
            {
                var kb = Keyboard.current;
                if (kb != null)
                    pressed |= kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;
            }
            else
            {
                int gamepadIndex = i - 1;
                if (gamepadIndex < pads.Count)
                    pressed |= pads[gamepadIndex].buttonSouth.wasPressedThisFrame;
            }

            if (pressed)
            {
                GameManager.Instance.Join(i);
                SpawnFrog(i);
                RefreshJoinUI();
            }
        }
    }

    void SpawnFrog(int playerIndex)
    {
        if (frogPrefab == null) return;
        if (activeFrogs[playerIndex] != null) return;

        Transform spawn = playerIndex < spawnPoints.Length ? spawnPoints[playerIndex] : null;
        Vector3 pos = spawn != null ? spawn.position : Vector3.zero;

        FrogController frog = Instantiate(frogPrefab, pos, Quaternion.identity);
        frog.playerIndex = playerIndex;

        Sprite closeSprite = playerIndex < closeSprites.Length ? closeSprites[playerIndex] : null;
        Sprite openSprite = playerIndex < openSprites.Length ? openSprites[playerIndex] : null;
        frog.closeMouthSprite = closeSprite;
        frog.openMouthSprite = openSprite;

        // Awake() already ran during Instantiate and cached the prefab-default sprite
        // before we set the fields above, so apply the correct one directly too.
        var sr = frog.GetComponent<SpriteRenderer>();
        if (sr != null && closeSprite != null) sr.sprite = closeSprite;

        activeFrogs[playerIndex] = frog;
    }

    void RefreshJoinUI()
    {
        int count = GameManager.Instance.JoinedCount;

        if (joinStatusText != null)
            joinStatusText.text = "Players Joined: " + count + "/4\nP1: Space/Enter (keyboard)   P2-P4: Gamepad A/South";

        if (readyBanner != null)
            readyBanner.SetActive(count >= minPlayersToStart);
    }

    void CheckEndZone()
    {
        if (endZone == null) return;

        var present = new List<FrogController>();
        for (int i = 0; i < 4; i++)
            if (activeFrogs[i] != null) present.Add(activeFrogs[i]);

        if (present.Count == 0) return;
        if (isHomeLobby && present.Count < minPlayersToStart) return;

        if (endZone.ContainsAllFrogs(present))
        {
            transitioning = true;
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
