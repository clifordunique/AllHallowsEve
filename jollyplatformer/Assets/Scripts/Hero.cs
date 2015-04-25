﻿using UnityEngine;
using System.Collections;
using Jolly;


public class Hero : MonoBehaviour
{
	public float MaxSpeed;
	public float MoveForce;
	public float JumpForce;
	public GameObject GroundDetector;
	public GameObject ScreenEdgeDetector;
	public GameObject ProjectileEmitLocator;
	public GameObject ChannelLocator;
	public GameObject Projectile;
	public GameObject ChannelVisual;
	public Camera RenderingCamera;
	public float ChannelTime;

	private HeroController HeroController;

	public Vector2 ProjectileLaunchForce;
	public float ProjectileDelay;
	private float TimeUntilNextProjectile = 0.0f;

	private bool ShouldJump = false;
	private bool AtEdgeOfScreen = false;
	private bool FacingRight = true;

	private float RespawnTimeLeft = 0.0f;
	private float TimeSpentChanneling = 0.0f;
	private bool IsChanneling = false;
	private GameObject ChannelVisualInstance;

	void Start ()
	{
		this.HeroController = this.GetComponent<HeroController>();

		JollyDebug.Watch (this, "FacingRight", delegate ()
		{
			return this.FacingRight;
		});
	}

	private float scale
	{
		set
		{
			this.transform.localScale = new Vector3((this.FacingRight ? 1.0f : -1.0f) * value, value, 1.0f);
		}
		get
		{
			return this.transform.localScale.y;
		}
	}

	bool IsGrounded()
	{
		return Physics2D.Linecast(this.transform.position, this.GroundDetector.transform.position, 1 << LayerMask.NameToLayer ("Ground"));;
	}

	void Update ()
	{
		if (this.RespawnTimeLeft > 0.0f)
		{
			this.transform.position = new Vector3(0.0f, -20.0f, 0.0f);

			this.RespawnTimeLeft -= Time.deltaTime;
			if (this.RespawnTimeLeft <= 0.0f)
			{
				this.transform.position = new Vector3(0,0,0);
			}
		}

		bool grounded = this.IsGrounded();
		JollyDebug.Watch (this, "Grounded", grounded);
		if (this.HeroController.Jump && grounded)
		{
			this.ShouldJump = true;
		}

		float viewportPointOfEdgeDetector = this.RenderingCamera.WorldToViewportPoint(this.ScreenEdgeDetector.transform.position).x;
		this.AtEdgeOfScreen = viewportPointOfEdgeDetector < 0.0f || viewportPointOfEdgeDetector >= 1.0f;

		this.TimeUntilNextProjectile -= Time.deltaTime;

		if (this.HeroController.GetBiggerStart && this.CanGrow())
		{
			this.StartChannelGrow();
		}
		if (this.HeroController.GetBiggerEnd)
		{
			this.StopChannelGrow();
		}
		if (this.HeroController.GetBiggerHold && this.IsChanneling)
		{
			this.TimeSpentChanneling += Time.deltaTime;

			if (this.TimeSpentChanneling > this.ChannelTime)
			{
				this.StopChannelGrow();
				this.Grow();
			}
		}
	}

	void FixedUpdate ()
	{
		bool canMove = !this.IsChanneling;
		bool canAct = !this.IsChanneling;

		float horizontal = this.HeroController.HorizontalMovementAxis;
		if (!canMove)
		{
			horizontal = 0;
		}

		bool movingIntoScreenEdge = (horizontal > 0 && this.FacingRight) || (horizontal < 0 && !this.FacingRight);
		if (this.AtEdgeOfScreen && movingIntoScreenEdge)
		{
			this.GetComponent<Rigidbody2D>().velocity = new Vector2(0, this.GetComponent<Rigidbody2D>().velocity.y);
			horizontal = 0.0f;
		}

		if (horizontal * this.GetComponent<Rigidbody2D>().velocity.x < this.MaxSpeed)
		{
			this.GetComponent<Rigidbody2D>().AddForce (Vector2.right * horizontal * MoveForce);
		}

		float maxSpeed = Mathf.Abs (this.MaxSpeed * horizontal);
		if (Mathf.Abs(this.GetComponent<Rigidbody2D>().velocity.x) > maxSpeed)
		{
			this.GetComponent<Rigidbody2D>().velocity = new Vector2(Mathf.Sign (this.GetComponent<Rigidbody2D>().velocity.x) * maxSpeed, this.GetComponent<Rigidbody2D>().velocity.y);
		}

		if (canAct)
		{
			if (this.ShouldJump)
			{
				this.GetComponent<Rigidbody2D>().AddForce (Vector2.up * JumpForce * 1/this.scale);
				this.ShouldJump = false;
			}

			if ((horizontal > 0 && !this.FacingRight) || (horizontal < 0 && this.FacingRight))
			{
				this.Flip();
			}

			if (this.HeroController.Shooting && this.TimeUntilNextProjectile < 0.0f)
			{
				this.TimeUntilNextProjectile = this.ProjectileDelay;
				GameObject projectile = (GameObject)GameObject.Instantiate(this.Projectile, this.ProjectileEmitLocator.transform.position, Quaternion.identity);
				projectile.GetComponent<Projectile>().OwnerHero = this;
				Vector2 launchForce = this.ProjectileLaunchForce;
				if (!this.FacingRight)
				{
					launchForce = new Vector2(launchForce.x * -1.0f, launchForce.y);
				}
				projectile.GetComponent<Rigidbody2D>().AddForce(launchForce);
			}
		}
	}

	void Flip ()
	{
		this.FacingRight = !this.FacingRight;
		this.scale = this.scale;
	}

	bool IsAlive()
	{
		return (this.RespawnTimeLeft <= 0.0f);
	}

	public void Hit (Hero attackingHero)
	{
		if (this == attackingHero)
		{
			return;
		}

		this.Die(attackingHero);
	}

	void Die (Hero attackingHero)
	{
		this.RespawnTimeLeft = 5.0f;
	}

	void StartChannelGrow()
	{
		this.TimeSpentChanneling = 0.0f;
		this.IsChanneling = true;
		this.ChannelVisualInstance = (GameObject)GameObject.Instantiate(this.ChannelVisual, this.ChannelLocator.transform.position, Quaternion.identity);
		this.ChannelVisualInstance.GetComponent<ChannelVisual>().ChannelTime = this.ChannelTime;
		this.ChannelVisualInstance.transform.localScale = new Vector3(this.ChannelVisualInstance.transform.localScale.x * this.scale, this.ChannelVisualInstance.transform.localScale.y * this.scale, this.ChannelVisualInstance.transform.localScale.z * this.scale);
	}

	void StopChannelGrow()
	{
		this.TimeSpentChanneling = 0.0f;
		this.IsChanneling = false;
		Destroy(this.ChannelVisualInstance);
	}

	bool CanGrow()
	{
		return (this.scale <= 3.0f && this.IsGrounded());
	}

	void Grow()
	{
		if (this.CanGrow())
		{
			Rigidbody2D rb = GetComponent<Rigidbody2D>();
			this.scale += 0.5f;
			rb.mass = (1.0f / this.scale);
		}
	}
}
