using System;
using Unity.InferenceEngine;
using UnityEngine;

public class PopButton : MonoBehaviour
{
    [SerializeField]
    private string contentId;

    private Action<string> popAction;

    public void Init(string contentId, Action<string> popAction)
    {
        this.popAction = popAction;
        this.contentId = contentId;
    }

    public void OnInteract()
    {
        popAction?.Invoke(contentId);
    }
}