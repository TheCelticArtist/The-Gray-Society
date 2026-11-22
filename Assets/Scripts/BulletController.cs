using UnityEngine;

public class BulletController : MonoBehaviour
{
    [SerializeField] float Speed;
    [SerializeField] float Lifetime;

    public Vector2 Direction = Vector2.right;

    void Start()
    {
        Destroy(gameObject, Lifetime);   // sparisce da solo dopo 2 secondi
    }

    void Update()
    {
        transform.position += (Vector3)(Direction * Speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
           
            Destroy(gameObject);
        }
        else if (other.CompareTag("Ground"))
        {
            Destroy(gameObject);
        }
    }
}