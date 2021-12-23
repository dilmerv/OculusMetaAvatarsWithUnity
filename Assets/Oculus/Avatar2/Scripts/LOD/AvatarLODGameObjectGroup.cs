using System;
using UnityEngine;


namespace Oculus.Avatar2 {
  public class AvatarLODGameObjectGroup : AvatarLODGroup {
    [SerializeField]
    private GameObject[] gameObjects_ = Array.Empty<GameObject>();

    public GameObject[] GameObjects {
      get { return this.gameObjects_; }
      set {
        this.gameObjects_ = value;
        count = GameObjects.Length;
        ResetLODGroup();
      }
    }

    public override void ResetLODGroup() {
      if (!Application.isPlaying) return;
      for (int i = 0; i < GameObjects.Length; i++) {
        if(GameObjects[i] == null) continue;
        GameObjects[i].SetActive(false);
      }

      UpdateAdjustedLevel();
      UpdateLODGroup();
    }

    public override void UpdateLODGroup() {
      if (prevAdjustedLevel_ != -1)
        GameObjects[prevAdjustedLevel_]?.SetActive(false);
      if (adjustedLevel_ != -1)
        GameObjects[adjustedLevel_].SetActive(true);

      prevLevel_ = Level;
      prevAdjustedLevel_ = adjustedLevel_;
    }
  }
}
