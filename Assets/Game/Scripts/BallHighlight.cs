using UnityEngine;

public class BallHighlight : MonoBehaviour
{
    [SerializeField] private Transform ball;
    [SerializeField] private Vector2 worldOffset = new(-0.06f, 0.08f);

    private void LateUpdate()
    {
        if (ball == null) return;

        transform.position = ball.position + (Vector3)worldOffset;
        transform.rotation = Quaternion.identity;
    }
}