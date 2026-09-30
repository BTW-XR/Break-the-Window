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

    [Tooltip("Minimum settle time (seconds) after release before the module detaches. 0 releases as soon as it catches up.")]
    [SerializeField]
    private float releaseSettleTime = 0f;

    [Tooltip("Maximum time (seconds) the module may take to settle after release before detaching anyway.")]
    [SerializeField]
    private float releaseSettleMaxTime = 2f;

    [Tooltip("How close (meters) the module must be to the release pose before detaching.")]
    [SerializeField]
    private float releaseSettleConvergence = 0.01f;

    [Tooltip("How close (degrees) the module's rotation must be to the release pose before detaching.")]
    [SerializeField]
    private float releaseSettleConvergenceAngle = 1f;

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

    // Skips all grab smoothing animation and jumps the module to its final (un-lagged)
    // state. Used before operations such as merge that read the real handle positions.
    private void SnapGrabToFinalState()
    {
        if (m_placementCoroutine != null)
        {
            StopCoroutine(m_placementCoroutine);
            m_placementCoroutine = null;
        }
        placementAnimating = false;

        if (smoothedGrabber != null && grabber != null)
        {
            smoothedGrabber.SetPositionAndRotation(grabber.position, grabber.rotation);
            smoothedGrabberVelocity = Vector3.zero;
            smoothTargetFrozen = false;
            oneEuroGrabFilter = new OneEuroFilter3(grabber.position, oneEuroMinCutoff, oneEuroBeta);
        }
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

    // Release path for mid-air drops: keep easing the module to the release pose until it
    // fully catches up to the grabber, then detach. Letting go mid-motion therefore settles
    // at the exact spot the grabber was put down instead of freezing short of it.
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
        while (smoothTargetFrozen)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= releaseSettleTime && GrabSpringConverged())
            {
                break;
            }
            if (elapsed >= releaseSettleMaxTime)
            {
                break;
            }
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

    private bool GrabSpringConverged()
    {
        if (smoothedGrabber == null)
        {
            return true;
        }

        float positionError = Vector3.Distance(smoothedGrabber.position, frozenTargetPosition);
        float rotationError = Quaternion.Angle(smoothedGrabber.rotation, frozenTargetRotation);
        return positionError <= releaseSettleConvergence
            && smoothedGrabberVelocity.sqrMagnitude <= releaseSettleConvergence * releaseSettleConvergence
            && rotationError <= releaseSettleConvergenceAngle;
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
