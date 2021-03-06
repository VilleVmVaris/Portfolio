﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//This is the code used for handling the movement of arrows in our 2D multiplayer action-platformer
//Some parts of the code were removed from this portfolio version, since they were not made by me

[RequireComponent(typeof(ArrowInfo))]
public class ArrowMovementScript : MonoBehaviour
{

    public int playerNumber;

    ArrowInfo arrowInfo;

    int hits;
    public float gravityTimer;
    public float fallRotationSpeed;
    float timeToGravity;

    public float gravityMultiplier;
    public float maxFallSpeed;

    public float speed;
    float origSpeed;
    float collisionCount;
    public float speedReduction;
    public float raycastLength;
    public float minAngleToGround;
    public float deflectBounce;

    bool drillArrow;
    bool drilling;
    bool hasDrilled;
    public LayerMask whatIsGround;
    public GameObject flames;
    public GameObject drillParticles;

    float arrowBounce;
    bool arrowBounced;
    bool collidedWithOtherArrow;

    [HideInInspector]
    public bool stuckToWall;


    Vector3 direction;
    Vector3 gravity;
    Vector3 arrowDeflection;

    int angle;
    [HideInInspector]
    public bool startsFalling;

    Vector3 onHitLocation;
    Quaternion onHitRotation;
    RaycastHit2D hit;

    bool arrowLeftPlayer;

    GamemanagerScript gm;

    public PolygonCollider2D flightCollider;
    public BoxCollider2D stuckCollider;

    float leftPlayerTimer = 0.2f;

    bool hasHat;
    float timeToPickable = 0.3f;
    bool pickable;

    void Start()
    {
        direction = Vector3.right;

        angle = (int)Vector3.Angle(Vector3.up, transform.right);
        arrowInfo = GetComponent<ArrowInfo>();
        origSpeed = speed;
        if(arrowInfo.GetArrowType() == arrowType.drill) {
            drillArrow = true;
        }
        var g = GameObject.Find("Gamemanager");
        if (g) {
            gm = g.GetComponent<GamemanagerScript>();
            gm.AddShotArrow(playerNumber);
        }
        else {
            print("No gamemanager found");
        }

    }

    void Update()
    {

        leftPlayerTimer -= Time.deltaTime;
        if(leftPlayerTimer < 0) {
            arrowLeftPlayer = true;
        }
        // If arrow has bounced and it's fall speed is high enough it becomes lethal again
        if (arrowBounced && gravity.y <= -40) {
            arrowBounced = false;
        }

        if (stuckToWall) {
            timeToPickable -= Time.deltaTime;
            if(timeToPickable < 0) {
                pickable = true;
            }
        }


        Movement();
    }

    //If arrows hit walls, they will either bounce off or get stuck in the obstacle. They can be picked up after getting stuck.
    public void OnTriggerEnter2D(Collider2D c)
    {
        if (c.gameObject.tag == "Obstacle")
        {
            collisionCount++;
            if (!drillArrow)
            {
                onHitRotation = gameObject.transform.rotation;
                onHitLocation = gameObject.transform.position;
                stuckToWall = true;
                Fabric.EventManager.Instance.PostEvent("Arrow/HitsObstacle");
                flightCollider.enabled = false;
                stuckCollider.enabled = true;

            }
            else if (c.gameObject.tag == "Arrow" && !stuckToWall && !collidedWithOtherArrow)
            {

                ArrowMovementScript aScript = c.gameObject.GetComponent<ArrowMovementScript>();

                if (aScript.stuckToWall)
                {
                    return;
                }
                else
                {
                    aScript.ArrowBounce();
                    collidedWithOtherArrow = true;
                    if (drillArrow)
                    {
                        hasDrilled = true;
                    }
                    ArrowBounce();
                    collidedWithOtherArrow = true;
                }
            }

        }
    }

