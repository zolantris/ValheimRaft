using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

// using ValheimVehicles.LayerUtils;
// using ValheimVehicles.Prefabs;

namespace ValheimVehicles.Scene
{
  
// [ExecuteInEditMode]
  [RequireComponent(typeof(Transform))]
  public class ScalableDoubleSidedCube : MonoBehaviour
  {
    public Vector3
      RectangleSize =
        Vector3.one; // This should match the local scale of a Unity cube

    public float baseFaceSize = 1f;

    // public Material InnerSelectiveMask;
    // public Material SurfaceWaterMaskMaterial;
    public Material CubeMaskMaterial;

    public Material CubeVisibleSurfaceMaterial;

    private static Color greenish = new Color(0.15f, 1.0f, 0.5f, 0.1f);
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MaxHeight = Shader.PropertyToID("_MaxHeight");

    public Color color = greenish;
    private List<GameObject> cubeObjs = new List<GameObject>();
    private List<Renderer> cubeRenders = new List<Renderer>();
    private GameObject Cube;
    public bool CanRenderTopOfCube = false;
    public int CubeLayer = 16;
    public int MaskRenderQueue = 2999;
    public bool ShouldUpdateHeight = true;
    public float ForcedMaxHeight = 30f;
    public bool HasForcedHeight = true;

    public int SideRenderQueue = 3000;

    // This should be only enabled for non-doublesided shaders
    public bool RenderDoubleSided = false;
    public bool RenderMaskOnSecondFace = false;

    private BlendMode SelectedDestinationBlend = BlendMode.OneMinusSrcAlpha;
    private GameObject? _cubeMaskObj = null;

    private void Start()
    {
      Setup();
    }

    public void InitCubes()
    {
      if (CubeMaskMaterial == null || CubeVisibleSurfaceMaterial == null)
      {
        return;
      }

      CreateCubeFaces();
    }

    private void OnEnable()
    {
      Setup();
    }

    private void Setup()
    {
      var scale = Vector3.zero;
      if (scale != Vector3.zero)
      {
        RectangleSize = scale;
      }

      transform.localScale = RectangleSize;

      InitCubes();
    }


    // private WaterVolume? _prevLiquidLevel = null;

    private void AlignTopOfCubeWithWater(float waterHeight)
    {
      if (!ShouldUpdateHeight)
      {
        return;
      }

      var cubeSizeOffset = transform.localScale.y / 2;
      var bottomOfCube = transform.position.y - cubeSizeOffset;
      var topOfCube = transform.position.y + cubeSizeOffset;
      if (_cubeMaskObj == null) return;
      // Moving the gameobject out of bounds should never happen.
      if (waterHeight < bottomOfCube || waterHeight > topOfCube)
      {
        return;
      }

      _cubeMaskObj.transform.position = new Vector3(transform.position.x,
        waterHeight, transform.position.z);
    }

    /// <summary>
    ///  This is pretty heavy. Likely will need to not call as much by doing some optimistic checks instead of setting shader every time.
    /// </summary>
    private void FixedUpdate()
    {
      // if (ZNetView.m_forceDisableInit) return;
      // var waterLevel =
      //   Floating.GetWaterLevel(transform.position,
      //     ref _prevLiquidLevel);
      AlignTopOfCubeWithWater(ForcedMaxHeight);

      if (transform.gameObject.layer != CubeLayer)
      {
        transform.gameObject.layer = CubeLayer;
      }

      if (transform.localScale != RectangleSize)
      {
        transform.localScale = RectangleSize;
      }

      if (_cubeMaskObj != null && _cubeMaskObj.gameObject.layer != CubeLayer)
      {
        _cubeMaskObj.gameObject.layer = CubeLayer;
      }

      foreach (var cubeRender in cubeRenders)
      {
        if (ShouldUpdateHeight)
        {
          // waterLevel = HasForcedHeight
          //   ? ForcedMaxHeight
          //   : Floating.GetWaterLevel(transform.position,
          //     ref _prevLiquidLevel);
          cubeRender.material.SetFloat(MaxHeight, ForcedMaxHeight);
        }

        if (cubeRender.material.color != color)
        {
          cubeRender.material.SetColor(ColorId, color);
        }

        if (cubeRender.gameObject.layer != CubeLayer)
        {
          cubeRender.gameObject.layer = CubeLayer;
        }
      }
    }

    private void SafeDestroy(GameObject obj)
    {
      if (!Application.isPlaying)
      {
        DestroyImmediate(obj);
      }
      else
      {
        Destroy(obj);
      }
    }

    public void Cleanup()
    {
      if (Cube != null) SafeDestroy(Cube);
      cubeRenders.Clear();
      foreach (var cubeObj in cubeObjs)
      {
        if (cubeObj == null) continue;
        SafeDestroy(cubeObj);
      }

      cubeObjs.Clear();
    }

