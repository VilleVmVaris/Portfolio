using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class GuardBehaviour : MonoBehaviour {

    //This is a script used in our stealth project to manage the behaviour of the enemy units
    //The project was done under a fairly strict time limit, so the code is a bit messy and there are some unnecessary parts to it, but it did its job considering the time limit
    //The guards' navigation uses Unity's NavMesh, and their vision was done throuhg raycasting

    public enum GuardType {
        Regular,
        Reinforcement
    }

    LevelState levelstate;

    public List<PlayerCharacter> players;

    public List<GuardBehaviour> guards;

    public GuardType type;
    public GameObject alarmEffect;
    public GameObject searchEffect;

	[HideInInspector]
	public Animator animator;

    public float attackRange;

    public float chaseSpeed;
    public float searchTime;

    public float alarmTurn;
    float ogTurns;

    public enum GuardState {
        Patrol,
        Chase,
        Search,
        Stunned,
        Distracted
    }

    public Transform reinfExit;

    [HideInInspector]
    public GuardState state;
    FieldOfView fovSensor;
    GuardPathfinding patrolPath;
    NavMeshAgent agent;
	GuardVisibility visibility;

    public float AlarmRaiseDuration;
    float alarmtime;
    bool raisingAlarm;

    public float friendlyDownTimer;
    float friendlytime;
    bool seenFriendlyDown;

    [HideInInspector]
    public bool hasBeenChecked;

    Vector3 distractionLocation;
    float distractionTime;
    float distractedFor;

    float searchTimer;
    Vector3 lastPlayerSighting;

    public bool unstunnable;
    public bool activator;
    public bool unlocker;

    public GameObject activateTarget;
    public OpenableDoor door;

	public float disruptionTime = 0;

    GUIManager guimanager;

    public GameObject attackRangeMarker;

    // Use this for initialization
    void Start() {

        if (type == GuardType.Regular)
        {
            state = GuardState.Patrol;

        }

        fovSensor = GetComponent<FieldOfView>();
        patrolPath = GetComponent<GuardPathfinding>();
        agent = GetComponent<NavMeshAgent>();
        searchTimer = searchTime;
        levelstate = GameObject.Find("GameManager").GetComponent<LevelState>();
		animator = GetComponentInChildren<Animator>();
		visibility = GetComponent<GuardVisibility>();
		animator.SetBool("kävelee", true);
        guimanager = GameObject.Find("GameManager").GetComponent<GUIManager>();
    }
   

    // Update is called once per frame
    void Update()
    {
        if (state == GuardState.Chase)
        {
            SetEffectRangeMarker(transform.position, attackRange);

        } 
		// Limit FOV while in disruptor effect
		if (disruptionTime > 0) {
			disruptionTime -= Time.deltaTime;
			fovSensor.limitMode = true;
		} else {
			fovSensor.limitMode = false;
		}

        if (state != GuardState.Stunned)
        {

            //Separate Gameobject in FOV to Guards and Players
            guards.Clear();
            players.Clear();


            foreach (var target in fovSensor.visibleTargets)
            {
               

                if (target.tag == "Player")
                {

                    if (!players.Contains(target.GetComponent<PlayerCharacter>())) {

                        players.Add(target.GetComponent<PlayerCharacter>());
                        
                        // print(target.name + " in sight");
                    }

                }
                else if (target.tag == "Guard")
                {
                    if (!guards.Contains(target.GetComponent<GuardBehaviour>()))
                    {

                        guards.Add(target.GetComponent<GuardBehaviour>());
                    }
                }


            }
            //Guard states

            if (state == GuardState.Patrol) {

                if (players.Count > 0)
                {
                    state = GuardState.Chase;
                    print(name + " Chasing");
                    alarmEffect.SetActive(true);
                    patrolPath.enabled = false;

                } //Check if fellow guard is stunned
                //If true, will get alerted, raise an alarm and search for possible target near that guard
                 else if (players.Count <= 0 && guards.Count > 0 && !seenFriendlyDown)
                {
                    foreach (var guard in guards)
                    {
                        if (guard.SeeIfStunned() == true && !guard.hasBeenChecked && levelstate.state != LevelState.StateofLevel.Alarm) // && state != GuardState.Chase && state != GuardState.Search)
                        {
                            print("kauhistellaan");
                            friendlytime += Time.deltaTime;
                            Debug.DrawRay(transform.position, guard.transform.position, Color.magenta);

                            if (friendlytime > friendlyDownTimer)
                            {
                                FriendlyDown(guard);
                              

                                if (players.Count <= 0 && (guard.transform.position - transform.position).magnitude < patrolPath.tolerance)
                                {
                                    guard.hasBeenChecked = true;
                                    friendlytime -= friendlyDownTimer;

                                }

                            }
                           
                        }
                        else continue;
                    }
                }                

            }
            //If players are visible, the guard raises alarm and starts chasing player
            //If guard loses visibility of players while chasing them, they enter search mode and look around the area where the player was last seen
            else if (state == GuardState.Chase) {
                if (players.Count > 0) {
                    print("pelaajaa näkyy");
                    RaiseAlarm();
                    print("Alarm!");

                    lastPlayerSighting = players
                        .OrderBy(p => Vector3.Distance(p.transform.position, transform.position))
                        .First().transform.position;

                } else {
                    if (Vector3.Distance(transform.position, lastPlayerSighting) < patrolPath.tolerance) {
                        state = GuardState.Search;
                        print(name + " Searching");
                        alarmEffect.SetActive(false);
                        searchEffect.SetActive(true);
                    }
                }
                SetNavMeshTarget(lastPlayerSighting);

            } else if (state == GuardState.Search) {
                if (searchTimer > 0) {
                    if (Vector3.Distance(transform.position, lastPlayerSighting) < patrolPath.tolerance) {
                        var randomPosition = new Vector3(Random.Range(-2, 2), 0, Random.Range(-2, 2));
                        lastPlayerSighting = randomPosition + transform.position;
                    }
                    patrolPath.enabled = false;
                    SetNavMeshTarget(lastPlayerSighting);
                    searchTimer -= Time.deltaTime;
                    print(gameObject.name + " is searching");
                    if (players.Count > 0)
                    {
                        searchEffect.SetActive(false);
                        state = GuardState.Chase;
                        print(name + " Chasing");
                        alarmEffect.SetActive(true);
                    }
                } else {
                    searchTimer = searchTime;
                    ReturnToPatrol();

                }

                //Player abilities can distract the guard. They will take a position close to the distraction ability's created object and stare at it for a while
            } else if (state == GuardState.Distracted)
            {
            
                    patrolPath.enabled = false;
                    var randomPosition = new Vector3(Random.Range(-2, 2), 0, Random.Range(-2, 2));
                    SetNavMeshTarget(distractionLocation);
                    distractedFor += Time.deltaTime;
                    Debug.DrawRay(transform.position, distractionLocation, Color.magenta);
                    if (distractedFor > distractionTime)
                    {
                        distractedFor -= distractionTime;
                        ReturnToPatrol();
                  //  }
                }
            }

            //Attacking PCs
            if (players != null)
            {

                Vector3 distanceToTarget;
                foreach (var target in players)
                {
                    distanceToTarget = target.transform.position - transform.position;
                    if (Mathf.Abs(distanceToTarget.magnitude) < attackRange && state != GuardState.Stunned)
                    {
                        var pc = target.GetComponent<PlayerCharacter>();

                        Attack(pc);

                    }
                }
            }
            //Alarms affect all the guards on a given level
            if (raisingAlarm)
            {
                print("hälytetään");
                alarmtime += Time.deltaTime;
                if (alarmtime > AlarmRaiseDuration)
                {
                    levelstate.GoToAlarm();
                    raisingAlarm = false;
                    alarmtime -= AlarmRaiseDuration;

                }
            } else if (!raisingAlarm)
            {
                alarmtime = 0;
            }
				
        }
        else
        {
			
            return;
        }
    }

    void ReturnToPatrol() {
        if (state != GuardState.Stunned)
        {
            alarmEffect.SetActive(false);
            state = GuardState.Patrol;
            alarmEffect.SetActive(false);
            searchEffect.SetActive(false);
            patrolPath.enabled = true;
            raisingAlarm = false;
            alarmtime = 0;
            print("palataan astialle");
            guimanager.HideProgressBar();
        }
        else return;
    }


    public void SetNavMeshTarget(Vector3 target) {
        if (state != GuardState.Stunned)
        {
            agent.SetDestination(target);
            agent.speed = chaseSpeed;
        }
        else return;
    }


    public void Attack(PlayerCharacter attackTarget)
    {
        if (state != GuardState.Stunned && state != GuardState.Distracted)
        {
            animator.SetBool("ampuminen", true);
            attackTarget.GetAttacked();
        }
        else return;
    }

    public void GetStunned()
    {
        if (!unstunnable)
        {
            StopRaisingAlarm();
            attackRangeMarker.SetActive(false);
			alarmEffect.SetActive(false);
			searchEffect.SetActive(false);
            state = GuardState.Stunned;
            print(gameObject.name + " stunned");
            fovSensor.enabled = false;
            GetComponent<NavMeshAgent>().enabled = false;
			animator.SetBool("kuolee", true);
			visibility.HideFromPlayer();
        }

        if(activator)
        {
            activateTarget.SetActive(true);
        }

        if(unlocker)
        {
            door.type = OpenableDoor.DoorType.Normal;
        }
    }

    public bool SeeIfStunned()
    {
        if (state == GuardState.Stunned) {
            return true;
        } else return false;
    }

    public void GetAlarmed()
    {
        if (state != GuardState.Stunned)
        {
            ogTurns = patrolPath.timeToTurn;
            patrolPath.timeToTurn = alarmTurn;
        }
        else return;
    }

    public void GetNormal()
    {
        patrolPath.timeToTurn = ogTurns;
        alarmEffect.SetActive(false);
    }

    void FriendlyDown(GuardBehaviour downedGuard)
    {
        if (state != GuardState.Stunned && state != GuardState.Distracted)
        {
            state = GuardState.Search;
            lastPlayerSighting = downedGuard.transform.position;
            RaiseAlarm();
        }

    }

    public void GetDistracted(float distractionDuration, Vector3 location)
    {
        if (state != GuardState.Stunned)
        {
            StopRaisingAlarm();
            guimanager.ShowProgressBar(gameObject, distractionDuration);
            state = GuardState.Distracted;
            distractionLocation = location;
            distractionTime = distractionDuration;
            Debug.DrawRay(transform.position, distractionLocation, Color.magenta);
        }
        else return;
    }

	public void GetDisrupted() {
		// Increase effect time while in "cyberelectronanoparticlesmoke"
		// Maybe useful for cool recovery effect, if time...
		disruptionTime += 1.2f * Time.deltaTime;
	}

    void RaiseAlarm()
    {
        if (!raisingAlarm && state != GuardState.Stunned)
        {
            guimanager.ShowProgressBar(gameObject, AlarmRaiseDuration);
            raisingAlarm = true;
        }
        else return;
    }

    void StopRaisingAlarm()
    {
        if(raisingAlarm)
        {
            raisingAlarm = false;
            guimanager.HideProgressBar();
        }
    }

    public void SetEffectRangeMarker(Vector3 target, float radius)
    {
        target.y = 0.1f;
        attackRangeMarker.transform.localScale = new Vector3(radius * 2.3f, radius * 2.3f, 1);
        attackRangeMarker.transform.position = target;
        attackRangeMarker.SetActive(true);
    }

}
