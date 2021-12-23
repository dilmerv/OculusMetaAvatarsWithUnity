using Oculus.Avatar2;
using UnityEngine;

public class TrackingTransformsInputControlDelegate : OvrAvatarInputControlDelegate
{
    public CAPI.ovrAvatar2ControllerType controllerType = CAPI.ovrAvatar2ControllerType.Invalid;

    public override bool GetInputControlState(out OvrAvatarInputControlState inputControlState)
    {
        inputControlState = default;
        inputControlState.type = controllerType;

        return true;
    }
}

public class TrackingTransformsInputTrackingDelegate : OvrAvatarInputTrackingDelegate
{
    private TrackingTransforms _transforms;

    public TrackingTransformsInputTrackingDelegate(TrackingTransforms transforms)
    {
        _transforms = transforms;
    }

    public override bool GetRawInputTrackingState(out OvrAvatarInputTrackingState inputTrackingState)
    {
        inputTrackingState = default;

        // HACK: Random rotations allow us to pass the BodyAPI "is in hand" check. Without it, BodyAPI overrides and goes into rest pose
        // I tried to get the random value as small as possible, but at .01 variance, rest pose triggers again :/
        Quaternion randomRot = Quaternion.Euler(Random.Range(-.1f, .1f), Random.Range(-.1f, .1f), Random.Range(-.1f, .1f));

        if (_transforms.hmd)
        {
            inputTrackingState.headset = _transforms.hmd;
            inputTrackingState.headsetActive = true;
        }


        if (_transforms.leftController)
        {
            inputTrackingState.leftController = _transforms.leftController;
            inputTrackingState.leftController.orientation *= randomRot;
            inputTrackingState.leftControllerActive = true;
            inputTrackingState.leftControllerVisible = _transforms.controllersVisbile;
        }
        else
        {
            inputTrackingState.leftControllerActive = false;
        }

        if (_transforms.rightController)
        {
            inputTrackingState.rightController = _transforms.rightController;
            inputTrackingState.rightController.orientation *= randomRot;
            inputTrackingState.rightControllerActive = true;
            inputTrackingState.rightControllerVisible = _transforms.controllersVisbile;
        }
        else
        {
            inputTrackingState.rightControllerActive = false;
        }

        return true;
    }
}

public class TrackingTransforms : OvrAvatarInputManager
{
    public Transform hmd;

    public Transform leftController;

    public Transform rightController;

    public bool controllersVisbile = false;

    private void Start()
    {
        if (BodyTracking != null)
        {
            BodyTracking.InputControlDelegate = new TrackingTransformsInputControlDelegate();
            BodyTracking.InputTrackingDelegate = new TrackingTransformsInputTrackingDelegate(this);
        }
    }
}
