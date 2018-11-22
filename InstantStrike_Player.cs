using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Anima2D;

//This is the player controller script for our 2D action-platformer project
//The movement controllers are based on Sebastian Lague's tutorials on 2D movement, link here: https://www.youtube.com/watch?v=MbWK8bCAU2w
//Note: The script also included animation handling for the player, but I have removed these parts from the portfolio version, since they weren't made by me
[RequireComponent(typeof(Controller2D))]
public class Player : MonoBehaviour, Damageable
{
    BoxCollider2D collider;
    public int health;
    GameManager gm;

    public float maxJumpHeight = 4;
    public float minJumpHeight = 1;
    public float timeToJumpApex = .4f;
    public float accelerationTimeAirborne = .2f;
    public float accelerationTimeGrounded = .1f;
    public float wallSlideSpeedMax = 3;
    public float wallSlideSpeedMultiplier;
    public Vector2 wallJumpClimb;
    public Vector2 wallJumpOff;
    public Vector2 wallLeap;
    public float wallStickTime = .25f;
    float timeToWallUnstick;
    bool wallSliding;
    int wallDirX;
    int maxHealth;
    bool dead;

    [HideInInspector]
    public bool crouching;
    bool midAir;
    bool justJumped;
    bool canDoubleJump;

    float origColliderX;
    float origColliderY;
    float colliderOffSet;
    float crouchColliderY;

    public float attackDurationTicks;

    public GameObject groundAttackObject;
    public GameObject chargeAttackObject;
	public GameObject ninjaSprite;
	public GameObject dashAttack;
	public ParticleSystem attackEffect;
	public ParticleSystem hitGlow;
	public Material slash;
	public Material chargeSlash;
	public ParticleSystem stepSandCloud;
	ParticleSystemRenderer attackEffectRenerer;

	SpriteMeshInstance[] sprites;
	Color originalColor;
	Color hitFlashColor = new Color(.9f, .3f, .3f, 1f);

	public GameObject swordChargeEffect;
    bool swordCharged;

    [HideInInspector]
    public bool chargingSword;

    public float chargeTime;
    float charged;
    public float timeBeforeCharge;

    [HideInInspector]
    public float timeToCharge;

    Vector3 velocity;
    float gravity = -20;

    public float moveSpeed = 6;
    float velocityXSmoothing;
	public float maxWalkSpeed = 4;

	Controller2D controller2D;

    Vector2 directionalInput;

    float maxJumpVelocity = 8;
    float minJumpVelocity;

	Animator animator;
    TimerManager timer;
	GUIManager gui;
	AudioManager sounds;
	CameraFollow cameraFollow;

    [HideInInspector]
    public BladeDash dash;

    [HideInInspector]
    public bool knockedBack;

    float knockForce;

    Vector2 knockDirection;

    float knockDuration;


    void Start()
    {
        controller2D = GetComponent<Controller2D>();

        gravity = -(2 * maxJumpHeight) / Mathf.Pow(timeToJumpApex, 2);
        maxJumpVelocity = Mathf.Abs(gravity) * timeToJumpApex;
        minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(gravity) * minJumpHeight);

        dash = GetComponent<BladeDash>();

        print("Gravity: " + gravity + " Jump velocity: " + maxJumpVelocity);

        timer = GameObject.Find("GameManager").GetComponent<TimerManager>();
		gui = GameObject.Find("GameCanvas").GetComponent<GUIManager>();
        gm = GameObject.Find("GameManager").GetComponent<GameManager>();
        collider = GetComponent<BoxCollider2D>();
		sounds = GameObject.Find("Audio").GetComponent<AudioManager>();
		cameraFollow = Camera.main.GetComponent<CameraFollow>();
        origColliderX = collider.size.x;
        origColliderY = collider.size.y;
        crouchColliderY = collider.size.y / 2;
        colliderOffSet = -collider.size.y / 4;
    
		animator = GetComponentInChildren<Animator>();

