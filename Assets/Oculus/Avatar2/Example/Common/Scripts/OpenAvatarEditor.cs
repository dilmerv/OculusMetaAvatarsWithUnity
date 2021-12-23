using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenAvatarEditor : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        // Button Press
        if (OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.LTouch | OVRInput.Controller.LHand))
        {
            AvatarEditorDeeplink.LaunchAvatarEditor();
        }
    }
}
