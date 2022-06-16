using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class eNEMYcONTROLLER2 : MonoBehaviour
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
    WaitForSeconds wait;
    private GameManager gm;
    #endregion

    #region /
    public AudioClip zsonido;
    public AudioClip zattack;
    AudioSource zSource;
    #endregion


    void Start()
    {
        gm = FindObjectOfType<GameManager>().GetComponent<GameManager>();

       
        agent = GetComponent<NavMeshAgent>();

        chaseTime = chaseInterval;

        anim = GetComponent<Animator>();

        target = GameObject.FindGameObjectWithTag("Player").transform;

        zSource = GetComponent<AudioSource>();

    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("Bullet"))
        {
            life--;
            Instantiate(particle, originParticlePoint.position, Quaternion.identity);
            if (life <= 0 )
            {
                StartCoroutine(Dead());
                gm.AddEnemyKill();             
            }
        }

        
    }

   
   

    // Update is called once per frame
    void Update()
    {
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
                agent.SetDestination(target.position);
                agent.stoppingDistance = distanceToChase;
                chaseTime = chaseInterval;
             //   zSource.PlayOneShot(zsonido);

            }
            else
            {
                Attack();
            }
        }
    }

    void Attack()
    {
        anim.Play("Attack");
        zSource.PlayOneShot(zsonido);
    }

    private IEnumerator Dead()
    {
        anim.Play("Dead");
        yield return new WaitForSeconds(1);
        Destroy(gameObject);

    }

}