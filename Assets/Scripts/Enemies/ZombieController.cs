using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class ZombieController : MonoBehaviour, IDamageable
{
    #region
    private Transform target;
    float distanceToTarget;
    public int life = 5;
    NavMeshAgent agent;
    Animator anim;

    public float distanceToChase = 3f;
    public float chaseInterval = 2f;
    float chaseTime;
    public GameObject particle;
    public Transform originParticlePoint;
    private GameManager gm;
    #endregion

    [Header("Ataque")]
    public int attackDamage = 10;
    [Tooltip("Segundos desde que empieza la animacion hasta que el golpe hace dano")]
    public float attackHitDelay = 0.5f;
    [Tooltip("Distancia maxima a la que el golpe alcanza al jugador")]
    public float attackReach = 2f;

    #region /
    public AudioClip zsonido;

    AudioSource zSource;
    #endregion

    // Lo usa WaveDirector para devolver el zombie al pool en vez de destruirlo
    public Action<ZombieController> Died;

    float currentLife;
    bool dead;
    PlayerHealth player;


    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        zSource = GetComponent<AudioSource>();
    }

    void OnEnable()
    {
        currentLife = life;
        dead = false;
        chaseTime = chaseInterval;
    }

    void Start()
    {
        gm = GameManager.instance != null ? GameManager.instance : FindFirstObjectByType<GameManager>();

        var playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            target = playerObject.transform;
            player = playerObject.GetComponentInChildren<PlayerHealth>();
        }
    }

    // Balas fisicas de las armas antiguas
    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("Bullet"))
            TakeHit(1, collision.GetContact(0).point, false);
        else if (collision.gameObject.CompareTag("Shotgun"))
            TakeHit(currentLife, collision.GetContact(0).point, false);
    }

    public void TakeHit(float damage, Vector3 point, bool headshot)
    {
        // Evita contar dos veces la muerte si llegan varios impactos en el mismo frame
        if (dead)
            return;

        currentLife -= damage;
        if (particle != null)
        {
            var origin = originParticlePoint != null ? originParticlePoint.position : point;
            Destroy(Instantiate(particle, headshot ? point : origin, Quaternion.identity), 3f);
        }

        if (currentLife <= 0)
            Die();
    }

    // Update is called once per frame
    void Update()
    {
        if (target == null || dead)
            return;

        if (gm != null && gm.CurrentState != GameManager.State.Playing)
        {
            if (agent.isOnNavMesh)
                agent.isStopped = true;
            return;
        }

        Vector3 posNoRot = new Vector3(target.position.x, transform.position.y, target.position.z);
        transform.LookAt(posNoRot);
        distanceToTarget = Vector3.Distance(transform.position, target.position);

        Chase();

    }
    void Chase()
    {
        chaseTime -= Time.deltaTime;
        if (chaseTime < 0)
        {
            if (distanceToChase < distanceToTarget)
            {
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(target.position);
                }
                agent.stoppingDistance = distanceToChase;
                chaseTime = chaseInterval;
                PlaySound();

            }
            else
            {
                Attack();
                chaseTime = chaseInterval;
            }
        }
    }

    void Attack()
    {
        anim.Play("Attack");
        PlaySound();
        StartCoroutine(HitPlayerAfterDelay());
    }

    IEnumerator HitPlayerAfterDelay()
    {
        yield return new WaitForSeconds(attackHitDelay);
        if (!dead && target != null && Vector3.Distance(transform.position, target.position) <= attackReach)
        {
            if (player != null)
                player.TakeDamage(attackDamage);
            else if (gm != null)
                gm.TakeDamage(attackDamage);
        }
    }

    void PlaySound()
    {
        if (zSource != null && zsonido != null)
            zSource.PlayOneShot(zsonido);
    }

    void Die()
    {
        dead = true;
        StopAllCoroutines();
        // Los cuchillos clavados no deben desaparecer con el zombie (vuelve al pool)
        foreach (var stuck in GetComponentsInChildren<MeleeWeapon>())
            stuck.Unstick();
        if (gm != null)
            gm.AddEnemyKill();

        if (Died != null)
            Died(this);
        else
            Destroy(gameObject);
    }



}
