using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Horizontal Movement Settings:")]
    [SerializeField] private float walkSpeed = 1;
    [SerializeField] private float acceleration = 0.08f;
    [SerializeField] private float deceleration = 0.06f;
    private float velocityXSmoothing;
    [Space(5)]


    [Header("Vertical Movement Settings")]
    [SerializeField] private float jumpForce = 45f;
    private int jumpBufferCounter = 0;
    [SerializeField] private int jumpBufferFrames;
    private float coyoteTimeCounter = 0;
    [SerializeField] private float coyoteTime;
    private int airJumpCounter = 0;
    [SerializeField] private int maxAirJumps;
    [Space(5)]


    [Header("Climb Settings")]
    [SerializeField] private float climbSpeed = 3f;
    private bool isOnLadder;
    private float yAxis;
    [Space(5)]


    [Header("Interact Settings")]
    [SerializeField] private Transform interactCheckPoint;
    [SerializeField] private float interactRadius = 1f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private GameObject interactPrompt;
    private IInteractable currentInteractable;
    [Space(5)]


    [Header("Ground Check Settings:")]
    [SerializeField] private Transform groundCheckPoint;
    [SerializeField] private float groundCheckY = 0.2f;
    [SerializeField] private float groundCheckX = 0.5f;
    [SerializeField] private LayerMask whatIsGround;
    [Space(5)]


    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed;
    [SerializeField] private float dashTime;
    [SerializeField] private float dashCooldown;
    [SerializeField] GameObject dashEffect;
    [Space(5)]

    PlayerStateList pState;
    private Rigidbody2D rb;
    private float xAxis;
    private float gravity;
    Animator anim;
    private bool canDash = true;
    private bool dashed;


    public static PlayerController Instance;

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }


    // Start is called before the first frame update
    void Start()
    {
        pState = GetComponent<PlayerStateList>();

        rb = GetComponent<Rigidbody2D>();

        anim = GetComponent<Animator>();

        gravity = rb.gravityScale;
    }

    // Update is called once per frame
    void Update()
    {
        GetInputs();
        UpdateJumpVariables();
        UpdateInteraction();

        if (pState.dashing) return;
        Flip();
        Move();
        HandleClimb();
        Jump();
        StartDash();
    }

    void GetInputs()
    {
        xAxis = Input.GetAxisRaw("Horizontal");
        yAxis = Input.GetAxisRaw("Vertical");
    }

    void UpdateInteraction()
    {
        if (interactCheckPoint == null)
        {
            interactCheckPoint = transform;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(interactCheckPoint.position, interactRadius, interactableLayer);
        IInteractable nearestInteractable = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            IInteractable interactable = GetInteractableFromCollider(hits[i]);

            if (interactable == null)
            {
                continue;
            }

            float distance = Vector2.Distance(interactCheckPoint.position, hits[i].transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestInteractable = interactable;
            }
        }

        currentInteractable = nearestInteractable;

        if (interactPrompt != null)
        {
            interactPrompt.SetActive(currentInteractable != null);
        }

        if (currentInteractable != null && Input.GetKeyDown(KeyCode.E))
        {
            currentInteractable.Interact();
        }
    }

    IInteractable GetInteractableFromCollider(Collider2D collider2D)
    {
        MonoBehaviour[] selfComponents = collider2D.GetComponents<MonoBehaviour>();
        for (int i = 0; i < selfComponents.Length; i++)
        {
            if (selfComponents[i] is IInteractable interactable)
            {
                return interactable;
            }
        }

        MonoBehaviour[] parentComponents = collider2D.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < parentComponents.Length; i++)
        {
            if (parentComponents[i] is IInteractable interactable)
            {
                return interactable;
            }
        }

        return null;
    }

    void Flip()
    {
        if (xAxis < 0)
        {
            transform.localScale = new Vector2(-1, transform.localScale.y);
        }
        else if (xAxis > 0)
        {
            transform.localScale = new Vector2(1, transform.localScale.y);
        }
    }

    private void Move()
    {
        float targetSpeed = xAxis * walkSpeed;
        float smoothTime = Mathf.Abs(xAxis) > 0.01f ? acceleration : deceleration;
        float newXVelocity = Mathf.SmoothDamp(rb.velocity.x, targetSpeed, ref velocityXSmoothing, smoothTime);

        rb.velocity = new Vector2(newXVelocity, rb.velocity.y);
        anim.SetBool("Walking", rb.velocity.x != 0 && Grounded());
    }

    void StartDash()
    {
        if (isOnLadder) return;

        if(Input.GetButtonDown("Dash") && canDash && !dashed)
        {
            StartCoroutine(Dash());
            dashed = true;
        }

        if (Grounded())
        {
            dashed = false;
        }
    }

    IEnumerator Dash()
    {
        canDash = false;
        pState.dashing = true;
        anim.SetTrigger("Dashing");
        rb.gravityScale = 0;
        rb.velocity = new Vector2(transform.localScale.x * dashSpeed, 0);
        if (Grounded()) Instantiate(dashEffect, transform);
        yield return new WaitForSeconds(dashTime);
        rb.gravityScale = gravity;
        pState.dashing = false;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    public bool Grounded()
    {
        if (Physics2D.Raycast(groundCheckPoint.position, Vector2.down, groundCheckY, whatIsGround) 
            || Physics2D.Raycast(groundCheckPoint.position + new Vector3(groundCheckX, 0, 0), Vector2.down, groundCheckY, whatIsGround) 
            || Physics2D.Raycast(groundCheckPoint.position + new Vector3(-groundCheckX, 0, 0), Vector2.down, groundCheckY, whatIsGround))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    void Jump()
    {
        if (isOnLadder)
        {
            pState.jumping = false;
            return;
        }

        if (!pState.jumping)
        {
            if (jumpBufferCounter > 0 && coyoteTimeCounter > 0)
            {
                rb.velocity = new Vector3(rb.velocity.x, jumpForce);

                pState.jumping = true;
            }
            else if(!Grounded() && airJumpCounter < maxAirJumps && Input.GetButtonDown("Jump"))
            {
                pState.jumping = true;

                airJumpCounter++;

                rb.velocity = new Vector3(rb.velocity.x, jumpForce);
            }
        }

        if (Input.GetButtonUp("Jump") && rb.velocity.y > 0)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0);

            pState.jumping = false;
        }

        anim.SetBool("Jumping", !Grounded());
    }

    void UpdateJumpVariables()
    {
        if (isOnLadder)
        {
            pState.jumping = false;
            coyoteTimeCounter = 0;
            jumpBufferCounter = 0;
            return;
        }

        if (Grounded())
        {
            pState.jumping = false;
            coyoteTimeCounter = coyoteTime;
            airJumpCounter = 0;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferFrames;
        }
        else
        {
            jumpBufferCounter--;
        }
    }

    void HandleClimb()
    {
        if (isOnLadder)
        {
            rb.gravityScale = 0;
            rb.velocity = new Vector2(rb.velocity.x, yAxis * climbSpeed);
            return;
        }

        rb.gravityScale = gravity;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Ladder"))
        {
            isOnLadder = true;
            dashed = false;
        }
    }

    void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Ladder"))
        {
            isOnLadder = false;
            rb.gravityScale = gravity;
        }
    }
}
