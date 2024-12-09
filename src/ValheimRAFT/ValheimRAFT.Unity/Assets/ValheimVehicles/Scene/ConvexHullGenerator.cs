using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using ValheimVehicles.Scene;

namespace ValheimVehicles.Scene
{
  
  [ExecuteInEditMode]
public class ConvexHullMeshGenerator : MonoBehaviour
{
 
   /// <summary>
  /// Gets all local-space points of a collider, adjusted for the object's scale.
  /// </summary>
  /// <param name="collider">The collider to extract points from.</param>
  /// <returns>A list of Vector3 points in local space, scaled correctly.</returns>
     public static List<Vector3> GetColliderPointsLocal(Collider collider)
    {
        var points = new List<Vector3>();
        var scale = collider.transform.localScale; // Object's local scale

        if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
        {
            // MeshCollider: Extract scaled local-space vertices
            foreach (var vertex in meshCollider.sharedMesh.vertices)
            {
                points.Add(Vector3.Scale(vertex, scale));
            }
        }
        else if (collider is BoxCollider boxCollider)
        {
            // BoxCollider: Calculate 8 corners in scaled local space
            var center = Vector3.Scale(boxCollider.center, scale);
            var size = Vector3.Scale(boxCollider.size * 0.5f, scale);

            var corners = new Vector3[]
            {
                center + new Vector3(-size.x, -size.y, -size.z),
                center + new Vector3(-size.x, -size.y, size.z),
                center + new Vector3(-size.x, size.y, -size.z),
                center + new Vector3(-size.x, size.y, size.z),
                center + new Vector3(size.x, -size.y, -size.z),
                center + new Vector3(size.x, -size.y, size.z),
                center + new Vector3(size.x, size.y, -size.z),
                center + new Vector3(size.x, size.y, size.z)
            };

            points.AddRange(corners);
        }
        else if (collider is SphereCollider sphereCollider)
        {
            // SphereCollider: Generate points on the sphere surface
            var center = Vector3.Scale(sphereCollider.center, scale);
            var radius = sphereCollider.radius * Mathf.Max(scale.x, scale.y, scale.z);

            const int latitudeSegments = 10; // Number of latitudinal divisions
            const int longitudeSegments = 20; // Number of longitudinal divisions

            for (int lat = 0; lat <= latitudeSegments; lat++)
            {
                var theta = Mathf.PI * lat / latitudeSegments; // Latitude angle
                var sinTheta = Mathf.Sin(theta);
                var cosTheta = Mathf.Cos(theta);

                for (int lon = 0; lon <= longitudeSegments; lon++)
                {
                    var phi = 2 * Mathf.PI * lon / longitudeSegments; // Longitude angle
                    var x = radius * sinTheta * Mathf.Cos(phi);
                    var y = radius * cosTheta;
                    var z = radius * sinTheta * Mathf.Sin(phi);

                    points.Add(center + new Vector3(x, y, z));
                }
            }
        }
        else if (collider is CapsuleCollider capsuleCollider)
        {
            // CapsuleCollider: Generate points for the body and spherical caps
            var center = Vector3.Scale(capsuleCollider.center, scale);
            var height = (capsuleCollider.height * 0.5f - capsuleCollider.radius) * scale.y;
            var radius = capsuleCollider.radius * Mathf.Max(scale.x, scale.z);

            const int segmentCount = 10; // Number of segments for spherical caps and cylinder

            var direction = capsuleCollider.direction; // 0 = X, 1 = Y, 2 = Z
            Vector3 up = Vector3.up, forward = Vector3.forward, right = Vector3.right;

            if (direction == 0) { up = Vector3.right; forward = Vector3.up; right = Vector3.forward; }
            else if (direction == 2) { up = Vector3.forward; forward = Vector3.up; right = Vector3.right; }

            // Generate cylinder body points
            for (int i = 0; i <= segmentCount; i++)
            {
                var angle = 2 * Mathf.PI * i / segmentCount;
                var x = radius * Mathf.Cos(angle);
                var z = radius * Mathf.Sin(angle);
                var bodyPoint = new Vector3(x, 0, z);

                points.Add(center + height * up + Vector3.Scale(bodyPoint, new Vector3(right.x, right.y, right.z)));
                points.Add(center - height * up + Vector3.Scale(bodyPoint, new Vector3(right.x, right.y, right.z)));
            }

            // Generate points for the spherical caps
            for (int i = 0; i <= segmentCount; i++)
            {
                var theta = Mathf.PI * i / segmentCount; // Polar angle
                var sinTheta = Mathf.Sin(theta);
                var cosTheta = Mathf.Cos(theta);

                for (int j = 0; j <= segmentCount; j++)
                {
                    var phi = 2 * Mathf.PI * j / segmentCount; // Azimuthal angle
                    var x = radius * sinTheta * Mathf.Cos(phi);
                    var y = radius * cosTheta;
                    var z = radius * sinTheta * Mathf.Sin(phi);

                    var capPoint = new Vector3(x, y, z);

                    points.Add(center + height * up + capPoint); // Top cap
                    points.Add(center - height * up + capPoint); // Bottom cap
                }
            }
        }
        else
        {
            Debug.LogWarning($"Unsupported collider type: {collider.GetType()}");
        }

        return points; // Local space, scaled
    }
   
