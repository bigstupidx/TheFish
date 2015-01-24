using LitJson;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum PawnRank
{
    D = 1,
	C,
	B,
	A,
    S,
}

public class PawnInfo
{
	public int index;
	public string name;
	public PawnRank rank;
}

public class Pawn : MonoBehaviour
{
	// static utilities
	public static Pawn SprayPawn ()
	{
		if (sprayPawnDelay > 0) {
			return null;
		}

		sprayPawnDelay = SPRAY_PAWN_DELAY;

		if (PawnManager.Instance ().isPawnMax ()) {
			// Pawn is full
			return null;
		}

		Pawn pawn = new GameObject ("Pawn").AddComponent<Pawn> ();
		PawnManager.Instance ().pawns.Add (pawn);

		pawn.gameObject.layer = LayerMask.NameToLayer ("UI");

		return pawn;
	}

	public static Pawn SprayPawn (Transform parent_transform, Vector3 localPosition)
	{
		Pawn pawn = SprayPawn ();
		if (null == pawn) {
			return null;
		}
		
		pawn.transform.localPosition = localPosition;
		return pawn;
	}

	public static Pawn SprayPawn (Transform parent_transform)
	{
		Pawn pawn = SprayPawn ();
		if (null == pawn) {
			return null;
		}

		pawn.transform.parent = parent_transform;
		return pawn;
	}

	public static Pawn SprayPawn (Transform parent_transform, int growthIndex)
	{
		Pawn pawn = SprayPawn (parent_transform);
		if (null == pawn) {
			return null;
		}

		pawn.growthIndex = growthIndex;
		return pawn;
	}

	public static Pawn SprayPawn (Transform parent_transform, int growthIndex, bool ignoreDelay)
	{
		Pawn pawn = null;
		if (ignoreDelay) {
			sprayPawnDelay = 0;
			pawn = SprayPawn (parent_transform, growthIndex);
		} else {
			pawn = SprayPawn (parent_transform, growthIndex);
		}

		if (null == pawn) {
			return null;
		}

		return pawn;
	}


	// static data
	public const int SPRAY_PAWN_DELAY = 10;
	public static int sprayPawnDelay = SPRAY_PAWN_DELAY;
	private static JsonData GrowthData = null;
	private static JsonData RankData = null;

	// public data
	public string pawnName = "";
	public Vector2 speed = Vector2.zero;
	public int rankFactor = 0;
	public string rankName = "C";
	public int growthIndex = 0;
	public double timeLeft = 0.0;

	// private data
	private Rect boundRect;
	private const float MIN_SPEED_X = 1.55f;
	private const float MAX_SPEED_X = 2.55f;
	private const float MIN_SPEED_Y = 0.54f;
	private const float MAX_SPEED_Y = 1.15f;
	private const float COLLIDE_RADIUS = 100.0f;

	private void Start ()
	{
		// load data
		if (null == GrowthData) {
			GrowthData = JsonMapper.ToObject (Resources.Load<TextAsset> ("info/Growth").text);

		}

		if (null == RankData) {
			RankData = JsonMapper.ToObject (Resources.Load<TextAsset> ("info/RankRate").text);
		}
		//
        timeLeft = Heater2.Instance().Level[FacilityManager.Instance().HeaterLevel].hatchTime;

		CalculateRank ();
		
		StartCoroutine (grow ());

		// add sprite component
		UISpriteAnimation spriteAnimation = gameObject.AddComponent<UISpriteAnimation> ();
		UISprite sprite = gameObject.GetComponent<UISprite> ();
		sprite.atlas = Game.Instance ().UIAtlasPawn;

		if (GrowthData ["data"] [growthIndex] ["sprites"].Count == 1) {
			spriteAnimation.namePrefix = (string)GrowthData ["data"] [growthIndex] ["sprites"] [rankName] [0];
		} else {
			spriteAnimation.namePrefix = (string)GrowthData ["data"] [growthIndex] ["sprites"] [rankName] [Random.Range (0, GrowthData ["data"] [growthIndex] ["sprites"] [rankName].Count)];
		}

		spriteAnimation.framesPerSecond = 6;

		if (speed.x < 0) {
			sprite.flip = UIBasicSprite.Flip.Horizontally;
		}

		// matches size
		sprite.MakePixelPerfect ();

		// boundaries
		boundRect = Game.Instance ().GameArea;
		boundRect.x += sprite.width / 2;
		boundRect.y += sprite.height / 2;
		boundRect.width -= sprite.width;
		boundRect.height -= sprite.height;

		float _x = Random.value * boundRect.width + boundRect.x;
		float _y = Random.value * boundRect.height + boundRect.y;

		transform.localPosition = new Vector3 (_x, _y, 0f);

		// add collider
		BoxCollider2D box2d = gameObject.AddComponent<BoxCollider2D> ();
		box2d.size = new Vector2 (203, 87);

		// set speed
		speed.x = Random.Range (MIN_SPEED_X, MAX_SPEED_X);
		speed.y = Random.Range (MIN_SPEED_Y, MAX_SPEED_Y);

		if (Random.value < .5f) {
			speed.x *= -1;
		}
		if (Random.value < .5f) {
			speed.y *= -1;
		}

		punch ();
	}

