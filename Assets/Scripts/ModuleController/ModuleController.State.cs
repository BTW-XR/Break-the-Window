using System.Collections.Generic;
using UnityEngine;

public partial class ModuleController
{
    private static readonly List<ModuleController> instances = new List<ModuleController>();

    [SerializeField]
    private Transform originalParent;

    private Transform[] markers;

    [Header("Merge settings")]
    [SerializeField]
    private float mergeDistanceThreshold = 0.1f;

    [Header("Markers (clockwise order)")]
    [SerializeField]
    private Transform bottomLeft;

    [SerializeField]
    private Transform bottomRight;

    [SerializeField]
    private Transform topRight;
    public Transform TopRight => topRight;

    [SerializeField]
    private Transform topLeft;

    private Transform[] corners => new Transform[] { bottomLeft, bottomRight, topRight, topLeft };

    [SerializeField]
    private float markerScale = 0.05f;

    [SerializeField]
    private float moveThreshold = 0.0001f;

    [Header("Grabber")]
    [SerializeField]
    private Transform grabber;

    [SerializeField]
    private Vector3 grabberOffset = new Vector3(0f, -0.1f, 0f);

    [Header("References")]
    [SerializeField]
    private Transform planeReference;

    [SerializeField]
    private Transform quad;

    [Header("Info")]
    [SerializeField]
    private bool doResize = true;

    [SerializeField]
    private bool doMerge = true;

    [SerializeField]
    private bool isGrabbed;

    [SerializeField]
    private bool isResizing;

    [Header("Prefabs")]
    [SerializeField]
    private GameObject markerPreviewPrefab;

    [SerializeField]
    private float mergePreviewThickness = 0.02f;

    [SerializeField]
    private Material mergePreviewMaterial;

    public Vector3 realPosition => quad.position;
    public Vector3 realScale => quad.lossyScale;
    public Quaternion realRotation => quad.rotation;

    // Runtime preview markers kept in BL/BR/TR/TL order.
    private readonly Transform[] mergePreviewMarkers = new Transform[4];

    // Runtime thick preview body spanning the four preview corners.
    private Transform mergePreviewQuad;

    // Merge partner associated with the currently rendered preview.
    private ModuleController activeMergeTarget;
    private int activeMergeEdgeIndex = -1;

    [Header("Layout")]
    [SerializeReference]
    private ModuleLayoutTree layoutTree;
    public ModuleLayoutTree LayoutTree
    {
        get => layoutTree;
        set
        {
            layoutTree = value;
            layoutTree.Validate();
            RegenerateLayout();
        }
    }

    [Header("Debug")]
    [SerializeField]
    private string debugLookupContentId = "content";

    [SerializeField]
    private Vector3 debugSpawnOffsetInModulePlane = new Vector3(0.6f, 0f, 0f);

    private readonly Vector3[] prevPositions = new Vector3[4];

    private static readonly string[] indexToName = new string[]
    {
        "bottomLeft",
        "bottomRight",
        "topRight",
        "topLeft",
    };

    private Edge[] edges;

    private static readonly Vector3[] edgeNormals = new Vector3[]
    {
        Vector3.up,
        Vector3.left,
        Vector3.down,
        Vector3.right,
    };

    private struct Corner
    {
        public Transform transform;
        public Vector3 positionVector;
        public int index;

        public Corner(Transform transform, int index)
        {
            this.transform = transform;
            this.positionVector = Vector3.zero;
            this.index = index;
        }

        public Corner(Vector3 positionVector, int index)
        {
            this.transform = null;
            this.positionVector = positionVector;
            this.index = index;
        }

        public Vector3 position => transform != null ? transform.position : positionVector;
    }

    private struct Edge
    {
        public Transform corner1;
        public int corner1Index => index;
        public Transform corner2;
        public int corner2Index => (index + 1) % 4;
        public int index;

        public Edge(Transform corner1, Transform corner2, int index = -1)
        {
            this.corner1 = corner1;
            this.corner2 = corner2;
            this.index = index;
        }

        public Vector3 corner1Position => corner1.position;
        public Vector3 corner2Position => corner2.position;

        public float Length => Vector3.Distance(corner1Position, corner2Position);

        public Vector3 normal => edgeNormals[index];
    }

    private struct MergeCandidate
    {
        public ModuleController other;
        public int myEdgeIndex;
        public int otherEdgeIndex => (myEdgeIndex + 2) % 4;
        public float distance;

        public MergeCandidate(ModuleController other, int edgeIndex, float distance)
        {
            this.other = other;
            this.myEdgeIndex = edgeIndex;
            this.distance = distance;
        }

        public override string ToString()
        {
            return $"(other: {other.name}, myEdgeIndex: {myEdgeIndex}, otherEdgeIndex: {otherEdgeIndex}, distance: {distance})";
        }
    }
}