		attackEffectRenerer = attackEffect.GetComponent<ParticleSystemRenderer>();
		sprites = ninjaSprite.GetComponentsInChildren<SpriteMeshInstance>();
		originalColor = sprites[0].color;
		swordChargeEffect.SetActive(false);
		stepSandCloud.Stop();
        maxHealth = health;
	}


    void Update()
    {

        if (gm.state != GameManager.Gamestate.Paused)
        {
            //see if character has moved since last frame
            if (dash.dashing)
            {
                if (controller2D.collisions.left || controller2D.collisions.right)
                {
                    dash.StopDash();
                    EndDashAttackEffect();
                }
            }

            
            if (dash.aiming)
            {
                velocity = Vector2.zero;
            }

            CalculateVelocity();
            HandleWallSliding();
            RotateY();

			if (health <= 0) {
				
				controller2D.Move(Vector2.down * 8f * Time.deltaTime, directionalInput);
				return; 
			}

            if (CheckCollisionStatus())
            {
                canDoubleJump = true;
            }
            else
            {
                PlayInAirAnimation();
            }

            if (dash.dashing)
            {
                controller2D.Move(dash.direction * dash.speed * Time.deltaTime, directionalInput);

            }
            else if (knockedBack)
            {
                controller2D.Move(knockDirection * knockForce * Time.deltaTime, directionalInput);
                timer.Once(StopKnockBack, knockDuration);
            }

            else
            {
                controller2D.Move(velocity * Time.deltaTime, directionalInput);
            }

            //This stops gravity from accumulating if the controllers detects collisions above or below or while dashing
            if (controller2D.collisions.above || controller2D.collisions.below || dash.dashing)
            {
                if (controller2D.collisions.slidingDownMaxSlope)
                {
                    velocity.y += controller2D.collisions.slopeNormal.y * -gravity * Time.deltaTime;
                }
                else
                {
                    velocity.y = 0;

                    

                }

            }

            if (chargingSword)
            {
                charged += Time.deltaTime;
                if (charged >= chargeTime)
                {
                    swordCharged = true;
                    swordChargeEffect.SetActive(true);
                }
            }

			cameraFollow.SetChrouchMode(crouching);
        }
    }
    //Attacking enable's player hitbox in the direction that they are holding on the movement
    public void Attack(Vector2 input)
    {

		if (!dash.dashing && !dash.aiming && !knockedBack && !dead) {
			if ((input.x == 0 && input.y == 0) || crouching) {
				input.x = controller2D.collisions.faceDir;
				input.y = 0;
			} if (wallSliding)
            {
                input.x = -controller2D.collisions.faceDir;
            }

			int attackDir = (int)(Vector2.Angle(input, Vector3.up));

			if (input.x < 0) {
				attackDir = 360 - attackDir;
			}

			attackDir = (attackDir + 22) / 45 * 45 % 360 - 90;
			 
			PlaySlashEffect(attackDir);
			sounds.Slash();

			groundAttackObject.transform.rotation = Quaternion.Euler(0, 0, -attackDir);
            groundAttackObject.GetComponent<BoxCollider2D>().enabled = true;
			timer.Once(EndNormalAttackEffect, attackDurationTicks);
		}
    }
    //Holding down attack creates a bigger, stronger attack hitbox
    public void ChargedAttack(Vector2 input)
    {
        if(!dash.dashing && !dash.aiming && !knockedBack && !dead) { 

        if (swordCharged)
        {
            if ((input.x == 0 && input.y == 0) || crouching)
            {
                input.x = controller2D.collisions.faceDir;
                input.y = 0;
            }

            if (wallSliding)
                {
                    
                    input.x = -controller2D.collisions.faceDir;
                }

            int attackDir = (int)(Vector2.Angle(input, Vector3.up));

            if (input.x < 0)
            {
                attackDir = 360 - attackDir;
            }

            attackDir = (attackDir + 22) / 45 * 45 % 360 - 90;

			PlaySlashEffect(attackDir, true);
			sounds.chargeSlash.Play();

            chargeAttackObject.transform.rotation = Quaternion.Euler(0, 0, -attackDir);

			chargeAttackObject.GetComponent<BoxCollider2D>().enabled = true;
			swordChargeEffect.SetActive(false);

            timer.Once(EndChargeAttackEffect, attackDurationTicks);

            ResetCharge();

        }
        else
        {
            ResetCharge();
        }

    }
    }

	

    public bool CheckCollisionStatus()
    {
        if (!controller2D.collisions.below && !controller2D.collisions.right && !controller2D.collisions.left)
        {
            return false;
        }
        else return true;
    }



    void EnableCrouch()
    {
        justJumped = false;
    }

    public void StartChargingSword()
    {
        chargingSword = true;

    }

    public void ResetCharge()
    {
        timeToCharge = 0;
        charged = 0;
        chargingSword = false;
        swordCharged = false;

    }

	public void BladeDashAttack(Vector2 input) {
        if(!dash.onCooldown && !dead) { 
		if (input == Vector2.zero) {
			input.x = controller2D.collisions.faceDir;
		}

		int attackDir = (int)(Vector2.Angle(input, Vector3.up));

		if (input.x < 0) {
			attackDir = 360 - attackDir;
		}

		attackDir = (attackDir + 22) / 45 * 45 % 360 - 90;
		dashAttack.transform.rotation = Quaternion.Euler(0, 0, -attackDir);
		dashAttack.SetActive(true);
		timer.Once(EndDashAttackEffect, dash.dashTicks);
	}
    }

    public void EndNormalAttackEffect()
    {
        groundAttackObject.GetComponent<BoxCollider2D>().enabled = false;
    }

    public void EndChargeAttackEffect()
    {

            chargeAttackObject.GetComponent<BoxCollider2D>().enabled = false; ;
    }

    public void EndDashAttackEffect()
    {
        dashAttack.SetActive(false);
    }

    void CalculateVelocity()
    {
        float targetVelocityX = directionalInput.x * moveSpeed;

        //Smoothen change from initial velocity to target velocity, by default making it slower while airborne but faster while grounded
        //Will want to test this and see what feels better
        velocity.x = Mathf.SmoothDamp(velocity.x, targetVelocityX, ref velocityXSmoothing, (controller2D.collisions.below) ? accelerationTimeGrounded : accelerationTimeAirborne);

        velocity.y += gravity * Time.deltaTime;
    }

    void HandleWallSliding()
    {
        wallDirX = (controller2D.collisions.left) ? -1 : 1;

        //Check for collisions on sides and below and make sure velocity.y is negative to enable wall slide
        wallSliding = false;
        if ((controller2D.collisions.left || controller2D.collisions.right) && !controller2D.collisions.below && velocity.y < 0 && controller2D.collisions.collisionLayer == 9)
        {
            wallSliding = true;

            if (velocity.y < -wallSlideSpeedMax)
            {
                velocity.y = -wallSlideSpeedMax;
            }

            if(directionalInput.y < -0.5)
            {
                velocity.y *= wallSlideSpeedMultiplier;
            }

            if (timeToWallUnstick > 0)
            {
                velocityXSmoothing = 0;
                velocity.x = 0;

                if (directionalInput.x != wallDirX && directionalInput.x != 0)
                {
                    timeToWallUnstick -= Time.deltaTime;
                }
                else
                {
                    timeToWallUnstick = wallStickTime;
                }
            }
            else
            {
                timeToWallUnstick = wallStickTime;
            }
        }

    }

    public void Crouch()
    {
        if (controller2D.collisions.below && crouching == false && !justJumped)
        {

            crouching = true;

            var crouchCollider = new Vector2(collider.size.x, crouchColliderY);

            var offset = colliderOffSet;

            collider.size = crouchCollider;

            collider.offset = new Vector2(0, offset);

        }

    }

    public void StandUp()
    {
        if (crouching == true)
        {
            var origSize = new Vector2(origColliderX, origColliderY);
            collider.size = origSize;
            collider.offset = new Vector2(0, 0);
            crouching = false;

        }
    }

    public void SetDirectionalInput(Vector2 input)
    {

        if (crouching)
        {
            input.x = 0;
        }

        directionalInput = input;
    }

    public void JumpInputDown()
    {
        if (wallSliding)
        {

            //Wall-hop is direction of the wall is equal to the input, as in player is holding towards the wall
            //Might want to change later so that input doesn't have to be exactly 1?
            //Will want to test out different values
            if (Mathf.Abs(wallDirX - directionalInput.x) < 0.5f)
            {
                velocity.x = -wallDirX * wallJumpClimb.x;
                velocity.y = wallJumpClimb.y;
            }
            else if (directionalInput.x == 0)
            {
                velocity.x = -wallDirX * wallJumpOff.x;
                velocity.y = wallJumpOff.y;
            }
            else
            {
                velocity.x = -wallDirX * wallLeap.x;
                velocity.y = wallLeap.y;
            }

        }
        if (controller2D.collisions.below)
        {
            if (controller2D.collisions.slidingDownMaxSlope)
            {
                if (directionalInput.x != -Mathf.Sign(controller2D.collisions.slopeNormal.x))
                { // not jumping against max slope
                    velocity.y = maxJumpVelocity * controller2D.collisions.slopeNormal.y;
                    velocity.x = maxJumpVelocity * controller2D.collisions.slopeNormal.x;
                }
            }
            else
            {
				
                if (controller2D.canFallThrough)
                {
                    controller2D.collisions.fallingThroughPlatform = true;
                    Invoke("ResetFallingThroughPlatform", .15f);
                    Invoke("StandUp", .15f);

                }
                else
                {
                    StandUp();
                    velocity.y = maxJumpVelocity;
                    justJumped = true;
                    Invoke("EnableCrouch", .1f);
                }
            }

        } else if (!CheckCollisionStatus() && canDoubleJump)
        {
            velocity.y = maxJumpVelocity;
            canDoubleJump = false;
        }

    }

    public void JumpInputUp()
    {
        if (velocity.y > minJumpVelocity)
        {
            velocity.y = minJumpVelocity;
        }
    }

    void ResetFallingThroughPlatform()
    {
        controller2D.collisions.fallingThroughPlatform = false;
    }

	// Rotates player sprite to movement direction
	void RotateY() { 
		if (velocity.x < -0.1f || (wallSliding && controller2D.collisions.right) ) {
			ninjaSprite.transform.rotation = Helpers.FlipYRotation;
			var effect = attackEffect.main;
			effect.startRotationY = Mathf.Deg2Rad * 180f;
			
		} else if (velocity.x > 0.1f || (wallSliding && controller2D.collisions.left) ) {
			ninjaSprite.transform.rotation = Quaternion.identity;
			var effect = attackEffect.main;
			effect.startRotationY = 0f;
			
		}
	}
    
	public bool TakeDamage(int damage) {
		if (dash.dashing) {
			// Invunerable during blade dash
			return false;
		}
        health -= damage;
		gui.SetHealth(health);
		foreach (var sprite in sprites) {
			sprite.color = hitFlashColor;
		}
		sounds.hit.Play();
		timer.Once(FlashSpriteColorBack, .1f);
        if (health <= 0) {
            print("Player died");
            Die();
			return true;
		} else {
			return false;
		} 

    }

    public void GetHealed(int amount)
    {
        health += amount;
        if(health > maxHealth)
        {
            health = maxHealth;
        }
        gui.SetHealth(health);
    }

	void FlashSpriteColorBack() {
		foreach (var sprite in sprites) {
			sprite.color = originalColor;
		}
	}

    public void KnockBack(Vector2 direction, float force, float duration)
    {
        if(!dash.dashing) { 
        knockDirection = direction;
        knockDuration = duration;
        knockForce = force;
        knockedBack = true;
        }
    }

    void StopKnockBack()
    {
        knockedBack = false;
    }

	public void Die() {
		if (!dead) {
			dead = true;
			timer.Once(gm.LoadGame, 20);
		}
	}

	public void Restart() {
		controller2D.enabled = true;
        dead = false;

	}

	public void SetStepDustEffect(bool active) {
		stepSandCloud.gameObject.SetActive(active);
	}

	public void AttackHitEnemy() {
		hitGlow.Play();
	}
}