    private void OnDestroy()
    {
      Cleanup();
    }

    private void OnDisable()
    {
      Cleanup();
    }

    private void CreateCubeFaces()
    {
      float halfSize = baseFaceSize / 2f;

      var topDirection = Vector3.up;
      var topPosition = new Vector3(0, halfSize, 0);
      var topRotation = new Vector3(90, 0, 0);

      // Define face directions and positions
      Vector3[] directions =
      {
        topDirection,
        Vector3.down, Vector3.forward, Vector3.back, Vector3.left,
        Vector3.right
      };

      Vector3[] positions =
      {
        topPosition,
        new(0, -halfSize, 0),
        new(0, 0, halfSize),
        new(0, 0, -halfSize),
        new(-halfSize, 0, 0),
        new(halfSize, 0, 0)
      };
      Vector3[] rotations =
      {
        topRotation,
        new(-90, 0, 0),
        new(0, 0, 0),
        new(0, 180, 0),
        new(0, -90, 0),
        new(0, 90, 0)
      };

      if (!CanRenderTopOfCube)
      {
        CreateFaceMesh(topPosition, Quaternion.Euler(topRotation),
          topDirection, CubeFaceType.MaskFace);
      }

      // Create each face with two meshes for double-sided rendering
      for (var i = 0; i < positions.Length; i++)
      {
        // Omit the top face based on the boolean
        if (i == 1 &&
            !CanRenderTopOfCube) // i == 0 corresponds to the top face i==1 is bottom, but we flip the cube so shader worldY works better so it's top.
          continue;

        // Front side of the face
        CreateFaceMesh(positions[i], Quaternion.Euler(rotations[i]),
          directions[i], CubeFaceType.HeightFace);

        // Back side of the face (flip normal)
        if (RenderDoubleSided || RenderMaskOnSecondFace)
        {
          CreateFaceMesh(positions[i],
            Quaternion.Euler(rotations[i] + new Vector3(0, 180, 0)),
            -directions[i], CubeFaceType.MaskFace);
        }
      }
    }

    private enum CubeFaceType
    {
      MaskFace,
      HeightFace
    }

    private void CreateCubeMesh(Vector3 position, Quaternion rotation,
      Vector3 direction, CubeFaceType faceType)
    {
      //
      // // Mesh setup
      // var mesh = new Mesh
      // {
      //   vertices = new Vector3[]
      //   {
      //     new Vector3(-0.5f, -0.5f, 0),
      //     new Vector3(0.5f, -0.5f, 0),
      //     new Vector3(-0.5f, 0.5f, 0),
      //     new Vector3(0.5f, 0.5f, 0)
      //   },
      //   triangles = new int[]
      //   {
      //     0, 2, 1, 2, 3, 1 // Single face with two triangles
      //   },
      //   normals = new Vector3[]
      //   {
      //     normal, normal, normal, normal
      //   }
      // };
      //
      // MeshRenderer renderer = cubeFace.AddComponent<MeshRenderer>();
      // MeshFilter filter = cubeFace.AddComponent<MeshFilter>();
      // filter.mesh = mesh;
    }

    public void UpdateScale(Vector3 scale)
    {
      RectangleSize = scale;
      transform.localScale = RectangleSize;
    }

    private void CreateFaceMesh(Vector3 position, Quaternion rotation,
      Vector3 normal, CubeFaceType faceType)
    {
      if (transform.localScale != RectangleSize)
      {
        transform.localScale = RectangleSize;
      }

      var primitiveType = faceType == CubeFaceType.HeightFace
        ? PrimitiveType.Quad
        : PrimitiveType.Sphere;
      var cubeFace = GameObject.CreatePrimitive(primitiveType);
      cubeFace.name = $"{Enum.GetName(typeof(CubeFaceType), (int)faceType)}";
      cubeFace.layer = CubeLayer;
      cubeFace.transform.SetParent(transform);
      // cubeFace.transform.localPosition =
      //   Vector3.Scale(position, transform.localScale);
      cubeFace.transform.localPosition = position;
      cubeFace.transform.localRotation = rotation;
      if (faceType == CubeFaceType.MaskFace)
      {
        cubeFace.transform.localScale =
          new Vector3(1, 1, 1);
      }
      else
      {
        cubeFace.transform.localScale = Vector3.one;
      }

      var componentRenderer = cubeFace.GetComponent<MeshRenderer>();
      componentRenderer.sharedMaterial = faceType == CubeFaceType.HeightFace
        ? CubeVisibleSurfaceMaterial
        : CubeMaskMaterial;

      if (faceType == CubeFaceType.HeightFace)
      {
        componentRenderer.material.SetColor(ColorId, color);
        cubeRenders.Add(componentRenderer);
        cubeObjs.Add(cubeFace);
      }
      else
      {
        _cubeMaskObj = cubeFace;
      }
    }
  }
}
