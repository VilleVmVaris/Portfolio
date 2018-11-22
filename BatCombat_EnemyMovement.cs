using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//This is a rather simple script that we used in our mobile shooter game
//It moves the group of enemies in a simple pattern and increases their speed when one of them is shot down
public class EnemyMovement : MonoBehaviour {

    public float moveSpeed;
    [HideInInspector]
    public float lastWaveSpeed;
    float origSpeed;

    bool goingRight;

    Vector3 drop = new Vector3(0, -1, 0);

    public float boundaryRight;

    public float boundaryLeft;

    public Transform limitRight;

    public Transform limitLeft;

    EnemySpawner es;

    Vector3 startingPoint;

    public Transform goalThreshold;

    public Transform goal;

    public float dropSpeed;

    bool droppingDown;

    public float dropTime;

    float dropping;

    public float deathSpeedUp;


    // Use this for initialization
    void Start() {
        es = GameObject.Find("GameManager").GetComponent<EnemySpawner>();

        SwitchLimit();

        startingPoint = transform.position;
        origSpeed = moveSpeed;
        lastWaveSpeed = origSpeed;
    }

    // Update is called once per frame
    void Update() {
        if(droppingDown)
        {
            transform.Translate(drop * Time.deltaTime * dropSpeed);
            dropping += Time.deltaTime;

            if(dropping > dropTime)
            {
                droppingDown = false;

                dropping -= dropTime;
            }

        } else Move(goingRight);

        //See if boundary has been reached
        if (limitRight.transform.position.x >= boundaryRight && goingRight && es.enemiesSpawned.Count != 0)
            {

            //boundary reached, drop a level and move towards other direction
            goingRight = false;
            DropALevel();

            SwitchLimit();

        } else if(limitLeft.transform.position.x <= boundaryLeft && !goingRight && es.enemiesSpawned.Count != 0) {
            goingRight = true;
            DropALevel();
            SwitchLimit();

        }

        //print(moveSpeed);

    }

    void Move(bool movingRight)
    {
        float movement = goingRight ? moveSpeed : -moveSpeed;

        transform.Translate(new Vector3(movement * Time.deltaTime, 0, 0));

    }

    void DropALevel()
    {

        droppingDown = true;
    }
   
    // Reset speed after each wave
    public void ResetSpeed(){
        moveSpeed = lastWaveSpeed;
        //lastWaveSpeed = moveSpeed;
        print("Movespeed reset to lastwavespeed after wave, new movespeed: " + moveSpeed);
    }

    public void ResetSpeedAfterBoss(){
        //moveSpeed = origSpeed;
        lastWaveSpeed = origSpeed - es.speedIncrease;
        print("Movespeed reset after boss, new movespeed: " + moveSpeed + ", lastwavespeed updated to " + lastWaveSpeed);
    }

    // Adjust the wave's left and right limit to the leftmost and rightmost bats in the wave.
    public void SwitchLimit()
    {
        //dropSpeed = dropSpeed / 0.9f;
        //dropTime = dropTime * 0.9f;
        if (es.waveNumber % es.bossWave != 0) {
            if (es.enemiesSpawned[0] == null) {
                Debug.LogError("enemiesspawned null!?!");
            }
            else {
                float max = es.enemiesSpawned[0].transform.position.x;
                float min = es.enemiesSpawned[0].transform.position.x;


                for (int i = 0; i < es.enemiesSpawned.Count; i++) {
                    if (es.enemiesSpawned[i].transform.position.x > max) {
                        max = es.enemiesSpawned[i].transform.position.x;
                    }
                    if (es.enemiesSpawned[i].transform.position.x < min) {
                        min = es.enemiesSpawned[i].transform.position.x;

                    }

                }

                limitLeft.position = new Vector3(min, 0, 0);
                limitRight.position = new Vector3(max, 0, 0);
            }
        }


    }
     // Increase speed when bat dies
    public void IncreaseSpeed(){
        moveSpeed = moveSpeed * deathSpeedUp;
        print("Move speed increased after bat died, new speed: " + moveSpeed);
    }

    // Return bat wave gameobject to the top of screen
    public void ReturnToStart() {

        transform.position = startingPoint;

    }

    // Increase speed after boss fight
    public void IncreaseOrigSpeed()
    {
        origSpeed += 0.5f;
        print("Orig speed increased, new origspeed: " + origSpeed + ", movespeed: " + moveSpeed);
    }

}
