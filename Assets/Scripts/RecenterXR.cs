using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class RecenterXR : MonoBehaviour
{
    void Start()
    {
        var subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        foreach (var subsystem in subsystems)
        {
            subsystem.TryRecenter();
        }
    }
}