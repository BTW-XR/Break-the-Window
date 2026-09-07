using Oculus.Interaction;
using UnityEngine;

public class FakeTableManager : MonoBehaviour
{
    public static FakeTableManager instance;
    [SerializeField]
    private float snapToHorizontalThreshold = 0.1f;
    [SerializeField]
    private float validOrientationThreshold = 0.5f;
    [SerializeField]
    private GameObject fakeTableVisual;

    [SerializeField]
    private Transform mainCameraTransform;
    [SerializeField]
    private Transform fakeTableTransform;


    private Vector3 tablePosition;
    public Vector3 TablePosition => tablePosition;

    private Vector3 tableUp;
    private Vector3 tableForward;

    private bool tableValid = false;
    public bool TableValid => tableValid;

    private void Awake()
    {
        instance = this;

        if (fakeTableTransform == null)
        {
            Debug.LogError("FakeTableTransform is not assigned in the inspector.");
        }

    }

    void Update()
    {
        CalibrateTable();
    }

    public void CalibrateTable()
    {
        tableValid = false;
        if (fakeTableTransform == null)
        {
            Debug.LogError("FakeTableTransform is not assigned in the inspector.");
            return;
        }

        tablePosition = fakeTableTransform.position;
        Vector3 axis0 = -fakeTableTransform.up;
        Vector3 axis1 = Vector3.Cross(Vector3.up, axis0);
        Vector3 axis2 = Vector3.Cross(axis0, axis1);

        Vector3 camToTable = (tablePosition - mainCameraTransform.position).normalized;

        if (axis1.magnitude < snapToHorizontalThreshold)
        {
            axis0 = Vector3.up;
            axis2 = Vector3.ProjectOnPlane(camToTable, axis0).normalized;
            tableValid = true;
        }
        else
        {
            axis1.Normalize();
            axis2.Normalize();

            if (Vector3.Dot(axis0, -camToTable) > (1 - validOrientationThreshold))
            {
                tableValid = true;
            }
        }

        tableUp = axis0;
        tableForward = axis2;

        UpdateTableVisual();
    }

    private void UpdateTableVisual()
    {
        if (fakeTableVisual != null)
        {
            fakeTableVisual.transform.position = tablePosition;
            fakeTableVisual.transform.rotation = Quaternion.LookRotation(tableForward, tableUp);
        }

        fakeTableVisual.SetActive(tableValid);
    }

    public Quaternion GetOrientation()
    {
        return Quaternion.LookRotation(-tableUp, tableForward);
    }
}
