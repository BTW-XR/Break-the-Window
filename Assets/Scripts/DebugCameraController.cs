using System.Collections.Generic;
using UnityEngine;
using Vuplex.WebView;

public class DebugCameraController : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> targets = new List<GameObject>();

    [SerializeField]
    private int currentTargetIndex = 0;

    [SerializeField]
    private float movementSmoothSpeed = 5f; // Higher values mean faster transitions

    [SerializeField]
    private float rotationSmoothSpeed = 5f;

    private Vector3 offsetFromTarget = new Vector3(0f, 0f, -0.5f);
    private bool hasTarget = false;

    void Start()
    {
        CanvasWebViewPrefab[] webViewPrefabs = FindObjectsByType<CanvasWebViewPrefab>(FindObjectsSortMode.InstanceID);
        foreach (CanvasWebViewPrefab prefab in webViewPrefabs)
        {
            targets.Add(prefab.gameObject);
        }

        if (targets.Count > 0)
        {
            hasTarget = true;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (targets.Count > 0)
            {
                currentTargetIndex = (currentTargetIndex + 1) % targets.Count;
                hasTarget = true;
            }
        }

        if (hasTarget && targets.Count > 0)
        {
            Transform targetTransform = targets[currentTargetIndex].transform;

            Vector3 desiredPosition = targetTransform.position + (targetTransform.rotation * offsetFromTarget);
            Quaternion desiredRotation = targetTransform.rotation;

            transform.position = Vector3.Lerp(transform.position, desiredPosition, movementSmoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.F1))
        {
            Vector3 d = Vector3.forward * 0.5f * Time.deltaTime;
            transform.Translate(d);
            offsetFromTarget += d;
            hasTarget = false;
        }

        if (Input.GetKey(KeyCode.F2))
        {
            Vector3 d = Vector3.back * 0.5f * Time.deltaTime;
            transform.Translate(d);
            offsetFromTarget += d;
            hasTarget = false;
        }
    }
}