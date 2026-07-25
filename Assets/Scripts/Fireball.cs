using UnityEngine;

public class Fireball : MonoBehaviour
{

    [SerializeField] private float explosionRadius;
    [SerializeField] private ParticleSystem effect;

    private void Explode()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider hit in hits)
        {
            if (hit.transform.TryGetComponent(out Enemy enemy))
            {
                //enemy.TakeDamage(10);
            }
        }
        Instantiate(effect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