  public static List<Vector3> ConvertToRelativeSpace(Collider collider,
    List<Vector3> localPoints)
  {
    var relativePoints = new List<Vector3>();
    foreach (var localPoint in localPoints)
    {
      relativePoints.Add(localPoint + collider.transform.localPosition);
    }

    return relativePoints;
  }

  public static List<Vector3> ConvertToWorldSpace(Collider collider,
    List<Vector3> localPoints)
  {
    var worldPoints = new List<Vector3>();
    foreach (var localPoint in localPoints)
    {
      worldPoints.Add(collider.transform.TransformPoint(localPoint));
    }

    return worldPoints;
  }

  public static List<Vector3> GetColliderPointsLocal(List<Collider> colliders)
  {
    return colliders
      .SelectMany(GetColliderPointsLocal)
      .Distinct()
      .ToList();
  }

  // Optional: Overload for world space conversion of multiple colliders
  public static List<Vector3> GetColliderPointsWorld(List<Collider> colliders)
  {
    return colliders
      .SelectMany(collider =>
        ConvertToWorldSpace(collider, GetColliderPointsLocal(collider)))
      .Distinct()
      .ToList();
    
  }

  public static List<Vector3> GetColliderPointsRelative(
    List<Collider> colliders)
  {
    return colliders
      .SelectMany(collider =>
        ConvertToRelativeSpace(collider, GetColliderPointsLocal(collider)))
      .Distinct()
      .ToList();
  }

  /// <summary>
  /// Copy & paste the output of Vector3Logger
  /// </summary>
  // private IEnumerable<Vector3> testPoints = new Vector3[]
  // {
  //   new Vector3(1.969051f, 0.03499834f, 4.98811f),
  //   new Vector3(5.969101f, 0.03500491f, -3.011944f),
  //   new Vector3(-2.030958f, 0.03499899f, 12.98798f),
  //   new Vector3(1.969054f, 0.01599588f, 12.98811f),
  //   new Vector3(1.969146f, 0.05399674f, -3.012f),
  //   new Vector3(5.969078f, 0.05400102f, 12.9881f),
  //   new Vector3(5.969075f, 0.05400009f, 4.988083f),
  //   new Vector3(0f, 0f, 0f),
  //   new Vector3(-1.150025f, 0.1638423f, 3.718254f),
  //   new Vector3(-2.030858f, 0.05399552f, -3.011994f),
  //   new Vector3(0.3230967f, 0.9961958f, -2.063026f),
  //   new Vector3(10.07709f, 0.5000018f, 5.488127f),
  //   new Vector3(10.07709f, 0.5000022f, 7.488121f),
  //   new Vector3(10.07696f, 0.5000033f, 9.487677f),
  // };
  
  public Vector3 transformPreviewOffset = new Vector3(1, 0, 0);

  private void Start()
  {
      GenerateMeshesFromChildColliders();
  }

  public void OnEnable()
  {
    GenerateMeshesFromChildColliders();
  }

  public void OnDisable()
  {
      DeleteMeshesFromChildColliders();
  }

  public void FixedUpdate()
  {
    GenerateMeshesFromChildColliders();
  }
  
  
      /// <summary>
    /// Groups colliders into clusters based on their proximity.
    /// </summary>
    /// <param name="gameObjects">List of GameObjects to process.</param>
    /// <param name="distanceThreshold">The distance threshold to determine if colliders are near each other.</param>
    /// <returns>A list of clusters, where each cluster is a list of colliders.</returns>
    public static List<List<Collider>> GroupCollidersByProximity(List<GameObject> gameObjects, float distanceThreshold)
    {
        var allColliders = new List<Collider>();
        foreach (var gameObject in gameObjects)
        {
            allColliders.AddRange(gameObject.GetComponentsInChildren<Collider>());
        }

        var clusters = new List<List<Collider>>();
        var processedColliders = new HashSet<Collider>();

        foreach (var collider in allColliders)
        {
            if (processedColliders.Contains(collider)) continue;

            // Start a new cluster and find all connected colliders
            var cluster = new List<Collider>();
            FindConnectedColliders(collider, allColliders, cluster, processedColliders, distanceThreshold);
            clusters.Add(cluster);
        }

        return clusters;
    }

