using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Ps5Maintenance
{
    // A standalone anchor owns the content pose; later image updates never move it.
    public sealed class MarkerPlacement : IDisposable
    {
        private readonly ARTrackedImageManager images;
        private readonly ARAnchorManager anchors;
        private readonly Transform content;
        private ARAnchor anchor;
        private bool pending, disposed;
        private int revision;
        private float retryAt;
        public string Error { get; private set; } = "";
        public bool HasPlacement => anchor != null;
        public bool IsPlacing => pending;
        public bool IsTracking => CanUsePlacement(HasPlacement, ARSession.state == ARSessionState.SessionTracking,
            anchor != null && anchor.trackingState == TrackingState.Tracking);

        public static bool CanUsePlacement(bool placed, bool sessionTracking, bool anchorTracking) =>
            placed && sessionTracking && anchorTracking;

        public MarkerPlacement(ARTrackedImageManager images, ARAnchorManager anchors, Transform content)
        {
            this.images = images;
            this.anchors = anchors;
            this.content = content;
        }

        public void Tick()
        {
            if (disposed) return;
            if (HasPlacement)
            {
                content.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
                return;
            }
            // Keep content outside the provider-owned anchor GameObject, so removal
            // of an anchor cannot destroy the instruction visuals during relocalization.
            if (!images.enabled) images.enabled = true;
            if (pending || Time.unscaledTime < retryAt ||
                ARSession.state != ARSessionState.SessionTracking || anchors.subsystem == null || !anchors.enabled) return;
            foreach (var image in images.trackables)
            {
                if (image.referenceImage.name != "ps5-maintenance" || image.trackingState != TrackingState.Tracking) continue;
                Place(new Pose(image.transform.position, image.transform.rotation), revision);
                break;
            }
        }

        private async void Place(Pose pose, int requestRevision)
        {
            pending = true;
            Error = "";
            try
            {
                var result = await anchors.TryAddAnchorAsync(pose);
                if (disposed || requestRevision != revision)
                {
                    if (result.status.IsSuccess() && result.value != null) Remove(result.value);
                    return;
                }
                if (!result.status.IsSuccess() || result.value == null)
                    throw new InvalidOperationException("Не удалось закрепить положение. Наведите камеру на маркер ещё раз.");
                anchor = result.value;
                content.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
                // Tracking now depends on the anchor and the AR session, not on the image.
                images.enabled = false;
            }
            catch (Exception e)
            {
                if (!disposed && requestRevision == revision)
                {
                    Error = "Не удалось закрепить подсказки. Повторите сканирование.";
                    retryAt = Time.unscaledTime + 2f;
                    Debug.LogWarning("AR anchor placement: " + e.Message);
                }
            }
            finally
            {
                if (requestRevision == revision) pending = false;
            }
        }

        public void Reset()
        {
            revision++;
            pending = false;
            Error = "";
            retryAt = 0;
            if (content != null) content.SetParent(null, true);
            if (anchor != null) Remove(anchor);
            anchor = null;
            if (images != null && !disposed) images.enabled = true;
        }

        private void Remove(ARAnchor oldAnchor)
        {
            if (oldAnchor == null) return;
            if (anchors != null && anchors.isActiveAndEnabled && anchors.subsystem != null && anchors.TryRemoveAnchor(oldAnchor)) return;
            UnityEngine.Object.Destroy(oldAnchor.gameObject);
        }

        public void Dispose()
        {
            disposed = true;
            Reset();
        }
    }
}