	private void Update ()
	{
		// count delay
		sprayPawnDelay--;

		// collide
		Vector3 tmplPoint = transform.localPosition;

		tmplPoint.x += speed.x;
		tmplPoint.y += speed.y;

		if (tmplPoint.x < boundRect.xMin) {
			tmplPoint.x = boundRect.xMin;
			speed.x *= -1f;

			punch ();
		}

		if (tmplPoint.x > boundRect.xMax) {
			tmplPoint.x = boundRect.xMax;
			speed.x *= -1f;

			punch ();
		}

		if (tmplPoint.y < boundRect.yMin) {
			tmplPoint.y = boundRect.yMin;
			speed.y *= -1f;

			punch ();
		}

		if (tmplPoint.y > boundRect.yMax) {
			tmplPoint.y = boundRect.yMax;
			speed.y *= -1f;

			punch ();
		}

		foreach (var pawn in PawnManager.Instance().pawns) {
			if (pawn == this) {
				continue;
			}

			Vector3 delta = transform.localPosition - pawn.transform.localPosition;
			if (delta.magnitude < COLLIDE_RADIUS) {
				// coliide!

				float angle = Mathf.Atan2 (delta.y, delta.x);

				speed.x = Mathf.Cos (angle) * speed.magnitude;
				speed.y = Mathf.Sin (angle) * speed.magnitude;

				punch ();

				if (growthIndex > 0 && pawn.growthIndex > 0) {
					StartCoroutine (MateManager.Mate ());
				}
			}
		}

		if (Mathf.Abs (speed.x) < 1f) {
			speed.x *= 1.045f;
		}

		// apply temp variable
		transform.localPosition = tmplPoint;
	}

	private void punch ()
	{
		if (speed.x < 0) {
			GetComponent<UISprite> ().flip = UIBasicSprite.Flip.Horizontally;
		} else {
			GetComponent<UISprite> ().flip = UIBasicSprite.Flip.Nothing;
		}

		iTween.PunchScale (gameObject, iTween.Hash ("amount", new Vector3 (- 0.3f, 0.3f, 0f)));
	}

	private IEnumerator grow ()
	{
		if (timeLeft < 0) {
			yield break;
		}

		yield return new WaitForSeconds ((float)timeLeft);

		growthIndex++;

        timeLeft = Heater2.Instance().Level[FacilityManager.Instance().HeaterLevel].hatchTime;
		GetComponent<UISpriteAnimation> ().namePrefix = (string)GrowthData ["data"] [growthIndex] ["sprites"] [rankName] [Random.Range (0, GrowthData ["data"] [growthIndex] ["sprites"] [rankName].Count)];

		punch ();
	}

	private void CalculateRank ()
	{
        PawnRank rank = PawnRank.D;

        var curInfo = Filter2.Instance().Level[FacilityManager.Instance().FilterLevel];
        float dice = Random.Range(0.0f, 100.0f);

        float sum = 0.0f;
        for (var i = 0; i < curInfo.percentage.Length; ++i)
        {
            PawnRank currentRank = i + PawnRank.D;
            if(PawnManager.Instance().IsRankAvailable(currentRank) == false)
            {
                rank = (PawnRank)System.Math.Max((int)currentRank - 1, (int)PawnRank.D);
                break;
            }

            float percentage = (float)curInfo.percentage[i];
            if(sum <= dice && dice <= sum + percentage)
            {
                rank = currentRank;
                break;
            }

            sum += percentage;
        }

        rankName = rank.ToString();
        Debug.Log(rankName);
	}

}

