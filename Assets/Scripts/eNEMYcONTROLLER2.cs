using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class eNEMYcONTROLLER2 : MonoBehaviour
{
    #region
    public Transform target;
    float distanceToTarget;
    public int life = 5;
    NavMeshAgent agent;
    Animator anim;
 //   public AudioClip zsonido;
    //public AudioClip zattack;
  //  AudioSource zSource;
    public float distanceToChase = 3f;
    public float chaseInterval = 2f;
    float chaseTime;
    public GameObject particle;
    public Transform originParticlePoint;
    WaitForSeconds wait;
    #endregion


    void Start()
    {

        agent = GetComponent<NavMeshAgent>();

        chaseTime = chaseInterval;

        anim = GetComponent<Animator>();

        target = GameObject.FindGameObjectWithTag("Player").transform;

     //   zSource = GetComponent<AudioSource>();

    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("Bullet"))
        {
            life--;
            Instantiate(particle, originParticlePoint.position, Quaternion.identity);
            if (life <= 0 )
            {
                Destroy(gameObject);

            }
        }

        
    }

    public bool DoDamage(int vld, bool isPlayer)
    {
        Debug.Log("HE RECIBIDO DA�O = " + vld + " isPlayer  =  " + isPlayer);
        if (isPlayer == true)
        {
            life -= vld;
            if (life <= 0)
            {
                StartCoroutine(Die());
            }
            return true;
        }
        return false;

    }

    IEnumerator Die()
    {
        
        yield return wait;
       // GameManager.instance.AddEnemyKill();
        Destroy(gameObject);

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
       // zSource.PlayOneShot(zsonido);
    }



}


