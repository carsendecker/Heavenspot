﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : Creature
{
	public enum Phase
	{
		Movement = 1,
		Choosing = 2,
		Attacking = 3,
	}

	public int AttackCount;
	public int AttackDamage = 1;
	public float iFrameTime;
	public float CooldownTime;
	public GameObject CrosshairPrefab, LockedCrosshairPrefab;
	public GameObject AttackParticlesPrefab;
	public Color SlowMoColor;
	public List<GameObject> EnemyList = new List<GameObject>();
	public List<GameObject> EnemyQueue = new List<GameObject>();
	public AudioClip hurtSound, attackSound, enterSlomoSound, selectTargetSound, moveTargetSound, deathSound;

	[HideInInspector] public Rigidbody2D rb;
	[HideInInspector] public GameObject targetedEnemy;
	[HideInInspector] public bool coolingDown;
	[HideInInspector] public bool canMove;
	
	private bool invincible;
	private Collider2D attackRange;
	private PlayerPhase currentPhase;
	private Dictionary<Phase, PlayerPhase> Phases = new Dictionary<Phase, PlayerPhase>();

	void Awake()
	{
		rb = GetComponent<Rigidbody2D>();
		attackRange = GetComponentInChildren<Collider2D>();
		
		//Adds all the phases to a dictionary for future access
		Phases.Add(Phase.Movement, new MovePhase(this));
		Phases.Add(Phase.Choosing, new ChoosePhase(this));
		Phases.Add(Phase.Attacking, new AttackPhase(this));
		SetPhase(Phase.Movement);
	}
	
	// Use this for initialization
	new void Start ()
	{
		base.Start();
		
		//Set stats and sliders and such
		canMove = true;
		MenuGod.MG.PlayerHealthSlider.maxValue = MaxHealth;
	}
	
	// Update is called once per frame
	void Update ()
	{
		currentPhase.Run();
		MenuGod.MG.PlayerHealthSlider.value = Mathf.Lerp(MenuGod.MG.PlayerHealthSlider.value, health, 0.2f);
	}

	//Deals damage to the player
	public override bool TakeDamage(int damage)
	{
		if (invincible) return false;
		base.TakeDamage(damage);
		iFramesForSeconds(iFrameTime, true);
		
		UtilityGod.UG.ShakeCamera(0.5f, 0.3f);
		AudioManager.AM.PlaySound(hurtSound, AMSource.PlayerSound);
		
		SetPhase(Phase.Movement);

		return false;
	}

	//Deals damage to the player, with an added knockback effect 
	public void TakeDamage(int damage, float knockbackForce, Transform damagingObj)
	{
		if (invincible) return;
		
		TakeDamage(damage);
		
		Vector2 forceDirection = transform.position - damagingObj.position;
		rb.AddForce(forceDirection.normalized * knockbackForce, ForceMode2D.Impulse);
	}

	protected override void Die()
	{
		EnemySpawner es = FindObjectOfType<EnemySpawner>();

		MenuGod.MG.ScoreText.text = "Final Score: " + MenuGod.MG.ScoreText.text;
		MenuGod.MG.ScoreText.transform.localPosition = new Vector2(-100, -150);
		MenuGod.MG.ScoreText.fontSize = 25;
		MenuGod.MG.PlayerHealthSlider.value = 0;
		
		Destroy(es);
		Destroy(gameObject);
	}

	//Takes input and moves the player around
	public void Move()
	{
		Vector2 tempVel = rb.velocity;
		
		if (InputManager.Pressed(Inputs.Right))
		{
			tempVel.x = Mathf.Lerp(tempVel.x, MoveSpeed, 0.2f);
		}
		else if (InputManager.Pressed(Inputs.Left))
		{
			tempVel.x = Mathf.Lerp(tempVel.x, -MoveSpeed, 0.2f);
		}
		else
		{
			tempVel.x = Mathf.Lerp(tempVel.x, 0, 0.2f);
		}
		
		if (InputManager.Pressed(Inputs.Up))
		{
			tempVel.y = Mathf.Lerp(tempVel.y, MoveSpeed, 0.2f);
		}
		else if (InputManager.Pressed(Inputs.Down))
		{
			tempVel.y = Mathf.Lerp(tempVel.y, -MoveSpeed, 0.2f);
		}
		else
		{
			tempVel.y = Mathf.Lerp(tempVel.y, 0, 0.2f);
		}
		
		rb.velocity = tempVel;
	}

	//Flashes player sprite and gives iFrames after being damaged
	private IEnumerator DamageFlash(float duration)
	{
		SpriteRenderer sr = GetComponent<SpriteRenderer>();
        
		for (int i = 0; i < Math.Round(duration / 0.1f); i++)
		{
			sr.enabled = false;
            
			yield return new WaitForSecondsRealtime(0.06f);
            
			sr.enabled = true;
            
			yield return new WaitForSeconds(0.06f);
		}
	}

	public void iFramesForSeconds(float time, bool flash)
	{
		if (invincible) return;
		if (flash)
			StartCoroutine(DamageFlash(time));
		StartCoroutine(iFrames(time));
	}
	
	//Makes player invincible for a specified amount of time
	private IEnumerator iFrames(float time)
	{
		invincible = true;
		yield return new WaitForSecondsRealtime(time);
		invincible = false;
	}
	
	
	
	private void OnCollisionEnter2D(Collision2D other)
	{
		currentPhase.OnCollisionEnter2D(other);
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		currentPhase.OnTriggerEnter2D(other);
		if (other.CompareTag("Enemy") && !EnemyList.Contains(other.gameObject))
		{
			EnemyList.Add(other.gameObject);
		}

		if (other.CompareTag("AggroTrigger"))
		{
			other.GetComponentInParent<Creature>().Aggro(true);
		}
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		currentPhase.OnTriggerExit2D(other);
		if (EnemyList.Contains(other.gameObject))
		{
			EnemyList.Remove(other.gameObject);
		}
		
		if (other.CompareTag("AggroTrigger"))
		{
			other.GetComponentInParent<Creature>().Aggro(false);
		}
	}
	
	private void OnParticleCollision(GameObject other)
	{
		if (other.CompareTag("Death Particles"))
		{
			//TODO: Passing damage value
			TakeDamage(1);
		}
	}


	
	//Checks to see if the player is in a certain phase
	public bool IsPhase(Phase phaseToCheck)
	{
		if (Phases.ContainsKey(phaseToCheck) && currentPhase.Equals(Phases[phaseToCheck]))
		{
			return true;
		}
		return false;
	}

	//Sets the phase of the player to newPhase
	public void SetPhase(Phase newPhase)
	{
		if (currentPhase != null && IsPhase(newPhase))
		{
			return;
		}

		currentPhase?.OnExit();
		currentPhase = Phases[newPhase];
		currentPhase.OnEnter();
	}

}



