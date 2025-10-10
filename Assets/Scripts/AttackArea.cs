using UnityEngine;

public class AttackArea : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private int damage = 10;

    private void OnTriggerEnter2D(Collider2D other)
    {
        other.GetComponent<BlobMonsterController>().TakeDamage(damage);
        Debug.Log("Successful Trigger");
    }
}
