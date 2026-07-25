using UnityEngine;

public class FireballThrower : MonoBehaviour
{
    [SerializeField] private Fireball fireballPrefab;
    [SerializeField] private Transform firePoint;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            Instantiate(fireballPrefab, firePoint.position, firePoint.rotation);
        }
    }
}
