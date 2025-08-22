using UnityEngine;
using Mirror;

public class gunScript : NetworkBehaviour
{
    public GameObject bulletSpawnSpot;
    public GameObject bulletPrefab;
    public GameObject lobBulletPrefab;
    public float bulletSpeed = 10.0f;
    public float lobBulletInitVel = 10.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (!isLocalPlayer)
            { return; }

        if (Input.GetMouseButtonDown(0))
        {
            spawnBullet();
        }
        if (Input.GetMouseButtonDown(1))
        {
            GameObject bullet = GameObject.Instantiate(lobBulletPrefab, bulletSpawnSpot.transform.position, Quaternion.identity);
            bullet.transform.rotation = this.transform.rotation;
            var dir = new Vector2(Mathf.Cos(Mathf.Deg2Rad * this.transform.eulerAngles.z), Mathf.Sin(Mathf.Deg2Rad * this.transform.eulerAngles.z));
            bullet.GetComponent<Rigidbody2D>().linearVelocity = dir * lobBulletInitVel;
        }
    }

    [Command]
    void spawnBullet()
    {
        GameObject bullet = GameObject.Instantiate(bulletPrefab, bulletSpawnSpot.transform.position, Quaternion.identity);
        NetworkServer.Spawn(bullet);
        fwBullet(bullet);
        
    }

    [ClientRpc]
    void fwBullet(GameObject bullet)
    {
        bullet.transform.position = bulletSpawnSpot.transform.position;
        bullet.transform.rotation = this.transform.rotation;
        var dir = new Vector2(Mathf.Cos(Mathf.Deg2Rad * this.transform.eulerAngles.z), Mathf.Sin(Mathf.Deg2Rad * this.transform.eulerAngles.z));
        bullet.GetComponent<Rigidbody2D>().linearVelocity = dir * bulletSpeed;
    }
}
