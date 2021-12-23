using Oculus.Avatar2;
using Node = UnityEngine.XR.XRNode;

/*
 *
 */
public class SampleInputTrackingDelegate : OvrAvatarInputTrackingDelegate
{
    private OVRCameraRig _ovrCameraRig = null;

    public SampleInputTrackingDelegate(OVRCameraRig ovrCameraRig)
    {
        _ovrCameraRig = ovrCameraRig;
    }

    public override bool GetRawInputTrackingState(out OvrAvatarInputTrackingState inputTrackingState)
    {
        bool leftControllerActive = false;
        bool rightControllerActive = false;
        if (OVRInput.GetActiveController() != OVRInput.Controller.Hands)
        {
            leftControllerActive = OVRInput.GetControllerOrientationTracked(OVRInput.Controller.LTouch);
            rightControllerActive = OVRInput.GetControllerOrientationTracked(OVRInput.Controller.RTouch);
        }

        if (_ovrCameraRig)
        {
            inputTrackingState = new OvrAvatarInputTrackingState
            {
                headsetActive = true,
                leftControllerActive = leftControllerActive,
                rightControllerActive = rightControllerActive,
                leftControllerVisible = false,
                rightControllerVisible = false,
                headset = _ovrCameraRig.centerEyeAnchor,
                leftController = _ovrCameraRig.leftControllerAnchor,
                rightController = _ovrCameraRig.rightControllerAnchor
            };
            return true;
        }
        else if (OVRNodeStateProperties.IsHmdPresent())
        {
            inputTrackingState = new OvrAvatarInputTrackingState();
            inputTrackingState.headsetActive = true;
            inputTrackingState.leftControllerActive = leftControllerActive;
            inputTrackingState.rightControllerActive = rightControllerActive;
            inputTrackingState.leftControllerVisible = true;
            inputTrackingState.rightControllerVisible = true;

            if (OVRNodeStateProperties.GetNodeStatePropertyVector3(Node.CenterEye, NodeStatePropertyType.Position,
                OVRPlugin.Node.EyeCenter, OVRPlugin.Step.Render, out var headPos))
            {
                inputTrackingState.headset.position = headPos;

            }
            if (OVRNodeStateProperties.GetNodeStatePropertyQuaternion(Node.CenterEye, NodeStatePropertyType.Orientation,
                OVRPlugin.Node.EyeCenter, OVRPlugin.Step.Render, out var headRot))
            {
                inputTrackingState.headset.orientation = headRot;
            }

            inputTrackingState.leftController.position = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
            inputTrackingState.rightController.position = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
            inputTrackingState.leftController.orientation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch);
            inputTrackingState.rightController.orientation = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);
            return true;
        }

        inputTrackingState = default;
        return false;
    }
}
