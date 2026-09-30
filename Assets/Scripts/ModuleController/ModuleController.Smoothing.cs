using System.Collections;
using UnityEngine;

public partial class ModuleController
{
    #region Grab Motion Smoothing

    [Header("Grab Motion Smoothing")]
    [Tooltip("Ease and filter the grabbed module's motion instead of rigidly following the hand.")]
    [SerializeField]
    private bool smoothGrabMotion = true;

    [Tooltip("Spring stiffness while held. Higher = snappier, lower = more weight/lag.")]
    [SerializeField]
    private float followStiffness = 70f;

    [Tooltip("One Euro filter minimum cutoff for the held position. Lower = smoother (more lag on slow motion).")]
    [SerializeField]
    private float oneEuroMinCutoff = 0.8f;

    [Tooltip("One Euro filter beta. Higher = more responsive to fast motion.")]
    [SerializeField]
    private float oneEuroBeta = 0.05f;

    [Tooltip("Rotation follow rate in 1/s. Lower = smoother, more sluggish rotation.")]
    [SerializeField]
    private float rotationFollowRate = 12f;

    [Tooltip("How long (seconds) the module coasts to a stop after being released in mid-air.")]
    [SerializeField]
    private float releaseSettleTime = 0.15f;

    // Runtime proxy that carries the module while it is grabbed. The proxy is driven by a
    // critically damped spring + One Euro filter toward the XRI-driven grabber, so the panel
    // follows the hand smoothly instead of 1:1 with raw tracking jitter.
    private Transform smoothedGrabber;
    private Vector3 smoothedGrabberVelocity;
    private bool placementAnimating;
    private bool smoothTargetFrozen;
    private Vector3 frozenTargetPosition;
    private Quaternion frozenTargetRotation;
    private OneEuroFilter3 oneEuroGrabFilter;

    // While grabbed, module children are parented to the proxy (when smoothing is enabled)
    // instead of the grabber, so their motion is filtered. Falls back to the raw grabber.
    private Transform GrabParent => smoothedGrabber != null ? smoothedGrabber : grabber;

    private void EnsureSmoothedGrabber()
    {
        if (grabber == null)
        {
            return;
        }

        if (smoothedGrabber == null)
        {
            var go = new GameObject("SmoothedGrabber");
            go.hideFlags = HideFlags.HideAndDontSave;
            smoothedGrabber = go.transform;
            if (grabber.parent != null)
            {
                smoothedGrabber.SetParent(grabber.parent, false);
            }
            smoothedGrabber.SetPositionAndRotation(grabber.position, grabber.rotation);
        }

        smoothedGrabberVelocity = Vector3.zero;
        smoothTargetFrozen = false;
        oneEuroGrabFilter = new OneEuroFilter3(grabber.position, oneEuroMinCutoff, oneEuroBeta);
    }

    private void DestroySmoothedGrabber()
    {
        if (smoothedGrabber == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(smoothedGrabber.gameObject);
        }
        else
        {
            DestroyImmediate(smoothedGrabber.gameObject);
        }
        smoothedGrabber = null;
    }

    private void StepGrabSmoothing(float deltaTime)
    {
        if (smoothedGrabber == null || grabber == null || placementAnimating)
        {
            return;
        }

        Vector3 targetPosition;
        Quaternion targetRotation;
        if (smoothTargetFrozen)
        {
            targetPosition = frozenTargetPosition;
            targetRotation = frozenTargetRotation;
        }
        else
        {
            targetPosition = oneEuroGrabFilter.Filter(grabber.position, deltaTime);
            targetRotation = grabber.rotation;
        }

        // Critically damped spring toward the target: adds weight/inertia and attenuates jitter.
        float damping = 2f * Mathf.Sqrt(followStiffness);
        Vector3 displacement = targetPosition - smoothedGrabber.position;
        Vector3 acceleration = displacement * followStiffness - smoothedGrabberVelocity * damping;
        smoothedGrabberVelocity += acceleration * deltaTime;
        smoothedGrabber.position += smoothedGrabberVelocity * deltaTime;

        // Exponential slerp for rotation (no overshoot).
        float t = 1f - Mathf.Exp(-rotationFollowRate * deltaTime);
        smoothedGrabber.rotation = Quaternion.Slerp(smoothedGrabber.rotation, targetRotation, t);
    }

    // Release path for mid-air drops: coast the module to a stop at the release pose
    // before detaching, so letting go mid-motion settles instead of freezing abruptly.
    private void BeginReleaseSettle()
    {
        if (!Application.isPlaying || smoothedGrabber == null || grabber == null)
        {
            OnGrabberRelease();
            return;
        }

        smoothTargetFrozen = true;
        frozenTargetPosition = grabber.position;
        frozenTargetRotation = grabber.rotation;
        StartCoroutine(FinishReleaseSettle());
    }

    private IEnumerator FinishReleaseSettle()
    {
        float elapsed = 0f;
        while (elapsed < releaseSettleTime && smoothTargetFrozen)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // A new grab aborts the settle (smoothTargetFrozen is reset), in which case the
        // module stays held and must not be released.
        if (smoothTargetFrozen)
        {
            smoothTargetFrozen = false;
            OnGrabberRelease();
        }
    }

    private struct OneEuroFilter3
    {
        private Vector3 m_LastRaw;
        private Vector3 m_LastFiltered;
        private readonly float m_MinCutoff;
        private readonly float m_Beta;

        public OneEuroFilter3(Vector3 initial, float minCutoff, float beta)
        {
            m_LastRaw = initial;
            m_LastFiltered = initial;
            m_MinCutoff = minCutoff;
            m_Beta = beta;
        }

        public Vector3 Filter(Vector3 raw, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return raw;
            }

            Vector3 speed = (raw - m_LastRaw) / deltaTime;
            Vector3 cutoff = new Vector3(m_MinCutoff, m_MinCutoff, m_MinCutoff)
                + Vector3.Scale(new Vector3(m_Beta, m_Beta, m_Beta), speed);
            Vector3 denominator = Vector3.one + cutoff;
            Vector3 alpha = new Vector3(
                1f / denominator.x,
                1f / denominator.y,
                1f / denominator.z
            );
            Vector3 filtered = Vector3.Scale(alpha, raw)
                + Vector3.Scale(Vector3.one - alpha, m_LastFiltered);

            m_LastRaw = raw;
            m_LastFiltered = filtered;
            return filtered;
        }
    }

    #endregion
}
