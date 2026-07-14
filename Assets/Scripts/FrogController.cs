using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FrogController : MonoBehaviour
{
    enum FrogState { Normal, Swallowed }

    [Header("Player")]
    [Tooltip("0-3. Maps to Gamepad.all[playerIndex]. Player 0 also gets keyboard/mouse.")]
    public int playerIndex = 0;
    public bool useKeyboardFallback = true;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float jumpForce = 9f;
    public float groundCheckRadius = 0.15f;
    public Transform groundCheck;
    public LayerMask groundLayer;

    [Header("Eat / Spit")]
    public Transform mouthPivot;
    public float spitForce = 12f;
    public float swallowCooldown = 0.5f;
    public float swallowedScaleFactor = 0.6f;

    [Header("Tongue")]
    public Transform tonguePivot;
    public Transform tongueVisual;
    public float swallowHopForce = 3f;
    public float growthPerSwallow = 0.12f;
    public float aimStickDeadzone = 0.2f;
    [Tooltip("Max upward angle (degrees) the tongue can aim, measured from horizontal on the facing side.")]
    public float maxTongueAngle = 40f;
    [Tooltip("Max downward angle (degrees) the tongue can aim, measured from horizontal on the facing side.")]
    public float maxDownwardTongueAngle = 35f;
    [Tooltip("The tongue artwork's own inherent diagonal tilt (degrees) baked into its pixels, measured from horizontal.")]
    public float tongueArtRestAngle = 37.05f;

    [Header("Sprites")]
    public Sprite closeMouthSprite;
    public Sprite openMouthSprite;

    [Header("Wall Cling")]
    public float wallCheckDistance = 0.5f;
    public float wallSlideSpeed = -1.5f;

    [Header("Fall / Respawn")]
    public float fallDeathY = -6f;
    public float respawnDropHeight = 6f;

    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    SpriteRenderer tongueVisualRenderer;
    FrogState state = FrogState.Normal;
    readonly List<FrogController> swallowedFrogs = new List<FrogController>();
    Vector2 aimDirection = Vector2.right;
    Vector2 launchDirection = Vector2.right;
    bool tongueHeld;
    float facing = 1f;
    float mouthPivotBaseX;
    Vector3 baseScale;
    Vector3 tongueVisualBaseScale = Vector3.one;
    int baseSortingOrder;
    Vector2 lastSafePosition;
    float invulnerableUntil = -1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        baseScale = transform.localScale;
        if (sr != null) baseSortingOrder = sr.sortingOrder;
        lastSafePosition = transform.position;
        if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Ground");
        if (mouthPivot != null) mouthPivotBaseX = mouthPivot.localPosition.x;
        if (tongueVisual != null)
        {
            tongueVisualBaseScale = tongueVisual.localScale;
            tongueVisualRenderer = tongueVisual.GetComponent<SpriteRenderer>();
        }
        // Sprites are pre-colored art — force white so no leftover prefab tint
        // (e.g. from an early placeholder) multiplies into the actual sprite colors.
        if (sr != null)
        {
            sr.color = Color.white;
            if (closeMouthSprite != null) sr.sprite = closeMouthSprite;
        }
        SetTongueActive(false);
    }

    void Update()
    {
        if (state == FrogState.Swallowed) return;

        ReadInput(out float horizontal, out bool jumpPressed, out bool tongueHeldNow, out Vector2 aimInput);

        if (Mathf.Abs(horizontal) > 0.01f)
            SetFacing(Mathf.Sign(horizontal));

        rb.linearVelocityX = horizontal * moveSpeed;

        bool grounded = groundCheck != null &&
            Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (jumpPressed && grounded)
            rb.linearVelocityY = jumpForce;

        if (grounded)
        {
            lastSafePosition = transform.position;
            if (sr != null) sr.sortingOrder = baseSortingOrder;
        }

        // Keep the tongue rendering behind this frog's own body, always relative to
        // whatever the body's current sortingOrder is (base, or lowered after a spit).
        if (tongueVisualRenderer != null && sr != null)
            tongueVisualRenderer.sortingOrder = sr.sortingOrder - 1;

        ApplyWallSlide(horizontal, grounded);

        if (transform.position.y < fallDeathY)
            Respawn();

        if (aimInput.sqrMagnitude > 0.0001f)
            aimDirection = aimInput.normalized;

        UpdateTongueAim();

        if (tongueHeldNow && !tongueHeld)
        {
            SetTongueActive(true);
        }
        else if (!tongueHeldNow && tongueHeld)
        {
            SetTongueActive(false);
            SpitAll();
        }
        tongueHeld = tongueHeldNow;
    }

    void ApplyWallSlide(float horizontal, bool grounded)
    {
        if (grounded) return;
        if (Mathf.Abs(horizontal) < 0.01f) return;
        if (rb.linearVelocityY >= wallSlideSpeed) return;

        float dir = Mathf.Sign(horizontal);
        bool touchingWall = Physics2D.Raycast(transform.position, new Vector2(dir, 0f), wallCheckDistance, groundLayer);

        if (touchingWall)
            rb.linearVelocityY = wallSlideSpeed;
    }

    void Respawn()
    {
        transform.position = lastSafePosition + Vector2.up * respawnDropHeight;
        rb.linearVelocity = Vector2.zero;

        if (tongueHeld)
        {
            SetTongueActive(false);
            tongueHeld = false;
        }
    }

    void SetFacing(float dir)
    {
        facing = dir;
        if (sr != null) sr.flipX = facing < 0f;
        if (mouthPivot != null)
        {
            Vector3 p = mouthPivot.localPosition;
            p.x = Mathf.Abs(mouthPivotBaseX) * facing;
            mouthPivot.localPosition = p;
        }
    }

    void SetTongueActive(bool active)
    {
        if (tonguePivot != null) tonguePivot.gameObject.SetActive(active);
        if (sr != null)
        {
            Sprite target = active ? openMouthSprite : closeMouthSprite;
            if (target != null) sr.sprite = target;
        }
    }

    void UpdateTongueAim()
    {
        if (tonguePivot == null) return;

        // Horizontal side is locked to the frog's facing direction (not the raw aim
        // vector's own sign) — the tongue always points the way the frog is facing.
        // Only the vertical tilt is free, clamped to [0, maxTongueAngle] above horizontal.
        float side = facing >= 0f ? 1f : -1f;

        float facingRelativeX = aimDirection.x * side;
        float aimAngle = Mathf.Atan2(aimDirection.y, facingRelativeX) * Mathf.Rad2Deg;
        float clampedAngle = Mathf.Clamp(aimAngle, -maxDownwardTongueAngle, maxTongueAngle);
        float worldAngle = side > 0f ? clampedAngle : 180f - clampedAngle;

        tonguePivot.rotation = Quaternion.Euler(0f, 0f, worldAngle);

        // Cache the actual clamped/mirrored direction so Spit() launches frogs exactly
        // where the tongue visually points, instead of the raw (unclamped) aim input.
        float worldAngleRad = worldAngle * Mathf.Deg2Rad;
        launchDirection = new Vector2(Mathf.Cos(worldAngleRad), Mathf.Sin(worldAngleRad));

        if (tongueVisual != null)
        {
            // Facing left: mirror the sprite across X (scale flip) instead of rotating
            // it 180 — a rotation would make an asymmetric sprite look upside-down,
            // a scale flip keeps it a true left/right mirror. The art's own inherent
            // diagonal tilt also mirrors under the flip, so its compensating rotation
            // must flip from -restAngle to -(180-restAngle) to still cancel out to 0.
            tongueVisual.localScale = new Vector3(tongueVisualBaseScale.x * side, tongueVisualBaseScale.y, tongueVisualBaseScale.z);
            float artCompensation = side > 0f ? -tongueArtRestAngle : -(180f - tongueArtRestAngle);
            tongueVisual.localRotation = Quaternion.Euler(0f, 0f, artCompensation);
        }
    }

    void ReadInput(out float horizontal, out bool jumpPressed, out bool tongueHeldOut, out Vector2 aimInput)
    {
        horizontal = 0f;
        jumpPressed = false;
        tongueHeldOut = false;
        aimInput = Vector2.zero;

        // Slot 0 is reserved for keyboard/mouse only; gamepads map to slots 1-3
        // (Gamepad.all[0] -> playerIndex 1, etc.) so a single connected controller
        // never doubles up on the same frog as the keyboard.
        if (playerIndex > 0)
        {
            var pads = Gamepad.all;
            int gamepadIndex = playerIndex - 1;
            if (gamepadIndex < pads.Count)
            {
                var pad = pads[gamepadIndex];
                float x = pad.leftStick.x.ReadValue();
                if (Mathf.Abs(x) > 0.2f) horizontal = x;
                jumpPressed |= pad.buttonSouth.wasPressedThisFrame;
                tongueHeldOut |= pad.rightTrigger.isPressed;

                Vector2 rightStick = pad.rightStick.ReadValue();
                if (rightStick.sqrMagnitude > aimStickDeadzone * aimStickDeadzone)
                    aimInput = rightStick;
            }
        }

        if (useKeyboardFallback && playerIndex == 0)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed) horizontal = -1f;
                if (kb.dKey.isPressed) horizontal = 1f;
                jumpPressed |= kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                tongueHeldOut |= mouse.leftButton.isPressed;

                var cam = Camera.main;
                if (cam != null)
                {
                    Vector2 screenPos = mouse.position.ReadValue();
                    Vector3 camPos = cam.transform.position;
                    Vector3 worldPoint = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -camPos.z));
                    Vector2 origin = tonguePivot != null ? (Vector2)tonguePivot.position : (Vector2)transform.position;
                    Vector2 dir = (Vector2)worldPoint - origin;
                    if (dir.sqrMagnitude > 0.0001f) aimInput = dir;
                }
            }
        }
    }

    void Swallow(FrogController other)
    {
        swallowedFrogs.Add(other);
        other.state = FrogState.Swallowed;
        other.rb.simulated = false;
        if (other.col != null) other.col.enabled = false;
        if (other.sr != null) other.sr.enabled = false;

        Transform anchor = mouthPivot != null ? mouthPivot : transform;
        other.transform.SetParent(anchor);
        other.transform.localPosition = Vector3.zero;
        // Absolute assignment from other's own cached original scale — SetParent
        // would otherwise bake in a compensated scale that drifts once this frog's
        // own scale changes (via UpdateGrowthVisual) while the child stays parented.
        other.transform.localScale = other.baseScale * swallowedScaleFactor;

        rb.linearVelocityY = swallowHopForce;
        UpdateGrowthVisual();
    }

    void SpitAll()
    {
        if (swallowedFrogs.Count == 0) return;

        Transform anchor = mouthPivot != null ? mouthPivot : transform;
        Vector2 dir = launchDirection;

        for (int i = 0; i < swallowedFrogs.Count; i++)
        {
            FrogController launched = swallowedFrogs[i];
            launched.transform.SetParent(null);
            launched.transform.position = (Vector2)anchor.position + dir * (0.5f + i * 0.3f);
            // Absolute restore — never derive from the current (possibly SetParent-distorted) scale.
            launched.transform.localScale = launched.baseScale;

            launched.state = FrogState.Normal;
            if (launched.col != null) launched.col.enabled = true;
            if (launched.sr != null)
            {
                launched.sr.enabled = true;
                // Render behind the spitting frog, not in front of it.
                if (sr != null) launched.sr.sortingOrder = sr.sortingOrder - 1;
            }
            launched.rb.simulated = true;
            launched.rb.linearVelocity = dir * spitForce;
            launched.invulnerableUntil = Time.time + swallowCooldown;
        }

        swallowedFrogs.Clear();
        UpdateGrowthVisual();
        invulnerableUntil = Time.time + swallowCooldown;
    }

    public void TryAbsorb(FrogController other)
    {
        if (state != FrogState.Normal) return;
        if (Time.time < invulnerableUntil) return;
        if (other == null || other == this) return;
        if (other.state != FrogState.Normal) return;
        if (Time.time < other.invulnerableUntil) return;

        Swallow(other);
    }

    void UpdateGrowthVisual()
    {
        float mult = 1f + growthPerSwallow * swallowedFrogs.Count;
        transform.localScale = baseScale * mult;
    }
}
