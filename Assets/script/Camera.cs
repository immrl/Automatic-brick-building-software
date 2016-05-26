using UnityEngine;
using System.Collections;

public class Camera : MonoBehaviour {
	public float speed;

	// Update is called once per frame
	void Update () {
		float d = Input.GetAxis("Mouse ScrollWheel")*speed;
		Vector3 position = transform.position;
		position.z -= d;
		transform.position = position;
		if (d > 0f)
		{
			// scroll up
		}
		else if (d < 0f)
		{
			// scroll down
		}
	}
}
