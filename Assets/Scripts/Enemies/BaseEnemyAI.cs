//Simon Choquet 2/4/2021

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 
/// This implements very basic AI that wanders until it spots a target.
/// After which it will run to the target or the last place the target was spotted.
/// If it loses track of the player it will restart the cycle.
/// However, If it finds itself within the specified range, it will try to maintain this range.
/// 
/// </summary>

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class BaseEnemyAI : LivingEntity
{
    //Inspector fields (public or serialized)
    [Header("AI Behaviour")]
    [Tooltip("What am I looking for?")] public Transform target;
    [Tooltip("What can we NOT see through?")] [SerializeField] private LayerMask blinds;
    [Tooltip("How close should I get to my destination before I say I've arrived? [Do not change if you do not understand]")] [SerializeField] protected float threshold = 0.8f;
    [Header("AI Stats")]
    [Tooltip("How far can I see?")] [Range(1, 100)] public float range = 20;
    [Tooltip("How far can I wander?")] [Range(1, 100)] public float wanderRange = 10;
    [Tooltip("How close do I want to get?")] [Range(0, 20)] public float approach = 7;
    [Space]
    [Header("Enemy Attack Parameters")]
    [Tooltip("Enemy's attack reach on the x and y-axes if melee")] [SerializeField] protected Vector2 attackReach = new Vector2(1f, 1f);
    [Tooltip("Projectile to shoot if ranged")] [SerializeField] private GameObject enemyProjectile;
    protected float projectileSpeed = 0;
    private bool attackCD = false;                  //Is the attack on cooldown?
    protected float cooldownTimer = 1;             //Cooldown between attacks in seconds
    private Collider2D playerHit;                   //Collider hit by enemy melee attack (player collider)
    private LayerMask layerMask;

    //Hidden fields (private or hidden)
    private bool wasPlayer = false;     //Did we last see the player or are we wandering
    public Rigidbody2D rigid;

    //Internals
    private Vector2 lastPoint;
    private float timeSinceMove = 0;


    //Unity Messages
    public void Start()
    {
        base.Start();
        rigid = GetComponent<Rigidbody2D>();        //Retrieve our rigid body
        target = manager.player.transform;          //Set player as our target
        //blinds = LayerMask.GetMask("Level");        //Layer with objects the enemy can't see through
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(this.transform.position, range);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(this.transform.position, approach);

        //Hitbox for enemy attack                               ***Written by Nicky (2 lines)
        Gizmos.color = Color.red;                           
        Gizmos.DrawWireCube(this.transform.position, attackReach);
    }

    private void FixedUpdate()
    {
        //If interrupted don't move and don't do anything 
        if (interrupt) return;

        //If the player is in sight
        if (Vector2.Distance(target.position, this.transform.position) <= range && !Physics2D.Linecast(this.transform.position, target.position, blinds))
        {
            gotoPoint = target.position;

            if (enemyProjectile != null && Vector2.Distance(target.position, this.transform.position) <= approach)
            {
                Action();
                return;
            }
            else if (enemyProjectile == null && Vector2.Distance(target.position, this.transform.position) <= threshold*2)
            {
                Action();
                return;
            }
        }
        else if (Physics2D.Linecast(this.transform.position, gotoPoint, blinds) || Vector2.Distance(gotoPoint,this.transform.position) <= threshold) //We can't reach our node or we reached it
        {
            //Pick a new wanderpoint
            float distanceToWander = Random.Range(0, wanderRange);
            Vector2 dir = Random.insideUnitCircle.normalized;
            RaycastHit2D hit = Physics2D.Raycast(this.transform.position, dir, distanceToWander, blinds);
            if (hit)
                gotoPoint = hit.point - dir;
            else
                gotoPoint = (Vector2)this.transform.position + (dir * distanceToWander);
        }
        
        rigid.velocity = (gotoPoint - (Vector2)this.transform.position).normalized * moveSpeed; //Head to point
        Rotate(rigid.velocity);
        if (this.rigid.velocity.magnitude > 0.1)
        {
            this.anim.SetFloat("Hor", this.rigid.velocity.x);
            this.anim.SetFloat("Ver", this.rigid.velocity.y);
            this.anim.SetBool("Mov", true);
        }
        else
            this.anim.SetBool("Mov", false);
        
        ////If we haven't arrived to our node walk that way
        //if (((gotoPoint - (Vector2)this.transform.position).magnitude > threshold))
        //{

        //}
        //else 
        //{
        //    pickPoint(); //Once we've arrived pick a new node
        //}


        //if (target)
        //{
        //    //If the target gets too close we automatically face it
        //    if ((target.position - this.transform.position).magnitude < approach)
        //    {
        //        Rotate(target.position - this.transform.position);
        //    }

        //    //If we can see our target head towards it
        //    if (canSee(target.position))
        //    {
        //        if (enemyProjectile != null && Vector3.Distance(target.position, transform.position) < approach)
        //        {
        //            rigid.velocity /= 1.25f;
        //            Action();
        //        }
        //        else
        //        {
        //            Vector3 dif = target.position - this.transform.position;
        //            float travelDist = dif.magnitude - approach;
        //            dif = dif.normalized;
        //            dif *= travelDist;
        //            gotoPoint = this.transform.position + dif;
        //            wasPlayer = true;
        //            if ((gotoPoint - (Vector2)this.transform.position).magnitude < threshold)
        //            {
        //                rigid.velocity /= 1.25f;
        //               
        //            }
        //        }
        //    }

        //}
        ////Update enemy's walk/idle animation


        ////Fix any form of stubborness
        //if (lastPoint == (Vector2)this.transform.position)
        //{
        //    timeSinceMove += Time.fixedDeltaTime;
        //    if(timeSinceMove > 1)
        //    {
        //        pickPoint();
        //    }
        //}
        //else
        //{
        //    lastPoint = (Vector2)this.transform.position;
        //    timeSinceMove = 0f;
        //}
    }

    //** ALL CUSTOM METHODS HERE **//

    //Action to perform when in range of player                             ***Code is written by Nicky from this line down
    protected void Action()
    {
        //Check if attack is on cooldown
        if(!attackCD)
        {
            interrupt = true;          //Pause roaming/follow AI
            Attack();
            interrupt = false;         //Resume AI behaviour
        }
    }

    //Enemy melee attack
    protected void MeleeAttack()
    {
        if (this.hp <= 0)               //Enemy can not attack if dead
            return;
        layerMask = LayerMask.GetMask("Shield");    
        playerHit = Physics2D.OverlapBox(transform.position, attackReach, 0f, layerMask);
        if (playerHit == null)          //If enemy didn't hit shield
        {
            layerMask = LayerMask.GetMask("Player");
            playerHit = Physics2D.OverlapBox(transform.position, attackReach, 0f, layerMask);
            if (playerHit == null) { }  //If we hit nothing
            else if (manager.player.stamina <= 0 || playerHit.gameObject == manager.player.gameObject) //If enemy hits the player or player is out of stamina
            {
                manager.player.Hurt(attack_damage, this.gameObject.transform);  //Deal damage to player
                StartCoroutine(AtkCooldownCoroutine());                         //Start attack cooldown
            }
        }  
        else if (manager.player.stamina > 0 && playerHit.gameObject == manager.player.shield)     //If enemy hits shield and player has stamina
        {
            manager.player.Hurt(0, this.gameObject.transform);      //Knock player back
            StartCoroutine(AtkCooldownCoroutine());                 //Start attack cooldown
            this.Hurt(0, manager.player.transform);                 //Knock enemy back
            manager.player.stamina -= manager.player.shieldCost;    //Reduce player stamina
        }
    }

    //Enemy ranged attack
    protected void RangedAttack()
    {
        GameObject projectile = Instantiate(enemyProjectile, transform.position + hitBoxOffset, transform.rotation);    //Create projectile copy
        projectile.GetComponent<Projectile>().damage = this.attack_damage;  //Match the projectile's damage to the enemy's attack
        projectile.GetComponent<Rigidbody2D>().velocity = (target.position - this.transform.position).normalized * projectileSpeed;   //Set projectile velocity
        StartCoroutine(AtkCooldownCoroutine());                 //Start attack cooldown
    }

    //Cooldown between regular attacks
    IEnumerator AtkCooldownCoroutine()
    {
        attackCD = true;            //Attack is on cooldown
        float cooldownTimer = this.cooldownTimer;
        while (cooldownTimer > 0)   
        {
            cooldownTimer -= Time.deltaTime;
            yield return null;
        }
        attackCD = false;           //Attack is no longer on cooldown
    }

    //Enemy death
    protected override void Death()
    {
        base.Death();
        anim.SetTrigger("Death");
        StartCoroutine(DeathCoroutine(1f));
    }

    //Wait 1 second before removing corpse
    private IEnumerator DeathCoroutine(float seconds)
    {
        while (seconds > 0)
        {
            seconds -= Time.deltaTime;
            yield return null;
        }
        if (this != manager.player)
            Destroy(gameObject);
    }
}
