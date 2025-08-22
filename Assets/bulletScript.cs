using UnityEngine;
using Mirror;
using System.Collections;
using System;

public class bulletScript : NetworkBehaviour
{
    public LayerMask bulletLM;
    public Texture2D[] fractureImages = new Texture2D[1];

    public float imageScale;

    public float impactRadius = 1.0f;
    public float impactForce = 1.0f;
    public float impactDensity = 2.0f;
    public int minImpactPoints = 20;
    public int maxImpactPoints = 30;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [ServerCallback]
    void OnCollisionEnter2D(Collision2D col)
    {
        // Get the layer of the colliding GameObject
        int otherLayer = col.gameObject.layer;

        // Check if the other GameObject's layer is included in the desiredLayers mask
        if (((1 << otherLayer) & bulletLM.value) > 0)
        {
            // Collision occurred with an object on one of the desired layers
            var pos = col.contacts[0].point;
            //ObjectFracturer.DoCircularBreak((Vector2)pos, 0.5f, 0.5f);
            int seed = (int)DateTime.Now.Ticks;
            Collider2D[] allObjects = Physics2D.OverlapCircleAll((Vector2)pos, impactRadius);
            uint[] networkIds = new uint[allObjects.Length];
            Vector3[] allTransforms = new Vector3[allObjects.Length];
            for (int i = 0;i < allObjects.Length;i++)
            {
                NetworkIdentity n;
                breakableObject b;
                if (allObjects[i].gameObject.TryGetComponent(out n) && allObjects[i].gameObject.TryGetComponent(out b))
                {
                    if (!b.isWorking)
                    {
                        networkIds[i] = n.netId;
                        allTransforms[i] = new Vector3(allObjects[i].transform.position.x, allObjects[i].transform.position.y, allObjects[i].transform.eulerAngles.z);
                    }
                    
                }
                
               
            }
            
            networkVoronoiBreak((Vector2)pos, impactRadius, impactForce, impactDensity, UnityEngine.Random.Range(minImpactPoints, maxImpactPoints), 1, seed, networkIds, allTransforms);
            //ObjectFracturer.DoImageBreakage(pos, fractureImages, imageScale);
            // Add your collision handling logic here
        }
        else
        {
            // Collision occurred with an object on a different layer
            Debug.Log("Collided with object on undesired layer: " + col.gameObject.name);
        }
        
    }

    [ClientRpc]
    void networkVoronoiBreak(Vector2 pos, float blastRad, float force, float density, int pointCt, int relaxIter, int clientSeed, uint[] allNetIDs, Vector3[] allTransform)
    {
        UnityEngine.Random.InitState(clientSeed);
        ObjectFracturer.DoNetworkedVoronoiBreak((Vector2)pos, blastRad, force, density, pointCt, relaxIter, allNetIDs, allTransform);
        GameObject.Destroy(this.gameObject);
    }
}