    public void OnTriggerStay2D(Collider2D c){
      
            if (c.gameObject.tag == "Player" && (c.gameObject.GetComponent<PlayerInput>().playerNumber != playerNumber || arrowLeftPlayer)) {
                var player = c.gameObject.GetComponent<Player>();
                if (player.dodging && !player.arrowsFull) {
                    if (arrowInfo.thisArrow != arrowType.bomb) {
                        Destroy(gameObject);
                        player.AddArrow(arrowInfo.thisArrow);
                        if (!stuckToWall) {
                            gm.AddDodge(c.gameObject.GetComponent<PlayerInput>().playerNumber);
                        }
                        
                        player.UpdateArrowCounter();
                        player.DodgeIntoArrow();
                    }
                }
                else if (player.dodging && player.arrowsFull && !stuckToWall && (c.gameObject.GetComponent<PlayerInput>().playerNumber != playerNumber || arrowLeftPlayer)) {
                    Destroy(gameObject);
                    gm.AddDodge(c.gameObject.GetComponent<PlayerInput>().playerNumber);
                    player.DodgeIntoArrow();
                }
                else if (stuckToWall && !player.arrowsFull && pickable && (c.gameObject.GetComponent<PlayerInput>().playerNumber != playerNumber || arrowLeftPlayer)) {
                    if (arrowInfo.thisArrow != arrowType.bomb) {
                        Destroy(gameObject);
                        player.AddArrow(arrowInfo.thisArrow);
                        
                        player.UpdateArrowCounter();
                    }

                }
                else if (!arrowBounced && !stuckToWall && (c.gameObject.GetComponent<PlayerInput>().playerNumber != playerNumber || arrowLeftPlayer)) {
                    player.PlayerDies();
                    if (playerNumber != c.gameObject.GetComponent<PlayerInput>().playerNumber) {
                        gm.AddScore(playerNumber, 1);
                    }
                    else {
                        gm.AddScore(playerNumber, -1);
                        gm.AddSuicide(playerNumber);
                    }
                }

            }

    }
    //Handles the movement and physics of arrow flight. If an arrow is short straight up, they turn around and fall directly back down after gravity starts affecting them
    public void Movement()
    {
        if (stuckToWall) {

            gameObject.transform.position = onHitLocation;
            gameObject.transform.rotation = onHitRotation;
        }

        else {
            if (!drilling) {
                timeToGravity += Time.deltaTime;
            }

            // Handles arrow being shot straight up
            if (angle == 0) {
                if (angle == 0 && !startsFalling) {

                    if (timeToGravity > gravityTimer) {
                        transform.Rotate(new Vector3(0, 0, 1), 180);
                        startsFalling = true;
                        speed = 0;
                        gravity = Vector3.zero;
                    }
                }
                if (angle == 0 && timeToGravity + 0.3 > gravityTimer) {
                    gravity -= Vector3.up;
                }
                transform.Translate((direction * speed * Time.deltaTime));
               
                if (Mathf.Abs(gravity.y) > maxFallSpeed) {
                    gravity.y = -maxFallSpeed;
                }

                transform.Translate(gravity * gravityMultiplier, Space.World);
            }

            // With other directions the arrows slowly turn towards the ground after gravity starts affecting them
            else if (angle != 0) {
                var angleToGround = Vector3.Angle(-Vector3.right, -transform.up);

                if (timeToGravity > gravityTimer) {
                    gravity -= Vector3.up;
                    var down = Quaternion.LookRotation(Vector3.forward, Vector3.right);

                    if (angleToGround > minAngleToGround) {

                        transform.rotation = Quaternion.RotateTowards(transform.rotation, down, fallRotationSpeed * Time.deltaTime);
                    }

                }

                speed -= speedReduction;
                transform.Translate((direction * speed * Time.deltaTime));
                if(Mathf.Abs(gravity.y) > maxFallSpeed) {
                    gravity.y = -maxFallSpeed;
                }
                transform.Translate(gravity * gravityMultiplier, Space.World);
            }
        }
    }

    public void SetDirection(Vector3 newDirection)
    {
        direction = newDirection;
    }
    public void ArrowBounce()
    {
        arrowBounced = true;
        speed = 0;
        gravity = Vector3.zero;
        gravity = Vector3.up * deflectBounce;
        direction = -Vector3.right;
        speed = 5;
        timeToGravity = gravityTimer;
        Fabric.EventManager.Instance.PostEvent("Arrow/Deflect");
    }

    public void Ignite()
    {
        flames.SetActive(true);
    }

    public void RemoveHat(GameObject player){
        if (hasHat) {
            GameObject hat = this.gameObject.transform.GetChild(0).gameObject;
            HeadScript hs = hat.gameObject.GetComponent<HeadScript>();
            hasHat = false;
        }
    }
}