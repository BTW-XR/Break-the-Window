using UnityEngine;

public class DistanceFactorSetter : MonoBehaviour
{
    private static readonly int GlobalWorldPosAId = Shader.PropertyToID("_GlobalWorldPosA");
    private static readonly int GlobalWorldPosBId = Shader.PropertyToID("_GlobalWorldPosB");

    [SerializeField]
    private Transform leftHand;

    [SerializeField]
    private Transform rightHand;

    private void Start()
    {
        if (leftHand == null || rightHand == null)
        {
            Debug.LogError("Left hand or right hand transform is not assigned.");
        }
    }

    private void LateUpdate()
    {
        if (leftHand == null || rightHand == null)
        {
            return;
        }

        Shader.SetGlobalVector(GlobalWorldPosAId, leftHand.position);
        Shader.SetGlobalVector(GlobalWorldPosBId, rightHand.position);
    }
}
