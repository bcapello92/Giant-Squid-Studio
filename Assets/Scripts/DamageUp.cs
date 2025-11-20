using UnityEngine;
using System.Collections;

public class DamageUp : MonoBehaviour
{
    public AudioSource powerUpAudio;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("test1");
        if(other.gameObject.tag == "Player")
        {
            Debug.Log("test2");
            GameObject weapon = other.gameObject;
            while(weapon.transform.childCount != 0)
            {
                weapon = weapon.transform.GetChild(0).gameObject;
            }
            AttackArea damageMod = weapon.GetComponent<AttackArea>();
            damageMod.damage++;
            StartCoroutine(Pickup());
        }
    }

    IEnumerator Pickup()
    {
        this.gameObject.transform.localScale = new Vector3(0, 0, 0);
        powerUpAudio.Play();
        yield return new WaitForSeconds(1);
        Destroy(this.gameObject);
    }
}