    /// <summary>
    /// Recursively finds all colliders connected to the given collider within the distance threshold.
    /// </summary>
    private static void FindConnectedColliders(
        Collider current,
        List<Collider> allColliders,
        List<Collider> cluster,
        HashSet<Collider> processedColliders,
        float distanceThreshold)
    {
        cluster.Add(current);
        processedColliders.Add(current);

        foreach (var other in allColliders)
        {
            if (current == other || processedColliders.Contains(other)) continue;

            if (AreCollidersNear(current, other, distanceThreshold))
            {
                FindConnectedColliders(other, allColliders, cluster, processedColliders, distanceThreshold);
            }
        }
    }

    /// <summary>
    /// Checks if two colliders are within a specified distance of each other.
    /// </summary>
    private static bool AreCollidersNear(Collider a, Collider b, float distanceThreshold)
    {
        var closestPointA = a.ClosestPoint(b.bounds.center);
        var closestPointB = b.ClosestPoint(a.bounds.center);
        var distance = Vector3.Distance(closestPointA, closestPointB);
        return distance <= distanceThreshold;
    }

  public List<Collider> GetColliderCluster(IEnumerable<GameObject> gameObjects)
  {
    var colliders = new List<Collider>();
    
    foreach (var go in gameObjects)
    {
      Debug.Log(go.transform.name);
      var goColliders = go.GetComponentsInChildren<Collider>();
      var goTopLevelCollider = go.GetComponent<Collider>();
      if (goTopLevelCollider)
      {
        colliders.Add(goTopLevelCollider);
      }
      if (goColliders != null)
      {
        colliders.AddRange(goColliders);
      }
    }
    return colliders;
  }

  public float DistanceThreshold = 0.1f;
  
  public List<GameObject> GeneratedMeshGameObjects = new();

  public void DeleteMeshesFromChildColliders()
  {
      var instances = GeneratedMeshGameObjects.ToList();
      GeneratedMeshGameObjects.Clear();
      
      foreach (var instance in instances)
      {
          AdaptiveDestroy(instance);
      }
  }
  
  public static List<GameObject> GetAllChildGameObjects(GameObject parentGo)
  {
      var result = new List<GameObject>();
      foreach (Transform child in parentGo.transform)
      {
          result.Add(child.gameObject);
          result.AddRange(GetAllChildGameObjects(child.gameObject));
      }
      return result;
  }
  
  public void GenerateMeshesFromChildColliders()
  {
      if (GeneratedMeshGameObjects.Count > 0)
      {
          DeleteMeshesFromChildColliders();
      }

      var childGameObjects = GetAllChildGameObjects(gameObject);
      var hullClusters = GroupCollidersByProximity(childGameObjects.ToList(), DistanceThreshold);
      Debug.Log($"HullCluster Count: {hullClusters.Count}");

      foreach (var hullCluster in hullClusters)
      {
          var colliderPoints = GetColliderPointsRelative(hullCluster);
          GenerateConvexHullMesh(colliderPoints);
      }
  }

  // Convex hull calculator instance
  private ConvexHullCalculator
    convexHullCalculator = new ConvexHullCalculator();

  private static void AdaptiveDestroy(GameObject gameObject)
  {
#if UNITY_EDITOR
    UnityEngine.Object.DestroyImmediate(gameObject);
#else
            UnityEngine.Object.Destroy(gameObject);
#endif
  }

  void GenerateConvexHullMesh(List<Vector3> points)
  {
    if (points.Count < 3)
    {
      Debug.LogError("Not enough points to generate a convex hull.");
      return;
    }
    
    Debug.Log("Generating convex hull...");

    // Step 1: Prepare output containers
    var verts = new List<Vector3>();
    var tris = new List<int>();
    var normals = new List<Vector3>();

    // Step 2: Generate convex hull and export the mesh
    convexHullCalculator.GenerateHull(points, true, ref verts, ref tris,
      ref normals);

    // Step 3: Create a Unity Mesh
    var mesh = new Mesh
    {
      vertices = verts.ToArray(),
      triangles = tris.ToArray(),
      normals = normals.ToArray()
    };

    // Step 5: Create a new GameObject to display the mesh
    var go = new GameObject($"ConvexHullPreview{GeneratedMeshGameObjects.Count}");
    GeneratedMeshGameObjects.Add(go);
    var meshFilter = go.AddComponent<MeshFilter>();
    var meshRenderer = go.AddComponent<MeshRenderer>();

    meshFilter.mesh = mesh;

    // Step 6: Create and assign the material
    var material = new Material(Shader.Find("Standard"))
    {
      color = Color.green
    };
    meshRenderer.material = material;

    // Optional: Adjust transform
    var transform1 = transform;
    go.transform.position = transform1.position + transformPreviewOffset;
    go.transform.rotation = transform1.rotation;
  }

  // Test the method with a sample list of points
  [ContextMenu("Generate Mesh")]
  private void TestGenerateMesh()
  {
    GenerateMeshesFromChildColliders();
  }
}
}